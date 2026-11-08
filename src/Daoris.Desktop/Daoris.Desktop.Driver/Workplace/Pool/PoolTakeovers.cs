namespace Daoris.Driver;

/// <summary>A dead run's claim, or this run's own whose undo waits, taken over by the look: its git processes ended, the claim taken by one guarded update, the
/// slot read from the disk (never the phase alone) and settled; a take or a return is put back by <see cref="PoolUndo"/>, every row written by <see cref="SlotStanding"/>.</summary>
internal sealed class PoolTakeovers(TreePool pool, PoolSeams seams)
{
    /// <summary>Take a dead run's claim on slot <paramref name="number"/> and settle it; refused, nothing changed, where it is not the look's to take.</summary>
    internal async Task<PoolDone> RecoverAsync(PoolSite site, int number, CancellationToken ct)
    {
        if (pool.ClaimOn(site.Workspace, site.Repository, number) is not { } found) return PoolDone.Not("the pool holds no claim on that slot");
        var taken = pool.TakeOver(site.Workspace, site.Repository, number, found.ClaimGeneration);
        if (taken.Claim is not { } claim || taken.Fence is not { } fence) return PoolDone.Not(taken.Refused ?? "the claim was not taken");

        // Before anything is read: the dead run's git processes ended and waited for, by the job's name the claim recorded.
        if (!await PoolGit.EndAsync(claim.Job).ConfigureAwait(false)) return PoolDone.Not("the claim's job would not empty, so the claim is held for the next look");

        using var git = PoolGit.Create(site.Home, PoolGit.NewJobName());
        if (git is null) return PoolDone.Not("there is no job to hold git in, so the claim is held for the next look");
        var slot = pool.Slot(site.Workspace, site.Repository, number);
        if (claim.Kind == TreeClaimKinds.End)
        {
            if (slot is not null) await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Free, ct).ConfigureAwait(false);
            pool.Complete(fence, null);
            return new PoolDone(slot, null, null, null);
        }

        if (claim.Kind is TreeClaimKinds.Finish or TreeClaimKinds.Retire && slot is not null)
        {
            if (SlotStanding.GitLockFile(slot.Path) is { } held && Directory.Exists(slot.Path))
            {
                return await LockedAsync(site, git, claim, fence, slot, held, ct).ConfigureAwait(false);
            }

            return await new PoolFinishRecoveries(pool, seams).RecoverAsync(site, git, claim, fence, ct).ConfigureAwait(false);
        }

        if (claim.Kind is not (TreeClaimKinds.Take or TreeClaimKinds.Return or TreeClaimKinds.Make))
        {
            return PoolDone.Not($"a {claim.Kind} claim is not settled by this build, so it is held");
        }

        if (slot is null) return await RecoverMakeAsync(site, git, claim, fence, ct).ConfigureAwait(false);
        if (SlotStanding.GitLockFile(slot.Path) is { } lockFile) return await LockedAsync(site, git, claim, fence, slot, lockFile, ct).ConfigureAwait(false);

        if (claim.Phase is TreeClaimPhases.OpeningRecord or TreeClaimPhases.Opened && seams.Opened is { } asked)
        {
            var (answer, record) = await asked(claim, ct).ConfigureAwait(false);
            if (answer == PoolOpens.NoAnswer) return PoolDone.Not("the service did not answer whether the open landed, so the claim is held for the next look");
            if (answer == PoolOpens.Landed)
            {
                await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Free, ct).ConfigureAwait(false);
                pool.Complete(fence, await new SlotStanding(slot, claim) { Made = true }.ReadAsync(ct).ConfigureAwait(false));
                return new PoolDone(pool.Slot(site.Workspace, site.Repository, number), null, null, record);
            }
        }

        return await FreeAsync(site, git, claim, fence, ct).ConfigureAwait(false);
    }

    /// <summary>The slot read from the disk and settled: a return kept as the returning line's where it switched nothing or from its clearing on, a take's
    /// slot at a commit of its own branch's <c>stuck</c>, and every other put back by the one undo, which a slot already put back passes through.</summary>
    internal async Task<PoolDone> FreeAsync(PoolSite site, PoolGit git, TreeClaim claim, ClaimFence fence, CancellationToken ct)
    {
        var slot = pool.Slot(claim.Workspace, claim.Repository, claim.Number)!;
        var branch = await WorkingTree.BranchAsync(slot.Path, ct).ConfigureAwait(false) ?? "";
        var head = await WorkingTree.HeadAsync(slot.Path, ct).ConfigureAwait(false) ?? "";
        var restoring = TreeClaimPhases.Restoring.Contains(claim.Phase);
        if (claim.Kind == TreeClaimKinds.Return && branch == claim.Branch && !restoring && (claim.ExpectBranch == claim.Branch || TreeClaimPhases.ClearedOn.Contains(claim.Phase)))
        {
            await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Free, ct).ConfigureAwait(false);
            pool.Complete(fence, await new SlotStanding(slot, claim) { Made = true }.ReadAsync(ct).ConfigureAwait(false));
            return new PoolDone(pool.Slot(claim.Workspace, claim.Repository, claim.Number), null, null, null);
        }

        var start = await FileGit.CommitAsync(slot.Path, claim.Start, ct).ConfigureAwait(false);
        if (claim.Kind == TreeClaimKinds.Take && branch == claim.Branch && head != start)
        {
            return await StuckAsync(site, git, claim, fence, slot, $"{claim.Branch} has a commit of its own, so the slot was left at it", ct).ConfigureAwait(false);
        }

        if (!restoring && head != claim.Expect && head != start)
        {
            return await StuckAsync(site, git, claim, fence, slot, "its HEAD is at neither where the claim began nor where it was switching to", ct).ConfigureAwait(false);
        }

        return await new PoolUndo(pool, seams).UndoAsync(site, git, claim, fence, null, ct).ConfigureAwait(false);
    }

    private async Task<PoolDone> LockedAsync(PoolSite site, PoolGit git, TreeClaim claim, ClaimFence fence, KeptTree slot, string lockFile, CancellationToken ct) =>
        await StuckAsync(site, git, claim, fence, slot, $"git's lock file {lockFile} is there; the slot is held until a person removes it", ct).ConfigureAwait(false);

    private async Task<PoolDone> StuckAsync(PoolSite site, PoolGit git, TreeClaim claim, ClaimFence fence, KeptTree slot, string why, CancellationToken ct)
    {
        var settled = await new SlotStanding(slot, claim).StuckAsync(site, git, why, ct).ConfigureAwait(false);
        await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Free, ct).ConfigureAwait(false);
        pool.Complete(fence, settled);
        return new PoolDone(pool.Slot(claim.Workspace, claim.Repository, claim.Number), null, settled.Said, null);
    }

    private async Task<PoolDone> RecoverMakeAsync(PoolSite site, PoolGit git, TreeClaim claim, ClaimFence fence, CancellationToken ct)
    {
        var folder = site.FolderOf(claim.Number);
        if (claim.Phase is TreeClaimPhases.OpeningRecord or TreeClaimPhases.Opened && seams.Opened is { } asked)
        {
            var (answer, record) = await asked(claim, ct).ConfigureAwait(false);
            if (answer == PoolOpens.NoAnswer) return PoolDone.Not("the service did not answer whether the open landed, so the claim is held for the next look");
            if (answer == PoolOpens.Landed && Directory.Exists(folder))
            {
                await PoolLocks.SetAsync(git, folder, PoolLocks.Free, ct).ConfigureAwait(false);
                var made = new KeptTree(site.Workspace, site.Repository, claim.Number, Path.GetFileName(folder), folder, "", "", KeptStates.Kept, "", DateTimeOffset.UtcNow, 0, "");
                pool.Complete(fence, await new SlotStanding(made, claim) { Made = true }.ReadAsync(ct).ConfigureAwait(false));
                return new PoolDone(pool.Slot(site.Workspace, site.Repository, claim.Number), null, null, record);
            }
        }

        // The open never landed: the folder and the branch it made are this claim's own and go.
        if (Directory.Exists(folder)) await TreeOpenings.UndoAsync(site.Root, folder, claim.Branch, ours: true).ConfigureAwait(false);
        pool.Complete(fence, null);
        return new PoolDone(null, null, null, null);
    }
}
