using System.IO.Enumeration;

namespace Daoris.Driver;

/// <summary>
/// Every write to an account's stored sign-in said in the machine log as <c>credentials.written</c>: which account, which file (the
/// store or a write's temp file), what happened to it, whether the file or its tokens are empty, and which sessions of Daoris's run on
/// the account at that moment. Never a value: the shape is read as <see cref="IStoredSignIn.ReadFile"/> reads it.
/// </summary>
/// <remarks>
/// <para>🔴 <b>Why</b>: accounts are signed out when two processes spend one single-use refresh token. Which process wrote the
/// store the moment its tokens went empty is the fact that says which two, and the file system names no writer, so the line names the
/// sessions running on the account (and the starts chosen on it not yet running); the machine log's <c>app.started</c> and its sessions'
/// reopening lines say the restart beside them.</para>
/// <para><b>How</b>: one watcher per account home, kept by each look (<see cref="Follow"/>), each on the home's own folder alone (an
/// agent's home holds its transcripts, which a recursive watch would drown in). A provider declares the store's name and the files of
/// its writes (<see cref="IStoredSignIn.FileName"/>, <see cref="IStoredSignIn.LeftoverPattern"/>); one that declares none is not watched.</para>
/// </remarks>
internal sealed class CredentialWrites(HarnessRoster roster, Action<AccountLine> say) : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private bool _said;
    private bool _disposed;

    /// <summary>How many account homes are watched now.</summary>
    public int Watching
    {
        get
        {
            lock (_gate) return _watchers.Count;
        }
    }

    /// <summary>
    /// Watch each account home whose provider declares a store that is not watched yet, and let go of those whose folder is gone. The
    /// first call that watches any says <c>credentials.watching</c>, once per process: the line the writes after it are read beside.
    /// </summary>
    public void Follow()
    {
        var wanted = new Dictionary<string, (string Agent, string Account, IStoredSignIn Store)>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in roster.Adapters.Agents)
        {
            if (roster.Adapters.AgentNamed(name) is not { Accounts.StoredSignIn: { FileName: { Length: > 0 } } store }
                || !string.Equals(roster.Adapters.OwnerOfAgent(name).Value, name, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var account in HarnessSettings.Profiles(roster.Home, name))
            {
                var home = HarnessSettings.ProfileHome(roster.Home, name, account);
                if (Directory.Exists(home)) wanted[home] = (name, account, store);
            }
        }

        var first = false;
        lock (_gate)
        {
            if (_disposed) return;
            foreach (var home in _watchers.Keys.Where(home => !wanted.ContainsKey(home)).ToList())
            {
                _watchers[home].Dispose();
                _watchers.Remove(home);
            }

            foreach (var (home, each) in wanted)
            {
                if (_watchers.ContainsKey(home) || Watch(home, each.Agent, each.Account, each.Store) is not { } watcher) continue;
                _watchers[home] = watcher;
            }

            if (_watchers.Count > 0 && !_said) first = _said = true;
        }

        if (first) say(AccountLine.CredentialsWatching(Watching));
    }

    private FileSystemWatcher? Watch(string home, string agent, string account, IStoredSignIn store)
    {
        try
        {
            var watcher = new FileSystemWatcher(home)
            {
                IncludeSubdirectories = false,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime,
                InternalBufferSize = 64 * 1024,
            };
            watcher.Created += (_, args) => Said(home, agent, account, store, args.Name, "created");
            watcher.Changed += (_, args) => Said(home, agent, account, store, args.Name, "changed");
            watcher.Deleted += (_, args) => Said(home, agent, account, store, args.Name, "deleted");
            watcher.Renamed += (_, args) =>
            {
                // A rename is a write's last step (the temp file over the store): the name it ends as is the file, and one it leaves a
                // name of ours for is said gone.
                if (Kind(store, args.Name) is not null) Said(home, agent, account, store, args.Name, "renamed");
                else Said(home, agent, account, store, args.OldName, "renamed-away");
            };
            watcher.EnableRaisingEvents = true;
            return watcher;
        }
        catch (Exception error) when (error is ArgumentException or IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            // A home that cannot be watched is read at the look as before: its writes go unsaid, nothing else changes.
            return null;
        }
    }

    /// <summary>The store, a write's temp file, or neither (null) for <paramref name="name"/>.</summary>
    internal static string? Kind(IStoredSignIn store, string? name)
    {
        if (name is null) return null;
        if (string.Equals(name, store.FileName, StringComparison.OrdinalIgnoreCase)) return "store";
        return store.LeftoverPattern is { } pattern && FileSystemName.MatchesSimpleExpression(pattern, name, ignoreCase: true) ? "temp" : null;
    }

    private void Said(string home, string agent, string account, IStoredSignIn store, string? name, string change)
    {
        if (Kind(store, name) is not { } file) return;
        bool? empty = null, access = null, refresh = null;
        if (change is not ("deleted" or "renamed-away"))
        {
            try
            {
                var path = Path.Combine(home, name!);
                empty = new FileInfo(path).Length == 0;
                if (empty == false && store.ReadFile(path) is { } shape) (access, refresh) = (shape.Access, shape.Refresh);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Gone again, or held by its writer: said with what read.
            }
        }

        var (running, starting) = SignInRefresh.SessionsOn(roster, agent, account);
        lock (_gate)
        {
            if (_disposed) return;
        }

        say(AccountLine.CredentialsWritten(agent, account, file, change, empty, access, refresh, running, starting));
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            foreach (var watcher in _watchers.Values) watcher.Dispose();
            _watchers.Clear();
        }
    }
}

/// <summary>
/// The files of writes that never finished, by the provider's own rule (<see cref="IStoredSignIn.LeftoverPattern"/>,
/// <see cref="IStoredSignIn.WriteLife"/>): one younger than a write takes may be in flight, which a start waits out; an older one is a
/// leftover, which a look removes.
/// </summary>
internal static class StoredLeftovers
{
    /// <summary>How long a start waits on a fresh temp file at most: a rename lands in milliseconds.</summary>
    public static readonly TimeSpan Settling = TimeSpan.FromSeconds(10);

    /// <summary>Remove each leftover of every account of every agent that declares a pattern; how many.</summary>
    public static int Sweep(HarnessRoster roster)
    {
        var removed = 0;
        var now = roster.Clock().UtcDateTime;
        foreach (var name in roster.Adapters.Agents)
        {
            if (roster.Adapters.AgentNamed(name) is not { Accounts.StoredSignIn: { LeftoverPattern: { Length: > 0 } } store }
                || !string.Equals(roster.Adapters.OwnerOfAgent(name).Value, name, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (var account in HarnessSettings.Profiles(roster.Home, name))
            {
                var home = HarnessSettings.ProfileHome(roster.Home, name, account);
                foreach (var file in Files(store, home).Where(file => Age(file, now) >= store.WriteLife))
                {
                    try
                    {
                        File.Delete(file);
                        removed++;
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                    {
                        // Held by a process: the next look tries again.
                    }
                }
            }
        }

        return removed;
    }

    /// <summary>Whether a temp file of the account's store is young enough to be a write in flight.</summary>
    public static bool InFlight(IStoredSignIn store, string home, DateTime now) =>
        Files(store, home).Any(file => Age(file, now) < (store.WriteLife < Settling ? store.WriteLife : Settling));

    private static IEnumerable<string> Files(IStoredSignIn store, string home)
    {
        if (store.LeftoverPattern is not { Length: > 0 } pattern || !Directory.Exists(home)) return [];
        try
        {
            return Directory.EnumerateFiles(home, pattern, SearchOption.TopDirectoryOnly).ToList();
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static TimeSpan Age(string file, DateTime now)
    {
        try
        {
            return now - File.GetLastWriteTimeUtc(file);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return TimeSpan.Zero;
        }
    }
}
