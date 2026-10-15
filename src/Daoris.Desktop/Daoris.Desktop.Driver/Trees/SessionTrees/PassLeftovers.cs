using System.Globalization;
using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>
/// What a pass a kill cut off left under a repository's trees (STRAYOTHERS1, D88's note): an opinion's copy
/// (<see cref="OpinionTree"/>, <c>o-</c>) or a replay's tree (<see cref="BranchReplays"/>, <c>r-</c>), each made by its own flow
/// outside the repository's hold and removed at its end. The next pass of the same kind in that repository moves each such folder
/// aside, as the look moves a tree's folder, once nothing running holds it and nothing in it was written for
/// <see cref="TreeStrays.Quiet"/>.
/// </summary>
/// <remarks>
/// <para><b>Who holds it.</b> A pass names its process where the next one reads it: a replay's tree in its lock's reason (<c>git
/// worktree add --lock</c>), an opinion's copy in a file in its own <c>.git</c> (<see cref="CopyHolder"/>). A process that runs
/// under the id named holds it, though the id may be another's since, and the folder waits until that one ends. A folder from
/// before STRAYOTHERS1 names nobody and is judged by its quiet alone, as is a replay's tree git's own lock from an add a kill cut
/// off still holds; any other lock or holder is someone else's, and keeps it.</para>
///
/// <para><b>Moved aside, never deleted here.</b> One rename on the same volume into the trees home's <c>.tidied</c>
/// (<see cref="TreeSetAside.MoveFolderAside"/>), which Windows refuses while anything holds a file in it open, so a folder still in
/// use stays for the next pass; the look's purge deletes it after its fourteen days, as it deletes a tree's. A replay's tree is
/// unlocked first and git's record of it let go of once its folder is gone
/// (<see cref="TreeSetAside.MoveAsideAsync(string, string, string, string, CancellationToken)"/>); where it stays, its lock is put back
/// as it was found. Nothing here is said: a pass's own removal says nothing either, and the purge logs what it deletes.</para>
/// </remarks>
internal sealed class PassLeftovers(TreeContext context, TreeSetAside setAside)
{
    /// <summary>What the words naming a pass's holder start with; the process's id and a closing bracket follow.</summary>
    private const string Holding = "Daoris is using this for one pass (process ";

    /// <summary>The file in an opinion's copy's own <c>.git</c> that names the process whose pass the copy is.</summary>
    internal const string CopyHolder = "daoris-holder";

    /// <summary>The words naming a pass's holder, which <c>git worktree list</c> shows a person for a replay's tree.</summary>
    internal static string HeldBy(int process) => Holding + process.ToString(CultureInfo.InvariantCulture) + ")";

    /// <summary>The process <paramref name="said"/> names as a pass's holder; null where the words are no pass's.</summary>
    internal static int? HolderOf(string? said) =>
        said is { } words && words.StartsWith(Holding, StringComparison.Ordinal) && words.EndsWith(')')
        && int.TryParse(words.AsSpan(Holding.Length, words.Length - Holding.Length - 1), NumberStyles.None, CultureInfo.InvariantCulture, out var process)
            ? process
            : null;

    /// <summary>
    /// The opinions' copies a killed pass left in one repository's trees (<paramref name="trees"/>, <c>trees/&lt;workspace&gt;/&lt;repository&gt;</c>),
    /// moved aside: where each went. Asked at a pass's start, before its own copy is made.
    /// </summary>
    /// <param name="now">What time it is, against what was last written in a copy.</param>
    /// <param name="running">Whether a process of that id runs (<see cref="TreeOpenings.Running"/>; a test's stand-in).</param>
    internal IReadOnlyList<string> ClearCopies(string trees, DateTimeOffset now, Func<int, bool> running)
    {
        var moved = new List<string>();
        if (Folders(trees, OpinionTree.Prefix) is not { Count: > 0 } folders) return moved;
        foreach (var folder in folders)
        {
            try
            {
                var mark = Path.Combine(folder, ".git", CopyHolder);
                int? holder = null;
                if (File.Exists(mark))
                {
                    holder = HolderOf(File.ReadAllText(mark).Trim());
                    // A file of that name Daoris did not write names someone it cannot ask.
                    if (holder is null) continue;
                }

                if (!Left(folder, holder, now, running)) continue;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            var (workspace, repository) = context.OwnerOf(folder);
            if (setAside.MoveFolderAside(folder, workspace, repository).To is { } to) moved.Add(to);
        }

        return moved;
    }

    /// <summary>
    /// The replays' trees a killed replay left in one repository's trees (<paramref name="trees"/>), moved aside and let go of by git:
    /// where each went. Asked at a replay's start, before its own tree is made. A list of the working trees git could not give is
    /// never "no tree here" (AUTOTIDY1): nothing goes.
    /// </summary>
    /// <param name="running">Whether a process of that id runs (<see cref="TreeOpenings.Running"/>; a test's stand-in).</param>
    internal async Task<IReadOnlyList<string>> ClearReplaysAsync(
        string root, string trees, DateTimeOffset now, Func<int, bool> running, CancellationToken ct)
    {
        var moved = new List<string>();
        // Asked before any git, so a repository with no replay's folder costs nothing.
        if (Folders(trees, BranchReplays.Prefix) is not { Count: > 0 } folders) return moved;
        if (await context.Worktrees.ListAsync(root, ct).ConfigureAwait(false) is not { } entries) return moved;
        // From here each step moves, or finishes a move: none is cancelled halfway, and cancellation is heard between folders.
        var done = CancellationToken.None;
        foreach (var folder in folders)
        {
            if (ct.IsCancellationRequested) break;
            var entry = entries.FirstOrDefault(each => SamePath(each.Path, folder));
            int? holder = null;
            if (entry is { Locked: true })
            {
                holder = HolderOf(entry.LockReason);
                // git's own lock from an add a kill cut off names nobody; any other reason is a person's.
                if (holder is null && entry.LockReason != TreeOpenings.GitsOwn) continue;
            }

            // A replay's tree stands detached: one on a branch holds someone's work there.
            if (entry is { Branch: not null }) continue;
            try
            {
                if (!Left(folder, holder, now, running)) continue;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            var (workspace, repository) = context.OwnerOf(folder);
            if (entry is null)
            {
                // Its folder is all there is of it: git lists no tree in it.
                if (setAside.MoveFolderAside(folder, workspace, repository).To is { } aside) moved.Add(aside);
                continue;
            }

            if (entry.Locked)
            {
                var (unlocked, _, _) = await WorkingTree.GitAsync(root, ["worktree", "unlock", folder], done).ConfigureAwait(false);
                if (unlocked != 0) continue;
            }

            var gone = await setAside.MoveAsideAsync(root, folder, workspace, repository, done).ConfigureAwait(false);
            if (gone.Gone)
            {
                if (gone.To is { } to) moved.Add(to);
                continue;
            }

            if (entry.Locked && Directory.Exists(folder))
            {
                await WorkingTree.GitAsync(root, ["worktree", "lock", "--reason", entry.LockReason ?? TreeOpenings.GitsOwn, folder], done)
                    .ConfigureAwait(false);
            }
        }

        return moved;
    }

    /// <summary>
    /// The folders under <paramref name="trees"/> whose names start with <paramref name="prefix"/>, links left out; null where the
    /// repository's trees are not there, or are reached through a link (AUTOTIDY1: somewhere else, whatever the path says).
    /// </summary>
    private List<string>? Folders(string trees, string prefix)
    {
        if (!Directory.Exists(trees) || context.Worktrees.LinkBelowTreesRoot(trees) is not null) return null;
        var found = new List<string>();
        foreach (var folder in Directory.EnumerateDirectories(trees))
        {
            if (!Path.GetFileName(folder).StartsWith(prefix, StringComparison.Ordinal)) continue;
            try
            {
                // A link is somewhere else: a move would take the link alone.
                if (new DirectoryInfo(folder).Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            found.Add(folder);
        }

        return found;
    }

    /// <summary>
    /// Whether a pass's folder was left by one a kill cut off: no running process holds it, and nothing in it was written or made
    /// for <see cref="TreeStrays.Quiet"/>. A folder that cannot be read throws.
    /// </summary>
    private static bool Left(string folder, int? holder, DateTimeOffset now, Func<int, bool> running) =>
        (holder is not { } process || !running(process)) && !TreeStrays.WrittenSince(folder, now - TreeStrays.Quiet);
}
