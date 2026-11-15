using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run whose <c>land</c> reached on a branch no record names any more (its landing trace dropped past the traces kept, its session branch
/// forgotten) has no branch unit its delivery steps could read, push or watch: a <c>push</c> active with an attempt that never reconciled stood
/// for good. Daoris passes such a step over, saying why, and the person's step after it is then theirs.
/// </summary>
public sealed class UnreadLandingDeliveryTests
{
    private const string Unit = WireEvaluationTests.Unit;

    private static readonly NodeDeclaration[] Nodes =
    [
        WireEvaluationTests.Node("landing", NodeKinds.Land),
        WireEvaluationTests.Node("push", NodeKinds.Push),
        new("pull-request", "acme-host/pull-request", 1, NodeReach.Performed),
    ];

    private static readonly Wire[] Wires =
    [
        WireEvaluationTests.After("c", "landing", "push"),
        WireEvaluationTests.After("d", "push", "pull-request"),
    ];

    /// <summary>A push started while its landing was read, whose landing's branch no record names by the later look.</summary>
    private static WireEvaluationTests.Graph Stuck(bool unread)
    {
        var g = new WireEvaluationTests.Graph(Nodes, Wires)
        {
            Run = WireEvaluationTests.Base with { Revisions = new Dictionary<string, string?> { [Unit] = "t1" } },
        };
        g.Through("landing");
        g.Start(g.Evaluate(), "push");
        g.Run = g.Run with { LandingUnread = unread };
        return g;
    }

    [Fact]
    public void A_push_active_past_a_landing_whose_branch_is_no_longer_read_is_passed_over_and_its_attempt_cleared()
    {
        var g = Stuck(unread: true);
        Assert.Equal((NodeStates.Active, 1), (g.State("push"), g.Statuses["push"].Unreconciled));

        g.Evaluate();
        g.Evaluate();

        var push = g.Statuses["push"];
        Assert.Equal((NodeStates.Skipped, NodeSource.Path, (int?)null), (push.State, push.By, push.Unreconciled));
        Assert.Contains("no longer on record", push.Words);
        Assert.Equal(NodeStates.Skipped, g.State("pull-request"));
    }

    [Fact]
    public void A_push_past_a_landing_whose_branch_is_read_is_left_alone()
    {
        var g = Stuck(unread: false);

        g.Evaluate();

        Assert.Contains(g.State("push"), new[] { NodeStates.Active, NodeStates.Unknown });
    }
}
