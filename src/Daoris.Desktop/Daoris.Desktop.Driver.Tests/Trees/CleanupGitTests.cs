using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CLEANUP1 over real git: a landing whose branch the person deleted is marked missing by the look on git's own word and reads as the
/// person's end; one whose branch does not read is kept; a recovery ref past its retention goes only where a branch or a tag holds its
/// commit, verified in the delete's own transaction, and one nothing else holds is kept.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class CleanupGitTests : LandedFixture
{
    private static Task<IReadOnlySet<string>> NoneAsync(CancellationToken ct) => Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

    /// <summary>No branch a landing made: the retention's holders are every branch and tag of the person's.</summary>
    private static readonly IReadOnlySet<string> NoneMade = new HashSet<string>();

    /// <summary>A branch git cannot read: its loose ref written over with something that is no commit.</summary>
    private static void Break(string root, string branch)
    {
        var loose = Path.Combine([root, ".git", "refs", "heads", .. branch.Split('/')]);
        Assert.True(File.Exists(loose), $"`{branch}` is a loose ref");
        File.WriteAllText(loose, "not a commit\n");
    }

    /// <summary>A commit dated long ago, with <paramref name="parent"/>'s tree and <paramref name="parent"/> as its parent, held by no ref.</summary>
    private static async Task<string> OldCommitAsync(string root, string parent, string message)
    {
        var tree = (await GitAsync(root, "rev-parse", $"{parent}^{{tree}}")).Trim();
        var body = $"tree {tree}\nparent {parent}\nauthor Tester <tester@example.invalid> 1600000000 +0000\n"
            + $"committer Tester <tester@example.invalid> 1600000000 +0000\n\n{message}\n";
        var (code, made, err) = await WorkingTree.GitAsync(root, ["hash-object", "-t", "commit", "-w", "--stdin"], body, CancellationToken.None);
        Assert.True(code == 0, err);
        return made.Trim();
    }

    private async Task<IReadOnlyList<WorkMilestone>> MilestonesAsync(string root)
    {
        var unread = new List<string>();
        return await WorkInventories.MilestonesAsync(Home, new Dictionary<string, string> { ["engine"] = root }, new HashSet<string>(), unread);
    }

    // ---- (1) a landing whose branch the person removed

    /// <summary>The session's tree and branch gone, as the look takes them once a landing holds them: the landing alone stands.</summary>
    private static async Task SessionGoneAsync(string root)
    {
        foreach (var line in (await GitAsync(root, "worktree", "list", "--porcelain")).Split('\n'))
        {
            if (line.StartsWith("worktree ", StringComparison.Ordinal) && !SamePath(line["worktree ".Length..].Trim(), root))
            {
                await GitAsync(root, "worktree", "remove", "--force", line["worktree ".Length..].Trim());
            }
        }

        foreach (var branch in (await GitAsync(root, "for-each-ref", "--format=%(refname:short)", "refs/heads/daoris/")).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            await GitAsync(root, "branch", "-D", branch);
        }

        static bool SamePath(string a, string b) =>
            string.Equals(Path.GetFullPath(a).TrimEnd('\\', '/'), Path.GetFullPath(b).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The person deleted the landing branch after its pull request: the look marks it missing, and it reads dropped.</summary>
    [Fact]
    public async Task A_landing_branch_the_person_deleted_is_marked_missing_and_reads_as_the_persons_end()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s1", "q1", "First"));
        await SessionGoneAsync(root);
        await GitAsync(root, "branch", "-D", landed.Branch!);

        using (HomeLease.TryAcquire(Home, LeaseKind.Headless, out _))
        {
            await trees.TidyEmptyAsync(root, "engine", "aurora", NoneAsync, CancellationToken.None);
        }

        var trace = Assert.Single(new LandedBranches(Home).Entries(), each => each.Branch == landed.Branch);
        Assert.NotNull(trace.GoneAt);
        Assert.Equal(LandedGone.Missing, trace.GoneAs);
        Assert.Null(trace.RemovedAs);
        var unit = Assert.Single(await MilestonesAsync(root), each => each.Entries.Any(entry => entry.Resource == landed.Branch));
        Assert.Equal(WorkEndState.Dropped, unit.End.State);
        Assert.Equal("completed", unit.Cleanup.State);
    }

    /// <summary>A trace from before the field (retired with no reason) is asked once, and marked.</summary>
    [Fact]
    public async Task A_trace_from_before_is_asked_once_and_marked_missing()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s1", "q1", "First"));
        await SessionGoneAsync(root);
        await GitAsync(root, "branch", "-D", landed.Branch!);
        new LandedBranches(Home).Gone("engine", [landed.Branch!]);

        using (HomeLease.TryAcquire(Home, LeaseKind.Headless, out _))
        {
            await trees.TidyEmptyAsync(root, "engine", "aurora", NoneAsync, CancellationToken.None);
        }

        Assert.Equal(LandedGone.Missing, Assert.Single(new LandedBranches(Home).Entries(), each => each.Branch == landed.Branch).GoneAs);
    }

    /// <summary>🔴 A landing branch that does not read is not missing: its record stays as it was, and it never reads dropped.</summary>
    [Fact]
    public async Task A_landing_whose_branch_does_not_read_is_kept_and_never_read_as_dropped()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s1", "q1", "First"));
        Break(root, landed.Branch!);

        using (HomeLease.TryAcquire(Home, LeaseKind.Headless, out _))
        {
            await trees.TidyEmptyAsync(root, "engine", "aurora", NoneAsync, CancellationToken.None);
        }

        var entry = Assert.Single(new LandedBranches(Home).Entries(), each => each.Branch == landed.Branch);
        Assert.Null(entry.GoneAs);
        Assert.DoesNotContain(await MilestonesAsync(root), unit => unit.End.State == WorkEndState.Dropped);
    }

    /// <summary>🔴 Packed refs compare case exactly, so git calls a name missing while it stands in another case: a doubt, kept.</summary>
    [Fact]
    public async Task A_landing_branch_standing_under_another_case_is_kept_as_standing()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s1", "q1", "First"));
        var tip = (await GitAsync(root, "rev-parse", landed.Branch!)).Trim();
        await GitAsync(root, "branch", "-D", landed.Branch!);
        await GitAsync(root, "update-ref", "refs/heads/" + landed.Branch!.ToUpperInvariant(), tip);
        await GitAsync(root, "pack-refs", "--all");

        using (HomeLease.TryAcquire(Home, LeaseKind.Headless, out _))
        {
            await trees.TidyEmptyAsync(root, "engine", "aurora", NoneAsync, CancellationToken.None);
        }

        var entry = Assert.Single(new LandedBranches(Home).Entries(), each => each.Branch == landed.Branch);
        Assert.NotEqual(LandedGone.Missing, entry.GoneAs);
        Assert.DoesNotContain(await MilestonesAsync(root), unit => unit.End.State == WorkEndState.Dropped);
    }

    /// <summary>
    /// 🔴 After review: the person renamed the landing branch. git calls the recorded name missing, but the new name holds the recorded
    /// tip: the work moved, so it is kept and says where, never read as dropped, and no recovery ref is made since a branch holds it.
    /// </summary>
    [Fact]
    public async Task A_renamed_landing_branch_reads_moved_not_dropped()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s1", "q1", "First"));
        await SessionGoneAsync(root);
        await GitAsync(root, "branch", "-m", landed.Branch!, "feature/renamed");

        using (HomeLease.TryAcquire(Home, LeaseKind.Headless, out _))
        {
            await trees.TidyEmptyAsync(root, "engine", "aurora", NoneAsync, CancellationToken.None);
        }

        var trace = Assert.Single(new LandedBranches(Home).Entries(), each => each.Branch == landed.Branch);
        Assert.Equal((LandedGone.Moved, "feature/renamed", (string?)null), (trace.GoneAs, trace.MovedTo, trace.KeptOn));
        // CLEANUP1b's guard: a branch of the person's is never the line, so no end is kept.
        Assert.Null(trace.Held);
        var unit = Assert.Single(await MilestonesAsync(root), each => each.Entries.Any(entry => entry.Resource == landed.Branch));
        Assert.Equal(WorkEndState.NotEnded, unit.End.State);
        Assert.Contains("`feature/renamed`", unit.End.Said, StringComparison.Ordinal);
        Assert.Equal("", (await GitAsync(root, "for-each-ref", "refs/daoris/discarded/")).Trim());
    }

    /// <summary>
    /// 🔴 After review: the person deleted the landing branch after Daoris removed the session branch, and no ref holds the recorded tip.
    /// Before the record is retired, Daoris keeps that commit on a recovery ref, held by nothing so kept at any age, and lists it with its
    /// delete command.
    /// </summary>
    [Fact]
    public async Task A_dropped_landing_whose_tip_nothing_holds_gets_a_recovery_ref_first()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s1", "q1", "First"));
        var tip = (await GitAsync(root, "rev-parse", landed.Branch!)).Trim();
        await SessionGoneAsync(root);
        await GitAsync(root, "branch", "-D", landed.Branch!);

        using (HomeLease.TryAcquire(Home, LeaseKind.Headless, out _))
        {
            await trees.TidyEmptyAsync(root, "engine", "aurora", NoneAsync, CancellationToken.None);
        }

        var trace = Assert.Single(new LandedBranches(Home).Entries(), each => each.Branch == landed.Branch);
        Assert.Equal((LandedGone.Missing, $"refs/daoris/discarded/{landed.Branch}"), (trace.GoneAs, trace.KeptOn));
        Assert.Equal(tip, (await GitAsync(root, "rev-parse", trace.KeptOn!)).Trim());
        var (units, recovery) = await WorkInventories.ReadAsync(Home, new Dictionary<string, string> { ["engine"] = root }, new HashSet<string>(), new List<string>());
        Assert.Equal(WorkEndState.Dropped, Assert.Single(units, each => each.Entries.Any(entry => entry.Resource == landed.Branch)).End.State);
        Assert.Contains(recovery, entry => entry.Resource == trace.KeptOn && entry.Detail.Contains($"git update-ref -d {trace.KeptOn}", StringComparison.Ordinal));
    }

    /// <summary>A moved landing is followed: once the branch it moved to is gone too, the look asks again, keeps its commit, and reads it dropped.</summary>
    [Fact]
    public async Task A_moved_landing_is_asked_again_once_the_branch_it_moved_to_is_gone()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s1", "q1", "First"));
        await SessionGoneAsync(root);
        await GitAsync(root, "branch", "-m", landed.Branch!, "feature/renamed");
        using (HomeLease.TryAcquire(Home, LeaseKind.Headless, out _))
        {
            await trees.TidyEmptyAsync(root, "engine", "aurora", NoneAsync, CancellationToken.None);
            await GitAsync(root, "branch", "-D", "feature/renamed");
            await trees.TidyEmptyAsync(root, "engine", "aurora", NoneAsync, CancellationToken.None);
        }

        var trace = Assert.Single(new LandedBranches(Home).Entries(), each => each.Branch == landed.Branch);
        Assert.Equal((LandedGone.Missing, $"refs/daoris/discarded/{landed.Branch}"), (trace.GoneAs, trace.KeptOn));
    }

    /// <summary>
    /// 🔴 Re-check: the branch a landing moved to is made again at an unrelated commit. Its name resolves, but it no longer holds the
    /// recorded tip, so the look asks again, keeps the commit nothing else holds, and reads it dropped.
    /// </summary>
    [Fact]
    public async Task A_moved_landing_whose_branch_is_made_again_elsewhere_is_asked_again()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s1", "q1", "First"));
        await SessionGoneAsync(root);
        await GitAsync(root, "branch", "-m", landed.Branch!, "feature/renamed");
        using (HomeLease.TryAcquire(Home, LeaseKind.Headless, out _))
        {
            await trees.TidyEmptyAsync(root, "engine", "aurora", NoneAsync, CancellationToken.None);
            await GitAsync(root, "branch", "-f", "feature/renamed", "main");
            await trees.TidyEmptyAsync(root, "engine", "aurora", NoneAsync, CancellationToken.None);
        }

        var trace = Assert.Single(new LandedBranches(Home).Entries(), each => each.Branch == landed.Branch);
        Assert.Equal((LandedGone.Missing, $"refs/daoris/discarded/{landed.Branch}"), (trace.GoneAs, trace.KeptOn));
    }

    /// <summary>🔴 Re-check: a local branch whose name ends in `/HEAD` is a branch like any other: a rename to one reads moved, not dropped.</summary>
    [Fact]
    public async Task A_landing_renamed_to_a_name_ending_in_HEAD_reads_moved()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s1", "q1", "First"));
        var tip = (await GitAsync(root, "rev-parse", landed.Branch!)).Trim();
        await SessionGoneAsync(root);
        await GitAsync(root, "update-ref", "refs/heads/feature/HEAD", tip);
        await GitAsync(root, "branch", "-D", landed.Branch!);

        using (HomeLease.TryAcquire(Home, LeaseKind.Headless, out _))
        {
            await trees.TidyEmptyAsync(root, "engine", "aurora", NoneAsync, CancellationToken.None);
        }

        var trace = Assert.Single(new LandedBranches(Home).Entries(), each => each.Branch == landed.Branch);
        Assert.Equal((LandedGone.Moved, "feature/HEAD"), (trace.GoneAs, trace.MovedTo));
    }

    // ---- CLEANUP1b: a landing whose deleted branch's tip the line holds

    /// <summary>The person's line merges the landing branch with a merge commit, as a pull request merged so and pulled leaves it.</summary>
    private static Task MergeIntoLineAsync(string root, string branch) => GitAsync(root, "merge", "--no-ff", "--no-edit", branch);

    private async Task<WorkMilestone> UnitOfAsync(string root, string branch) =>
        Assert.Single(await MilestonesAsync(root), each => each.Entries.Any(entry => entry.Resource == branch));

    /// <summary>
    /// 🔴 CLEANUP1b: a pull request merged with a merge commit and pulled, then the person deleted the landing branch. The line holds its
    /// recorded tip, so the end judgement keeps the end as the look retires it: ended, its cleanup completed, never read as moved to the
    /// line, and no recovery ref, since the line holds its commits.
    /// </summary>
    [Fact]
    public async Task A_landing_branch_the_line_merged_and_the_person_deleted_reads_ended_with_its_cleanup_completed()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s1", "q1", "First"));
        var tip = (await GitAsync(root, "rev-parse", landed.Branch!)).Trim();
        await SessionGoneAsync(root);
        await MergeIntoLineAsync(root, landed.Branch!);
        await GitAsync(root, "branch", "-D", landed.Branch!);

        using (HomeLease.TryAcquire(Home, LeaseKind.Headless, out _))
        {
            await trees.TidyEmptyAsync(root, "engine", "aurora", NoneAsync, CancellationToken.None);
        }

        var trace = Assert.Single(new LandedBranches(Home).Entries(), each => each.Branch == landed.Branch);
        Assert.Equal((LandedGone.Missing, (string?)null, (string?)null), (trace.GoneAs, trace.MovedTo, trace.KeptOn));
        Assert.Equal((EndBy.Merged, tip, "main", "main"), (trace.Held?.By, trace.Held?.Tip, trace.Held?.Line, trace.Held?.On));
        var unit = await UnitOfAsync(root, landed.Branch!);
        Assert.Equal((WorkEndState.Ended, "completed"), (unit.End.State, unit.Cleanup.State));
        Assert.Equal("", (await GitAsync(root, "for-each-ref", "refs/daoris/discarded/")).Trim());
    }

    /// <summary>
    /// 🔴 CLEANUP1b: what an earlier look wrote of such a landing is judged once: a trace from before CLEANUP1's field, and one CLEANUP1 read
    /// as moved to the line while the line stands where it was found, each reads ended once the line is proved to hold its recorded tip.
    /// </summary>
    [Fact]
    public async Task A_trace_from_before_or_read_moved_to_the_line_is_judged_and_reads_ended()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var before = await LandAsync(trees, root, "one.txt", "one\n", new LandingSubject("s1", "q1", "First"));
        var moved = await LandAsync(trees, root, "two.txt", "two\n", new LandingSubject("s2", "q2", "Second"));
        await SessionGoneAsync(root);
        await MergeIntoLineAsync(root, before.Branch!);
        await MergeIntoLineAsync(root, moved.Branch!);
        await GitAsync(root, "branch", "-D", before.Branch!);
        await GitAsync(root, "branch", "-D", moved.Branch!);
        var records = new LandedBranches(Home);
        records.Gone("engine", [before.Branch!]);
        records.GoneAs(records.Of("engine", moved.Branch!)!, LandedGone.Moved, "main", null, (await GitAsync(root, "rev-parse", "main")).Trim());

        using (HomeLease.TryAcquire(Home, LeaseKind.Headless, out _))
        {
            await trees.TidyEmptyAsync(root, "engine", "aurora", NoneAsync, CancellationToken.None);
        }

        foreach (var branch in new[] { before.Branch!, moved.Branch! })
        {
            var trace = Assert.Single(new LandedBranches(Home).Entries(), each => each.Branch == branch);
            Assert.Equal((LandedGone.Missing, EndBy.Merged, (string?)null), (trace.GoneAs, trace.Held?.By, trace.MovedTo));
            var unit = await UnitOfAsync(root, branch);
            Assert.Equal((WorkEndState.Ended, "completed"), (unit.End.State, unit.Cleanup.State));
        }
    }

    /// <summary>🔴 CLEANUP1b: the clean-up's press, which retires such a landing too, keeps its end the same way.</summary>
    [Fact]
    public async Task The_clean_ups_press_reads_a_landing_the_line_merged_and_the_person_deleted_as_ended()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s1", "q1", "First"));
        await SessionGoneAsync(root);
        await MergeIntoLineAsync(root, landed.Branch!);
        await GitAsync(root, "branch", "-D", landed.Branch!);

        using (HomeLease.TryAcquire(Home, LeaseKind.Headless, out _))
        {
            await trees.CleanAsync([("engine", "aurora", root)], new HashSet<string>());
        }

        var trace = Assert.Single(new LandedBranches(Home).Entries(), each => each.Branch == landed.Branch);
        Assert.Equal((LandedGone.Missing, EndBy.Merged, (string?)null), (trace.GoneAs, trace.Held?.By, trace.MovedTo));
        Assert.Equal(WorkEndState.Ended, (await UnitOfAsync(root, landed.Branch!)).End.State);
    }

    /// <summary>
    /// CLEANUP1b's guard: a session branch is never the line, so a landing whose deleted branch's tip only another session branch holds
    /// still reads as CLEANUP1 says: moved there, not ended, and no fact kept.
    /// </summary>
    [Fact]
    public async Task A_landing_only_another_session_branch_holds_still_reads_moved_not_ended()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s1", "q1", "First"));
        var tip = (await GitAsync(root, "rev-parse", landed.Branch!)).Trim();
        await SessionGoneAsync(root);
        await GitAsync(root, "update-ref", "refs/heads/daoris/other", tip);
        await GitAsync(root, "branch", "-D", landed.Branch!);

        using (HomeLease.TryAcquire(Home, LeaseKind.Headless, out _))
        {
            await trees.TidyEmptyAsync(root, "engine", "aurora", NoneAsync, CancellationToken.None);
        }

        var trace = Assert.Single(new LandedBranches(Home).Entries(), each => each.Branch == landed.Branch);
        Assert.Equal((LandedGone.Moved, "daoris/other", (HeldFact?)null), (trace.GoneAs, trace.MovedTo, trace.Held));
        Assert.Equal(WorkEndState.NotEnded, (await UnitOfAsync(root, landed.Branch!)).End.State);
    }

    /// <summary>
    /// 🔴 Re-check: the recovery for a dropped landing is made in one transaction with git's word that its branch is still absent, so a
    /// branch made again between the look's read and the keep refuses it: no ref, and nothing written for the landing.
    /// </summary>
    [Fact]
    public async Task The_keep_for_a_dropped_landing_is_refused_once_its_branch_stands_again()
    {
        var root = await RepositoryAsync("engine");
        var main = (await GitAsync(root, "rev-parse", "main")).Trim();
        var work = await OldCommitAsync(root, main, "work of a landing");
        await GitAsync(root, "update-ref", "refs/heads/feature/back", work);

        var (kept, why) = await ContentHolds.KeepDiscardedAsync(root, "feature/back", work, CancellationToken.None, absent: "refs/heads/feature/back");

        Assert.Null(kept);
        Assert.NotNull(why);
        Assert.Equal("", (await GitAsync(root, "for-each-ref", "refs/daoris/discarded/")).Trim());
    }

    // ---- (2) recovery refs past their retention

    /// <summary>
    /// 🔴 Re-check: a symbolic branch is never a holder. `personal` names a session branch, which the same look may remove, so a ref only
    /// they hold is kept at any age, and the read says nothing of the person's holds it.
    /// </summary>
    [Fact]
    public async Task A_symbolic_branch_is_never_a_recovery_refs_holder()
    {
        var root = await RepositoryAsync("engine");
        var main = (await GitAsync(root, "rev-parse", "main")).Trim();
        var work = await OldCommitAsync(root, main, "work a session branch holds");
        await GitAsync(root, "update-ref", "refs/heads/daoris/s-aliased", work);
        await GitAsync(root, "symbolic-ref", "refs/heads/personal", "refs/heads/daoris/s-aliased");
        await GitAsync(root, "update-ref", "refs/daoris/discarded/daoris/s-c", work);

        Assert.Empty(await RecoveryRetention.TidyAsync(root, "engine", NoneMade, DateTimeOffset.UtcNow, CancellationToken.None));

        Assert.Equal(work, (await GitAsync(root, "rev-parse", "refs/daoris/discarded/daoris/s-c")).Trim());
        Assert.False(Assert.Single((await RecoveryRetention.ReadAsync(root, "engine", NoneMade, CancellationToken.None)).Refs!).Held);
    }

    /// <summary>
    /// 🔴 After review: only a branch or a tag of the person's holds a recovery ref's commit. A session branch, which the same look may
    /// remove on a squash's record, and a branch Daoris's landing made, never do, so a ref only they hold is kept at any age.
    /// </summary>
    [Fact]
    public async Task A_session_branch_or_a_landing_branch_is_never_a_recovery_refs_holder()
    {
        var root = await RepositoryAsync("engine");
        var main = (await GitAsync(root, "rev-parse", "main")).Trim();
        var bySession = await OldCommitAsync(root, main, "work a session branch holds");
        var byLanding = await OldCommitAsync(root, main, "work a landing branch holds");
        await GitAsync(root, "update-ref", "refs/heads/daoris/s-other", bySession);
        await GitAsync(root, "update-ref", "refs/heads/feature/landed", byLanding);
        await GitAsync(root, "update-ref", "refs/daoris/discarded/daoris/s-a", bySession);
        await GitAsync(root, "update-ref", "refs/daoris/discarded/daoris/s-b", byLanding);
        var made = new HashSet<string> { "feature/landed" };

        Assert.Empty(await RecoveryRetention.TidyAsync(root, "engine", made, DateTimeOffset.UtcNow, CancellationToken.None));

        Assert.Equal(bySession, (await GitAsync(root, "rev-parse", "refs/daoris/discarded/daoris/s-a")).Trim());
        Assert.Equal(byLanding, (await GitAsync(root, "rev-parse", "refs/daoris/discarded/daoris/s-b")).Trim());
        var (refs, unread) = await RecoveryRetention.ReadAsync(root, "engine", made, CancellationToken.None);
        Assert.Null(unread);
        Assert.All(refs!, each => Assert.False(each.Held));
    }

    /// <summary>The line holds it and it is past its retention: removed, the holder verified in the same transaction.</summary>
    [Fact]
    public async Task A_recovery_ref_past_retention_the_line_holds_is_removed()
    {
        var root = await RepositoryAsync("engine");
        var main = (await GitAsync(root, "rev-parse", "main")).Trim();
        var old = await OldCommitAsync(root, main, "old work");
        await GitAsync(root, "update-ref", "refs/heads/main", old, main);
        await GitAsync(root, "update-ref", "refs/daoris/discarded/daoris/s-old", old);

        var gone = await RecoveryRetention.TidyAsync(root, "engine", NoneMade, DateTimeOffset.UtcNow, CancellationToken.None);

        var one = Assert.Single(gone);
        Assert.Equal(("refs/daoris/discarded/daoris/s-old", old, "refs/heads/main"), (one.Ref, one.Commit, one.Holder));
        Assert.Equal("", (await GitAsync(root, "for-each-ref", "refs/daoris/discarded/")).Trim());
    }

    /// <summary>🔴 Nothing else holds its commit: kept at any age, and the gather says so.</summary>
    [Fact]
    public async Task A_recovery_ref_nothing_else_holds_is_kept_at_any_age_and_listed()
    {
        var root = await RepositoryAsync("engine");
        var main = (await GitAsync(root, "rev-parse", "main")).Trim();
        var alone = await OldCommitAsync(root, main, "work nothing else holds");
        await GitAsync(root, "update-ref", "refs/daoris/discarded/daoris/s-alone", alone);

        Assert.Empty(await RecoveryRetention.TidyAsync(root, "engine", NoneMade, DateTimeOffset.UtcNow, CancellationToken.None));

        Assert.Equal(alone, (await GitAsync(root, "rev-parse", "refs/daoris/discarded/daoris/s-alone")).Trim());
        var (refs, unread) = await RecoveryRetention.ReadAsync(root, "engine", NoneMade, CancellationToken.None);
        Assert.Null(unread);
        var read = Assert.Single(refs!);
        Assert.Equal((false, alone), (read.Held, read.Commit));
        Assert.NotNull(read.Dated);
    }

    /// <summary>Held, but younger than its retention: kept, and the gather says from when it goes.</summary>
    [Fact]
    public async Task A_recovery_ref_held_but_within_retention_is_kept()
    {
        var root = await RepositoryAsync("engine");
        var main = (await GitAsync(root, "rev-parse", "main")).Trim();
        await GitAsync(root, "update-ref", "refs/daoris/discarded/daoris/s-young", main);

        Assert.Empty(await RecoveryRetention.TidyAsync(root, "engine", NoneMade, DateTimeOffset.UtcNow, CancellationToken.None));

        var read = Assert.Single((await RecoveryRetention.ReadAsync(root, "engine", NoneMade, CancellationToken.None)).Refs!);
        Assert.True(read.Held);
        Assert.True(RecoveryRetention.Until(read) > DateTimeOffset.UtcNow);
    }

    /// <summary>The look's tidy runs it, in the lease's process, and says what went.</summary>
    [Fact]
    public async Task The_looks_tidy_removes_a_held_recovery_ref_past_retention_and_says_so()
    {
        var root = await RepositoryAsync("engine");
        var main = (await GitAsync(root, "rev-parse", "main")).Trim();
        var old = await OldCommitAsync(root, main, "old work");
        await GitAsync(root, "update-ref", "refs/heads/main", old, main);
        await GitAsync(root, "update-ref", "refs/daoris/discarded/daoris/s-old", old);

        TidyPass pass;
        using (HomeLease.TryAcquire(Home, LeaseKind.Headless, out _))
        {
            pass = await new SessionTrees(Home).TidyEmptyAsync(root, "engine", "aurora", NoneAsync, CancellationToken.None);
        }

        Assert.Equal("refs/daoris/discarded/daoris/s-old", Assert.Single(pass.Recovered).Ref);
        Assert.Contains(new BranchTidying(Home).Said([pass], null, whole: false), line => line.Contains("refs/daoris/discarded/daoris/s-old", StringComparison.Ordinal));
    }
}
