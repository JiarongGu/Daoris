using static Daoris.Driver.TreeGit;

namespace Daoris.Driver;

/// <summary>What a session's tree came to when a start looked before going on in it (TREEGONE1).</summary>
public enum TreeStand
{
    /// <summary>git lists it at its folder, which is there.</summary>
    Stands,

    /// <summary>Its folder was gone and its branch stood, so the tree was made again there, on that branch.</summary>
    Remade,

    /// <summary>Its folder and its branch are both gone: nothing on this machine holds its work under its name.</summary>
    Gone,

    /// <summary>It is not there, and it could not be made again: git could not list or add it, or a folder stands in its place.</summary>
    Unmade,
}

/// <param name="Stand">What it came to.</param>
/// <param name="Branch">The session's branch, where the tree is one this home opened; null otherwise.</param>
/// <param name="Said">What its record says of it: the remake, or why nothing goes on in it. Null where it stands.</param>
public sealed record TreeStanding(TreeStand Stand, string? Branch, Noted? Said);

/// <summary>
/// A session never runs with its tree gone (TREEGONE1, D51, D131): before a start or a resume goes on in a session's tree, the tree is
/// asked for as git lists it, on the session's branch. A folder git no longer lists, while its branch stands, is made again at the
/// same place on that branch, and the record says so; with the branch gone too, nothing holds the session's work, so nothing is made
/// and the start is held for the person. A harness is never started anywhere else in its place: the repository's checkout, a folder
/// left behind, or a fresh tree on another branch.
/// </summary>
/// <remarks>
/// <para><b>Why git's list and not the disk.</b> A tool that removes a worktree may leave its folder behind with what it could not
/// delete, and <see cref="Directory.Exists"/> then answers yes for a folder that is no tree: git asked there walks up to whatever
/// holds it. A folder left where the tree was is moved aside, never deleted, as the look's own tidy moves one
/// (<see cref="TreeSetAside.MoveFolderAside"/>), so nothing in it is lost.</para>
///
/// <para><b>The branch the session is on</b> is the one the layout names, or the one its agent moved its tree onto, as the ledger
/// learned it (TREEFOLLOW1): a tree is made again on that, so the work goes on where it was.</para>
/// </remarks>
internal sealed class TreeReturns(TreeContext context, TreeFollows follows, TreeSetAside setAside)
{
    /// <summary>
    /// The tree at <paramref name="tree"/>, of the checkout at <paramref name="root"/>, as a start finds it, made again where its
    /// folder is gone and its branch stands. Asked with the repository's trees held for a start (<see cref="TreeLock"/>).
    /// </summary>
    internal async Task<TreeStanding> EnsureAsync(string root, string tree, CancellationToken ct = default)
    {
        var full = Path.GetFullPath(tree);
        if (await TreeWorktrees.EntriesAsync(root, ct).ConfigureAwait(false) is not { } entries)
        {
            return Unmade(null, $"{TreeWorktrees.ListUnread}, so whether the tree at {full} is there could not be told; the next look asks again.");
        }

        var listed = entries.FirstOrDefault(entry => SamePath(entry.Path, full));
        var folder = Directory.Exists(full);
        if (listed is not null && folder && Path.Exists(Path.Combine(full, ".git"))) return new(TreeStand.Stands, listed.Branch, null);

        // Only a tree this home opened is made again: its place and its branch are the layout's, and nothing else is Daoris's to make.
        if (follows.BranchOfTree(full) is not { } layout)
        {
            return Unmade(null, $"the tree at {full} is gone, and it is not one this machine's Daoris opened, so it is not made again.");
        }

        // The branch its agent moved it onto, as the ledger learned it, else the layout's.
        var branch = context.Grown.Following(layout.Repository, layout.Branch)?.Now ?? layout.Branch;
        var (stands, _, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], ct)
            .ConfigureAwait(false);
        if (stands != 0)
        {
            return new(TreeStand.Gone, branch, Noted.Of(
                NoteCodes.TreeGone,
                $"its tree and its branch `{branch}` are both gone, so nothing on this machine holds its work under that name. It does not "
                + $"start anywhere else: bring the branch `{branch}` back in the repository's checkout and the next look makes its tree "
                + "again, or end this session.",
                ("branch", branch)));
        }

        // Checked out in another tree that stands: two trees on one branch is what git refuses, and that tree is someone's.
        if (entries.FirstOrDefault(entry => string.Equals(entry.Branch, branch, StringComparison.Ordinal) && !SamePath(entry.Path, full))
            is { } other && Directory.Exists(other.Path))
        {
            return Unmade(branch, $"its tree at {full} is gone, and its branch `{branch}` is checked out in {other.Path}, so it is not made again here.");
        }

        if (folder)
        {
            // A folder git no longer lists as a tree: moved aside, never deleted.
            var (to, _, why) = setAside.MoveFolderAside(full, layout.Workspace, layout.Repository, "the folder its tree left", "it");
            if (to is null) return Unmade(branch, $"git no longer lists {full} as its tree, and {why}.");
        }

        // git's record of the tree, its folder gone: that record alone, never a prune of every stale one (as an open's undo does).
        if (listed is not null)
        {
            await WorkingTree.GitAsync(root, ["worktree", "remove", "--force", "--force", full], ct).ConfigureAwait(false);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var (code, _, stderr) = await WorkingTree.GitAsync(root, ["worktree", "add", full, branch], ct).ConfigureAwait(false);
        if (code != 0) return Unmade(branch, $"its tree at {full} is gone, and git would not make it again on `{branch}`: {Failure(stderr)}");

        RefMoves.Mine(context.Home, layout.Repository, layout.Workspace, root);
        return new(TreeStand.Remade, branch, Noted.Of(
            NoteCodes.TreeRemade,
            $"its tree at {full} was gone, so Daoris made it again there on its branch `{branch}`, where its commits are. Anything it had "
            + "not committed went with the folder, and the tree holds what git tracks: its dependencies are installed again there.",
            ("branch", branch)));
    }

    private static TreeStanding Unmade(string? branch, string sentence) =>
        new(TreeStand.Unmade, branch, Noted.Of(NoteCodes.TreeUnmade, $"It does not start: {sentence}"));
}
