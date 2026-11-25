namespace Daoris.Driver;

/// <summary>
/// What a read of an account (its status, its windows) does to its stored sign-in: it starts no refresh, and a refresh its agent makes
/// anyway that is refused and empties the store is undone from bytes held in memory for the read alone.
/// </summary>
internal static class ReadGuard
{
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

    /// <summary>Run <paramref name="ask"/> and put the store back where it left it with no tokens that it had before.</summary>
    /// <param name="restore">False for a renewal, whose refresh is its own.</param>
    public static async Task<(T Answer, bool Kept)> AskAsync<T>(IStoredSignIn? store, string? profileHome, bool restore, Func<Task<T>> ask)
    {
        if (!restore || store?.FileName is not { Length: > 0 } name || profileHome is null) return (await ask().ConfigureAwait(false), false);

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
            Release(store, profileHome, path, before);
            throw;
        }

        var put = Release(store, profileHome, path, before);
        return (answer, put);
    }

    /// <summary>Put the store back, clear the held bytes, then let go of the account, so the watch never reads between.</summary>
    private static bool Release(IStoredSignIn store, string profileHome, string path, byte[] before)
    {
        try
        {
            return Restore(store, profileHome, path, before);
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

    private static bool Restore(IStoredSignIn store, string profileHome, string path, byte[] before)
    {
        if (store.Read(profileHome) is not { Cleared: true }) return false;
        if (HarnessSettings.AccountAt(profileHome) is { } at && SignInMarks.Files(at.Home, at.Agent, at.Account).Count > 0) return false;

        var temp = path + ".daoris-kept." + Guid.NewGuid().ToString("N")[..8];
        try
        {
            File.WriteAllBytes(temp, before);
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
