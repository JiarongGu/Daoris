using System.Text.Json;
using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// What a folder under a repository's trees that is no tree of its own was to the look (AUTOTIDY2c, OPENCUT1): the machine log's
/// <c>branch.tidied</c> <c>kind</c>, beside the clean-up's kinds.
/// </summary>
public static class StrayKind
{
    /// <summary>A folder git lists no tree in: one a removed tree left, a half-removed tree's, or one a session wrote beside its tree.</summary>
    public const string Folder = "stray";

    /// <summary>A tree whose opening was cut off: locked by an open that no running process holds (OPENCUT1).</summary>
    public const string Opening = "opening";
}

/// <summary>A folder under a repository's trees that the look met as no tree of its own (AUTOTIDY2c, OPENCUT1): moved aside, or kept and why.</summary>
/// <param name="Folder">Where it was, under the trees home.</param>
/// <param name="Kind">What it was (<see cref="StrayKind"/>).</param>
/// <param name="Moved">Whether it was moved aside.</param>
/// <param name="Message">Kept: why, in a clause. A cut-off opening moved aside: why its lock was a cut-off opening's, in a clause.</param>
public sealed record StrayResult(string Folder, string Kind, bool Moved, string Message)
{
    /// <summary>Where it was moved aside to; null where it stays.</summary>
    public string? MovedTo { get; init; }

    /// <summary>A cut-off opening's session branch (<c>daoris/s-…</c>), where it had one; null for a folder git lists no tree in.</summary>
    public string? Branch { get; init; }

    /// <summary>A cut-off opening moved aside whose branch stayed: why, in a clause; null where the branch went, or there was none.</summary>
    public string? BranchKept { get; init; }

    /// <summary>Kept: why, as the machine log's <c>branch.kept</c> code (<see cref="TidyKept"/>); null where it was moved aside.</summary>
    public string? Kept { get; init; }

    /// <summary>Kept <see cref="TidyKept.Unread"/>: which read it was (<see cref="TidyCause"/>).</summary>
    public string? Cause { get; init; }
}

/// <summary>
/// The look's own tidy of the folders under a repository's trees that are no tree of its own (AUTOTIDY2c, OPENCUT1; D88's
/// AUTOTIDY2c note): each folder git lists no tree in, and each tree whose opening was cut off, moved aside whole into the trees
/// home's <c>.tidied</c>, as the look moves a tree's folder, so nothing in it is lost before the purge's fourteen days.
/// </summary>
/// <remarks>
/// <para><b>What goes.</b> A folder under <c>trees/&lt;workspace&gt;/&lt;repository&gt;/</c> that git lists no tree in, once nothing
/// in it was written for <see cref="Quiet"/>: an empty one a removed tree left, a tree half removed, a folder a session wrote beside
/// its tree. And a tree git lists, locked by an opening no running process holds (<see cref="TreeOpenings.CutOff"/>), on its own
/// session branch or none yet: its lock let go of, its folder moved aside and its record let go of, and its branch removed at the
/// commit judged where every commit on it is held by another ref.</para>
///
/// <para><b>What stays.</b> A folder a session running or waiting names, or a review shows from; a link; the folders other flows
/// make and remove themselves outside the repository's hold (an opinion's copy, a replay's tree), whose next pass moves aside what
/// a killed one left (<see cref="PassLeftovers"/>, STRAYOTHERS1); every tree git lists that is not a
/// cut-off opening's; and everything, where git could not list the trees or the service did not say which sessions run.</para>
///
/// <para><b>Judged, held, judged again.</b> As the session branches are (AUTOTIDY2a): judged before the hold, the sessions asked only
/// where something may go, so a look with nothing to move asks nothing more and holds nothing; then inside the repository's hold
/// (<see cref="TreeLock"/>), which every start takes while it opens a tree, with the sessions asked again, each judged again and
/// moved only where it is judged the same. A move is never cancelled halfway.</para>
/// </remarks>
internal sealed class TreeStrays(TreeContext context, TreeSetAside setAside, ContentHolds content)
{
    /// <summary>
    /// How long a folder git lists no tree in is left untouched before the look moves it aside: one written in since may be one a
    /// start is making or a session is using.
    /// </summary>
    internal static readonly TimeSpan Quiet = TimeSpan.FromMinutes(10);

    /// <summary>
    /// What the folders start with that other flows make under a repository's trees and remove themselves, outside the repository's
    /// hold: an opinion's copy (<see cref="OpinionTree.Prefix"/>) and a replay's tree (<see cref="BranchReplays.Prefix"/>). What a
    /// killed pass of theirs left, the next pass of its kind moves aside (<see cref="PassLeftovers"/>, STRAYOTHERS1).
    /// </summary>
    private static readonly string[] OthersOwn = [OpinionTree.Prefix, BranchReplays.Prefix];

    /// <summary>A folder judged to go: what it is, and for a cut-off opening its branch, the commit judged, its lock's reason and why.</summary>
    private sealed record Found(string Folder, string Kind, string? Branch = null, string? Tip = null, string? Reason = null, string? Clause = null)
    {
        /// <summary>The other ref that held the branch's tip when it was judged, at its commit then (FABRIC-END4a, second review): the delete verifies it.</summary>
        public (string Ref, string Commit)? Holder { get; init; }
    }

    /// <summary>
    /// The look's tidy of the folders under one repository's trees (AUTOTIDY2c, OPENCUT1): what it moved aside and what it kept. A
    /// repository it could not judge, or held by a start, is left quietly for a later look: its session branches' pass says so.
    /// </summary>
    /// <param name="inUse">The trees sessions still running or waiting name, and those a review shows from.</param>
    /// <param name="now">What time it is, against what was last written in a folder (<see cref="Quiet"/>).</param>
    internal async Task<IReadOnlyList<StrayResult>> TidyAsync(
        string root, string repository, string? workspace, Func<CancellationToken, Task<IReadOnlySet<string>>> inUse, DateTimeOffset now,
        CancellationToken ct = default)
    {
        var space = RemoteTarget.Workspace(workspace);
        var trees = Path.Combine(context.TreesRoot, space, repository);
        // Asked before any git, so a repository with nothing under its trees costs nothing.
        if (!Directory.Exists(trees) || !Directory.EnumerateDirectories(trees).Any()) return [];
        // Its trees reached through a link are somewhere else (AUTOTIDY1), and git lists them where they are.
        if (context.Worktrees.LinkBelowTreesRoot(trees) is not null) return [];
        // A checkout that is not a repository of its own would list the trees of the repository above it (FIX-LOG), and every
        // folder here would be none of them.
        if (!Directory.Exists(Path.Combine(root, ".git"))) return [];
        var (topCode, toplevel, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--show-toplevel"], ct).ConfigureAwait(false);
        if (topCode != 0 || !SamePath(toplevel.Trim(), root)) return [];

        // The sessions are asked only where something may go, so the common look, at a repository's trees alone, asks nothing more.
        var (first, unread) = await FindAsync(root, trees, new HashSet<string>(), now, ct).ConfigureAwait(false);
        if (first.Count == 0) return unread;
        if (await BusyAsync(inUse, ct).ConfigureAwait(false) is not { } before) return unread;
        var found = first.Where(each => !before.Contains(Normal(each.Folder))).ToList();
        if (found.Count == 0) return unread;

        using var hold = TreeLock.TryReplaying(context.Home, space, repository);
        if (hold is null) return unread;
        if (await BusyAsync(inUse, ct).ConfigureAwait(false) is not { } busy) return unread;
        var (again, _) = await FindAsync(root, trees, busy, now, ct).ConfigureAwait(false);
        var results = new List<StrayResult>(unread);
        // From here each step moves, or finishes a move: none is cancelled halfway, and cancellation is heard between folders.
        var done = CancellationToken.None;
        foreach (var each in again.Where(found.Contains))
        {
            if (ct.IsCancellationRequested) break;
            results.Add(each.Kind == StrayKind.Opening
                ? await CutOffAsync(root, space, repository, each, done).ConfigureAwait(false)
                : Moved(each, setAside.MoveFolderAside(each.Folder, space, repository)));
        }

        return results;
    }

    /// <summary>
    /// The folders under <paramref name="trees"/> that go, judged now, and those whose folders could not be read. A list of the
    /// working trees git could not give is never "no tree here" (AUTOTIDY1): nothing goes.
    /// </summary>
    private async Task<(List<Found> Found, List<StrayResult> Unread)> FindAsync(
        string root, string trees, IReadOnlySet<string> busy, DateTimeOffset now, CancellationToken ct)
    {
        var found = new List<Found>();
        var unread = new List<StrayResult>();
        if (await context.Worktrees.ListAsync(root, ct).ConfigureAwait(false) is not { } entries) return (found, unread);
        var listed = entries.Select(entry => (Entry: entry, Normal: Normal(entry.Path))).ToList();
        foreach (var folder in Directory.EnumerateDirectories(trees))
        {
            var name = Path.GetFileName(folder);
            if (OthersOwn.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal))) continue;
            var normal = Normal(folder);
            if (busy.Contains(normal)) continue;
            if (listed.FirstOrDefault(each => SamePath(each.Normal, normal)).Entry is { } entry)
            {
                if (await OpeningAsync(root, folder, name, entry, ct).ConfigureAwait(false) is { } cut) found.Add(cut);
                continue;
            }

            try
            {
                // A link is somewhere else: a move would take the link alone, and the purge never follows one (TIDIEDLINK1).
                if (new DirectoryInfo(folder).Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                if (WrittenSince(folder, now - Quiet)) continue;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                unread.Add(new StrayResult(folder, StrayKind.Folder, false, $"its folders could not be read ({error.Message.Trim()})")
                {
                    Kept = TidyKept.Unread, Cause = TidyCause.Folders,
                });
                continue;
            }

            found.Add(new Found(folder, StrayKind.Folder));
        }

        return (found, unread);
    }

    /// <summary>
    /// A tree git lists whose opening was cut off (OPENCUT1): a session tree's folder, locked by an opening no running process holds,
    /// on its own branch or none yet, whose branch holds no commit another ref does not. Null where it is any other tree.
    /// </summary>
    private static async Task<Found?> OpeningAsync(string root, string folder, string name, WorktreeEntry entry, CancellationToken ct)
    {
        if (!entry.Locked || !name.StartsWith("s-", StringComparison.Ordinal) || !TreeOpenings.CutOff(entry.LockReason, TreeOpenings.Running))
        {
            return null;
        }

        var branch = TreeFollows.SessionPrefix + name;
        // A tree on any other branch holds someone's work there, whatever its lock says.
        if (entry.Branch is { } on && on != branch) return null;
        var (code, tip, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}^{{commit}}"], ct)
            .ConfigureAwait(false);
        var judged = code == 0 ? tip.Trim() : null;
        // An open grows its branch from a branch that stands and makes no commit of its own: one holding a commit no other ref holds
        // is someone's work, and stays the press's.
        var holder = judged is null ? null : await HeldElsewhereAsync(root, branch, judged, ct).ConfigureAwait(false);
        if (judged is not null && holder is null) return null;
        var clause = entry.LockReason == TreeOpenings.GitsOwn
            ? "git's own lock from the start of its add was left on it"
            : "the Daoris process that opened it no longer runs";
        return new Found(folder, StrayKind.Opening, judged is null ? null : branch, judged, entry.LockReason, clause) { Holder = holder };
    }

    /// <summary>
    /// The other ref that holds <paramref name="tip"/>, and so every commit on <paramref name="branch"/>, at the commit git found holding it;
    /// null where none does or git could not say.
    /// </summary>
    private static async Task<(string Ref, string Commit)?> HeldElsewhereAsync(string root, string branch, string tip, CancellationToken ct)
    {
        var (code, refs, _) = await WorkingTree.GitAsync(root, ["for-each-ref", "--contains", tip, "--format=%(objectname) %(refname)"], ct).ConfigureAwait(false);
        if (code != 0) return null;
        foreach (var row in refs.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (row.Split(' ', 2) is [var commit, var reference] && reference != $"refs/heads/{branch}") return (reference, commit);
        }

        return null;
    }

    /// <summary>
    /// A cut-off opening's tree let go of (OPENCUT1): its lock first, since git keeps a locked tree's record; then its folder moved
    /// aside and its record let go of, as the look takes a tree (<see cref="TreeSetAside.MoveAsideAsync(string, string, string, string, CancellationToken)"/>);
    /// then its branch, only while it is the commit judged. A tree that stays is locked again as it was found.
    /// </summary>
    private async Task<StrayResult> CutOffAsync(string root, string workspace, string repository, Found cut, CancellationToken done)
    {
        var (unlocked, _, unlockErr) = await WorkingTree.GitAsync(root, ["worktree", "unlock", cut.Folder], done).ConfigureAwait(false);
        if (unlocked != 0)
        {
            return new StrayResult(cut.Folder, StrayKind.Opening, false, $"git would not let go of its lock: {FirstLine(unlockErr)}")
            {
                Branch = cut.Branch, Kept = TidyKept.Refused,
            };
        }

        var moved = await setAside.MoveAsideAsync(root, cut.Folder, workspace, repository, done).ConfigureAwait(false);
        if (!moved.Gone)
        {
            await WorkingTree.GitAsync(root, ["worktree", "lock", "--reason", cut.Reason ?? TreeOpenings.GitsOwn, cut.Folder], done).ConfigureAwait(false);
            return new StrayResult(cut.Folder, StrayKind.Opening, false, moved.Sentence)
            {
                Branch = cut.Branch, Kept = moved.Code ?? TidyKept.Refused, MovedTo = moved.To,
            };
        }

        // In one transaction with the ref that held its tip when judged (FABRIC-END4a, second review).
        var branchKept = cut is { Branch: { } branch, Tip: { } tip, Holder: { } holder }
            ? await content.DeleteJudgedAsync(root, branch, tip, [holder], done).ConfigureAwait(false)
            : null;
        return new StrayResult(cut.Folder, StrayKind.Opening, true, cut.Clause ?? "")
        {
            MovedTo = moved.To, Branch = cut.Branch, BranchKept = branchKept,
        };
    }

    /// <summary>A folder git lists no tree in, moved aside, or kept and why (<see cref="TreeSetAside.MoveFolderAside"/>).</summary>
    private static StrayResult Moved(Found stray, (string? To, string? Code, string Sentence) moved) =>
        moved.Code is { } code
            ? new StrayResult(stray.Folder, StrayKind.Folder, false, moved.Sentence) { Kept = code }
            : new StrayResult(stray.Folder, StrayKind.Folder, true, "") { MovedTo = moved.To };

    /// <summary>
    /// Whether anything in <paramref name="folder"/>, the folder itself included, was written or made after <paramref name="since"/>:
    /// read from the folders, hidden ones included, never following a link, and stopping at the first. A folder that cannot be
    /// read throws. A pass's leftover is judged by it too (<see cref="PassLeftovers"/>).
    /// </summary>
    internal static bool WrittenSince(string folder, DateTimeOffset since)
    {
        var at = since.UtcDateTime;
        var top = new DirectoryInfo(folder);
        if (Newer(top)) return true;
        var options = new EnumerationOptions { IgnoreInaccessible = false, AttributesToSkip = 0, RecurseSubdirectories = false };
        var pending = new Stack<DirectoryInfo>();
        pending.Push(top);
        while (pending.TryPop(out var below))
        {
            foreach (var entry in below.EnumerateFileSystemInfos("*", options))
            {
                if (Newer(entry)) return true;
                if (entry is DirectoryInfo inner && !inner.Attributes.HasFlag(FileAttributes.ReparsePoint)) pending.Push(inner);
            }
        }

        return false;

        // A copy keeps its source's write time and takes a new creation time: either is a write here.
        bool Newer(FileSystemInfo entry) => entry.LastWriteTimeUtc > at || entry.CreationTimeUtc > at;
    }

    /// <summary>The trees sessions running or waiting name, each as it is on disk; null where the service did not say.</summary>
    private static async Task<IReadOnlySet<string>?> BusyAsync(Func<CancellationToken, Task<IReadOnlySet<string>>> inUse, CancellationToken ct)
    {
        try
        {
            return (await inUse(ct).ConfigureAwait(false)).Select(Normal).ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or JsonException)
        {
            return null;
        }
    }
}
