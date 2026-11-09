namespace Daoris.Driver;

/// <summary>A line that has landed or is done with its output, to finish: its own ignored paths set aside with its record and its branch deleted.</summary>
/// <param name="LineBranch">The canonical line its work grew from, which names the finish's holder.</param>
internal sealed record PoolFinish(PoolSite Site, string Holder, string Line, string? LineBranch);

/// <summary>
/// A line's finish over a kept slot: the set-aside of the line's own ignored paths, the slot detached at the line's tip, the branch deleted
/// through the journal, and the record posted from the manifest by the pass that posts every finished tree's. The manifest is written
/// <c>judged</c> before anything moves, with the tip held on a recovery ref the delete makes in its own transaction. The slot keeps the
/// tracked files, its dependencies, its intermediates and Daoris's own copies for the next line.
/// </summary>
internal sealed class TreePoolFinishes(TreePool pool, PoolSeams seams)
{
    internal async Task<PoolDone> FinishAsync(PoolFinish finish, CancellationToken ct)
    {
        var site = finish.Site;
        var branch = $"{TreeFollows.SessionPrefix}{finish.Line}";
        if (pool.LastEnd(site.Workspace, site.Repository, finish.Line) is not { } end) return PoolDone.Not("no let-go of the line is recorded, so its tip is not known");
        if (end.Kind != LineEndKinds.Output) return PoolDone.Not($"a {end.Kind} line has no finish: its branch stays");

        var (stands, at, _) = await WorkingTree.GitAsync(site.Root, ["rev-parse", "--verify", "--quiet", $"refs/heads/{branch}"], ct).ConfigureAwait(false);
        if (stands != 0) return PoolDone.Not("its branch is gone, so there is nothing to finish");
        if (at.Trim() != end.Tip) return PoolDone.Not("its branch moved since the line let it go");

        var slot = pool.WhereIs(site.Workspace, site.Repository, finish.Line);
        if (await TreeWorktrees.EntriesAsync(site.Root, ct).ConfigureAwait(false) is not { } trees) return PoolDone.Not("git could not list the repository's working trees");
        if (trees.FirstOrDefault(each => each.Branch == branch) is { } checkedOut && (slot is null || !TreeGit.SamePath(checkedOut.Path, slot.Path)))
        {
            return PoolDone.Not($"its branch is checked out at {checkedOut.Path}");
        }

        var (keep, why) = await RecoveryKeeps.NameAsync(site.Root, branch, end.Tip, ct).ConfigureAwait(false);
        if (keep is null) return PoolDone.Not($"Daoris could not keep its commits on a ref of their own ({why})");

        var aside = Path.Combine(SetAsideRoot(site), $"{finish.Line}-{DateTimeOffset.UtcNow.ToString(TreeSetAside.SetAsideStamp, System.Globalization.CultureInfo.InvariantCulture)}");
        var manifest = SlimManifest.PathFor(aside);
        if (slot is null) return await FinishedAsync(site, finish, end, branch, keep, aside, manifest, [], slot: null, ct).ConfigureAwait(false);

        var head = await WorkingTree.HeadAsync(slot.Path, ct).ConfigureAwait(false) ?? "";
        var standing = await WorkingTree.BranchAsync(slot.Path, ct).ConfigureAwait(false) ?? "";
        var job = PoolGit.NewJobName();
        var claimed = pool.Claim(new TreeClaimOpening(site.Workspace, site.Repository, slot.Number, TreeClaimKinds.Finish, finish.Holder, finish.Line, branch, end.Tip, head, job)
        {
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

        if (await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Claimed(pool.Run), ct).ConfigureAwait(false) is { } lockWhy)
        {
            pool.Complete(fence, null);
            return PoolDone.Not(lockWhy);
        }

        var seed = (seams.Holds is { } holds ? await holds(slot, ct).ConfigureAwait(false) : new SlotJudgementFacts()) with { Claimed = false, Visited = false };
        var judged = await PoolJudging.JudgeAsync(site, slot, seed, end.Tip, ct).ConfigureAwait(false);
        if (!judged.Verdict.Clean && judged.Verdict.Code != SlotJudgement.LinesOwn)
        {
            await ReleaseAsync(git, fence, slot, ct).ConfigureAwait(false);
            return PoolDone.Not($"{judged.Verdict.Code}: {judged.Verdict.Why}");
        }

        // The judgement stops at the line's own ignored paths, which a finish sets aside, so the step after them is asked here.
        if (judged.Head != end.Tip)
        {
            await ReleaseAsync(git, fence, slot, ct).ConfigureAwait(false);
            return PoolDone.Not($"{SlotJudgement.HeadMoved}: its HEAD changed since its line let it go");
        }

        var own = await FinishSort.AsideAsync(site, slot.Path, end.Tip, judged.Ignored, ct).ConfigureAwait(false);
        var written = WriteManifest(site, finish, end, branch, keep, aside, manifest, own);
        if (written is not null)
        {
            await ReleaseAsync(git, fence, slot, ct).ConfigureAwait(false);
            return PoolDone.Not($"its manifest could not be written ({written}), so the branch is kept");
        }

        pool.Advance(fence, TreeClaimPhases.SettingAside, opens: aside);
        await seams.ReachedAsync(TreeClaimPhases.SettingAside, slot.Path).ConfigureAwait(false);
        if (Move(slot.Path, aside, own) is { } busy)
        {
            PutBack(slot.Path, aside, own);
            SetAsideMarks.Forget(manifest);
            await ReleaseAsync(git, fence, slot, ct).ConfigureAwait(false);
            return PoolDone.Not($"busy: {busy}");
        }

        SetAsideMarks.MarkMoved(manifest);
        pool.Advance(fence, TreeClaimPhases.SetAside);
        await seams.ReachedAsync(TreeClaimPhases.SetAside, slot.Path).ConfigureAwait(false);

        var after = await FillJudgement.StatusAsync(slot.Path, ignored: true, ct).ConfigureAwait(false);
        var reappeared = own.Any(top => after.ContainsKey(top));
        if (after.Any(each => each.Value != "!!") || reappeared)
        {
            PutBack(slot.Path, aside, own);
            SetAsideMarks.Forget(manifest);
            await ReleaseAsync(git, fence, slot, ct).ConfigureAwait(false);
            return PoolDone.Not("the slot changed as its own paths were set aside, so they were put back and the branch is kept");
        }

        var hooks = TreeOpenings.HooksHeld(site.Home);
        var (detached, _, said) = await git.GitAsync(slot.Path, [.. hooks, "switch", "--detach", end.Tip], ct).ConfigureAwait(false);
        if (detached != 0)
        {
            PutBack(slot.Path, aside, own);
            SetAsideMarks.Forget(manifest);
            await ReleaseAsync(git, fence, slot, ct).ConfigureAwait(false);
            return PoolDone.Not($"git would not detach the slot: {TreeGit.FirstLine(said)}");
        }

        pool.Advance(fence, TreeClaimPhases.Detached);
        await seams.ReachedAsync(TreeClaimPhases.Detached, slot.Path).ConfigureAwait(false);
        await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Free, ct).ConfigureAwait(false);
        pool.Complete(fence, await new SlotStanding(slot, claim).ReadAsync(ct).ConfigureAwait(false));
        return await FinishedAsync(site, finish, end, branch, keep, aside, manifest, own, slot, ct).ConfigureAwait(false);
    }

    /// <summary>Everything after the slot is detached (or when no slot serves the line): the held folders gathered in, the branch deleted through the journal, the manifest marked <c>deleted</c>.</summary>
    internal async Task<PoolDone> FinishedAsync(
        PoolSite site, PoolFinish finish, LineEnd end, string branch, RecoveryName keep, string aside, string manifest, IReadOnlyList<string> own, KeptTree? slot, CancellationToken ct)
    {
        if (slot is null)
        {
            Directory.CreateDirectory(aside);
            if (WriteManifest(site, finish, end, branch, keep, aside, manifest, own) is { } unwritten) return PoolDone.Not($"its manifest could not be written ({unwritten}), so the branch is kept");
            SetAsideMarks.MarkMoved(manifest);
        }

        PoolAsides.GatherInto(site, finish.Line, aside);
        if (await TreeWorktrees.EntriesAsync(site.Root, ct).ConfigureAwait(false) is not { } trees || trees.Any(each => each.Branch == branch))
        {
            return PoolDone.Not("its branch is checked out, or the trees could not be listed, so it is kept");
        }

        var deleted = await JournaledDeletes.DeleteAsync(site.Home, site.Root, site.Repository, branch, end.Tip, [], _ => Task.FromResult<string?>(null), keep).ConfigureAwait(false);
        if (deleted.Kept is not null || deleted.Said is not null)
        {
            SetAsideMarks.Mark(manifest, SlimStates.Refused, DateTimeOffset.UtcNow);
            return PoolDone.Not($"git would not delete the branch: {deleted.Kept ?? deleted.Said}");
        }

        SetAsideMarks.Mark(manifest, SlimStates.Deleted, DateTimeOffset.UtcNow);
        return new PoolDone(slot is null ? null : pool.Slot(site.Workspace, site.Repository, slot.Number), null, null, null);
    }

    private string? WriteManifest(PoolSite site, PoolFinish finish, LineEnd end, string branch, RecoveryName keep, string aside, string manifest, IReadOnlyList<string> own)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(aside)!);
            var grown = new SessionBranches(site.Home).Of(site.Repository, branch);
            var tree = new FinishedTree(
                end.Read, site.Workspace, Path.Combine(site.Trees, finish.Line), finish.LineBranch, end.Tip, grown?.From, grown?.Unit,
                new FinishedHold("finished", keep.Ref, "branch"), keep.Ref, new FinishedLeft([.. own.Select(top => top.TrimEnd('/'))], own.Count, false),
                KeepPolicy.Of(site.Home).SetAside.Whole)
            {
                LandingWhy = "none recorded for its unit or branch",
            };
            new SlimManifest(SlimStates.Judged, DateTimeOffset.UtcNow, null, site.Repository, branch, []) { Finished = tree }.Write(manifest);
            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return error.Message;
        }
    }

    /// <summary>Each top renamed whole into the folder, at its own relative place; null where all went, else why one did not.</summary>
    private static string? Move(string slot, string aside, IReadOnlyList<string> tops)
    {
        try
        {
            foreach (var top in tops)
            {
                var relative = top.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar);
                var target = Path.Combine(aside, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                var source = Path.Combine(slot, relative);
                if (Directory.Exists(source)) Directory.Move(source, target);
                else if (File.Exists(source)) File.Move(source, target);
            }

            return null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return error.Message.Trim();
        }
    }

    /// <summary>The tops renamed back into the slot, whole; a top something stands at again stays aside.</summary>
    internal static void PutBack(string slot, string aside, IReadOnlyList<string> tops)
    {
        foreach (var top in tops)
        {
            var relative = top.TrimEnd('/').Replace('/', Path.DirectorySeparatorChar);
            var source = Path.Combine(aside, relative);
            var target = Path.Combine(slot, relative);
            if (Path.Exists(target)) continue;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (Directory.Exists(source)) Directory.Move(source, target);
                else if (File.Exists(source)) File.Move(source, target);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                // Left aside with its manifest: the next look's resume reads it.
            }
        }
    }

    private async Task ReleaseAsync(PoolGit git, ClaimFence fence, KeptTree slot, CancellationToken ct)
    {
        await PoolLocks.SetAsync(git, slot.Path, PoolLocks.Free, ct).ConfigureAwait(false);
        pool.Complete(fence, null);
    }

    internal static string SetAsideRoot(PoolSite site) =>
        Path.Combine(DaorisHome.Entry(site.Home, HomeEntry.Trees), TreeSetAside.SetAsideFolder, site.Workspace, site.Repository);
}
