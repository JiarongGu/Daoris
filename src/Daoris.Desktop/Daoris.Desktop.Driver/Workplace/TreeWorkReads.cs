namespace Daoris.Driver;

/// <summary>
/// What one session's own tree holds that nobody has accepted yet (SESSUX1a, D126 §2.1; PART10k, out of <c>SessionTrees.Work.cs</c>):
/// the fact Sessions' *To review* is read from, by the clean-up's own proof (D88).
/// </summary>
/// <remarks>
/// It takes the squash, the content proof and the follow for the members it reads (<c>SquashedHolderAsync</c>, <c>LandedHere</c>,
/// <c>BranchOfTree</c>; PART10l), and names their statics by their classes (<see cref="ContentHolds.HeldByContentAsync"/>,
/// <see cref="ContentHolds.HoldsAsync"/>, <see cref="TreeFollows.OwnBranchOfAsync"/>, <see cref="TreeFollows.UnlandedAsync"/>,
/// <see cref="ContentHolds.DiscardedPrefix"/>).
/// </remarks>
internal sealed class TreeWorkReads(TreeContext context, TreeSquashes squashes, ContentHolds content, TreeFollows follows)
{
    /// <summary>
    /// The tree's uncommitted paths and the commits on it no branch of the person's holds (<see cref="TreeFollows.UnlandedAsync"/>);
    /// null where there is no tree of this home's to ask. Where there are such commits, whether the line or a person's branch
    /// holds them by content (SQUASHTIDY1b), and what an unforced discard would do then.
    /// </summary>
    /// <remarks>
    /// <para><b>Only a tree this home opened, still here, and a repository of its own.</b> 🔴 git walks UP: a folder under
    /// the trees that is no repository of its own (a leftover, D109 §5) would answer for whatever repository encloses the
    /// home, so it is never asked, and nothing is said of it. The clean-up's leftovers are where such a folder goes.</para>
    ///
    /// <para><b>Reads, and never writes.</b> <c>git status</c> and <c>git log</c> in the tree; a count git cannot give is
    /// null, which the reader keeps as work, as the clean-up keeps what it cannot clear. The content proof writes nothing
    /// either: the recovery ref an unforced discard would keep is named, never made.</para>
    ///
    /// <para><b>One commit judged</b> (SQUASHTIDY1c): the tree's commit is read once, and the count and the content proof are
    /// both asked of that id, so a commit made meanwhile is never half of what was judged.</para>
    /// </remarks>
    /// <param name="uncommitted">
    /// The tree's uncommitted paths as its door read them a moment before (LANDGIT1c: the look's automatic landing reads them where it
    /// judges the entry), so `status` is not asked again; null asks git.
    /// </param>
    /// <param name="here">The tree's repository and branch as a landing read them a moment ago (LANDGIT1b); null reads them where needed.</param>
    internal async Task<TreeWork?> WorkAsync(
        string tree, CancellationToken ct = default, int? uncommitted = null, TreeGit.CommonDirAndBranch? here = null)
    {
        if (!HeldHere(tree) || TreeFolders.TreeGone(tree) || !await WorkingTree.IsTopLevelAsync(tree, ct).ConfigureAwait(false)) return null;

        uncommitted ??= await UncommittedOfAsync(tree, ct).ConfigureAwait(false);
        var (tipCode, tipOut, _) = await WorkingTree.GitAsync(tree, ["rev-parse", "--verify", "--quiet", "HEAD^{commit}"], ct)
            .ConfigureAwait(false);
        var judged = tipCode == 0 ? tipOut.Trim() : null;
        // TREEFOLLOW1: a tree its agent moved onto a branch of its own is judged by what that branch holds beyond every other.
        var own = await TreeFollows.OwnBranchOfAsync(tree, ct).ConfigureAwait(false);
        var work = new TreeWork(await TreeFollows.UnlandedAsync(tree, judged ?? "HEAD", ct, own).ConfigureAwait(false), uncommitted) { Branch = own };
        // PROCESS1b-ii (point 3): a squashing landing's record holds them, as the branch a landing made holds its commits by ancestry.
        if (work.Commits is > 0 && judged is not null
            && await squashes.SquashedHolderAsync(tree, context.OwnerOf(Path.GetFullPath(tree)).Repository, judged, ct).ConfigureAwait(false) is not null)
        {
            return work with { Commits = 0 };
        }

        // SQUASHTIDY1b: a squash merge or a cherry-pick holds commits ancestry cannot see, and landing them again would make a
        // branch of work already there.
        if (work.Commits is not > 0 || judged is null) return work;
        var (found, lacks) = await HeldWorkAsync(tree, judged, own, here, ct).ConfigureAwait(false);
        // REVIEWLANDED1: where the line holds some of its commits under ids of their own (a landing replayed them, and the session was told
        // to rebase and made more), only the commits it lacks are work to land, as the landing's plan counts them.
        return found is not null ? work with { Held = found }
            : lacks is { Count: > 0 } && lacks.Count < work.Commits ? work with { Commits = lacks.Count }
            : work;
    }

    /// <summary>
    /// How many paths the session's own tree holds that no commit does (REVIEWDIRTY1-a, D164 point 4): what the landing's plan says
    /// before the press, since Accept refuses until they are committed. Null where the tree is not one this home opened (a person's
    /// checkout is never counted, D49 §3), is gone, or git cannot say; one <c>git status</c>, where <see cref="WorkAsync"/> reads the commits too.
    /// </summary>
    internal async Task<int?> UncommittedAsync(string tree, CancellationToken ct = default)
    {
        if (!HeldHere(tree) || TreeFolders.TreeGone(tree) || !await WorkingTree.IsTopLevelAsync(tree, ct).ConfigureAwait(false)) return null;
        return await UncommittedOfAsync(tree, ct).ConfigureAwait(false);
    }

    /// <summary>One <c>git status</c> of the tree, counted: null where git cannot say.</summary>
    internal static async Task<int?> UncommittedOfAsync(string tree, CancellationToken ct)
    {
        var (code, porcelain, _) = await WorkingTree.GitAsync(tree, ["--no-optional-locks", "status", "--porcelain"], ct).ConfigureAwait(false);
        return code != 0
            ? null
            : porcelain.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;
    }

    /// <summary>
    /// What a tree offers where the line, or a branch of the person's, holds its commits by content (SQUASHTIDY1b): the session's
    /// head's own judgement (<see cref="WorkAsync"/>, then <see cref="DiscardOffer.Of"/>), which every landing asks before anything
    /// is made (SQUASHTIDY1f) and the terminal's plan says. Null where nothing holds them, or there is no tree of this home's to ask.
    /// </summary>
    internal async Task<DiscardOffer?> DiscardOfferAsync(string tree, CancellationToken ct = default, TreeGit.CommonDirAndBranch? here = null) =>
        await WorkAsync(tree, ct, here: here).ConfigureAwait(false) is { } work ? DiscardOffer.Of(tree, work) : null;

    /// <summary>Whether this home opened the tree; a path that is no path is no tree of it.</summary>
    private bool HeldHere(string tree)
    {
        try
        {
            return context.Holds(tree);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// Where a tree's commits are held by content, by the review's Discard's own proof (SQUASHTIDY1's
    /// <see cref="ContentHolds.HeldByContentAsync"/>, with SQUASHTIDY1c's guarantees), and what an unforced discard of it would do now; null
    /// where nothing holds them, or git could not say.
    /// </summary>
    /// <remarks>
    /// <para>Asked as <see cref="SessionTrees.RemoveAsync"/> asks it: in the tree, of <paramref name="judged"/>, against the line of the
    /// workspace the layout names, and never vouched for by a branch a landing made and recorded here, which the clean-up
    /// removes on its own proof.</para>
    ///
    /// <para>🔴 <b>What the tree holds that no commit does keeps it</b>, as the unforced removal asks it: an uncommitted or
    /// untracked path, whatever git's settings hide, and an ignored path the registered checkout lacks. Then no discard is
    /// offered to go, and the clause says why. Otherwise the ref named is the one the discard would keep, numbered as it numbers
    /// one whose name another commit holds.</para>
    /// </remarks>
    /// <param name="treeBranch">The tree's own branch (TREEFOLLOW1) as the judgement read it a moment ago (LANDGIT1b), null where it is on its session
    /// branch or detached: the same read of the head, not a second one.</param>
    /// <param name="read">The tree's repository and branch as a landing read them (LANDGIT1b); null reads them here.</param>
    /// <returns>Where the work is held; else, where the line holds some of its commits, those it lacks (REVIEWLANDED1).</returns>
    private async Task<(HeldWork? Held, IReadOnlyList<LandingCommit>? Lacks)> HeldWorkAsync(
        string tree, string judged, string? treeBranch, TreeGit.CommonDirAndBranch? read, CancellationToken ct)
    {
        try
        {
            var full = Path.GetFullPath(tree);
            // Its repository, and the branch the discard below names, in one start (PROC1d-iii); a landing's own read, where it hands one.
            var here = read ?? await TreeGit.CommonDirAndBranchAsync(full, ct).ConfigureAwait(false);
            if (here.Code != 0) return (null, null);
            var root = Path.GetDirectoryName(here.CommonDir.Trim())!;
            var (workspace, repository) = context.OwnerOf(full);
            var line = (await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
            // TREEFOLLOW1: the branch its agent moved the tree onto holds its commits because it is where they are, not a copy.
            var notHolders = content.LandedHere(repository);
            if (treeBranch is { } ownBranch)
            {
                notHolders = new HashSet<string>(notHolders, StringComparer.Ordinal) { ownBranch };
            }

            var proof = await ContentHolds.HeldByContentAsync(full, judged, line, notHolders, ct).ConfigureAwait(false);
            if (proof.Hold is not { } held) return (null, proof.Lacks);

            var holds = await ContentHolds.HoldsAsync(full, root, ct).ConfigureAwait(false);
            var stays = holds is null ? "Daoris cannot tell what it holds beyond its commits"
                : holds.Uncommitted.Count > 0 ? $"it has {holds.Uncommitted.Count} uncommitted path(s), which a discard would destroy"
                : holds.OwnSaid is { } own ? $"it holds {own}, which a discard would destroy"
                : null;
            if (stays is not null) return (new HeldWork(held, null, stays), null);

            // The name the discard keeps its commits under: the tree's branch, or for a detached tree the one its layout names.
            var named = here.Branch.Trim() is { Length: > 0 } branch and not "HEAD" ? branch : follows.BranchOfTree(full)?.Branch ?? "HEAD";
            return (await DiscardedNameAsync(root, named, judged, ct).ConfigureAwait(false) is { } keeps
                ? new HeldWork(held, keeps, null)
                : new HeldWork(held, null, "every name for a ref to keep its commits at is taken"), null);
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            // A comparison that could not be made proves nothing: its commits are offered to land as ancestry counted them.
            return (null, null);
        }
    }

    /// <summary>
    /// The recovery ref an unforced discard of <paramref name="branch"/> at <paramref name="judged"/> would keep its commits at
    /// (SQUASHTIDY1c's <see cref="ContentHolds.KeepDiscardedAsync"/>), named without making it: the first of its names no ref holds, or one
    /// that holds that commit already. Null where every name is taken.
    /// </summary>
    private static async Task<string?> DiscardedNameAsync(string root, string branch, string judged, CancellationToken ct)
    {
        for (var n = 1; n <= ContentHolds.DiscardedNames; n++)
        {
            var reference = ContentHolds.DiscardedPrefix + branch + (n == 1 ? "" : $"-{n}");
            var (exists, at, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", reference], ct).ConfigureAwait(false);
            if (exists != 0 || at.Trim() == judged) return reference;
        }

        return null;
    }
}
