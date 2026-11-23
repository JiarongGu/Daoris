using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A landing the integration replayed reads as landed at the run's <c>land</c> step, though no commit of the branch has a patch the line
/// holds: the replay settled a file (the backlog's archive) as the line stood, so its commit is another change than the branch's, and
/// merging the branch into the line adds nothing.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class ReplayedLandingTests : GraphRunFixture
{
    private static readonly DateTimeOffset Bound = new(2026, 11, 23, 0, 0, 0, TimeSpan.Zero);

    private static string Lines(string second, string last) => string.Join('\n', ["l1", second, "l3", "l4", "l5", "l6", "l7", "l8", "l9", last]);

    [Fact]
    public async Task A_landing_whose_replay_settled_a_file_reaches_the_line_though_no_patch_of_it_matches()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "log.txt", Lines("l2", "l10"), "the log");
        var tree = await DoneSessionAsync(root, "s1", "q1");
        await CommitAsync(tree, "log.txt", Lines("l2 of the work", "l10"), "the second part");
        var tip = await HeadAsync(tree);
        var first = (await GitAsync(tree, "rev-parse", "HEAD~1")).Trim();
        RunHome.Bind(_home, "q1", RunHome.SaveGraph(_home, ClaimedThenLand("""{"method":"merge"}""")), Bound);
        var branch = Assert.Single(new SessionBranches(_home).All());

        await CommitAsync(root, "line.txt", "the line moves", "the line moves");
        await GitAsync(root, "checkout", "-b", "replayed");
        await GitAsync(root, "cherry-pick", first);
        // The replay of the second commit carries what the line settled beside it: its patch is another change than the branch's.
        await CommitAsync(root, "log.txt", Lines("l2 of the work", "l10 as the line settled it"), "the second part");
        await GitAsync(root, "checkout", "main");
        await GitAsync(root, "merge", "--no-ff", "replayed", "-m", "merge replayed work");
        var line = await HeadAsync(root);

        Assert.Equal(tip, await HeadAsync(tree));
        Assert.NotEqual(tip, (await GitAsync(root, "rev-parse", "replayed")).Trim());
        Assert.Contains($"+ {tip}", await GitAsync(root, "cherry", line, tip));

        for (var pass = 0; pass < 3; pass++) await PassAsync(root, branch, tree);

        var status = new NodeRecordFiles(_home, () => false).Read(RunUnits.IdOf("q1"))!.Statuses["landing"];
        Assert.Equal(NodeStates.Reached, status.State);
        Assert.Equal(tip, status.Acceptance?.SubjectRevision);
        Assert.Contains("already holds all 2 commit(s)", status.Words);
        Assert.Equal(line, await HeadAsync(root));
    }

    [Fact]
    public async Task A_landing_whose_work_the_line_lacks_is_not_read_as_landed()
    {
        var root = await RepositoryAsync("engine");
        await CommitAsync(root, "log.txt", Lines("l2", "l10"), "the log");
        var tree = await DoneSessionAsync(root, "s1", "q1");
        await CommitAsync(tree, "log.txt", Lines("l2 of the work", "l10"), "the second part");
        RunHome.Bind(_home, "q1", RunHome.SaveGraph(_home, ClaimedThenLand("""{"method":"merge"}""")), Bound);
        var branch = Assert.Single(new SessionBranches(_home).All());
        await CommitAsync(root, "line.txt", "the line moves", "the line moves");

        for (var pass = 0; pass < 3; pass++) await PassAsync(root, branch, tree);

        var status = new NodeRecordFiles(_home, () => false).Read(RunUnits.IdOf("q1"))!.Statuses["landing"];
        Assert.NotEqual(NodeStates.Reached, status.State);
    }
}
