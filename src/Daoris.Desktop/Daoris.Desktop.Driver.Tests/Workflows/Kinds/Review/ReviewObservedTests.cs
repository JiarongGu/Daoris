using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A review reached because its landing was seen made, with nobody's press, says it was observed: its source is the landing record's
/// (git's), never the person's, which only their Accept carries.
/// </summary>
public sealed class ReviewObservedTests : NodeRunFixture
{
    private const string ReviewGraph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"review","kind":"review","version":1,"reach":"pressed"},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>r","kind":"after","from":"claimed","to":"review"},
                  {"id":"r>h","kind":"after","from":"review","to":"handed"}]}
        """;

    private static NodeDeclaration Review() => new("review", NodeKinds.Review, 1, NodeReach.Pressed);

    private async Task<NodeStatus> WaitingAsync()
    {
        BoundTo(ReviewGraph);
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
        await RunPassAsync(Runtime());
        var review = RunStatus("review");
        Assert.Equal(NodeStates.Active, review.State);
        return review;
    }

    [Fact]
    public async Task A_review_seen_landed_with_no_press_is_reached_as_observed_never_as_the_persons()
    {
        var review = await WaitingAsync();
        var seen = new NodeMove(NodeStates.Reached, NodeCause.Authority) { Source = NodeSource.Git, Attempt = review.Attempt, Acceptance = review.Binding };

        var moved = NodeLifecycle.Apply(Review(), review, seen);

        Assert.True(moved.Moved, moved.Refused);
        Assert.Equal(NodeSource.Git, moved.Status.By);
        Assert.Equal(NodeSource.Git, NodeKit.Row(NodeKinds.Review)!.ObservedBy);
    }

    [Fact]
    public async Task Only_the_landing_record_and_the_persons_accept_reach_a_review()
    {
        var review = await WaitingAsync();
        var plugin = new NodeMove(NodeStates.Reached, NodeCause.Authority) { Source = NodeSource.Plugin, Attempt = review.Attempt, Acceptance = review.Binding };

        Assert.Equal(NodeRefusal.NotAuthority, NodeLifecycle.Apply(Review(), review, plugin).Refused);
    }
}
