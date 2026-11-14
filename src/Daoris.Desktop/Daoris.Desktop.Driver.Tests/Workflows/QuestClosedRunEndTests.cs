using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run whose quests all closed and whose standing step reads only the quest (its holds, on a done nothing reads any more) can never move
/// again: it ends stopped by Daoris at that step, and its sessions end with it, where none is reopened to wait on it.
/// </summary>
public sealed class QuestClosedRunEndTests
{
    private const string Unit = "run:r1";

    private static readonly NodeGraph Graph = new(
        [
            new NodeDeclaration("claimed", NodeKinds.Claimed, 1, NodeReach.Observed),
            new NodeDeclaration("holds", NodeKinds.QuestHolds, 1, NodeReach.Observed),
            new NodeDeclaration("handed", NodeKinds.Land, 1, NodeReach.Performed),
        ],
        [
            new Wire("c>h", WireKinds.After, new WireEnd("claimed"), new WireEnd("holds")),
            new Wire("h>l", WireKinds.After, new WireEnd("holds"), new WireEnd("handed")),
        ], ["claimed"], "handed");

    private static NodeRecord Record(string claimed, string holds) => new(Unit)
    {
        Statuses = new Dictionary<string, NodeStatus>
        {
            ["claimed"] = new(Unit, "claimed", claimed) { Attempt = 1, Tries = 1, Binding = new NodeAcceptance(Unit, "tip", "gph", "claimed", 1, 1) },
            ["holds"] = new(Unit, "holds", holds) { Attempt = 1, Tries = 1, Binding = new NodeAcceptance(Unit, "tip", "gph", "holds", 1, 1) },
        },
    };

    [Fact]
    public void A_holds_step_standing_on_a_closed_quest_ends_the_run_whether_or_not_its_branch_stands()
    {
        var record = Record(NodeStates.Reached, NodeStates.Active);

        Assert.Equal(ClosedRunEnds.QuestClosed, ClosedRunEnds.Why(Graph, record, false, true, new HashSet<string>()));
        Assert.Equal(ClosedRunEnds.QuestClosed, ClosedRunEnds.Why(Graph, record, false, false, new HashSet<string>()));
    }

    [Fact]
    public void The_end_is_a_stop_by_daoris_at_the_standing_step()
    {
        var ended = RunOutcomes.OfClosedQuest(Graph, Record(NodeStates.Reached, NodeStates.Active), "q1", DateTimeOffset.UnixEpoch);

        Assert.Equal((RunOutcomes.Stopped, "holds", NodeSource.Daoris), (ended.Outcome, ended.Node, ended.By));
        Assert.Equal(ClosedRunEnds.QuestClosed, ended.Why);
    }
}
