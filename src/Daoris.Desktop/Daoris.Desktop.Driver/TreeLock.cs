namespace Daoris.Driver;

/// <summary>
/// A repository's session trees, held while a session starts in one and while bringing its branches up to date
/// replays them (WSR6's open window, closed by LEFT2): one file under the home per repository, which a start takes
/// shared and a replay takes alone.
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
/// <para><b>Never waited for.</b> A start that finds a replay holding the repository is held for this look and
/// carried on at the next, as every other hold before a record is. A replay that finds a start leaves the branches as
/// they were and says so. Neither holds the other for long: a start holds it for the seconds between choosing its
/// tree and opening its record, and a replay for its rebases.</para>
///
/// <para><b>Under the home's <c>locks/</c></b>, not the trees home, which the clean-up tidies and the tree list reads.</para>
/// </remarks>
public sealed class TreeLock : IDisposable
{
    /// <summary>The folder under the home.</summary>
    public const string Folder = "locks";

    private readonly FileStream _file;

    private TreeLock(FileStream file) => _file = file;

    /// <summary>
    /// Where a repository's lock is. One file whatever the name's case, since a registry names a repository without
    /// regard to it, and a workspace left unnamed is the default one.
    /// </summary>
    public static string PathOf(string home, string? workspace, string repository) => Path.Combine(
        home, Folder, "trees", RemoteTarget.Workspace(workspace).ToLowerInvariant(), $"{repository.Trim().ToLowerInvariant()}.lock");

    /// <summary>
    /// Hold the repository for a session starting in one of its trees, beside any other start — or null while a
    /// replay holds it.
    /// </summary>
    public static TreeLock? TryStarting(string home, string? workspace, string repository) =>
        TryOpen(PathOf(home, workspace, repository), FileAccess.Read, FileShare.Read);

    /// <summary>
    /// Hold the repository alone, to replay its branches — or null while a session is starting in one of its trees,
    /// or another replay holds it.
    /// </summary>
    public static TreeLock? TryReplaying(string home, string? workspace, string repository) =>
        TryOpen(PathOf(home, workspace, repository), FileAccess.ReadWrite, FileShare.None);

    /// <summary>What a start says when a replay holds its repository: held now, carried on at the next look.</summary>
    public static string Replaying(string repository) =>
        $"`{repository}`'s branches are being brought up to date right now, so a session does not start in one of its "
        + "trees until that is done; it starts at the next look.";

    /// <summary>What a replay says of each branch it left because a session was starting in the repository.</summary>
    public static string Starting(string repository) =>
        $"a session was starting in one of `{repository}`'s trees, so its branches were left as they were for another look";

    /// <remarks>
    /// A sharing violation is the file held the other way, which is an answer: null. A folder that cannot be made or
    /// a file the person may not open is thrown, since "cannot tell" is never "free".
    /// </remarks>
    private static TreeLock? TryOpen(string path, FileAccess access, FileShare share)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        try
        {
            return new TreeLock(new FileStream(path, FileMode.OpenOrCreate, access, share));
        }
        catch (IOException)
        {
            return null;
        }
    }

    public void Dispose() => _file.Dispose();
}
