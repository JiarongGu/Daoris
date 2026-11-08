namespace Daoris.Driver;

/// <summary>
/// A claim whose run is dead, taken over by the look: the run's git processes proved gone, the claim taken by one guarded update, the
/// slot read from the disk (never the phase alone), and then freed, finished or held, by what is found. A slot a take leaves on its new
/// branch with no commit of its own is detached where the take began and that branch deleted through the journal; no step deletes a ref any
/// other way.
/// </summary>
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

        if (claim.Kind is not (TreeClaimKinds.Take or TreeClaimKinds.Return or TreeClaimKinds.Make))
        {
            return PoolDone.Not($"a {claim.Kind} claim is not settled by this build, so it is held");
        }

        if (slot is null) return await RecoverMakeAsync(site, git, claim, fence, ct).ConfigureAwait(false);
        if (GitLockFile(slot.Path) is { } lockFile)
        {
            var why = $"git's lock file {lockFile} is there; the slot is held until a person removes it";
            pool.Complete(fence, new SlotSettled(slot.Registration, slot.Path, slot.Line, await HeadOf(slot, ct).ConfigureAwait(false), KeptStates.Stuck, why, slot.Bytes, slot.Toolchain));
            return new PoolDone(pool.Slot(site.Workspace, site.Repository, number), null, why, null);
        }

        if (claim.Phase is TreeClaimPhases.OpeningRecord or TreeClaimPhases.Opened && seams.Opened is { } asked)
        {
            var (answer, record) = await asked(claim, ct).ConfigureAwait(false);
            if (answer == PoolOpens.NoAnswer) return PoolDone.Not("the service did not answer whether the open landed, so the claim is held for the next look");
            if (answer == PoolOpens.Landed)
            {
                await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Free, ct).ConfigureAwait(false);
                pool.Complete(fence, new SlotSettled(slot.Registration, slot.Path, claim.Line, "", KeptStates.Kept, "", slot.Bytes, slot.Toolchain));
                return new PoolDone(pool.Slot(site.Workspace, site.Repository, number), null, null, record);
            }
        }

        return await FreeAsync(site, git, claim, fence, ct).ConfigureAwait(false);
    }

    /// <summary>The slot read from the disk and put back to a free state, the claim ended: undone, kept as the return left it, or <c>stuck</c> where its HEAD is at neither place.</summary>
    internal async Task<PoolDone> FreeAsync(PoolSite site, PoolGit git, TreeClaim claim, ClaimFence fence, CancellationToken ct)
    {
        var slot = pool.Slot(claim.Workspace, claim.Repository, claim.Number)!;
        var branch = await WorkingTree.BranchAsync(slot.Path, ct).ConfigureAwait(false);
        var head = await WorkingTree.HeadAsync(slot.Path, ct).ConfigureAwait(false) ?? "";
        SlotSettled settled;
        string? stuck = null;
        if (branch == claim.Branch && claim.Kind == TreeClaimKinds.Return)
        {
            settled = new SlotSettled(slot.Registration, slot.Path, claim.Line, "", KeptStates.Kept, "", slot.Bytes, slot.Toolchain);
        }
        else if (branch == claim.Branch)
        {
            if (head != await FileGit.CommitAsync(slot.Path, claim.Start, ct).ConfigureAwait(false))
            {
                stuck = $"{claim.Branch} has a commit of its own, so the slot was left on it";
                settled = new SlotSettled(slot.Registration, slot.Path, slot.Line, head, KeptStates.Stuck, stuck, slot.Bytes, slot.Toolchain);
            }
            else if (await UndoAsync(site, git, claim, ct).ConfigureAwait(false) is { } failed)
            {
                stuck = failed;
                settled = new SlotSettled(slot.Registration, slot.Path, slot.Line, head, KeptStates.Stuck, failed, slot.Bytes, slot.Toolchain);
            }
            else
            {
                settled = new SlotSettled(slot.Registration, slot.Path, "", head, KeptStates.Kept, "", slot.Bytes, slot.Toolchain);
            }
        }
        else if (head == claim.Expect)
        {
            await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Free, ct).ConfigureAwait(false);
            pool.Complete(fence, null);
            return new PoolDone(slot, null, null, null);
        }
        else
        {
            stuck = "its HEAD is at neither where the claim began nor where it was switching to";
            settled = new SlotSettled(slot.Registration, slot.Path, slot.Line, head, KeptStates.Stuck, stuck, slot.Bytes, slot.Toolchain);
        }

        await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Free, ct).ConfigureAwait(false);
        pool.Complete(fence, settled);
        return new PoolDone(pool.Slot(claim.Workspace, claim.Repository, claim.Number), null, stuck, null);
    }

    /// <summary>
    /// A take undone: the slot detached where the take began and the new branch deleted through the journal at that commit, its Undo
    /// standing. Null where it is done, or the claim is a return's, which leaves the slot on the returning line's branch; else why not.
    /// </summary>
    internal async Task<string?> UndoAsync(PoolSite site, PoolGit git, TreeClaim claim, CancellationToken ct)
    {
        if (claim.Kind != TreeClaimKinds.Take) return null;
        var slot = pool.Slot(claim.Workspace, claim.Repository, claim.Number)!;
        var home = site.Home;
        var hooks = TreeOpenings.HooksHeld(home);
        var start = await FileGit.CommitAsync(slot.Path, claim.Start, ct).ConfigureAwait(false);
        var (code, _, err) = await git.RunAsync(slot.Path, [.. hooks, "switch", "--detach", start], ct).ConfigureAwait(false);
        if (code != 0) return $"git would not detach the slot: {TreeGit.FirstLine(err)}";

        var (stands, tip, _) = await WorkingTree.GitAsync(slot.Path, ["rev-parse", "--verify", "--quiet", $"refs/heads/{claim.Branch}"], ct).ConfigureAwait(false);
        if (stands != 0) return null;
        if (tip.Trim() != start) return $"{claim.Branch} moved on, so it was kept";
        var deleted = await JournaledDeletes.DeleteAsync(home, site.Root, claim.Repository, claim.Branch, start, [], _ => Task.FromResult<string?>(null)).ConfigureAwait(false);
        return deleted.Kept is { } kept ? $"{claim.Branch} was kept: {kept}" : null;
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
                pool.Complete(fence, new SlotSettled(Path.GetFileName(folder), folder, claim.Line, "", KeptStates.Kept, "", 0, ""));
                return new PoolDone(pool.Slot(site.Workspace, site.Repository, claim.Number), null, null, record);
            }
        }

        // The open never landed: the folder and the branch it made are this claim's own and go.
        if (Directory.Exists(folder)) await TreeOpenings.UndoAsync(site.Root, folder, claim.Branch, ours: true).ConfigureAwait(false);
        pool.Complete(fence, null);
        return new PoolDone(null, null, null, null);
    }

    private static string? GitLockFile(string slot) =>
        GitBranches.GitDirectory(slot) is { } git ? new[] { "index.lock", "HEAD.lock" }.FirstOrDefault(name => File.Exists(Path.Combine(git, name))) : null;

    private static async Task<string> HeadOf(KeptTree slot, CancellationToken ct) => await WorkingTree.HeadAsync(slot.Path, ct).ConfigureAwait(false) ?? slot.Tip;
}
