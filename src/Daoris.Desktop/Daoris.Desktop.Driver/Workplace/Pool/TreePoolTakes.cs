namespace Daoris.Driver;

/// <summary>A free slot taken for a new line (<c>take</c>) or switched to a returning one (<c>return</c>), every step a phase of its claim and every
/// git a process of the claim's job. The slot is judged inside the claim, Daoris's copies are deleted and its shares unlinked, the switch is made in place with the
/// repository's hooks held and never forced, the outputs are moved aside, and the check passes before the keyed open of the line's record is
/// requested. The claim records the branch, commit and line the slot stood at; a refusal after the switch (a switch git refuses, a top held
/// at the clearing, a check that fails) runs the one undo, <see cref="PoolUndo"/>, which puts the slot back there before it is released.</summary>
internal sealed class TreePoolTakes(TreePool pool, PoolSeams seams)
{
    internal async Task<PoolDone> TakeAsync(PoolTake take, CancellationToken ct)
    {
        var site = take.Site;
        if (pool.Slot(site.Workspace, site.Repository, take.Number) is not { State: KeptStates.Kept } slot) return PoolDone.Not("the pool has no kept slot with that number");

        if (await PoolSubmodules.TrackedAsync(site.Root, take.Start, ct).ConfigureAwait(false) is { } submodule)
        {
            return PoolDone.Not($"the start tracks a submodule ({submodule}), and a repository with one is never pooled");
        }

        var head = await WorkingTree.HeadAsync(slot.Path, ct).ConfigureAwait(false) ?? "";
        var branch = await WorkingTree.BranchAsync(slot.Path, ct).ConfigureAwait(false) ?? "";
        var inPlace = take.Returns && slot.Line == take.Line && branch == take.Branch;
        var job = PoolGit.NewJobName();
        var claimed = pool.Claim(new TreeClaimOpening(
            site.Workspace, site.Repository, take.Number, take.Returns ? TreeClaimKinds.Return : TreeClaimKinds.Take, take.Holder, take.Line, take.Branch, take.Start,
            head, job) { InPlace = inPlace, ExpectBranch = branch, ExpectLine = slot.Line });
        if (claimed.Fence is not { } fence) return PoolDone.Not(claimed.Refused ?? "the slot is claimed");

        using var git = PoolGit.Create(site.Home, job);
        if (git is null)
        {
            pool.Complete(fence, null);
            return PoolDone.Not("there is no job to hold the claim's git in, so the pool is not offered here");
        }

        return await PoolAbandoned.GuardAsync(job, async () =>
        {
            if (await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Claimed(pool.Run), ct).ConfigureAwait(false) is { } lockWhy)
            {
                pool.Complete(fence, null);
                return PoolDone.Not(lockWhy);
            }

            var expected = inPlace ? head : ExpectedOf(slot);
            var judged = inPlace
                ? new PoolJudged(SlotVerdict.Passed, head, SlotIgnored.None)
                : await PoolJudging.JudgeAsync(site, slot, await HoldsAsync(slot, ct).ConfigureAwait(false), expected, ct).ConfigureAwait(false);
            if (!judged.Verdict.Clean)
            {
                await ReleaseAsync(git, fence, slot, ct).ConfigureAwait(false);
                return PoolDone.Not($"{judged.Verdict.Code}: {judged.Verdict.Why}");
            }

            await seams.ReachedAsync(TreeClaimPhases.Judged, slot.Path).ConfigureAwait(false);
            var previous = slot.Line;
            var toolchain = slot.Toolchain;
            if (!inPlace)
            {
                var from = previous.Length > 0 ? previous : Path.GetFileName(slot.Path);
                var plan = await TakePlans.WriteAsync(site, git, slot.Path, new TakeFacts(take.Start, from, previous, head, job, DateTimeOffset.UtcNow), judged.Ignored, ct)
                    .ConfigureAwait(false);
                if (seams.BeforeSwitch is { } planted) await planted(slot.Path).ConfigureAwait(false);
                PoolClearing.DeleteCopies(slot.Path, judged.Ignored.Copies);
                if (TreeShares.Unlink(site.Home, slot.Path) is { } linked) return await UndoneAsync(git, fence, take, $"busy: {linked}", ct).ConfigureAwait(false);

                var switched = await git.Repository.SwitchAsync(
                    slot.Path, new GitSwitch(take.Branch, take.Returns ? null : take.Start, Creates: !take.Returns, KeepIgnored: true, HooksPath: TreeOpenings.HooksPath(site.Home)), ct)
                    .ConfigureAwait(false);
                if (!switched.Ok) return await UndoneAsync(git, fence, take, TreeGit.FirstLine(switched.Error), ct).ConfigureAwait(false);
                if (!take.Returns) RecordGrown(take);

                pool.Advance(fence, TreeClaimPhases.Switched);
                await seams.ReachedAsync(TreeClaimPhases.Switched, slot.Path).ConfigureAwait(false);

                toolchain = seams.Toolchain is { } read ? await read(slot.Path, ct).ConfigureAwait(false) : await PoolToolchain.KeyAsync(site, slot.Path, ct).ConfigureAwait(false);
                var renew = slot.Bytes > (seams.RenewBytes ?? PoolLimits.RenewBytes);
                var moved = await PoolClearing.ClearAsync(site, slot.Path, plan, previous, (slot.Toolchain.Length > 0 && toolchain != slot.Toolchain) || renew, seams.Renaming, ct)
                    .ConfigureAwait(false);
                if (moved.Held is { } held) return await UndoneAsync(git, fence, take, $"busy: {held} is held by a running process", ct).ConfigureAwait(false);
                pool.Advance(fence, TreeClaimPhases.Cleared);
                await seams.ReachedAsync(TreeClaimPhases.Cleared, slot.Path).ConfigureAwait(false);
            }

            if (await CheckAsync(site, slot, take.Branch, inPlace ? null : take.Start, strict: !inPlace, ct).ConfigureAwait(false) is { } failed)
            {
                return await UndoneAsync(git, fence, take, failed, ct).ConfigureAwait(false);
            }

            // The mark is written once the check has passed, so the files stage installs here; a slot refused before this stays as it stands.
            if (!inPlace) TreeFilesRecord.Mark(site.Home, slot.Path, head.Length > 0 ? head : null);
            pool.Advance(fence, TreeClaimPhases.Checked);
            await seams.ReachedAsync(TreeClaimPhases.Checked, slot.Path).ConfigureAwait(false);

            return await OpenAsync(take.Site, git, fence, slot, take.Line, take.Session, toolchain, ct).ConfigureAwait(false);
        });
    }

    /// <summary>
    /// The keyed open requested under the key the claim writes first, the record's id written, and one transaction ending the claim with the
    /// slot serving the line. An open that did not land frees the slot as the takeover would.
    /// </summary>
    internal async Task<PoolDone> OpenAsync(PoolSite site, PoolGit git, ClaimFence fence, KeptTree slot, string line, string session, string toolchain, CancellationToken ct)
    {
        var opens = $"pool-{Guid.NewGuid():N}";
        pool.Advance(fence, TreeClaimPhases.OpeningRecord, opens);
        await seams.ReachedAsync(TreeClaimPhases.OpeningRecord, slot.Path).ConfigureAwait(false);
        var claim = pool.ClaimOn(site.Workspace, site.Repository, slot.Number)!;
        var record = seams.Open is { } open ? await open(claim, ct).ConfigureAwait(false) : null;
        if (record is null && claim.Kind == TreeClaimKinds.Make)
        {
            await TreeOpenings.UndoAsync(site.Root, slot.Path, claim.Branch, ours: true, home: site.Home).ConfigureAwait(false);
            pool.Complete(fence, null);
            return PoolDone.Not("the record's open did not land, so the new slot was removed");
        }

        if (record is null)
        {
            await new PoolTakeovers(pool, seams).FreeAsync(site, git, claim, fence, ct).ConfigureAwait(false);
            return PoolDone.Not("the record's open did not land, so the slot was freed");
        }

        pool.Advance(fence, TreeClaimPhases.Opened, record: record);
        var locked = session.Length > 0 ? await SessionLocks.HoldAsync(slot.Path, session, ct).ConfigureAwait(false) : await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Free, ct).ConfigureAwait(false);
        pool.Complete(fence, await new SlotStanding(slot, claim) { Made = true, Said = locked ?? "", Toolchain = toolchain }.ReadAsync(ct).ConfigureAwait(false));
        return new PoolDone(pool.Slot(site.Workspace, site.Repository, slot.Number), null, null, record);
    }

    /// <summary>
    /// The slot as the check reads it after a switch: git lists it and binds it to no other checkout, its HEAD is the target branch (at
    /// <paramref name="at"/> where given), and, unless it is the line's own going on, the judgement's reads of its links, status and ignored
    /// tops hold again. Null where it passed; else why not.
    /// </summary>
    internal async Task<string?> CheckAsync(PoolSite site, KeptTree slot, string branch, string? at, bool strict, CancellationToken ct)
    {
        if (await WorkingTree.BranchAsync(slot.Path, ct).ConfigureAwait(false) != branch) return $"the slot is not on {branch}";
        if (at is not null && await WorkingTree.HeadAsync(slot.Path, ct).ConfigureAwait(false) != await WorkingTree.TipAsync(slot.Path, at, ct).ConfigureAwait(false))
        {
            return $"the slot's HEAD is not at {at}";
        }

        if (!strict)
        {
            if (await TreeBinding.RefusalAsync(slot.Path, ct).ConfigureAwait(false) is { } bound) return bound;
            return null;
        }

        var head = await WorkingTree.HeadAsync(slot.Path, ct).ConfigureAwait(false) ?? "";
        var judged = await PoolJudging.JudgeAsync(site, slot, new SlotJudgementFacts(), head, ct).ConfigureAwait(false);
        return judged.Verdict.Clean ? null : $"{judged.Verdict.Code}: {judged.Verdict.Why}";
    }

    /// <summary>A refusal after the switch: the one undo puts the slot back as the claim found it and answers with <paramref name="why"/>.</summary>
    private Task<PoolDone> UndoneAsync(PoolGit git, ClaimFence fence, PoolTake take, string why, CancellationToken ct) =>
        new PoolUndo(pool, seams).UndoAsync(take.Site, git, pool.ClaimOn(take.Site.Workspace, take.Site.Repository, take.Number)!, fence, why, ct);

    private async Task ReleaseAsync(PoolGit git, ClaimFence fence, KeptTree slot, CancellationToken ct)
    {
        await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Free, ct).ConfigureAwait(false);
        pool.Complete(fence, null);
    }

    private async Task<SlotJudgementFacts> HoldsAsync(KeptTree slot, CancellationToken ct) =>
        (seams.Holds is { } holds ? await holds(slot, ct).ConfigureAwait(false) : new SlotJudgementFacts()) with { Claimed = false, Visited = false };

    /// <summary>The commit a free slot's HEAD is expected at: where its line let it go, else where it is parked.</summary>
    private string ExpectedOf(KeptTree slot) =>
        slot.Line.Length > 0 ? pool.LastEnd(slot.Workspace, slot.Repository, slot.Line)?.Tip ?? "" : slot.Tip;

    private void RecordGrown(PoolTake take)
    {
        var site = take.Site;
        var tip = WorkingTree.HeadAsync(site.FolderOf(take.Number), CancellationToken.None).GetAwaiter().GetResult() ?? take.Start;
        new SessionBranches(site.Home).Record(new GrownBranch(site.Repository, site.Workspace, take.Branch, take.LineBranch, tip, take.GrewFrom, DateTimeOffset.UtcNow)
        {
            Unit = GrownBranch.NewUnit(),
        });
    }
}
