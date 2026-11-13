using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The performer judges a <c>land</c> against its start: a landing of the session accepted since the node started closes it, and a
/// tree whose head is not the node's tip lands nothing. A real pass makes the node active and a recording door keeps its start, which the
/// performer then takes after the tree moved.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Parallel, Category.Git)]
public sealed class GraphRunLandStartTests : GraphRunFixture
{
    /// <summary>A door that keeps what the pass starts and lands nothing.</summary>
    private sealed class RecordingDoor : ILandingDoor
    {
        public List<LandStart> Starts { get; } = [];

        public bool Start(LandStart start)
        {
            Starts.Add(start);
            return true;
        }
    }

    private LandPerformer Performer() => new(
        _home, _world, new SessionTrees(_home), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), _lines.Add) { HoldsLease = () => true };

    /// <summary>The start the pass made of the node at the tree's tip now, its node clocked at <paramref name="at"/>.</summary>
    private async Task<LandStart> StartedAsync(string root, GrownBranch branch, string tree, DateTimeOffset at)
    {
        var door = new RecordingDoor();
        await PassAsync(root, branch, tree, door: door);
        return door.Starts[^1] with { Since = at };
    }

    /// <summary>The node started, and its tip's landing accepted after it by another way: the landing closes as already made, though a later commit is unlanded.</summary>
    [Fact]
    public async Task A_landing_accepted_since_the_node_started_closes_the_land_as_already_made()
    {
        var (root, tree, branch) = await BoundRunAsync(BranchLand);
        var trees = new SessionTrees(_home);
        await PassAsync(root, branch, tree);
        var landed = await trees.LandAsync(tree, Work, gate: await trees.GateAsync(tree, "q1", _world, "s1"));
        Assert.True(landed.Landed, landed.Message);
        var made = await HeadAsync(tree);
        var accepted = trees.Recorded.Landing("s1")!.AcceptedAt("s1");

        await CommitAsync(tree, "late.txt", "made after the landing", "a late change");
        var start = await StartedAsync(root, branch, tree, accepted.AddMinutes(-1));
        Assert.NotEqual(made, start.Tip);
        await Performer().PerformAsync(start);

        Assert.Equal(made, (await GitAsync(root, "rev-parse", PushBranch)).Trim());
        Assert.Contains(_lines, line => line.Event == "landing.auto" && CodeOf(line) == LandingCode.Already);
    }

    /// <summary>A node started after the earlier landing is not closed by it: the work since lands.</summary>
    [Fact]
    public async Task A_landing_accepted_before_the_node_started_does_not_close_the_land()
    {
        var (root, tree, branch) = await BoundRunAsync(BranchLand);
        var trees = new SessionTrees(_home);
        await PassAsync(root, branch, tree);
        Assert.True((await trees.LandAsync(tree, Work, gate: await trees.GateAsync(tree, "q1", _world, "s1"))).Landed);
        var accepted = trees.Recorded.Landing("s1")!.AcceptedAt("s1");

        await CommitAsync(tree, "late.txt", "made after the landing", "a late change");
        var start = await StartedAsync(root, branch, tree, accepted.AddMinutes(1));
        await Performer().PerformAsync(start);

        Assert.Equal(await HeadAsync(tree), (await GitAsync(root, "rev-parse", PushBranch)).Trim());
        Assert.DoesNotContain(_lines, line => line.Event == "landing.auto" && CodeOf(line) == LandingCode.Already);
    }

    /// <summary>A start for a tip the tree has moved past lands nothing, as a conversation's does; the start for the new tip lands it.</summary>
    [Fact]
    public async Task A_head_that_is_not_the_nodes_tip_lands_nothing()
    {
        var (root, tree, branch) = await BoundRunAsync(BranchLand);
        var stale = await StartedAsync(root, branch, tree, DateTimeOffset.UtcNow);
        await CommitAsync(tree, "late.txt", "made after the node started", "a late change");
        var current = await StartedAsync(root, branch, tree, DateTimeOffset.UtcNow);
        Assert.NotEqual(stale.Tip, current.Tip);

        await Performer().PerformAsync(stale);

        Assert.False(await BranchAsync(root, PushBranch), "nothing is made from a head the node was not started at");
        Assert.Contains(_lines, line => line.Event == "landing.auto" && CodeOf(line) == LandingCode.Moved);

        await Performer().PerformAsync(current);
        Assert.Equal(current.Tip, (await GitAsync(root, "rev-parse", PushBranch)).Trim());
    }
}
