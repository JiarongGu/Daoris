namespace Daoris.Driver;

/// <summary>
/// A slot let go for good: judged clean inside its claim, its shares unlinked, its whole folder renamed into the set-aside folders with a manifest written
/// before the move, then git's record of the vanished path dropped and the slot's row removed. A rename something holds open is
/// <c>busy</c>: the slot stays, its lock put back.
/// </summary>
internal sealed class TreePoolRetires(TreePool pool, PoolSeams seams)
{
    internal async Task<PoolDone> RetireAsync(PoolSite site, int number, string holder, CancellationToken ct)
    {
        if (pool.Slot(site.Workspace, site.Repository, number) is not { State: KeptStates.Kept or KeptStates.Retiring } slot) return PoolDone.Not("the pool has no kept or retiring slot with that number");

        var head = await WorkingTree.HeadAsync(slot.Path, ct).ConfigureAwait(false) ?? "";
        var job = PoolGit.NewJobName();
        var claimed = pool.Claim(new TreeClaimOpening(site.Workspace, site.Repository, number, TreeClaimKinds.Retire, holder, "", "", "", head, job));
        if (claimed.Fence is not { } fence) return PoolDone.Not(claimed.Refused ?? "the slot is claimed");

        using var git = PoolGit.Create(site.Home, job);
        if (git is null)
        {
            pool.Complete(fence, null);
            return PoolDone.Not("there is no job to hold the claim's git in, so the pool is not offered here");
        }

        if (await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Claimed(pool.Run), ct).ConfigureAwait(false) is { } lockWhy)
        {
            pool.Complete(fence, null);
            return PoolDone.Not(lockWhy);
        }

        var seed = (seams.Holds is { } holds ? await holds(slot, ct).ConfigureAwait(false) : new SlotJudgementFacts()) with { Claimed = false, Visited = false };
        var expected = slot.Line.Length > 0 ? pool.LastEnd(site.Workspace, site.Repository, slot.Line)?.Tip ?? "" : slot.Tip;
        var judged = await PoolJudging.JudgeAsync(site, slot, seed, expected, ct).ConfigureAwait(false);
        if (!judged.Verdict.Clean)
        {
            await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Free, ct).ConfigureAwait(false);
            pool.Complete(fence, null);
            return PoolDone.Not($"{judged.Verdict.Code}: {judged.Verdict.Why}");
        }

        if (TreeShares.Unlink(site.Home, slot.Path) is { } linked)
        {
            await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Free, ct).ConfigureAwait(false);
            pool.Complete(fence, null);
            return PoolDone.Not($"busy: {linked}, so the slot stays");
        }

        var aside = Path.Combine(TreePoolFinishes.SetAsideRoot(site), $"{Path.GetFileName(slot.Path)}-{DateTimeOffset.UtcNow.ToString(TreeSetAside.SetAsideStamp, System.Globalization.CultureInfo.InvariantCulture)}");
        var manifest = SlimManifest.PathFor(aside);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(aside)!);
            new SlimManifest(SlimStates.Judged, DateTimeOffset.UtcNow, null, site.Repository, "", []).Write(manifest);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Free, ct).ConfigureAwait(false);
            pool.Complete(fence, null);
            return PoolDone.Not($"its manifest could not be written ({error.Message.Trim()}), so the slot stays");
        }

        pool.Advance(fence, TreeClaimPhases.SlimJudged, opens: aside);
        await seams.ReachedAsync(TreeClaimPhases.SlimJudged, slot.Path).ConfigureAwait(false);
        pool.Advance(fence, TreeClaimPhases.Moving);
        await seams.ReachedAsync(TreeClaimPhases.Moving, slot.Path).ConfigureAwait(false);

        // Git refuses to drop a locked registration, so the pool's lock goes just before the rename and comes back where the rename is refused.
        await PoolLocks.UnlockAsync(git, slot.Path, ct).ConfigureAwait(false);
        try
        {
            Directory.Move(slot.Path, aside);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            SetAsideMarks.Forget(manifest);
            await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Free, ct).ConfigureAwait(false);
            pool.Complete(fence, null);
            return PoolDone.Not($"busy: something on this machine is using the slot ({error.Message.Trim()}), so it stays");
        }

        SetAsideMarks.MarkMoved(manifest);
        pool.Advance(fence, TreeClaimPhases.Moved);
        await seams.ReachedAsync(TreeClaimPhases.Moved, aside).ConfigureAwait(false);
        return await DropAsync(site, fence, slot.Path, ct).ConfigureAwait(false);
    }

    /// <summary>Git's record of the vanished folder dropped, the tree's files record forgotten, and the slot's row removed with the claim.</summary>
    internal async Task<PoolDone> DropAsync(PoolSite site, ClaimFence fence, string folder, CancellationToken ct)
    {
        await WorkingTree.GitAsync(site.Root, ["worktree", "prune"], ct).ConfigureAwait(false);
        TreeFilesRecord.Forget(site.Home, folder);
        pool.Advance(fence, TreeClaimPhases.Dropped);
        pool.Retired(fence);
        return new PoolDone(null, null, null, null);
    }
}
