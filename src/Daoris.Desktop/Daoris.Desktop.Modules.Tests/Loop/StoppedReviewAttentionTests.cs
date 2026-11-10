using System.Text.Json.Nodes;
using Daoris.Driver;
using static Daoris.Desktop.Modules.Tests.AttentionTestRows;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A session whose run's review the person stopped is no work ready for them: *Ready for you* keeps no row for it, though Sessions'
/// list still places its tree to review. The stopped runs are read from the node records (<see cref="StoppedRuns"/>), by the quests
/// their nodes are bound on.
/// </summary>
public sealed class StoppedReviewAttentionTests
{
    private static JsonObject Placed() => With(
        """{"session":"r3v13w00","group":"review","shown":"completed","archived":false,"teammate":false,"work":{"commits":3,"uncommitted":0}}""", "{}");

    private static AttentionFacts Facts(IReadOnlySet<string> stopped) => new()
    {
        Sessions = [Session("""{"id":"r3v13w00","quest":"7a82cc","state":"completed","updated":"2026-09-21T11:00:00Z"}""")],
        Registry = [Registration("engine")],
        Groups = [Placed()],
        StoppedQuests = stopped,
    };

    private static NodeStatus Bound(string node, string state, string subject, string? by = null) => new("run:7a82cc", node, state)
    {
        Attempt = 1,
        By = by,
        Binding = new NodeAcceptance("run:7a82cc", "c79ec557", "graph-1", node, 1, 1) { Subject = subject },
    };

    [Fact]
    public void A_stopped_review_removes_its_sessions_ready_row()
    {
        Assert.Equal(["review:r3v13w00"], Keys(Rows(Facts(new HashSet<string>(StringComparer.Ordinal)))));
        Assert.Empty(Rows(Facts(new HashSet<string>(StringComparer.Ordinal) { "7a82cc" })));
    }

    [Fact]
    public void The_stopped_runs_are_the_quests_a_run_the_person_stopped_is_bound_on()
    {
        NodeRecord Run(string review, string? by) => new("run:7a82cc")
        {
            Statuses = new Dictionary<string, NodeStatus>(StringComparer.Ordinal)
            {
                ["holds"] = Bound("holds", NodeStates.Reached, "quest:7a82cc"),
                ["review"] = Bound("review", review, "unit-a", by),
            },
        };

        Assert.Equal(["7a82cc"], StoppedRuns.Of([Run(NodeStates.Stopped, NodeCause.Person)]).ToArray());
        Assert.Empty(StoppedRuns.Of([Run(NodeStates.Active, null)]));
        Assert.Empty(StoppedRuns.Of([Run(NodeStates.Stopped, NodeCause.Daoris)]));
    }
}
