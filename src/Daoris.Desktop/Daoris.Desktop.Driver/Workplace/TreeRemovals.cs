using static Daoris.Driver.TreeGit;
using static Daoris.Driver.TreeLeftovers;

namespace Daoris.Driver;

/// <summary>
/// A session tree's removal: only a tree under the trees home, unforced only behind the clean-up's proof, its branch
/// deleted only while it is the commit judged. The tidy, the landing's tidy and abandon's discard take it.
/// </summary>
internal sealed class TreeRemovals(TreeContext context, TreeFollows follows, ContentHolds content, TreeSquashes squashes)
{
    /// <summary>How long a discard waits for a landing of its tree before it leaves the tree.</summary>
    private static readonly TimeSpan DiscardWait = TimeSpan.FromSeconds(30);

    internal static string Reserved(string path, TreeHandOff row) =>
        $"the tree at {path} stays: run `{row.Run}`'s `resolve` holds it until {row.UntilSaid}; stop that step to discard it.";

    /// <summary>
    /// Remove a session tree — <b>and only a session tree</b>: anything outside the trees home is
    /// somebody's checkout, and is refused before git is asked anything.
    /// </summary>
    /// <param name="force">The person saying it again, meaning it: work and branch go too.</param>
    /// <param name="service">The person's door supplies the local ledger: a held tree stays, and force can lift an ended session's own lock.</param>
    /// <param name="landing">A landing's own tidy, which holds the tree's landing hold already.</param>
    internal async Task<TreeRemoval> RemoveAsync(
        string path, bool force = false, CancellationToken ct = default, ServiceClient? service = null, bool landing = false)
    {
        var full = Path.GetFullPath(path);
        if (!context.Holds(full))
        {
            return new(false, $"{path} is not a session tree — this removes only trees Daoris opened, "
                + $"under {context.TreesRoot}. A checkout is never a side effect's to delete."
                // A branch a landing made is not a tree, and has a door of its own.
                + (context.Recorded.All().Any(entry => string.Equals(entry.Branch, path, StringComparison.Ordinal))
                    ? $" `{path}` names a branch a landing made: `trees remove {path} --why \"…\"` withdraws it where the line will never hold its work."
                    : ""));
        }

        if (!Directory.Exists(full))
        {
            return new(false, $"there is no tree at {path}.");
        }

        // The main root, from git itself: the common dir is the main repository's .git, wherever the registered root is.
        // Read with the branch and the working root before anything is written, the session lock a forced removal lets go of
        // below included, since a tree git binds to another checkout stays, said, forced or not.
        var here = await CommonDirAndBranchAsync(full, ct).ConfigureAwait(false);
        if (here.Code == 0 && await TreeBinding.RefusalAsync(full, ct, here.TopLevel).ConfigureAwait(false) is { } bound)
        {
            return new(false, $"the tree at {path} stays: {bound}.");
        }

        // The tree's landing hold first, as the door's lock order takes it, so a press cannot bind it mid-removal; a
        // landing's own tidy holds it already.
        using var landingHold = landing ? null : await LandingHolds.TakeAsync(context.Home, full, ct, DiscardWait).ConfigureAwait(false);
        if (!landing && landingHold is null) return new(false, $"the tree at {path} stays: a landing of it was still under way.");

        if (new TreeHandOffs(context.Home).Of(full) is { } handOff) return new(false, Reserved(path, handOff));

        // Keep the repository's starts out from the ledger read through the removal. A stale lock can name an ended
        // session while a later one holds the same tree, so the live-tree check precedes the lock owner's record.
        var owner = context.OwnerOf(full);
        using var removing = service is null ? null : TreeLock.TryReplaying(context.Home, full, owner.Repository, TreeLock.Tidying);
        // Then the bindings under the record lock, with no service too, where no tree lock is taken.
        if ((service is null || removing is not null) && TogetherGuard.Refusal(context.Home, full) is { } together)
        {
            return new(false, $"the tree at {path} stays: {together}.");
        }

        if (service is not null)
        {
            if (removing is null) return new(false, TreeLock.Starting(owner.Repository, TreeLock.RefusedBy(context.Home, full)));
            if ((await service.ActiveSessionsAsync(ct).ConfigureAwait(false)).Any(session => session.Tree is { Length: > 0 } tree && SamePath(tree, full)))
                return new(false, SessionBranchDiscard.Held(owner.Repository, follows.BranchOfTree(full)?.Branch ?? Path.GetFileName(full)));
            if (force && SessionLocks.HolderOf(full) is { } holder
                && await service.RecordAsync(holder, ct).ConfigureAwait(false) is { Tree: { Length: > 0 } heldTree } record && SamePath(heldTree, full))
                await SessionLocks.LetGoEndedAsync(record).ConfigureAwait(false);
        }

        if (here.Code != 0)
        {
            return new(false, $"{path} is not a working tree git recognises: {FirstLine(here.Err)}");
        }

        var root = Path.GetDirectoryName(here.CommonDir.Trim())!;
        var branch = here.Branch.Trim();
        var detached = branch is "" or "HEAD";
        // The name a recovery ref is kept under: the branch, or for a detached tree the one its layout names.
        var named = detached ? follows.BranchOfTree(full)?.Branch ?? "HEAD" : branch;
        // The branch the session's agent made where the tree is on one: it goes with no tree, whatever force says.
        var madeBy = detached ? null : await TreeFollows.OwnBranchOfAsync(full, ct).ConfigureAwait(false);
        ContentHold? held = null;
        ContentHolds.TreeHolds? seen = null;
        LandedBranch? squashedOn = null;
        string? judged = null;
        string? kept = null;
        RecoveryName? keep = null;
        // A detached tree's commit kept while its tree goes, until what its proof rested on is shown standing.
        string? recovery = null;
        // What the proof that clears the branch rests on, each ref at the commit it read: returned by that proof
        // and verified exactly by the delete, never looked for again.
        IReadOnlyList<(string Ref, string Commit)> rests = [];

        if (!force)
        {
            // Nothing deletes itself. Two different kinds of work can be lost here, and
            // the refusal names the kind, because they are fixed differently: uncommitted changes are
            // committed or abandoned IN the tree; unmerged commits are merged FROM it.
            // Asked so no setting hides one: an untracked file `status.showUntrackedFiles=no` hides,
            // and an ignored one only this tree holds, which `git worktree remove` deletes without asking.
            // The output the repository declares counts only as the owner accepted it for integration.
            var declared = await context.ScratchAsync(root, owner.Repository, owner.Workspace, ct).ConfigureAwait(false);
            var holds = await ContentHolds.HoldsAsync(
                full, root, declared, ct, new LinePlaces(context.Home).SlotAt(owner.Workspace, owner.Repository, full),
                TreeFilesRecord.Read(context.Home, full)).ConfigureAwait(false);
            seen = holds;
            if (holds is null)
            {
                return new(false, $"Daoris cannot tell what the tree at {path} holds beyond its commits. "
                    + "The tree stays; say it again with --force to discard it.");
            }

            if (holds.Uncommitted.Count > 0)
            {
                return new(false,
                    $"the tree at {path} has uncommitted work — {holds.Uncommitted.Count} path(s). Removing it would "
                    + "destroy that work; commit it there or say it again with --force, meaning it.");
            }

            if (holds.OwnSaid is { } own)
            {
                return new(false,
                    $"the tree at {path} holds {own}. Removing it would destroy them; move them somewhere "
                    + "of yours, or say it again with --force, meaning it.");
            }

            // A tree on a branch its agent made loses no commit when it goes, since that branch stays and holds them: only
            // what no commit holds is asked of it (above), and the branch is never taken with it.
            if (madeBy is null)
            {
                // The commit judged, read once: every question below is asked of it, and the branch goes only while
                // it is still that commit.
                judged = await MachinePorts.Repository.CommitOfAsync(full, "HEAD", ct).ConfigureAwait(false);
                if (judged is null)
                {
                    return new(false,
                        $"Daoris cannot tell whether the work on `{branch}` is landed: git names no commit at HEAD. "
                        + "The tree stays; say it again with --force to discard it.");
                }

                var (workspace, repository) = context.OwnerOf(full);
                var line = (await context.LineAsync(root, repository, workspace, ct).ConfigureAwait(false)).Branch;
                var canonical = line ?? "HEAD";
                // Landed: a branch of the person's holds every commit — the line, a branch the branch form
                // made, one they pushed. Asked of the tree's own HEAD, so a detached tree is judged by what it
                // holds. 🔴 A comparison git could not make keeps the tree: an empty answer is not "landed".
                var (logCode, unmerged, logErr) = await TreeFollows.UnlandedLogAsync(full, judged, ct).ConfigureAwait(false);
                if (logCode != 0)
                {
                    return new(false,
                        $"Daoris cannot tell whether the work on `{branch}` is landed: {FirstLine(logErr)} "
                        + "The tree stays; say it again with --force to discard it.");
                }

                // A squash merge or a cherry-pick holds the work under commits of its own, which ancestry cannot see;
                // the content can. Before it, a squashing landing's record, which vouches as ancestry would.
                // Work held nowhere still refuses, and force stays its door.
                ContentProof? proof = null;
                if (string.IsNullOrWhiteSpace(unmerged))
                {
                    // The branch of the person's holding it, at the commit read, is what the delete verifies; one
                    // gone since the judgement read none is a refusal.
                    if (await ContentHolds.HoldersAsync(root, judged, named, ct).ConfigureAwait(false) is not { } holders)
                    {
                        return new(false, $"Daoris cannot tell which branch of yours holds the work on `{branch}`. "
                            + "The tree stays; say it again with --force to discard it.");
                    }

                    if (ContentHolds.HolderOf(holders, line, ContentHolds.PersonsRef) is not { } holder)
                    {
                        return new(false, $"the branch of yours that held the work on `{branch}` went as it was judged. "
                            + "The tree stays; say it again with --force to discard it.");
                    }

                    rests = [holder];
                }
                else if (await squashes.SquashedHoldAsync(full, repository, judged, ct).ConfigureAwait(false) is { } squashed)
                {
                    squashedOn = squashed.Entry;
                    rests = [($"refs/heads/{squashed.Entry.Branch}", squashed.At)];
                }
                else if ((held = (proof = await ContentHolds.HeldByContentAsync(full, judged, line, content.LandedHere(repository), ct).ConfigureAwait(false)).Hold) is null)
                {
                    // A proof git could not read is not "held nowhere"; the refusal says it could not tell.
                    if (proof!.Unread is { } unread)
                    {
                        return new(false,
                            $"Daoris cannot tell whether the work on `{branch}` is held by content: {unread}. "
                            + "The tree stays; say it again with --force to discard it.");
                    }

                    // Where the line holds some of them under ids of their own, only those it lacks are named.
                    return new(false,
                        $"the tree at {path} holds commits `{canonical}` has not taken, and no other branch of yours holds "
                        + $"them:\n{proof.Lacking(unmerged)}\nLand them from the review, or say it again with --force to discard them.");
                }

                // The proof compared files, so the commits themselves are kept on a ref of their own: in the delete's own act
                // for a branch, and in an act of its own before the tree goes for a detached tree, which deletes no branch.
                if (held is not null)
                {
                    rests = proof!.Rests;
                    string? why;
                    if (detached)
                    {
                        (kept, why) = await RecoveryKeeps.KeepAsync(context.Home, root, context.OwnerOf(full).Repository, named, judged).ConfigureAwait(false);
                    }
                    else
                    {
                        (keep, why) = await RecoveryKeeps.NameAsync(root, named, judged, ct).ConfigureAwait(false);
                        kept = keep?.Ref;
                    }

                    if (kept is null)
                    {
                        return new(false, $"Daoris could not keep the commits on `{branch}` on a ref of their own ({why}), so nothing "
                            + "was removed. Say it again with --force to discard them.");
                    }
                }

                // A detached tree's commit is held by the tree's own HEAD, which goes with it, and by what its proof rested on,
                // which may move as it goes. No branch is deleted to verify that in, so the commit is kept on a recovery ref first, dropped
                // only in a transaction that verifies what the proof rested on once the tree went.
                if (detached && kept is null)
                {
                    var (reference, why) = await RecoveryKeeps.KeepAsync(context.Home, root, context.OwnerOf(full).Repository, named, judged).ConfigureAwait(false);
                    if (reference is null)
                    {
                        return new(false, $"Daoris could not keep the commit the tree at {path} has out on a ref of its own ({why}), so nothing "
                            + "was removed. Say it again with --force to discard it.");
                    }

                    recovery = reference;
                }
            }
        }

        // Nothing is removed: the ref this call kept a detached tree's commit on goes again, as before it; a branch's is made only as it goes.
        async Task<TreeRemoval> KeptAsync(string said)
        {
            if (recovery is not null) await ContentHolds.DropKeptVerifiedAsync(root, recovery, judged!, rests, CancellationToken.None).ConfigureAwait(false);
            return new(false, said);
        }

        // Git's removal follows a junction out of the tree and deletes what it points at, so each link in it goes as a
        // link first, forced or not; a tree whose links cannot all go stays.
        if (TreeLinks.Locked(full) is { } locked) return await KeptAsync($"the tree at {path} stays: {locked}.").ConfigureAwait(false);
        if (TreeLinks.Unlinked(full) is { } linked) return await KeptAsync($"the tree at {path} stays: {linked}.").ConfigureAwait(false);

        // A tree past Windows' 260 characters is removed with long paths, which every git call here says:
        // without them git let go of the tree, then failed deleting its files (2026-09-28).
        var removed = await MachinePorts.Repository.RemoveTreeAsync(root, full, force, ct).ConfigureAwait(false);
        string? left = null;
        if (!removed.Ok)
        {
            // Git lets go of a tree before its folder, and a folder something holds open stays (the first real
            // post-merge run): then the tree is gone and only its folder is left, which is said plainly.
            var (letGo, why) = await LetGoAsync(root, full, ct).ConfigureAwait(false);
            if (!letGo) return await KeptAsync($"git would not remove the tree: {FirstLine(removed.Error)}").ConfigureAwait(false);

            left = why;
        }

        TreeFilesRecord.Forget(context.Home, full);

        // The line has finished: the ignored tops its tree held, read before it went, are what the found layer's finish looks at.
        if (seen is not null) NoteFinished(owner.Repository, seen);

        // The branch goes with its tree — where this call's proof cleared it, only while it is still the commit judged,
        // or where the person forced it. Never git's own -d, which asks only whether the checkout's HEAD holds
        // it and kept every branch the branch form had landed. A branch left behind would resurrect "never reused" as a pile.
        string? stays = null;
        if (madeBy is not null)
        {
            // Never a branch the session's agent made, which is the pull request's and the only copy of its commits.
            return new(true, $"removed the session tree at {path}, and kept its branch `{madeBy}`, which the session made and which holds its "
                + "commits; delete it with git where you no longer want it." + (left is null ? "" : $" {left}"));
        }

        if (!detached)
        {
            if (judged is null)
            {
                stays = await JournaledDeletes.ForcedAsync(content, root, context.OwnerOf(full).Repository, branch, ct).ConfigureAwait(false);
            }
            else
            {
                stays = await content.DeleteJudgedAsync(root, context.OwnerOf(full).Repository, branch, judged, rests, ct, keep).ConfigureAwait(false);
            }

            // The record drops what is gone: a name no branch has is never judged by a start it no longer has.
            if (stays is null) Forget(context.OwnerOf(full).Repository, branch);
        }
        else if (recovery is not null
                 && !await ContentHolds.DropKeptVerifiedAsync(root, recovery, judged!, rests, CancellationToken.None).ConfigureAwait(false))
        {
            // Not cancelled, as a judged delete is not: the ref goes only where the transaction showed the holder standing.
            return new(true, $"removed the session tree at {path}. The commit it had out stays at `{recovery}` until you delete that ref: git "
                + "would not show what its proof rested on standing where the proof read it, once the tree went." + (left is null ? "" : $" {left}"));
        }

        if (stays is not null)
        {
            return new(true, $"removed the session tree at {path}, and kept its branch `{branch}`: {stays}"
                + (stays == ContentHolds.MovedSince ? ", so it holds work this removal did not judge." : ".") + (left is null ? "" : $" {left}"));
        }

        // How the work is found held, where ancestry could not say, and where its commits stay: the
        // sentence the review's Discard shows as it is.
        return new(true, $"removed the session tree at {path} (branch `{branch}`)"
            + (held is not null ? $": {held.Said}." : squashedOn is not null ? $": {TreeSquashes.SquashedSaid(squashedOn)}." : ".")
            + (left is null ? "" : $" {left}") + (kept is null ? "" : $" {ContentHold.KeptAt(named, kept)}"));
    }

    /// <summary>The found layer's finish for one removed tree; a layer that cannot be written is written again by the next reading.</summary>
    private void NoteFinished(string repository, ContentHolds.TreeHolds seen)
    {
        try
        {
            FoundReads.Finished(context.Home, repository, [.. seen.Shared, .. seen.Written, .. seen.Ignored]);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Nothing else depends on the observation.
        }
    }

    /// <summary>The record forgets one branch; a record that could not be written only keeps a name no branch has.</summary>
    internal void Forget(string repository, string branch)
    {
        try
        {
            context.Grown.Forget(repository, [branch]);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The next landing's tidy, or the clean-up, forgets it again.
        }
    }
}
