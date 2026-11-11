using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A landing observes the same changes after a rebase without replaying work the line already holds.</summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Parallel, Category.Git)]
public sealed class PatchEquivalentLandingTests : GraphRunFixture
{
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(true, false)]
    public async Task A_landing_reaches_the_line_when_all_its_commits_are_rebased_equivalents(bool withHolds, bool observedBeforeMerge)
    {
        var (root, tree, branch) = await SetUpAsync(withHolds);

        await CommitAsync(tree, "part.txt", "the second part", "the second part");
        var tip = await HeadAsync(tree);
        if (observedBeforeMerge)
        {
            await PassAsync(root, branch, tree);
            Assert.Equal(NodeStates.Active, Status().State);
        }

        await CommitAsync(root, "line.txt", "the line moves", "the line moves");
        await GitAsync(root, "checkout", "-b", "rebased");
        await GitAsync(root, "cherry-pick", tip + "~1", tip);
        var rebased = await HeadAsync(root);
        Assert.NotEqual(tip, rebased);
        await GitAsync(root, "checkout", "main");
        await GitAsync(root, "merge", "--no-ff", "rebased", "-m", "merge rebased work");
        if (observedBeforeMerge) await CommitAsync(root, "s1.txt", "the work of s1, updated on the line", "update landed work");
        var line = await HeadAsync(root);
        Assert.NotEqual(tip, (await GitAsync(root, "merge-base", tip, line)).Trim());
        var plan = await new SessionTrees(_home).PlanAsync(tree, Work);
        Assert.Empty(plan.Lands!);
        Assert.Equal(2, plan.LeftOut!.Count);
        if (observedBeforeMerge) Assert.All(plan.LeftOut, commit => Assert.Equal(LeftOutWhy.Patch, commit.Why));

        for (var pass = 0; pass < 3; pass++) await PassAsync(root, branch, tree);

        var status = Status();
        Assert.Equal(NodeStates.Reached, status.State);
        Assert.Equal(tip, status.Acceptance?.SubjectRevision);
        Assert.Contains("the same change", status.Words);
        Assert.Contains("the same change", WorkflowGraphRuns.Said(new NodeDeclaration("landing", NodeKinds.Land, 1, NodeReach.Performed), status));
        Assert.Equal(line, await HeadAsync(root));
        Assert.Equal(tip, await HeadAsync(tree));
        Assert.Equal("the second part", (await GitAsync(root, "show", "main:part.txt")).Trim());
        Assert.Equal(observedBeforeMerge ? "the work of s1, updated on the line" : "the work of s1", (await GitAsync(root, "show", "main:s1.txt")).Trim());
    }

    [Fact]
    public async Task A_commit_the_line_lacks_still_lands_without_replaying_its_equivalent()
    {
        using var lease = HomeLease.TryAcquire(_home, LeaseKind.Headless, out _);
        var (root, tree, branch) = await SetUpAsync(withHolds: true);
        var first = await HeadAsync(tree);
        await CommitAsync(root, "line.txt", "the line moves", "the line moves");
        await GitAsync(root, "cherry-pick", first);
        await CommitAsync(root, "s1.txt", "the work of s1, updated on the line", "update landed work");
        await CommitAsync(tree, "missing.txt", "the missing work", "the missing work");
        var plan = await new SessionTrees(_home).PlanAsync(tree, Work);
        Assert.Equal(first, Assert.Single(plan.LeftOut!).Sha);
        Assert.Equal("the missing work", Assert.Single(plan.Lands!).Subject);

        var door = new LandingDoor(() => new LandPerformer(
            _home, _world, new SessionTrees(_home), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), _lines.Add) { HoldsLease = () => true });
        await PassAsync(root, branch, tree, door: door);
        await door.IdleAsync();

        Assert.Equal("the missing work", (await GitAsync(root, "show", "main:missing.txt")).Trim());
        Assert.Equal("the line moves", (await GitAsync(root, "show", "main:line.txt")).Trim());
        Assert.Equal("the work of s1, updated on the line", (await GitAsync(root, "show", "main:s1.txt")).Trim());
        Assert.Equal("", (await GitAsync(tree, "status", "--porcelain")).Trim());
    }

    private async Task<(string Root, string Tree, GrownBranch Branch)> SetUpAsync(bool withHolds)
    {
        var root = await RepositoryAsync("engine");
        var tree = await DoneSessionAsync(root, "s1", "q1");
        var graph = ClaimedThenLand("""{"method":"merge"}""");
        if (withHolds)
        {
            var source = await WorkflowOperations.SourceAsync(Config(), _home, _ => Task.FromResult(new ChoicesRegistry(null, "unread")),
                "merge-automatic", opinion: false, look: false, CancellationToken.None);
            graph = source.Graph!.ToJsonString();
        }

        RunHome.Bind(_home, "q1", RunHome.SaveGraph(_home, graph), DateTimeOffset.UtcNow);
        return (root, tree, Assert.Single(new SessionBranches(_home).All()));
    }

    private NodeStatus Status() => new NodeRecordFiles(_home, () => false).Read(RunUnits.IdOf("q1"))!.Statuses["landing"];
}
