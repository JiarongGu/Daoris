using System.Collections.Immutable;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A <c>land</c> attempt a lapse left unreconciled is settled where no delivery bound at its commit stands unresolved, whether its status still
/// holds its binding or its judgement again on a fresh binding took it away: the commit is then the one its history kept, so a delivery that
/// held it back once never holds the node back for good.
/// </summary>
public sealed class LandAttemptsTests
{
    private const string Unit = "run:r1";

    private static readonly NodeGraph Graph = new(
        [new NodeDeclaration("claimed", NodeKinds.Claimed, 1, NodeReach.Observed), new NodeDeclaration("landing", NodeKinds.Land, 1, NodeReach.Performed)],
        [], ["claimed"], "landing");

    private static readonly NodeAcceptance Bound = new(Unit, "tip-1", "graph-1", "landing", 1, 1);

    /// <summary>The land's first attempt invalidated by its subject's move, still unreconciled, its binding on the status.</summary>
    private static readonly NodeStatus Invalidated = new(Unit, "landing", NodeStates.Invalidated) { Attempt = 1, Tries = 1, Binding = Bound, Unreconciled = 1 };

    private static NodeRecord Holding(NodeStatus landing, params NodeHistory[] history) => new(Unit)
    {
        Statuses = ImmutableSortedDictionary<string, NodeStatus>.Empty.Add("landing", landing),
        History = history,
    };

    /// <summary>A delivery at <paramref name="tip"/> stands unresolved, the tip asked kept in <paramref name="asked"/>.</summary>
    private static bool Unresolved(List<string> asked, string tip)
    {
        asked.Add(tip);
        return true;
    }

    [Fact]
    public void An_invalidated_attempt_is_settled_at_its_bound_commit_and_held_while_a_delivery_there_is_unresolved()
    {
        var record = Holding(Invalidated);
        var asked = new List<string>();

        Assert.True(LandAttempts.Holds(Graph, record));
        Assert.Null(LandAttempts.Settled(Graph, record, tip => Unresolved(asked, tip)));
        Assert.Null(LandAttempts.Settled(Graph, record, _ => false)!.Statuses["landing"].Unreconciled);
        Assert.Equal(["tip-1"], asked);
    }

    [Fact]
    public void An_attempt_judged_again_on_a_fresh_binding_is_settled_at_the_commit_its_history_kept()
    {
        var ready = Invalidated with { State = NodeStates.Ready, Tries = 0, Binding = null };
        var record = Holding(ready, new NodeHistory(DateTimeOffset.UtcNow, WireWhy.Eligible, Invalidated));
        var asked = new List<string>();

        Assert.True(LandAttempts.Holds(Graph, record));
        Assert.Null(LandAttempts.Settled(Graph, record, tip => Unresolved(asked, tip)));
        var settled = LandAttempts.Settled(Graph, record, _ => false)!.Statuses["landing"];

        Assert.Equal(["tip-1"], asked);
        Assert.Equal((NodeStates.Ready, (int?)null), (settled.State, settled.Unreconciled));
    }

    [Fact]
    public void An_attempt_whose_commit_no_status_or_history_keeps_is_not_held()
    {
        var record = Holding(Invalidated with { State = NodeStates.Ready, Binding = null });

        Assert.False(LandAttempts.Holds(Graph, record));
        Assert.Null(LandAttempts.Settled(Graph, record, _ => false));
    }
}
