using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Daoris.Driver;

/// <summary>
/// A repository's checkout and its session trees, held while a session starts in one of them, while bringing its branches up
/// to date replays them (WSR6's open window, closed by LEFT2), and while Daoris writes the checkout (GITLOCK1): one file under
/// the home per checkout, which a start takes shared and a replay or a writer takes alone.
/// </summary>
/// <remarks>
/// <para><b>Why.</b> Bringing a repository up to date judges each session branch by whether a session running or
/// waiting holds its tree, and the service's ledger says which do. A driver that resumed a session in a tree (D79,
/// D80), or opened a fresh one from the line before it moved, between that look and the replay would start an agent
/// in a tree being rebased under it. The ledger holds a tree once the session's record is open; this holds the
/// repository from before a start chooses its tree until that record is open, and a replay takes it alone and asks
/// the ledger again inside it. Whichever comes first, the other sees it.</para>
///
/// <para><b>Shared and alone, by the file system's own sharing.</b> A start opens the file to read and lets others
/// read; a replay opens it to write and lets nobody in. Two starts in one repository go together, and a start and a
/// replay never do. Windows enforces the sharing on every handle; elsewhere .NET takes an advisory lock, shared for
/// the first and exclusive for the second, which every Daoris process honours. The handle is the lock, so a process
/// that dies lets go of it with its handles, and nothing stale can block.</para>
///
/// <para><b>One per checkout</b> (GITLOCK1): keyed by the checkout's common git directory, as git resolves it, so a checkout
/// registered twice, under two names or in two workspaces, is one lock, and a session tree names its checkout's.</para>
///
/// <para><b>Every writer of the checkout holds it alone</b> (GITLOCK1, D51's note): a line's pull and a promotion's move, each
/// from its fresh judgement through its write and its read after it (<see cref="TryWriting"/>), and every start holds it shared,
/// in its own tree or the checkout. <c>CheckoutWritersCensusTests</c> names every git write and where it writes. A holder alone
/// says what it is beside the lock (<see cref="HolderOf"/>), so a start or a writer it refuses names it.</para>
///
/// <para><b>Never waited for, and nothing waited for under it.</b> A start that finds the repository held is held for this look
/// and carried on at the next, as every other hold before a record is; a replay or a writer that finds it held leaves its work
/// and says so. A flow holding it alone waits only for a process start's place (<see cref="ProcessStarts"/>) and a record's lock
/// (<see cref="RecordLock"/>), neither of whose holders waits for anything; the other locks a flow may wait for (a tree's landing
/// hold, a line's one run, the look's tidy, the end's keeping) are taken before it or not at all, which <see cref="LockWaits"/> lets a
/// test see. So no cycle of waits passes through it. It is taken once in a call: a door that reaches a writer through another
/// (the legacy merge, through the promotion's effect) takes none itself, since a second open in one process is refused too.</para>
///
/// <para><b>Under the home's <c>locks/</c></b>, not the trees home, which the clean-up tidies and the tree list reads.</para>
/// </remarks>
public sealed class TreeLock : IDisposable
{
    /// <inheritdoc cref="HeldBy.Updating"/>
    public const string Updating = HeldBy.Updating;

    /// <inheritdoc cref="HeldBy.Tidying"/>
    public const string Tidying = HeldBy.Tidying;

    /// <inheritdoc cref="HeldBy.Promoting"/>
    public const string Promoting = HeldBy.Promoting;

    private readonly FileStream _file;
    private readonly string? _by;
    private readonly object? _outer;

    // A replay alone holds the repository for the due moments too (FABRIC-END6c), in this process: a removing press or bringing it up
    // to date, which a moment skips rather than run beside.
    private IDisposable? _held;
    private int _gone;

    private TreeLock(FileStream file, string? by, object? outer) => (_file, _by, _outer) = (file, by, outer);

    /// <summary>Whether this holds the repository alone, as a replay or a writer of its checkout does.</summary>
    internal bool Alone => _by is not null;

    /// <summary>
    /// Where a checkout's lock is: one file per common git directory, named by its digest, whatever name or workspace the checkout is
    /// registered under. <paramref name="checkout"/> is the checkout or any session tree of it.
    /// </summary>
    public static string PathOf(string home, string checkout) =>
        DaorisHome.Entry(home, HomeEntry.Locks, "trees", $"{Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(CommonDirOf(checkout))))[..32]}.lock");

    /// <summary>
    /// The checkout's common git directory as git resolves it, read from its files so no git is started: a <c>.git</c> folder, or the one
    /// a <c>.git</c> file names and then its <c>commondir</c>; full, without a trailing separator, and in one case on Windows. A folder
    /// holding no <c>.git</c> is its own key.
    /// </summary>
    internal static string CommonDirOf(string checkout)
    {
        var full = Path.GetFullPath(checkout);
        var dotGit = Path.Combine(full, ".git");
        var dir = full;
        try
        {
            if (Directory.Exists(dotGit))
            {
                dir = dotGit;
            }
            else if (File.Exists(dotGit) && File.ReadAllText(dotGit).Trim() is var text && text.StartsWith("gitdir:", StringComparison.Ordinal))
            {
                dir = Path.GetFullPath(Path.Combine(full, text["gitdir:".Length..].Trim()));
                var common = Path.Combine(dir, "commondir");
                if (File.Exists(common) && File.ReadAllText(common).Trim() is { Length: > 0 } relative) dir = Path.GetFullPath(Path.Combine(dir, relative));
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            dir = full;
        }

        var key = Path.TrimEndingDirectorySeparator(dir).Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return OperatingSystem.IsWindows() ? key.ToLowerInvariant() : key;
    }

    /// <summary>
    /// Hold the checkout for a session starting in it or one of its trees, beside any other start — or null while a replay or a writer
    /// holds it.
    /// </summary>
    public static TreeLock? TryStarting(string home, string checkout) =>
        TryOpen(PathOf(home, checkout), FileAccess.Read, FileShare.Read, by: null);

    /// <summary>
    /// Hold the checkout alone, to replay or tidy its trees — or null while a session is starting in it or one of its trees, or another
    /// holds it alone. <paramref name="by"/> says what holds it.
    /// </summary>
    /// <remarks>Held, it holds the repository for the due moments as well (<see cref="RepositoryHolds"/>, FABRIC-END6c).</remarks>
    public static TreeLock? TryReplaying(string home, string checkout, string repository, string by) => TryAlone(home, checkout, repository, by);

    /// <summary>
    /// Hold the checkout alone, to write it — or null while a session is starting in it or one of its trees, or a replay or another
    /// writer holds it.
    /// </summary>
    public static TreeLock? TryWriting(string home, string checkout, string repository, string by) => TryAlone(home, checkout, repository, by);

    private static TreeLock? TryAlone(string home, string checkout, string repository, string by)
    {
        var path = PathOf(home, checkout);
        var alone = TryOpen(path, FileAccess.ReadWrite, FileShare.None, by);
        if (alone is null) return null;
        alone._held = RepositoryHolds.Hold(home, repository);
        try
        {
            using var self = Process.GetCurrentProcess();
            AtomicFile.WriteText(ByOf(path), $"{by}\n{Environment.ProcessId}\n{self.StartTime.ToUniversalTime().Ticks}\n");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // What holds it is a courtesy to whoever it refuses: they say "another of Daoris's writes" instead.
        }

        LockWaits.Holding = alone;
        return alone;
    }

    /// <summary>
    /// What holds the checkout alone now (<see cref="Updating"/>, <see cref="Tidying"/>, <see cref="Promoting"/>), as its holder wrote it;
    /// null where nothing says, or the process that said it has ended.
    /// </summary>
    public static string? HolderOf(string home, string checkout)
    {
        try
        {
            var lines = File.ReadAllText(ByOf(PathOf(home, checkout))).Split('\n', StringSplitOptions.TrimEntries);
            if (lines.Length < 3 || !int.TryParse(lines[1], out var id) || !long.TryParse(lines[2], out var started)) return null;
            using var holder = Process.GetProcessById(id);
            return holder.HasExited || holder.StartTime.ToUniversalTime().Ticks != started ? null : lines[0];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException
                                          or System.ComponentModel.Win32Exception)
        {
            return null;
        }
    }

    /// <summary>What a start says when a replay holds its repository: held now, carried on at the next look.</summary>
    public static string Replaying(string repository) =>
        HeldWords.ReplayingWhy(repository);

    /// <summary>
    /// What a replay says of each branch it left because the repository was held: a session starting in it, or, where <paramref name="by"/>
    /// names the writer that held it alone (<see cref="HolderOf"/>), that writer.
    /// </summary>
    public static string Starting(string repository, string? by = null) => by is null
        ? $"a session was starting in one of `{repository}`'s trees, so its branches were left as they were for another look"
        : $"{Held(repository, by)}, so its branches were left as they were for another look";

    /// <summary>
    /// What a writer of the checkout says of what held the repository when it was refused: its holder alone, by what it said, or else a
    /// start, which says nothing.
    /// </summary>
    public static string Held(string repository, string? by) => by switch
    {
        Updating => $"`{repository}`'s branches were being brought up to date",
        Tidying => $"`{repository}`'s session trees were being tidied",
        Promoting => $"`{repository}`'s line was being moved to what its integration promoted",
        _ => $"a session was starting in `{repository}`, or another of Daoris's writes held it",
    };

    private static string ByOf(string path) => Path.ChangeExtension(path, ".by");

    /// <remarks>
    /// A sharing violation is the file held the other way, which is an answer: null. A folder that cannot be made or
    /// a file the person may not open is thrown, since "cannot tell" is never "free".
    /// </remarks>
    private static TreeLock? TryOpen(string path, FileAccess access, FileShare share, string? by)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        RemoveOldKeys(Path.GetDirectoryName(path)!);
        try
        {
            return new TreeLock(new FileStream(path, FileMode.OpenOrCreate, access, share), by, by is null ? null : LockWaits.Holding);
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static readonly HashSet<string> Cleared = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Removes, once for this folder in this process, the lock files of the key before the checkout's (<c>trees/&lt;workspace&gt;/&lt;repository&gt;.lock</c>),
    /// which nothing opens any more; one a live process still holds cannot be opened alone and stays.
    /// </summary>
    private static void RemoveOldKeys(string folder)
    {
        lock (Cleared)
        {
            if (!Cleared.Add(folder)) return;
        }

        try
        {
            foreach (var old in Directory.EnumerateDirectories(folder).SelectMany(each => Directory.EnumerateFiles(each, "*.lock")))
            {
                try
                {
                    using (new FileStream(old, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                    File.Delete(old);
                }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                {
                    // Held by a live process, or not ours to remove: it stays.
                }
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A folder that cannot be listed has nothing removed.
        }
    }

    public void Dispose()
    {
        // Once: a second dispose after another holder took the lock would delete what that holder said.
        if (Interlocked.Exchange(ref _gone, 1) == 1) return;
        if (Alone)
        {
            try
            {
                File.Delete(ByOf(_file.Name));
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // A holder's words left behind name a process that has ended, which HolderOf reads as nothing.
            }

            if (ReferenceEquals(LockWaits.Holding, this)) LockWaits.Holding = _outer;
        }

        _file.Dispose();
        _held?.Dispose();
    }
}
