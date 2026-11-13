namespace Daoris.Driver;

/// <summary>
/// The one undo of a take or a return, run by its own step on a refusal after the switch, by its run's look and by a takeover alike. The
/// claim enters <c>undoing</c> with the slot's row naming no line; the slot goes back where the claim found it, onto the branch it stood on
/// only where that branch's tip is still the commit it stood at (else detached at that commit), with the hooks held and no ignored file
/// overwritten; a take's own branch then goes through the journal where it still points at its start. The slot is released, its row
/// written by <see cref="SlotStanding"/>, once it reads back so and binds to no other checkout. Where the branch is checked out in another
/// checkout, which passes, the claim waits at <c>undo-waiting</c> for its run's look; anything else that keeps it from going back settles
/// the slot <c>stuck</c>, detached where it stands and naming no line, and a take's own branch still goes through the journal.
/// </summary>
internal sealed class PoolUndo(TreePool pool, PoolSeams seams)
{
    private enum Put
    {
        Back,
        Detached,
        Waits,
        Refused,
    }

    /// <param name="why">The refusal answered once the slot is put back; null for a look or a takeover, which answers done.</param>
    internal async Task<PoolDone> UndoAsync(PoolSite site, PoolGit git, TreeClaim claim, ClaimFence fence, string? why, CancellationToken ct)
    {
        var slot = pool.Slot(claim.Workspace, claim.Repository, claim.Number)!;
        pool.Undoing(fence);
        try
        {
            await seams.ReachedAsync(TreeClaimPhases.Undoing, slot.Path).ConfigureAwait(false);
            var (put, said) = await PutBackAsync(site, git, claim, slot, ct).ConfigureAwait(false);
            if (put == Put.Waits)
            {
                pool.Advance(fence, TreeClaimPhases.UndoWaiting);
                return PoolDone.Not(Words(why, $"the slot waits to be put back: {said}"));
            }

            if (put == Put.Refused) return await StuckAsync(site, git, claim, fence, slot, Words(why, said), ct).ConfigureAwait(false);
            if (await ReleaseRefusalAsync(slot, put, ct).ConfigureAwait(false) is { } held) return await StuckAsync(site, git, claim, fence, slot, Words(why, held), ct).ConfigureAwait(false);

            TreeBases.Undo(site.Home, slot.Path, claim.Job);
            var kept = claim.Kind == TreeClaimKinds.Take ? await DeleteOwnBranchAsync(site, claim, slot, ct).ConfigureAwait(false) : null;
            await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Free, ct).ConfigureAwait(false);
            pool.Complete(fence, await new SlotStanding(slot, claim).ReadAsync(ct).ConfigureAwait(false));
            var answer = why is null ? kept : Words(why, kept);
            return new PoolDone(pool.Slot(claim.Workspace, claim.Repository, claim.Number), answer, null, null);
        }
        catch (Exception error) when (error is not TreePoolRefusedException)
        {
            // A step that throws mid-undo leaves the claim to its run's look, never at a phase no look takes.
            pool.Advance(fence, TreeClaimPhases.UndoWaiting);
            return PoolDone.Not(Words(why, $"the slot waits to be put back: {error.Message}"));
        }
    }

    private static async Task<(Put Put, string Said)> PutBackAsync(PoolSite site, PoolGit git, TreeClaim claim, KeptTree slot, CancellationToken ct)
    {
        if (claim.Expect.Length == 0) return (Put.Refused, "the claim recorded no commit the slot stood at");
        var onto = claim.ExpectBranch;
        if (onto.Length > 0 && await TipAsync(slot.Path, onto, ct).ConfigureAwait(false) != claim.Expect) onto = "";
        var put = onto == claim.ExpectBranch ? Put.Back : Put.Detached;
        if (await StandsAsync(slot, onto, claim.Expect, ct).ConfigureAwait(false)) return (put, "");

        if (onto.Length > 0 && await CheckedOutAtAsync(site, slot, onto, ct).ConfigureAwait(false) is { } there) return (Put.Waits, $"{onto} is checked out at {there}");
        var back = await git.Repository.SwitchAsync(
            slot.Path, new GitSwitch(onto.Length > 0 ? onto : null, onto.Length > 0 ? null : claim.Expect, KeepIgnored: true, HooksPath: TreeOpenings.HooksPath(site.Home)), ct).ConfigureAwait(false);
        if (!back.Ok) return (Put.Refused, $"git would not put the slot back: {TreeGit.FirstLine(back.Error)}");
        return await StandsAsync(slot, onto, claim.Expect, ct).ConfigureAwait(false) ? (put, "") : (Put.Refused, "the slot did not read back where the claim found it");
    }

    /// <summary>Why a slot put back is not released, or null: bound to another checkout, or, detached off its moved branch, not clean.</summary>
    private static async Task<string?> ReleaseRefusalAsync(KeptTree slot, Put put, CancellationToken ct)
    {
        if (await TreeBinding.RefusalAsync(slot.Path, ct).ConfigureAwait(false) is { } bound) return bound;
        if (put == Put.Detached && (await FillJudgement.StatusAsync(slot.Path, ignored: false, ct).ConfigureAwait(false)).Count > 0)
        {
            return "the slot, detached where its line's branch moved on from, holds changes";
        }

        return null;
    }

    private async Task<PoolDone> StuckAsync(PoolSite site, PoolGit git, TreeClaim claim, ClaimFence fence, KeptTree slot, string why, CancellationToken ct)
    {
        var settled = await new SlotStanding(slot, claim).StuckAsync(site, git, why, ct).ConfigureAwait(false);
        if (claim.Kind == TreeClaimKinds.Take && await DeleteOwnBranchAsync(site, claim, slot, ct).ConfigureAwait(false) is { } kept) settled = settled with { Said = $"{settled.Said}; {kept}" };
        await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Free, ct).ConfigureAwait(false);
        pool.Complete(fence, settled);
        return new PoolDone(pool.Slot(claim.Workspace, claim.Repository, claim.Number), null, settled.Said, null);
    }

    /// <summary>The take's own branch deleted through the journal where it still points at its start; null where done or gone, else why it stays.</summary>
    private static async Task<string?> DeleteOwnBranchAsync(PoolSite site, TreeClaim claim, KeptTree slot, CancellationToken ct)
    {
        var start = await FileGit.CommitAsync(slot.Path, claim.Start, ct).ConfigureAwait(false);
        var tip = await TipAsync(slot.Path, claim.Branch, ct).ConfigureAwait(false);
        if (tip is null || start is null) return null;
        if (tip != start) return $"{claim.Branch} moved on, so it was kept";
        var deleted = await JournaledDeletes.DeleteAsync(site.Home, site.Root, claim.Repository, claim.Branch, start, [], _ => Task.FromResult<string?>(null)).ConfigureAwait(false);
        return deleted.Kept is { } kept ? $"{claim.Branch} was kept: {kept}" : null;
    }

    private static async Task<bool> StandsAsync(KeptTree slot, string branch, string at, CancellationToken ct) =>
        (await WorkingTree.BranchAsync(slot.Path, ct).ConfigureAwait(false) ?? "") == branch && await WorkingTree.HeadAsync(slot.Path, ct).ConfigureAwait(false) == at;

    private static Task<string?> TipAsync(string folder, string branch, CancellationToken ct) => WorkingTree.TipAsync(folder, $"refs/heads/{branch}", ct);

    private static async Task<string?> CheckedOutAtAsync(PoolSite site, KeptTree slot, string branch, CancellationToken ct) =>
        await TreeWorktrees.EntriesAsync(site.Root, ct).ConfigureAwait(false) is { } trees
            ? trees.FirstOrDefault(each => each.Branch == branch && !TreeGit.SamePath(each.Path, slot.Path))?.Path
            : null;

    private static string Words(string? why, string? more) => why is null ? more ?? "" : more is null or "" ? why : $"{why}; {more}";
}
