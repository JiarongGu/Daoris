using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-END2a: the end judgement's proofs over real git, where its facts come through the proofs' own seams (the fast half is
/// <see cref="EndJudgementTests"/>). Canary 6's cases that need a repository: a remote branch reading <c>[gone]</c>, a commit the line
/// holds only by patch, and a plugin's word the line does not hold.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class EndJudgementGitTests : LandedFixture
{
    private static readonly IReadOnlySet<string> NoHolders = new HashSet<string>();

    private static async Task<string> BranchAsync(string root, string name, string file, string content)
    {
        await GitAsync(root, "checkout", "--quiet", "-b", name);
        await CommitAsync(root, file, content, $"work on {name}");
        var tip = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "checkout", "--quiet", "main");
        return tip;
    }

    private static Task<EndVerdict> JudgeSessionAsync(string root, string branch, string tip, LandedBranch? landing = null) =>
        EndJudgement.JudgeAsync(root, EndCycle.NoLanding, new EndSubject(branch, tip, EndRole.Session, landing), "main", NoHolders, null, CancellationToken.None);

    [Fact]
    public async Task A_session_branch_the_line_merged_is_ended_and_one_it_has_not_is_a_claim()
    {
        var root = await RepositoryAsync("engine");
        var merged = await BranchAsync(root, "daoris/s1", "a.txt", "a\n");
        var open = await BranchAsync(root, "daoris/s2", "b.txt", "b\n");
        await GitAsync(root, "merge", "--no-ff", "--quiet", "-m", "merge s1", "daoris/s1");

        var ended = await JudgeSessionAsync(root, "daoris/s1", merged);
        var claim = await JudgeSessionAsync(root, "daoris/s2", open);

        Assert.Equal((EndState.Ended, EndBy.Merged, "main"), (ended.State, ended.By, ended.Where));
        Assert.Equal(EndState.Claim, claim.State);
    }

    [Fact]
    public async Task A_remote_branch_reading_gone_is_not_an_end()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var tip = await BranchAsync(root, "daoris/s1", "a.txt", "a\n");
        await GitAsync(root, "push", "--quiet", "-u", "origin", "daoris/s1");
        await GitAsync(root, "push", "--quiet", "origin", "--delete", "daoris/s1");
        await GitAsync(root, "fetch", "--prune", "--quiet");
        Assert.Contains("gone", await GitAsync(root, "branch", "-vv", "--list", "daoris/s1"));

        var verdict = await JudgeSessionAsync(root, "daoris/s1", tip);

        Assert.Equal(EndState.Claim, verdict.State);
    }

    /// <summary>The line holds the commit's patch under a commit of its own, and has changed the file since: patch equivalence, never an end (D102, D148).</summary>
    [Fact]
    public async Task A_commit_the_line_holds_only_by_patch_is_not_an_end()
    {
        var root = await RepositoryAsync("engine");
        var tip = await BranchAsync(root, "daoris/s1", "b.txt", "two\n");
        // The line gained a file first, so no tree of its own is the branch's tree (a one-commit cherry-pick of a tree that is still
        // the same would read as a squash, which is SQUASHTIDY1's proof), then took the patch and changed the file again.
        await CommitAsync(root, "c.txt", "c\n", "another change");
        await GitAsync(root, "cherry-pick", "-x", tip);
        await CommitAsync(root, "b.txt", "three\n", "the line moves on");
        // git itself calls the commit equivalent: the case is real.
        Assert.StartsWith("- ", await GitAsync(root, "cherry", "main", tip));

        var verdict = await JudgeSessionAsync(root, "daoris/s1", tip);

        Assert.NotEqual(EndState.Ended, verdict.State);
    }

    [Fact]
    public async Task A_landing_branch_ends_when_the_line_holds_it_and_is_handed_until_then()
    {
        var root = await RepositoryAsync("engine");
        var tip = await BranchAsync(root, "feature/q1-work", "a.txt", "a\n");
        var entry = new LandedBranch("engine", "aurora", "feature/q1-work", "main", tip, "s1", "q1", "Work", DateTimeOffset.UnixEpoch);
        var cycle = EndCycle.Recorded(entry);
        var subject = new EndSubject(entry.Branch, tip, EndRole.Landing, entry);

        var handed = await EndJudgement.JudgeAsync(root, cycle, subject, "main", NoHolders, null, CancellationToken.None);
        await GitAsync(root, "merge", "--ff-only", "--quiet", "feature/q1-work");
        var ended = await EndJudgement.JudgeAsync(root, cycle, subject, "main", NoHolders, null, CancellationToken.None);

        Assert.Equal(EndState.Handed, handed.State);
        Assert.Equal((EndState.Ended, EndBy.Merged), (ended.State, ended.By));
    }

    /// <summary>A completed answer whose merge commit is not on the line here is the platform's word alone: handed, never ended (D148 point 4).</summary>
    [Fact]
    public async Task A_pull_requests_completed_answer_without_its_merge_commit_here_is_not_an_end()
    {
        var root = await RepositoryAsync("engine");
        var tip = await BranchAsync(root, "feature/q1-work", "a.txt", "a\n");
        var unknown = new string('c', 40);
        var entry = new LandedBranch("engine", "aurora", "feature/q1-work", "main", tip, "s1", "q1", "Work", DateTimeOffset.UnixEpoch)
        {
            Plugin = "forge",
            PullRequestState = new PullRequestState(PullRequestStates.Completed) { MergeCommit = unknown, SourceCommit = tip, Target = "main" },
        };
        var subject = new EndSubject(entry.Branch, tip, EndRole.Landing, entry);

        var verdict = await EndJudgement.JudgeAsync(root, EndCycle.Recorded(entry), subject, "main", NoHolders, null, CancellationToken.None);

        Assert.Equal(EndState.Handed, verdict.State);
        Assert.Contains(PullRequestCodes.MergeNotHere, verdict.Evidence);
    }

    [Fact]
    public async Task A_session_inside_a_landing_the_line_holds_is_ended_as_landed()
    {
        var root = await RepositoryAsync("engine");
        var tip = await BranchAsync(root, "daoris/s1", "a.txt", "a\n");
        // A squashing landing: one commit of its own on the landing branch, the session's tip recorded as what it squashed.
        await GitAsync(root, "checkout", "--quiet", "-b", "feature/q1-work", "main");
        await GitAsync(root, "merge", "--squash", "--quiet", "daoris/s1");
        await GitAsync(root, "commit", "--quiet", "-m", "Work (squashed)");
        var squashed = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "checkout", "--quiet", "main");
        var entry = new LandedBranch("engine", "aurora", "feature/q1-work", "main", squashed, "s1", "q1", "Work", DateTimeOffset.UnixEpoch) { Squashed = tip };
        var before = await JudgeSessionAsync(root, "daoris/s1", tip, entry);
        await GitAsync(root, "merge", "--ff-only", "--quiet", "feature/q1-work");

        var after = await JudgeSessionAsync(root, "daoris/s1", tip, entry);

        Assert.Equal(EndState.Handed, before.State);
        Assert.Equal((EndState.Ended, EndBy.Landed, "feature/q1-work"), (after.State, after.By, after.Where));
    }
}
