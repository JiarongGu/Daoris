using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// One status question per configuration home at a time (TOOL6g): an agent asked whether an account is signed in may
/// refresh that account's token to answer, and two asked at once on one account spend one single-use refresh token twice.
/// The second is refused, and the agent signs the account out, the first one's new tokens with it.
/// </summary>
/// <remarks>
/// <para><b>Two halves.</b> In this process, one gate per home, by the lock file's full path; across processes, the lock
/// file, since the CLI's <c>daoris agent list</c> asks the same accounts. The file is made only by creating it new, so two
/// takers cannot both hold it: it carries who took it and when, and is deleted when let go. A file left by a process that
/// died is stale after <see cref="Stale"/> (three times a status question's own 20 seconds), and the next taker removes it.
/// A taker that cannot have it within its patience does not ask: the account is then said to be unknown, never asked twice
/// at once.</para>
/// <para><b>Where.</b> Beside the accounts, under the home's <c>harnesses/.probing/</c>, never inside an account's
/// directory, which is the agent's (D66 §3): <c>&lt;agent&gt;/&lt;account&gt;.lock</c> for an account, and
/// <c>&lt;agent&gt;.lock</c> for the tool's own sign-in; and <c>&lt;agent&gt;/&lt;account&gt;.signed-in</c>, the mark a
/// sign-in or a key into the account leaves. The CLI's <c>probelock.ts</c> is the twin, and the two tables
/// (<c>ProbeLockTests</c>, <c>probelock.test.ts</c>) hold the same rows.</para>
/// </remarks>
public static class ProbeLock
{
    /// <summary>The folder under the home's <c>harnesses</c> the locks are kept in; no agent is ever named so.</summary>
    public const string Folder = ".probing";

    /// <summary>How old a lock file may be before it is taken for a holder that died: three times a status question's patience.</summary>
    public static readonly TimeSpan Stale = TimeSpan.FromSeconds(60);

    /// <summary>How long a taker waits for another holder before it gives up and does not ask.</summary>
    public static readonly TimeSpan Patience = TimeSpan.FromSeconds(30);

    private static readonly TimeSpan Poll = TimeSpan.FromMilliseconds(100);

    // One gate per lock file in this process, so two of its callers wait on each other before either touches the file.
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The lock for one configuration home: an account's, or with <paramref name="profile"/> null the tool's own.</summary>
    public static string PathOf(string home, string owner, string? profile) => profile is null
        ? Path.Combine(home, "harnesses", Folder, owner + ".lock")
        : Path.Combine(home, "harnesses", Folder, owner, profile + ".lock");

    /// <summary>
    /// Where a sign-in or a key into an account is marked (TOOL6g), beside its lock: the walk believes the agent's word that
    /// an account is signed out until a mark is newer than it, so a start held on it asks that account once per sign-out,
    /// never at every look, and a sign-in at the terminal is seen at the next look.
    /// </summary>
    public static string SignedInPathOf(string home, string owner, string profile) =>
        Path.Combine(home, "harnesses", Folder, owner, profile + ".signed-in");

    /// <summary>When an account was last marked signed in, by the mark's last write; null where it never was.</summary>
    public static DateTimeOffset? SignedIn(string home, string owner, string profile)
    {
        var path = SignedInPathOf(home, owner, profile);
        try
        {
            return File.Exists(path) ? new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Mark a sign-in or a key into the account at <paramref name="profileHome"/> (TOOL6g): a home outside the accounts'
    /// layout (<c>&lt;home&gt;/harnesses/&lt;agent&gt;/&lt;account&gt;</c>) marks nothing. A mark not written costs the
    /// walk one sign-out's wait for its backstop, never the sign-in.
    /// </summary>
    public static void MarkSignedIn(string profileHome, DateTimeOffset now)
    {
        var account = new DirectoryInfo(Path.GetFullPath(profileHome));
        if (account.Parent is not { Parent: { Name: "harnesses", Parent: { } home } } agent) return;
        var path = SignedInPathOf(home.FullName, agent.Name, account.Name);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AtomicFile.WriteText(path, $"{{\"at\":\"{now.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)}\"}}\n");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The sign-in stands; the walk believes its last word on the account until the backstop.
        }
    }

    /// <summary>
    /// Take the lock at <paramref name="path"/>, waiting while another holds it, for at most <paramref name="patience"/>
    /// (<see cref="Patience"/> when null). Null when it stayed held: the caller then does not ask.
    /// </summary>
    public static async Task<IAsyncDisposable?> TakeAsync(string path, TimeSpan? patience = null, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(path);
        var bound = patience ?? Patience;
        var waited = Stopwatch.StartNew();
        var gate = Gates.GetOrAdd(full, _ => new SemaphoreSlim(1, 1));
        if (!await gate.WaitAsync(bound, ct).ConfigureAwait(false)) return null;

        try
        {
            while (true)
            {
                if (TryCreate(full) is { } held) return new Held(gate, held);
                if (IsStale(full, DateTime.UtcNow))
                {
                    TryDelete(full);
                    continue;
                }

                if (waited.Elapsed >= bound)
                {
                    gate.Release();
                    return null;
                }

                await Task.Delay(Poll, ct).ConfigureAwait(false);
            }
        }
        catch
        {
            gate.Release();
            throw;
        }
    }

    /// <summary>Whether a lock file was last written longer than <see cref="Stale"/> before <paramref name="now"/>; a missing one is not.</summary>
    internal static bool IsStale(string path, DateTime now)
    {
        try
        {
            return File.Exists(path) && now - File.GetLastWriteTimeUtc(path) > Stale;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// The file, created new and held open: deleted when closed, so a process that dies on Windows lets it go with its
    /// handles. Null where it is there already.
    /// </summary>
    private static FileStream? TryCreate(string path)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var stream = new FileStream(
                path, FileMode.CreateNew, FileAccess.Write, FileShare.Read | FileShare.Delete, 256, FileOptions.DeleteOnClose);
            var said = $"{{\"pid\":{Environment.ProcessId},\"at\":\"{DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture)}\"}}\n";
            stream.Write(Encoding.UTF8.GetBytes(said));
            stream.Flush();
            return stream;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Another taker removed it first, or its holder still has it: the next try says which.
        }
    }

    private sealed class Held(SemaphoreSlim gate, FileStream file) : IAsyncDisposable
    {
        private int _released;

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 1) return;
            try
            {
                await file.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
