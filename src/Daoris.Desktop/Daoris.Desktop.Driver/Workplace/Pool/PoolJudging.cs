namespace Daoris.Driver;

/// <summary>What the judgement of a slot found: the verdict, the commit its HEAD stood at, and the ignored tops it sorted.</summary>
internal sealed record PoolJudged(SlotVerdict Verdict, string Head, SlotIgnored Ignored);

/// <summary>
/// The git and disk reads behind <see cref="SlotJudgement"/>: git's list and binding of the slot, its lock, the links and repositories
/// below it (a share's link Daoris's own while it still leads to its place, any other link foreign), a half-done operation, the status sorted by the effective rules, and where HEAD stands against where its line let it go.
/// Each stage reads only what the step before it let through, and the first that fails answers; nothing is reset to make a slot pass.
/// </summary>
internal static class PoolJudging
{
    private static readonly string[] Operations = ["MERGE_HEAD", "CHERRY_PICK_HEAD", "REVERT_HEAD", "BISECT_LOG", "rebase-merge", "rebase-apply"];

    /// <param name="seed">What the caller knows of the holders (step 1); a judgement inside the slot's own claim clears the claim and the visits.</param>
    /// <param name="expected">The commit its line let it go at, or the slot's parked tip; empty where none is recorded.</param>
    internal static async Task<PoolJudged> JudgeAsync(PoolSite site, KeptTree slot, SlotJudgementFacts seed, string expected, CancellationToken ct)
    {
        var facts = seed with { Expected = expected };
        var none = new PoolJudged(SlotVerdict.Passed, "", SlotIgnored.None);
        if (Through(facts, 1) is { } held) return none with { Verdict = held };

        var path = slot.Path;
        if (await TreeWorktrees.EntriesAsync(site.Root, ct).ConfigureAwait(false) is not { } entries)
        {
            return none with { Verdict = new(2, SlotJudgement.Unlisted, "git could not list the repository's working trees") };
        }

        var entry = entries.FirstOrDefault(each => TreeGit.SamePath(each.Path, path));
        facts = facts with
        {
            Listed = entry is not null,
            Lock = entry is null ? SlotLocks.Free : PoolLocks.Read(entry),
            BoundElsewhere = entry is not null && await TreeBinding.RefusalAsync(path, ct).ConfigureAwait(false) is not null,
        };
        if (Through(facts, 2) is { } listed) return none with { Verdict = listed };

        var context = new TreeContext(site.Home, WorkplacePlugins.Current.Wire(site.Home));
        facts = facts with { LinkUnderTrees = context.Worktrees.LinkBelowTreesRoot(path) is not null };
        var (hidden, indexRead) = await HiddenAsync(path, ct).ConfigureAwait(false);
        facts = facts with { PathHidden = hidden.Count > 0, Unread = !indexRead };
        var head = await WorkingTree.HeadAsync(path, ct).ConfigureAwait(false) ?? "";
        try
        {
            var (nested, links) = await SetAsideReads.NestedRepositoriesAsync(path, ct).ConfigureAwait(false);
            facts = facts with
            {
                RepositoryInside = nested.Count > 0,
                LinksNotRecorded = (await WorkspaceLinks.UnaccountedAsync(path, TreeShares.Unshared(site.Home, path, links), ct).ConfigureAwait(false)).Count,
            };
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            facts = facts with { Unread = true };
        }

        if (Through(facts, 3) is { } linked) return none with { Verdict = linked, Head = head };

        facts = facts with { OperationHalfDone = HalfDone(path) };
        if (Through(facts, 4) is { } half) return none with { Verdict = half, Head = head };

        IReadOnlyDictionary<string, string> status;
        try
        {
            status = await MachinePorts.Repository.StatusAsync(path, ignored: true, ct).ConfigureAwait(false);
        }
        catch (DriverException)
        {
            return none with { Verdict = new(3, SlotJudgement.Unread, "git could not read its status"), Head = head };
        }

        var sorted = head.Length == 0 ? SlotIgnored.None : await SlotIgnored.ReadAsync(site, path, status, head, ct).ConfigureAwait(false);
        facts = facts with
        {
            TrackedChange = status.Any(each => each.Value is not ("!!" or "??")),
            UntrackedPath = status.Any(each => each.Value == "??"),
            LineOwnIgnored = sorted.Own.FirstOrDefault() ?? "",
            Head = head,
            RefHolds = head.Length > 0 && await HeldByRefAsync(path, head, ct).ConfigureAwait(false),
        };
        return new PoolJudged(SlotJudgement.Judge(facts), head, sorted);
    }

    /// <summary>The verdict where a step up to <paramref name="step"/> failed; the later steps' facts are not read yet, so they say nothing.</summary>
    private static SlotVerdict? Through(SlotJudgementFacts facts, int step) =>
        SlotJudgement.Judge(facts) is { Clean: false } verdict && verdict.Step <= step ? verdict : null;

    /// <summary>Whether a branch, a remote branch, a tag or a ref of the pool's own holds <paramref name="commit"/> in its history.</summary>
    internal static async Task<bool> HeldByRefAsync(string folder, string commit, CancellationToken ct)
    {
        var holding = await MachinePorts.Repository.RefsAsync(folder, ["refs/heads", "refs/remotes", "refs/tags", "refs/daoris"], commit, 1, ct).ConfigureAwait(false);
        return holding is { Count: > 0 };
    }

    private static async Task<(IReadOnlyList<string> Hidden, bool Read)> HiddenAsync(string path, CancellationToken ct) =>
        await MachinePorts.Repository.HiddenAsync(path, ct).ConfigureAwait(false) is { } hidden ? (hidden, true) : ([], false);

    private static bool HalfDone(string path) =>
        GitBranches.GitDirectory(path) is { } git && Operations.Any(name => File.Exists(Path.Combine(git, name)) || Directory.Exists(Path.Combine(git, name)));
}
