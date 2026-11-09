namespace Daoris.Driver;

/// <summary>A line's slot to let go at its run's end.</summary>
internal sealed record PoolEnd(PoolSite Site, int Number, string Holder, string Session);

/// <summary>
/// A line's slot read at its run's end: where HEAD stands against the line's branch and what is uncommitted or ignored, written as a
/// <c>line_ends</c> row with the tip the next judgement compares, and the pool's free lock put back. The slot stays the line's; it is
/// switched only by a later take. It runs beside a person's terminal in the slot, so a visit does not hold it back.
/// </summary>
internal sealed class TreePoolEnds(TreePool pool, PoolSeams seams)
{
    internal async Task<PoolDone> EndAsync(PoolEnd end, CancellationToken ct)
    {
        var site = end.Site;
        if (pool.Slot(site.Workspace, site.Repository, end.Number) is not { Line.Length: > 0 } slot) return PoolDone.Not("no line holds that slot");

        var branch = $"{TreeFollows.SessionPrefix}{slot.Line}";
        var head = await WorkingTree.HeadAsync(slot.Path, ct).ConfigureAwait(false) ?? "";
        var standing = await WorkingTree.BranchAsync(slot.Path, ct).ConfigureAwait(false) ?? "";
        var job = PoolGit.NewJobName();
        var claimed = pool.Claim(new TreeClaimOpening(site.Workspace, site.Repository, end.Number, TreeClaimKinds.End, end.Holder, slot.Line, branch, "", head, job)
        {
            InPlace = true,
            ExpectBranch = standing,
            ExpectLine = slot.Line,
        });
        if (claimed is not { Claim: { } claim, Fence: { } fence }) return PoolDone.Not(claimed.Refused ?? "the slot is claimed");

        using var git = PoolGit.Create(site.Home, job);
        if (git is null)
        {
            pool.Complete(fence, null);
            return PoolDone.Not("there is no job to hold the claim's git in, so the pool is not offered here");
        }

        return await PoolAbandoned.GuardAsync(job, async () =>
        {
            var onBranch = standing == branch;
            var status = await FillJudgement.StatusAsync(slot.Path, ignored: true, ct).ConfigureAwait(false);
            var sorted = head.Length == 0 ? SlotIgnored.None : await SlotIgnored.ReadAsync(site, slot.Path, status, head, ct).ConfigureAwait(false);
            var uncommitted = status.Any(each => each.Value != "!!");
            var grown = new SessionBranches(site.Home).Of(site.Repository, branch);
            var kind = !onBranch ? LineEndKinds.Held
                : uncommitted ? LineEndKinds.Uncommitted
                : grown is not null && grown.From == head ? LineEndKinds.NoOutput
                : LineEndKinds.Output;
            var toolchain = seams.Toolchain is { } read ? await read(slot.Path, ct).ConfigureAwait(false) : await PoolToolchain.KeyAsync(site, slot.Path, ct).ConfigureAwait(false);
            var bytes = sorted.Outputs.Concat(sorted.Intermediates).Concat(sorted.Own).Concat(sorted.Copies)
                .Where(top => !top.StartsWith(BuildFolderList.Packages, StringComparison.OrdinalIgnoreCase))
                .Sum(top => FileWalk.Of(Path.Combine(slot.Path, top.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar))).Size.Bytes);
            var now = DateTimeOffset.UtcNow;
            pool.RecordEnd(new LineEnd(site.Workspace, site.Repository, slot.Line, branch, end.Session, end.Number, kind, head, now, string.Join(',', sorted.Own), toolchain, now));
            pool.Advance(fence, TreeClaimPhases.Recorded);
            await seams.ReachedAsync(TreeClaimPhases.Recorded, slot.Path).ConfigureAwait(false);

            var lockWhy = await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Free, ct).ConfigureAwait(false);
            pool.Advance(fence, TreeClaimPhases.Locked);
            pool.Complete(fence, await new SlotStanding(slot, claim) { Made = true, State = slot.State, Said = slot.Said, Bytes = bytes, Toolchain = toolchain }.ReadAsync(ct).ConfigureAwait(false));
            return new PoolDone(pool.Slot(site.Workspace, site.Repository, end.Number), null, lockWhy, null);
        });
    }
}
