using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeLeftovers;
using static Daoris.Driver.TreeWorktrees;

namespace Daoris.Driver;

/// <summary>
/// The person's door for one session branch (PART10l-ii, out of <c>SessionTrees.Tidy.cs</c>). Over the trees' context, the content
/// proof, the squash's record and the removal. The words beside a kept branch's row, which the terminal and the bridge say, are
/// <see cref="SessionBranchDoors"/>.
/// </summary>
internal sealed class TreeTidies(TreeContext context, ContentHolds content, TreeSquashes squashes, TreeRemovals removals)
{
    /// <summary>
    /// A session branch removed by the person's door (LAND3): a failed or superseded attempt's, whose commits no branch of
    /// theirs holds, so no landing's tidy or clean-up takes it. With its tree where it still has one, through
    /// <see cref="TreeRemovals.RemoveAsync"/>; without, the branch alone. <b>Only a session branch</b> (<c>daoris/…</c>): a branch of the
    /// person's is theirs to delete.
    /// </summary>
    /// <remarks>
    /// Unforced it is the clean-up's proof (D88): it goes only where a branch of the person's holds every commit, or where its
    /// work is held by content (SQUASHTIDY1, <see cref="ContentHolds.HeldByContentAsync"/>), and the refusal names the commits it would lose.
    /// It deletes the branch only while it is the commit judged, and a content-held one's commits stay on a recovery ref the
    /// sentence names (SQUASHTIDY1c).
    /// <paramref name="force"/> is the person saying it again, meaning it. The record
    /// forgets it once it is gone. The person's doors supply their local ledger through <see cref="SessionBranchDiscard"/>:
    /// a removal with a tree checks it again under the repository's start lock (TREELOCK2).
    /// </remarks>
    internal async Task<TreeRemoval> RemoveBranchAsync(string root, string repository, string branch, bool force = false, CancellationToken ct = default,
        ServiceClient? service = null)
    {
        if (!branch.StartsWith(TreeFollows.SessionPrefix, StringComparison.Ordinal))
        {
            return new(false, $"`{branch}` is not a session branch — only Daoris's own (`{TreeFollows.SessionPrefix}…`) are removed here, and a "
                + "branch of yours is yours to delete.");
        }

        // The root must BE the top of its own repository: git walks UP, and would answer for the one above it (FIX-LOG).
        var (topCode, toplevel, topErr) = await WorkingTree.GitAsync(root, ["rev-parse", "--show-toplevel"], ct).ConfigureAwait(false);
        if (topCode != 0 || !SamePath(toplevel.Trim(), root))
        {
            return new(false, $"`{repository}`'s checkout at {root} is not the top of a repository of its own"
                + (topCode != 0 ? $" ({FirstLine(topErr)})" : "") + ", so nothing there is removed.");
        }

        var (exists, _, _) = await WorkingTree.GitAsync(root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], ct)
            .ConfigureAwait(false);
        if (exists != 0)
        {
            // RECORDUNREAD1: a branch git could not read (a broken ref, a failed start) is not gone, so its record stays; only git's own
            // "missing" forgets it.
            var (missing, said) = await ContentHolds.MissingAsync(root, $"refs/heads/{branch}", ct).ConfigureAwait(false);
            if (!missing)
            {
                return new(false, $"`{branch}` stays: git could not read it ({said}), so it is kept as unread.");
            }

            removals.Forget(repository, branch);
            return new(false, $"there is no branch `{branch}` in `{repository}` — it is gone already.");
        }

        // A list git could not give is never "no tree here" (AUTOTIDY1): forced, it would take the branch from under a tree.
        if (await context.Worktrees.ReadAsync(root, ct).ConfigureAwait(false) is not { } worktrees)
        {
            return new(false, $"`{branch}` stays: {ListUnread}, so whether a tree has it checked out is not known.");
        }

        if (worktrees.GetValueOrDefault(branch) is { } tree)
        {
            return context.Holds(tree)
                ? await removals.RemoveAsync(tree, force, ct, service).ConfigureAwait(false)
                : new(false, $"`{branch}` is checked out at {tree}, which is not a session tree — Daoris removes nothing there.");
        }

        ContentHold? held = null;
        LandedBranch? squashedOn = null;
        string? kept = null;
        if (!force)
        {
            // The commit judged, read once (SQUASHTIDY1c): every question below is asked of it, and the branch goes only while it
            // is still that commit.
            var (tipCode, tipOut, tipErr) = await WorkingTree.GitAsync(
                root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}^{{commit}}"], ct).ConfigureAwait(false);
            var judged = tipOut.Trim();
            // The line it grew against (SQUASHTIDY1's proof by content asks it).
            var grewIn = context.Grown.All().LastOrDefault(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase)
                                                                    && string.Equals(entry.Branch, branch, StringComparison.Ordinal))?.Workspace;
            var line = (await context.LineAsync(root, repository, RemoteTarget.Workspace(grewIn), ct).ConfigureAwait(false)).Branch;
            var (logCode, unlanded, logErr) = tipCode != 0 ? (tipCode, "", tipErr) : await TreeFollows.UnlandedLogAsync(root, judged, ct).ConfigureAwait(false);
            if (logCode != 0)
            {
                return new(false, $"Daoris cannot tell whether the work on `{branch}` is landed: {FirstLine(logErr)} "
                    + "The branch stays; say it again with --force to discard it.");
            }

            // What the proof that clears it rests on (FABRIC-END4a, third review), each ref at the commit it read, returned by that proof and
            // verified exactly by the delete: the branch of the person's holding it, a squashing landing's branch, or the ref holding its
            // work by content.
            IReadOnlyList<(string Ref, string Commit)> rests = [];
            if (string.IsNullOrWhiteSpace(unlanded))
            {
                if (await ContentHolds.HoldersAsync(root, judged, branch, ct).ConfigureAwait(false) is not { } holders)
                {
                    return new(false, $"Daoris cannot tell which branch of yours holds the work on `{branch}`, so it stays. "
                        + "Say it again with --force to discard it.");
                }

                if (ContentHolds.HolderOf(holders, line, ContentHolds.PersonsRef) is not { } holder)
                {
                    return new(false, $"the branch of yours that held the work on `{branch}` went as it was judged, so it stays. "
                        + "Say it again with --force to discard it.");
                }

                rests = [holder];
            }
            else if (await squashes.SquashedHoldAsync(root, repository, judged, ct).ConfigureAwait(false) is { } squash)
            {
                // A squashing landing's record vouches as ancestry would and keeps no ref (PROCESS1b-ii point 3), while its branch stands.
                squashedOn = squash.Entry;
                rests = [($"refs/heads/{squash.Entry.Branch}", squash.At)];
            }
            else
            {
                // SQUASHTIDY1: the same proof by content as a tree's removal, against the line of the workspace it grew in.
                var proof = await ContentHolds.HeldByContentAsync(root, judged, line, content.LandedHere(repository), ct).ConfigureAwait(false);
                held = proof.Hold;
                rests = proof.Rests;
                if (proof.Unread is { } unread)
                {
                    // PROOF-UNREAD1: a proof git could not read is not "held nowhere"; the refusal says it could not tell.
                    return new(false, $"Daoris cannot tell whether the work on `{branch}` is held by content: {unread}. "
                        + "The branch stays; say it again with --force to discard it.");
                }

                if (held is null)
                {
                    return new(false, $"`{branch}` holds commits no branch of yours holds:\n{proof.Lacking(unlanded)}\n"
                        + "If its work is not wanted — a failed or superseded attempt — say it again with --force to discard them.");
                }

                // SQUASHTIDY1c: the proof compared files, so the commits themselves are kept on a ref of their own first.
                var (reference, why) = await ContentHolds.KeepDiscardedAsync(root, branch, judged, ct).ConfigureAwait(false);
                if (reference is null)
                {
                    return new(false, $"Daoris could not keep the commits on `{branch}` on a ref of their own ({why}), so it stays. "
                        + "Say it again with --force to discard them.");
                }

                kept = reference;
            }

            var stays = await content.DeleteJudgedAsync(root, repository, branch, judged, rests, ct).ConfigureAwait(false);
            if (stays is not null)
            {
                await ContentHolds.DropKeptAsync(root, branch, kept, judged, ct).ConfigureAwait(false);
                return new(false, $"`{branch}` stays: {stays}"
                    + (stays == ContentHolds.MovedSince ? ", so it holds work this removal did not judge." : "."));
            }
        }
        else
        {
            if (await JournaledDeletes.ForcedAsync(content, root, repository, branch, ct).ConfigureAwait(false) is { } stays) return new(false, $"`{branch}` stays: {stays}.");
        }

        removals.Forget(repository, branch);
        return new(true, $"removed the session branch `{branch}` from `{repository}`"
            + (held is not null ? $": {held.Said}." : squashedOn is not null ? $": {TreeSquashes.SquashedSaid(squashedOn)}." : ".")
            + (kept is null ? "" : $" {ContentHold.KeptAt(branch, kept)}"));
    }
}
