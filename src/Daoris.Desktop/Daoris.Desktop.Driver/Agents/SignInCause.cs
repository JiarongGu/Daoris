namespace Daoris.Driver;

/// <summary>
/// Who held an account when its stored sign-in changed, where the credentials watch can tell: the words a reading keeps
/// (<see cref="AccountRead.By"/>) and the machine log's <c>credentials.written</c> line carries. A sign-out whose writer is
/// <see cref="Outside"/> came from nothing Daoris had at the account.
/// </summary>
public static class SignInCause
{
    /// <summary>A session of Daoris's ran on the account, or a start chosen on it had not yet run: its process may have written the store.</summary>
    public const string Session = "session";

    /// <summary>A status question of Daoris's (or the CLI's) held the account's probe lock.</summary>
    public const string Probe = "probe";

    /// <summary>Daoris's own sign-in into the account is running (<see cref="SignInMarks"/>).</summary>
    public const string SignIn = "sign-in";

    /// <summary>Nothing of Daoris's held the account: the writer is something outside it, as an agent run by hand with the account's home.</summary>
    public const string Outside = "outside";

    /// <summary>Whether <paramref name="word"/> is one of the four.</summary>
    public static bool Known(string? word) => word is Session or Probe or SignIn or Outside;

    /// <summary>
    /// Who holds the account <paramref name="account"/> of <paramref name="owner"/> now: Daoris's own sign-in first (its write is the
    /// sign-in's), then a probe (its question may refresh the token), then a session; else outside. The file system names no writer, so
    /// this is who could have written, never who did.
    /// </summary>
    internal static string Of(HarnessRoster roster, string owner, string account, int running, int starting)
    {
        var now = roster.Clock();
        if (SignInMarks.Active(roster.Home, owner, account, now)) return SignIn;
        var probe = ProbeLock.PathOf(roster.Home, owner, account);
        if (File.Exists(probe) && !ProbeLock.IsStale(probe, now.UtcDateTime)) return Probe;
        return running > 0 || starting > 0 ? Session : Outside;
    }
}

/// <summary>
/// A mark that Daoris's own sign-in into an account is running, beside the account's probe lock under <c>harnesses/.probing/</c>: a file
/// for each sign-in, which its flow makes as it starts and removes as it ends, and which a watch in another process reads. One left by a process that died
/// is stale after <see cref="Stale"/> and read as none.
/// </summary>
public static class SignInMarks
{
    /// <summary>How long a sign-in may run before its mark is taken for one left behind: a person's browser sign-in takes minutes.</summary>
    public static readonly TimeSpan Stale = TimeSpan.FromMinutes(30);

    /// <summary>The marks of the sign-ins into <paramref name="account"/>: one file for each running, so two never share one.</summary>
    public static IReadOnlyList<string> Files(string home, string owner, string account)
    {
        var folder = Path.GetDirectoryName(DaorisHome.Entry(home, HomeEntry.Probing, owner, "x"))!;
        try
        {
            return Directory.Exists(folder) ? [.. Directory.EnumerateFiles(folder, account + ".signing-in.*")] : [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Whether a sign-in into the account is running by its marks, as of each file's clock.</summary>
    public static bool Active(string home, string owner, string account, DateTimeOffset now)
    {
        try
        {
            return Files(home, owner, account).Any(file => now.UtcDateTime - File.GetLastWriteTimeUtc(file) < Stale);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Mark a sign-in into the account at <paramref name="profileHome"/> until the result is disposed; a home outside the accounts' layout
    /// marks nothing. A mark not written costs the writes of this sign-in their cause, never the sign-in.
    /// </summary>
    public static IDisposable Begin(string profileHome)
    {
        if (HarnessSettings.AccountAt(profileHome) is not { } at) return Mark.None;
        var path = DaorisHome.Entry(at.Home, HomeEntry.Probing, at.Agent, $"{at.Account}.signing-in.{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{}\n");
            return new Mark(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return Mark.None;
        }
    }

    private sealed class Mark(string? path) : IDisposable
    {
        public static readonly Mark None = new(null);

        public void Dispose()
        {
            if (path is null) return;
            try
            {
                File.Delete(path);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Stale after half an hour: no sign-in is read from it then.
            }
        }
    }
}
