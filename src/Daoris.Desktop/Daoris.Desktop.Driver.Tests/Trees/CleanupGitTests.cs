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

    /// <summary>The owner's 18: the person deleted the landing branch after its pull request; the look marks it missing and it reads dropped.</summary>
    [Fact]
    public async Task A_landing_branch_the_person_deleted_is_marked_missing_and_reads_as_the_persons_end()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var landed = await LandAsync(trees, root, "shared.txt", "one\ntwo\n", new LandingSubject("s1", "q1", "First"));
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

    // ---- (2) recovery refs past their retention

    /// <summary>The line holds it and it is past its retention: removed, the holder verified in the same transaction.</summary>
    [Fact]
    public async Task A_recovery_ref_past_retention_the_line_holds_is_removed()
    {
        var root = await RepositoryAsync("engine");
        var main = (await GitAsync(root, "rev-parse", "main")).Trim();
        var old = await OldCommitAsync(root, main, "old work");
        await GitAsync(root, "update-ref", "refs/heads/main", old, main);
        await GitAsync(root, "update-ref", "refs/daoris/discarded/daoris/s-old", old);

        var gone = await RecoveryRetention.TidyAsync(root, "engine", DateTimeOffset.UtcNow, CancellationToken.None);

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

        Assert.Empty(await RecoveryRetention.TidyAsync(root, "engine", DateTimeOffset.UtcNow, CancellationToken.None));

        Assert.Equal(alone, (await GitAsync(root, "rev-parse", "refs/daoris/discarded/daoris/s-alone")).Trim());
        var (refs, unread) = await RecoveryRetention.ReadAsync(root, "engine", CancellationToken.None);
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

        Assert.Empty(await RecoveryRetention.TidyAsync(root, "engine", DateTimeOffset.UtcNow, CancellationToken.None));

        var read = Assert.Single((await RecoveryRetention.ReadAsync(root, "engine", CancellationToken.None)).Refs!);
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
