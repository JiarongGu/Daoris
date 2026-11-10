using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeRefs;
using static Daoris.Driver.TreeWorktrees;
using static Daoris.Driver.LandingSayings;

namespace Daoris.Driver;

/// <summary>A session tree's work merged into its line by the rule's merge form.</summary>
internal sealed class TreeMerges(TreeContext context)
{
    /// <summary>
    /// Merge a session tree's commits into the canonical line, in the repository's own root.
    /// </summary>
    /// <remarks>
    /// <para><b>Nothing merges itself.</b> This is only ever the person pressing it — automating
    /// a local, reversible act would be allowed, and it stays a press because it is <i>where their
    /// verification lands</i>.</para>
    ///
    /// <para>🔴 <b>This is the one place Daoris writes into a checkout it did not create</b>, so every
    /// guard `reaching-in` names is here and each refuses rather than repairing. The tree must be
    /// under the trees home (a checkout is never ours to merge FROM); the root must be <b>clean</b>,
    /// because somebody's work in flight is exactly what that document is written from; the root must
    /// already be <b>on</b> the canonical line, because switching a branch in a checkout we do not own
    /// is the same trespass in a smaller form; and every check is made <b>immediately before</b> the
    /// merge in this one call, because a look at the root expires the moment you look away.
    /// Assume another session is working in that repository right now — it very often is.</para>
    ///
    /// <para><b>A conflict aborts and reports</b>, leaving the root exactly as it stood. Nothing here
    /// resolves anything, forces anything or removes anything: the tree survives a merge whether it
    /// succeeded or not, and discarding it stays a separate act the person asks for.</para>
    /// </remarks>
    /// <param name="replay">
    /// What the landing replayed onto the line here, leaving out what the line holds:
    /// its tip is merged in place of the session's branch. Null merges the branch as made.
    /// </param>
    /// <param name="uncommitted">
    /// The session tree's uncommitted paths as the landing's judgement counted them a moment before; null asks git.
    /// </param>
    /// <param name="here">
    /// The session tree's repository and branch as its landing read them a moment ago; null reads them here. Only the
    /// tree's: every check of the root is still made here, immediately before the merge.
    /// </param>
    /// <param name="pinned">
    /// The commit the door judged (<see cref="LandingGate.Tip"/>: a graph run's <c>handed</c>'s): the branch must stand there
    /// immediately before the merge, and the merge takes that commit, never the branch as it stands then. Null merges the branch as it stands.
    /// </param>
    /// <param name="beside">
    /// The ledger's sessions and their trees (<see cref="BranchTidying.HoldersAsync"/>), which the promotion reads inside the
    /// checkout's hold, so the merge waits for a session working in that checkout. Null asks nothing and merges as before.
    /// </param>
    /// <param name="delivering">
    /// The delivery this merge performs: the line's entry written ahead of the press, which the promotion's effect makes under the
    /// checkout's lock, and its outcome read from the line after. Null merges as before.
    /// </param>
    internal async Task<TreeMerge> MergeAsync(
        string path, CancellationToken ct = default, LandReplay? replay = null, int? uncommitted = null, CommonDirAndBranch? here = null,
        string? pinned = null, Func<CancellationToken, Task<IReadOnlyList<TreeHolder>>>? beside = null, TreeDelivering? delivering = null)
    {
        var full = Path.GetFullPath(path);
        if (!context.Holds(full))
        {
            return new(false, $"{path} is not a session tree — this merges only trees Daoris opened, "
                + $"under {context.TreesRoot}.");
        }

        if (!Directory.Exists(full))
        {
            return new(false, $"there is no tree at {path}.");
        }

        // Its repository and its branch, one start, or the landing's own read of them a moment ago: what it lands
        // is read from that branch below, as it stands.
        here ??= await CommonDirAndBranchAsync(full, ct).ConfigureAwait(false);
        if (here.Code != 0)
        {
            return new(false, $"{path} is not a working tree git recognises: {FirstLine(here.Err)}");
        }

        // The tree's own working root, from the read above, before its status is read or anything merged.
        if (await TreeBinding.RefusalAsync(full, ct, here.TopLevel).ConfigureAwait(false) is { } bound)
        {
            return new(false, $"Nothing was merged: {bound}.");
        }

        var root = Path.GetDirectoryName(here.CommonDir.Trim())!;
        var branch = here.Branch.Trim();
        if (branch is "" or "HEAD")
        {
            return new(false, $"the tree at {path} is not on a branch, so there is nothing to merge.");
        }

        // Uncommitted work in the SESSION tree would silently not travel. Saying "merged" while
        // leaving it behind is the worst answer available: the person believes the work moved.
        if ((uncommitted ?? await DirtyAsync(full, ct).ConfigureAwait(false)) is > 0 and var count)
        {
            return new(false,
                $"the session's tree has uncommitted work — {count} path(s) — which a merge would "
                + "leave behind. Commit it in the tree first, or decide it is not wanted.")
            { Refusal = LandingCode.Uncommitted };
        }

        var (workspace, repository) = context.OwnerOf(full);
        var line = await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false);
        if (line.Branch is not { } canonical)
        {
            return new(false, "this repository has no canonical line git can name, and none is set, so "
                + "there is nowhere to merge to. Set one with `daoris place line`.");
        }

        // Pinned, the work merged is the commit judged, read here, as late as the root's own checks.
        if (pinned is not null && await CommitOfAsync(root, $"refs/heads/{branch}", ct).ConfigureAwait(false) is { } stands
            && !string.Equals(stands, pinned, StringComparison.OrdinalIgnoreCase))
        {
            return new(false, Moved(branch, stands, pinned).Message);
        }

        var merging = pinned ?? branch;

        // 🔴 Compared where git has the line, and a comparison git could not make is not "nothing to
        // merge": a line only origin has made `log` fail, and its empty output would say the work is there.
        var against = await ComparableAsync(root, canonical, ct).ConfigureAwait(false) ?? canonical;
        var (aheadCode, ahead, aheadErr) = await WorkingTree.GitAsync(
            root, ["log", "--oneline", $"{against}..{merging}"], ct).ConfigureAwait(false);
        if (aheadCode != 0)
        {
            return new(false, $"git cannot compare `{branch}` with `{canonical}` here: {FirstLine(aheadErr)} "
                + "Fetch it, or set another line with `daoris place line`.");
        }

        if (string.IsNullOrWhiteSpace(ahead))
        {
            // An empty merge commit would say work moved when none did.
            return new(false, $"`{canonical}` already holds everything on `{branch}` — nothing to merge.") { Refusal = LandingCode.LineHolds };
        }

        // 🔴 The root's state, read as late as possible. A tree-state observation is a fact about a
        // moment, and the whole premise here is that somebody else may be working in this repository.
        var (clean, detail, rootStatus) = await WorkingTree.ReadCleanAsync(root, ct).ConfigureAwait(false);
        if (!clean)
        {
            return new(false,
                $"the repository's own checkout is not clean ({detail}), and merging into somebody's "
                + "work in flight is exactly what Daoris does not do. Deal with it there, then merge.")
            {
                Refusal = rootStatus is null ? null : LandingCode.RootDirty,
                RootStatus = rootStatus,
            };
        }

        var (_, onOut, _) = await WorkingTree.GitAsync(
            root, ["rev-parse", "--abbrev-ref", "HEAD"], ct).ConfigureAwait(false);
        var on = onOut.Trim();
        if (!string.Equals(on, canonical, StringComparison.Ordinal))
        {
            return new(false,
                $"the repository's checkout is on `{on}`, not {CanonicalLine.Describe(line)}. Daoris does "
                + $"not switch a branch in a checkout it did not create — put it on `{canonical}`, then merge.");
        }

        // The press is carried by the integration transport under `{ owner, none, 1, merge }`.
        // The merge is made `--no-ff` in the line's integration tree, one commit a person can read and revert as a unit, under the branch's
        // name whether a replay's tip or a pinned commit is merged, with the repository's own hooks; the checkout is then
        // only fast-forwarded to it. A conflict or a refusing hook leaves the checkout as it stood, in today's words.
        var frozen = replay?.Now ?? pinned ?? await CommitOfAsync(root, $"refs/heads/{branch}", ct).ConfigureAwait(false);
        if (frozen is null) return new(false, $"git could not read where `{branch}` stands, so nothing was merged.");
        // No hold here. The checkout's checks above are this press's early words; the promotion's effect holds the checkout alone
        // and reads them again before it moves it, and a hold taken here would refuse that one in this very process.
        if (delivering is not null && await delivering.BeginMergeAsync(canonical, frozen).ConfigureAwait(false) is { } unbegun) return new(false, $"Nothing was merged: {unbegun}.");
        // Current's delivery is admitted by its rule and says today's message, as the press always has.
        var (carried, refused, code) = await IntegrationPresses.MergeAsync(
            context.Home, context.Config(), root, repository, workspace, canonical, branch, frozen, ct, beside, delivered: delivering is { Live: false },
            message: delivering?.Template, automatic: delivering?.Accepted == AcceptedBy.Auto).ConfigureAwait(false);
        if (delivering is not null) await delivering.MergedAsync(root, canonical, frozen, carried ? null : refused, ct).ConfigureAwait(false);
        if (!carried) return new(false, refused!) { Refusal = code };

        var landed = ahead.Trim().Split('\n').Length;
        // The line moved: the registry follows it at the next look, whichever door pressed this.
        RegistryFollowing.Moved(context.Home, repository, DateTimeOffset.UtcNow);
        return replay is null
            ? new(true, $"merged `{branch}` into `{canonical}` — {landed} commit(s).{TreeStays}")
            : new(true, $"merged `{branch}` into `{canonical}` — {LandingSyncWords.Replayed(replay.Landed.Count, canonical, replay.LeftOut)}."
                + $"{LandingSyncWords.Emptied(replay.LeftOut)}{LandingSyncWords.Settled(replay)}{TreeStays}");
    }

    /// <summary>
    /// How many paths <c>git status</c> names in the session's tree: the dirty check's own read where the landing is told nothing. A
    /// status git could not give names none, as the check always read it.
    /// </summary>
    internal static async Task<int> DirtyAsync(string full, CancellationToken ct)
    {
        var (_, dirty, _) = await WorkingTree.GitAsync(full, ["--no-optional-locks", "status", "--porcelain"], ct).ConfigureAwait(false);
        return string.IsNullOrWhiteSpace(dirty) ? 0 : dirty.Trim().Split('\n').Length;
    }
}
