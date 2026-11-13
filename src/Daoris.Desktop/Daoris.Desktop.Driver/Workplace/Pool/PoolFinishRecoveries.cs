namespace Daoris.Driver;

/// <summary>
/// A dead run's <c>finish</c> or <c>retire</c> claim, settled from its phase and the disk. A finish stopped before it moved anything is
/// dropped, its manifest left to the set-aside's one resume rule; stopped while it moved, what moved is renamed back first; once the paths
/// are aside its detach is done again and the rest follows. A retirement stopped before its rename leaves the slot, else git's record goes.
/// </summary>
internal sealed class PoolFinishRecoveries(TreePool pool, PoolSeams seams)
{
    internal async Task<PoolDone> RecoverAsync(PoolSite site, PoolGit git, TreeClaim claim, ClaimFence fence, CancellationToken ct) =>
        claim.Kind == TreeClaimKinds.Finish ? await FinishAsync(site, git, claim, fence, ct).ConfigureAwait(false) : await RetireAsync(site, git, claim, fence, ct).ConfigureAwait(false);

    private async Task<PoolDone> FinishAsync(PoolSite site, PoolGit git, TreeClaim claim, ClaimFence fence, CancellationToken ct)
    {
        var slot = pool.Slot(claim.Workspace, claim.Repository, claim.Number)!;
        var aside = claim.Opens;
        var manifest = aside.Length > 0 ? SlimManifest.PathFor(aside) : "";
        var finished = manifest.Length > 0 ? SlimManifest.Read(manifest).Manifest?.Finished : null;
        var own = finished?.Left.Names ?? [];
        if (claim.Phase is TreeClaimPhases.Judged or TreeClaimPhases.SettingAside)
        {
            if (claim.Phase == TreeClaimPhases.SettingAside && aside.Length > 0)
            {
                TreePoolFinishes.PutBack(slot.Path, aside, own, manifest);
            }

            await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Free, ct).ConfigureAwait(false);
            pool.Complete(fence, null);
            return new PoolDone(slot, null, null, null);
        }

        if (await WorkingTree.BranchAsync(slot.Path, ct).ConfigureAwait(false) == claim.Branch)
        {
            var detached = await git.Repository.SwitchAsync(slot.Path, new GitSwitch(null, claim.Start, HooksPath: TreeOpenings.HooksPath(site.Home)), ct).ConfigureAwait(false);
            if (!detached.Ok) return PoolDone.Not($"git would not detach the slot: {TreeGit.FirstLine(detached.Error)}; the claim is held");
        }

        await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Free, ct).ConfigureAwait(false);
        pool.Complete(fence, await new SlotStanding(slot, claim).ReadAsync(ct).ConfigureAwait(false));
        if (pool.LastEnd(claim.Workspace, claim.Repository, claim.Line) is not { } end || finished is null) return new PoolDone(pool.Slot(claim.Workspace, claim.Repository, claim.Number), null, null, null);

        var (keep, _) = await RecoveryKeeps.NameAsync(site.Root, claim.Branch, claim.Start, ct).ConfigureAwait(false);
        if (keep is null) return PoolDone.Not("every name for its recovery ref is taken, so the branch is kept");
        var stands = await WorkingTree.TipAsync(site.Root, $"refs/heads/{claim.Branch}", ct).ConfigureAwait(false) is not null;
        if (!stands)
        {
            SetAsideMarks.Mark(manifest, SlimStates.Deleted, DateTimeOffset.UtcNow);
            return new PoolDone(pool.Slot(claim.Workspace, claim.Repository, claim.Number), null, null, null);
        }

        return await new TreePoolFinishes(pool, seams).FinishedAsync(site, new PoolFinish(site, claim.Holder, claim.Line, finished.Line), end, claim.Branch, keep, aside, manifest, own.Select(name => name + "/").ToList(), slot, ct)
            .ConfigureAwait(false);
    }

    private async Task<PoolDone> RetireAsync(PoolSite site, PoolGit git, TreeClaim claim, ClaimFence fence, CancellationToken ct)
    {
        var slot = pool.Slot(claim.Workspace, claim.Repository, claim.Number)!;
        var manifest = claim.Opens.Length > 0 ? SlimManifest.PathFor(claim.Opens) : "";
        if (Directory.Exists(slot.Path))
        {
            if (manifest.Length > 0) SetAsideMarks.Forget(manifest);
            await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Free, ct).ConfigureAwait(false);
            pool.Complete(fence, null);
            return new PoolDone(slot, null, null, null);
        }

        if (manifest.Length > 0) SetAsideMarks.MarkMoved(manifest);
        return await new TreePoolRetires(pool, seams).DropAsync(site, fence, slot.Path, ct).ConfigureAwait(false);
    }
}
