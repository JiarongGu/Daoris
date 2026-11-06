using Daoris.Driver;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What each page's read asks of git (OPTIMIZE1a): the Git page's reads and plans, and a landed session's review, each counted on its
/// first open over a repository with origin, a session's tree two commits ahead, a branch of the person's and a dirty checkout. Each is
/// held to the starts measured and to the same command asked again in the same folder as measured. The figures are said in the test's
/// output, and recorded in the git area's table of starts.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ViewGitCountTests(ITestOutputHelper output) : LandedFixture
{
    // Each read's starts on its first open, and those asked twice, as measured (2026-11-06). The commit page and a compare are two by
    // design (D147 §2.4, §2.6); a plan reads the checkout's top, head and branch one start each before the state its press must find
    // unchanged. Asked twice: the stash plan's `refs/stash` (the plan's state, and the stash step's own read of it), left for the plan's own cut.
    private static readonly Dictionary<string, (int Starts, int Repeated)> Budgets = new(StringComparer.Ordinal)
    {
        ["GIT_BRANCHES"] = (3, 0),
        ["CHECKOUT_BRANCH"] = (1, 0),
        ["GIT_CHANGES"] = (5, 0),
        ["GIT_HUNKS"] = (5, 0),
        ["GIT_GRAPH"] = (1, 0),
        ["GIT_COMMIT"] = (2, 0),
        ["GIT_HISTORY"] = (1, 0),
        ["GIT_BLAME"] = (1, 0),
        ["GIT_COMPARE"] = (2, 0),
        ["GIT_COMMIT_PLAN"] = (10, 0),
        ["GIT_SWITCH_PLAN"] = (7, 0),
        ["GIT_STASH_PLAN"] = (11, 1),
        ["GIT_DELETES"] = (0, 0), // the journal alone, where it holds no delete
        ["LANDED_REVIEW"] = (6, 0),
    };

    [Fact]
    public async Task Each_git_page_read_asks_git_within_the_starts_and_repeats_measured()
    {
        var (root, _) = await RepositoryWithOriginAsync("engine");
        var trees = new SessionTrees(Home);
        var tree = await trees.OpenAsync(root, "engine", "aurora");
        var line = (await GitAsync(root, "rev-parse", "HEAD")).Trim();
        await CommitAsync(tree.Path, "work.txt", "the work\n", "the work");
        await CommitAsync(tree.Path, "more.txt", "more\n", "more of the work");
        var tip = (await GitAsync(tree.Path, "rev-parse", "HEAD")).Trim();
        await GitAsync(root, "branch", "topic");
        await File.WriteAllTextAsync(Path.Combine(root, "shared.txt"), "one\nmine\n");
        await File.WriteAllTextAsync(Path.Combine(root, "loose.txt"), "loose\n");
        var sessions = new[] { new SessionRecord("s-view", "engine", "running") { Tree = tree.Path, Created = DateTimeOffset.UtcNow } };
        var config = DriverConfig.Load(DaorisHome.Entry(Home, HomeEntry.DriverConfig));
        var journal = new ActJournal(Home);

        await CountAsync("GIT_BRANCHES", async () => (await GitBranches.ListAsync([("engine", null, root)], config, Home, sessions)).Repositories.Count == 1);
        await CountAsync("CHECKOUT_BRANCH", async () => await WorkingTree.BranchAsync(root) == "main");
        await CountAsync("GIT_CHANGES", async () => (await GitChanges.ReadAsync(root)).Value is not null);
        await CountAsync("GIT_HUNKS", async () => (await GitHunks.ReadAsync(root, "shared.txt", GitHunks.Unstaged)).Value is not null);
        // Each GitReads its own, as a first open is: a second asks nothing, its answer kept by the commit's id.
        await CountAsync("GIT_GRAPH", async () => (await new GitReads().GraphAsync(root, [tip])).Value is not null);
        await CountAsync("GIT_COMMIT", async () => (await new GitReads().CommitAsync(root, tip)).Value is not null);
        await CountAsync("GIT_HISTORY", async () => (await new GitReads().HistoryAsync(root, tip, "work.txt")).Value is not null);
        await CountAsync("GIT_BLAME", async () => (await new GitReads().BlameAsync(root, tip, "work.txt")).Value is not null);
        await CountAsync("GIT_COMPARE", async () => (await new GitReads().CompareAsync(root, line, tip)).Value is not null);
        await CountAsync("GIT_COMMIT_PLAN", async () => (await new CommitActs(journal).PlanAsync(root, CommitVerbs.Commit, CancellationToken.None)).Value is not null);
        await CountAsync("GIT_SWITCH_PLAN", async () => (await new SwitchActs(journal).PlanAsync(root, "topic", CancellationToken.None)).Value is not null);
        await CountAsync("GIT_STASH_PLAN", async () => (await new StashActs(journal).PlanAsync(root, CancellationToken.None)).Value is not null);
        await CountAsync("GIT_DELETES", async () => (await DeletedBranches.ReadAsync(Home, root, "engine", CancellationToken.None)).Value is not null);
    }

    [Fact]
    public async Task A_landed_review_asks_git_within_the_starts_and_repeats_measured()
    {
        var root = await RepositoryAsync("engine");
        var trees = new SessionTrees(Home);
        var subject = new LandingSubject("s1a2b3c4", "0fda18", "Fix the API gap");
        await LandAsync(trees, root, "shared.txt", "one\ntwo\n", subject, extra: ("added.txt", "new\n"));
        var entry = trees.Recorded.Landing(subject.Session)!;

        await CountAsync("LANDED_REVIEW", async () => (await trees.LandedReviewAsync(root, entry)).Changes is TreeDiff);
    }

    private async Task CountAsync(string view, Func<Task<bool>> read)
    {
        using var tally = GitStartTally.Open(Scratch);
        Assert.True(await read(), $"{view} did not answer");
        var report = tally.Report(view);
        output.WriteLine(report);
        Assert.True(tally.Starts.Count <= Budgets[view].Starts, report);
        Assert.True(tally.Repeated <= Budgets[view].Repeated, report);
    }
}
