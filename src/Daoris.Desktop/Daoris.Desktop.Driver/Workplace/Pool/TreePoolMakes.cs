namespace Daoris.Driver;

/// <summary>A new slot to make: the number the pool gives it and the line whose branch is made with it.</summary>
/// <param name="From">A branch a chain's step grows from, as an open takes it.</param>
internal sealed record PoolMake(PoolSite Site, int Number, string Holder, string Line, string Session, string? From = null, string? Landed = null, string? PartOf = null);

/// <summary>
/// A new slot made by today's open at the slot's own fixed folder, its first line's branch made with it. The claim names the line before
/// the open, so a takeover finds the folder and the branch it made; the slot's first life is a fresh tree, marked unworked by the open.
/// </summary>
internal sealed class TreePoolMakes(TreePool pool, PoolSeams seams)
{
    internal async Task<PoolDone> MakeAsync(PoolMake make, CancellationToken ct)
    {
        var site = make.Site;
        var branch = $"{TreeFollows.SessionPrefix}{make.Line}";
        if (await PoolSubmodules.TrackedAsync(site.Root, "HEAD", ct).ConfigureAwait(false) is { } submodule)
        {
            return PoolDone.Not($"the repository tracks a submodule ({submodule}), and a repository with one is never pooled");
        }

        var job = PoolGit.NewJobName();
        var claimed = pool.Claim(new TreeClaimOpening(site.Workspace, site.Repository, make.Number, TreeClaimKinds.Make, make.Holder, make.Line, branch, "", "", job)
        {
            Phase = TreeClaimPhases.Opening,
        });
        if (claimed.Fence is not { } fence) return PoolDone.Not(claimed.Refused ?? "the slot is claimed");

        using var git = PoolGit.Create(site.Home, job);
        if (git is null)
        {
            pool.Complete(fence, null);
            return PoolDone.Not("there is no job to hold the claim's git in, so the pool is not offered here");
        }

        var folder = site.FolderOf(make.Number);
        try
        {
            var opening = new TreeOpening(new TreeContext(site.Home, WorkplacePlugins.Current.Wire(site.Home)));
            await opening.OpenAsync(site.Root, site.Repository, site.Workspace, ct, make.From, make.Landed, make.PartOf, folder, make.Line).ConfigureAwait(false);
        }
        catch (DriverException error)
        {
            pool.Complete(fence, null);
            return PoolDone.Not(error.Message);
        }

        pool.Advance(fence, TreeClaimPhases.Made);
        await seams.ReachedAsync(TreeClaimPhases.Made, folder).ConfigureAwait(false);

        var slot = new KeptTree(site.Workspace, site.Repository, make.Number, Path.GetFileName(folder), folder, "", "", KeptStates.Kept, "", DateTimeOffset.UtcNow, 0, "");
        var takes = new TreePoolTakes(pool, seams);
        if (await PoolLocks.SetAsync(git, folder, PoolLocks.Claimed(pool.Run), ct).ConfigureAwait(false) is { } lockWhy)
        {
            await TreeOpenings.UndoAsync(site.Root, folder, branch, ours: true, home: site.Home).ConfigureAwait(false);
            pool.Complete(fence, null);
            return PoolDone.Not(lockWhy);
        }

        if (await takes.CheckAsync(site, slot, branch, null, strict: true, ct).ConfigureAwait(false) is { } failed)
        {
            await TreeOpenings.UndoAsync(site.Root, folder, branch, ours: true, home: site.Home).ConfigureAwait(false);
            pool.Complete(fence, null);
            return PoolDone.Not($"the new slot did not pass the check: {failed}");
        }

        pool.Advance(fence, TreeClaimPhases.Checked);
        return await takes.OpenAsync(site, git, fence, slot, make.Line, make.Session, "", ct).ConfigureAwait(false);
    }
}
