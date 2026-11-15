using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run whose work is done in place, on the checkout's line with no session branch, has nothing for its branch steps to read or land:
/// once its work step has reached and nothing waits, the run ends, so its set-up quest is not held in <c>awaiting-step</c> for good.
/// </summary>
public sealed class InPlaceRunEndTests
{
    private const string Unit = "run:r1";

    private static readonly NodeGraph Graph = new(
        [
            new NodeDeclaration("work", NodeKinds.Claimed, 1, NodeReach.Waited),
            new NodeDeclaration("content", NodeKinds.ContentHolds, 1, NodeReach.Observed),
            new NodeDeclaration("landing", NodeKinds.Land, 1, NodeReach.Performed),
        ],
        [
            new Wire("a", WireKinds.After, new WireEnd("work"), new WireEnd("content")),
            new Wire("b", WireKinds.After, new WireEnd("content"), new WireEnd("landing")),
        ], ["work"], "landing");

    private static NodeRecord Record(string work, string content = NodeStates.Ready) => new(Unit)
    {
        Statuses = new Dictionary<string, NodeStatus>
        {
            ["work"] = new(Unit, "work", work) { Attempt = 1, Tries = 1, Binding = new NodeAcceptance(Unit, "q", "gph", "work", 1, 1) },
            ["content"] = new(Unit, "content", content),
            ["landing"] = new(Unit, "landing", NodeStates.Blocked),
        },
    };

    [Fact]
    public void Reached_work_with_no_branch_ends_the_run_though_its_branch_steps_stand()
    {
        Assert.True(InPlaceRunEnds.Ends(Graph, Record(NodeStates.Reached), hasBranch: false, attributed: true, waits: false));
    }

    [Fact]
    public void A_branch_work_still_to_come_an_unread_attribution_or_a_wait_keeps_the_run()
    {
        Assert.False(InPlaceRunEnds.Ends(Graph, Record(NodeStates.Reached), hasBranch: true, attributed: true, waits: false));
        Assert.False(InPlaceRunEnds.Ends(Graph, Record(NodeStates.Active), hasBranch: false, attributed: true, waits: false));
        Assert.False(InPlaceRunEnds.Ends(Graph, Record(NodeStates.Reached), hasBranch: false, attributed: false, waits: false));
        Assert.False(InPlaceRunEnds.Ends(Graph, Record(NodeStates.Reached), hasBranch: false, attributed: true, waits: true));
    }

    [Fact]
    public void A_run_with_nothing_standing_has_nothing_to_end_here()
    {
        Assert.False(InPlaceRunEnds.Ends(Graph, Record(NodeStates.Reached, NodeStates.Reached), hasBranch: false, attributed: true, waits: false));
    }
}
