namespace Daoris.Driver;

/// <summary>Why an account's agent is asked: a read never refreshes, a press may, a start's renewal is for it.</summary>
internal enum ReadKind
{
    /// <summary>A look's, a start's or a turn's read: starts no refresh, and a refused one is undone at the limit.</summary>
    Read,

    /// <summary>The person's press: may refresh the account where nothing runs on it; a refused one is undone at the limit.</summary>
    Press,

    /// <summary>A counted start's renewal (<see cref="SignInRefresh"/>): its refresh is its own.</summary>
    Renewal,
}

/// <summary>
/// What a read of an account (its status, its windows) does to its stored sign-in: it starts no refresh, and a refresh its agent makes
/// anyway that is refused and empties the store is undone from bytes held in memory for the read alone.
/// </summary>
internal static class ReadGuard
{
    // The window a restore was made in, by account home: a second emptying in it is a real one and is left.
    private static readonly Dictionary<string, DateTimeOffset> Restored = new(StringComparer.OrdinalIgnoreCase);

    // Account homes with a read in flight: the credentials watch marks no sign-out in them meanwhile.
    private static readonly Dictionary<string, int> InFlight = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether a read is in flight on the account at <paramref name="profileHome"/>.</summary>
    public static bool Holds(string profileHome)
    {
        lock (InFlight) return InFlight.ContainsKey(profileHome);
    }

    /// <summary>Whether asking the agent would have it refresh: its access token is expired or expires within <see cref="SignInRefresh.Margin"/>.</summary>
    public static bool WouldRefresh(IStoredSignIn? store, string? profileHome, DateTimeOffset now) =>
        store is not null && profileHome is not null
        && store.Read(profileHome) is { Cleared: false } shape && shape.Expiring(now, SignInRefresh.Margin);

    /// <summary>The cool-off an account's own reading puts it at: a window at 100% before its reset; null where it says none.</summary>
    public static CoolingEntry? Limit(string home, string owner, string account, DateTimeOffset now)
    {
        try
        {
            return AccountRotation.Spent(owner, account, new AccountFacts(Said: AccountWindows.SaidOf(home, owner, account, now)), now);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Run <paramref name="ask"/> and, where the account is at its limit by its own reading and none was put back in this window, put the
    /// store back where it left it with no tokens that it had before.
    /// </summary>
    /// <param name="restore">False for a renewal, whose refresh is its own.</param>
    public static async Task<(T Answer, bool Kept)> AskAsync<T>(
        IStoredSignIn? store, string? profileHome, bool restore, DateTimeOffset? now, Func<Task<T>> ask)
    {
        if (!restore || now is null || store?.FileName is not { Length: > 0 } name || profileHome is null) return (await ask().ConfigureAwait(false), false);

        var path = Path.Combine(profileHome, name);
        var before = Held(store, profileHome, path);
        if (before is null) return (await ask().ConfigureAwait(false), false);

        lock (InFlight) InFlight[profileHome] = InFlight.GetValueOrDefault(profileHome) + 1;
        T answer;
        try
        {
            answer = await ask().ConfigureAwait(false);
        }
        catch
        {
            Release(store, profileHome, path, before, now.Value);
            throw;
        }

        var put = Release(store, profileHome, path, before, now.Value);
        return (answer, put);
    }

    /// <summary>Put the store back, clear the held bytes, then let go of the account, so the watch never reads between.</summary>
    private static bool Release(IStoredSignIn store, string profileHome, string path, byte[] before, DateTimeOffset now)
    {
        try
        {
            return Restore(store, profileHome, path, before, now);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(before);
            lock (InFlight)
            {
                if (InFlight.GetValueOrDefault(profileHome) <= 1) InFlight.Remove(profileHome);
                else InFlight[profileHome]--;
            }
        }
    }

    /// <summary>The store's bytes where it holds tokens, else null.</summary>
    private static byte[]? Held(IStoredSignIn store, string profileHome, string path)
    {
        try
        {
            if (store.Read(profileHome) is not { Cleared: false }) return null;
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
            return bytes;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool Restore(IStoredSignIn store, string profileHome, string path, byte[] before, DateTimeOffset now)
    {
        if (store.Read(profileHome) is not { Cleared: true }) return false;
        if (HarnessSettings.AccountAt(profileHome) is not { } at || SignInMarks.Files(at.Home, at.Agent, at.Account).Count > 0) return false;
        if (Limit(at.Home, at.Agent, at.Account, now) is not { } limit) return false;
        lock (Restored)
        {
            if (Restored.TryGetValue(profileHome, out var window) && window == limit.Until) return false;
        }

        for (var attempt = 1; ; attempt++)
        {
            if (Put(path, before)) break;
            if (attempt == Attempts) return false;
            // A scanner or a reader may hold the store a moment without sharing its deletion.
            SpinWait.SpinUntil(static () => false, 40);
        }

        lock (Restored) Restored[profileHome] = limit.Until;
        return true;
    }

    private const int Attempts = 5;

    private static bool Put(string path, byte[] before)
    {
        var temp = path + ".daoris-kept." + Guid.NewGuid().ToString("N")[..8];
        try
        {
            var options = new FileStreamOptions { Mode = FileMode.CreateNew, Access = FileAccess.Write };
            if (!OperatingSystem.IsWindows()) options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            using (var stream = new FileStream(temp, options)) stream.Write(before);
            if (!OperatingSystem.IsWindows() && File.Exists(path)) File.SetUnixFileMode(temp, File.GetUnixFileMode(path));
            File.Move(temp, path, overwrite: true);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            try { File.Delete(temp); } catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException) { }
            return false;
        }
    }
}
