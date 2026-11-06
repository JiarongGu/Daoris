using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// <c>push</c> (G8; D241 point 2) is Daoris's own push read against git's remote-tracking ref: unread, it is unknown; read at the pushed tip it
/// is reached by git; elsewhere it is not yet. Nothing pushed is no outcome, and the push act's refusal is what fails it.
/// </summary>
public sealed class PushNodeTests
{
    private static readonly DateTimeOffset Then = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);
    private const string Graph = "graph-1";

    private static LandedBranch Landed(bool pushed = false) =>
        new("engine", "default", "feature/x", "main", "t1", "s1", "q1", null, Then) { Units = ["unit-a"], Pushed = pushed, PushedTip = pushed ? "t1" : null };

    private static WorkUnit Unit(LandedBranch? landing = null) =>
        Assert.Single(WorkInventory.Units(new InventoryFacts(
            [new GrownBranch("engine", "default", "daoris/s1", "main", "f0", null, Then) { Unit = "unit-a" }], landing is null ? [] : [landing], [], [],
            new HashSet<string>(), _ => false, Then.AddDays(1))));

    private static readonly NodeDeclaration Push = new(NodeKinds.Push, NodeKinds.Push, 1, NodeReach.Performed);

    private static NodeMoved Applied(WorkUnit unit, NodeGitReads? git = null)
    {
        var observation = NodeObserving.Observe(unit, Push, Graph, 1, git);
        Assert.Null(observation.Refused);
        Assert.NotNull(observation.Move);
        var started = new NodeStatus(unit.Id, Push.Id, NodeStates.Active) { Attempt = 1, Tries = 1, Binding = new NodeAcceptance(unit.Id, "t1", Graph, Push.Id, 1, 1) };
        return NodeLifecycle.Apply(Push, started, observation.Move);
    }

    [Fact]
    public void It_is_reached_by_gits_ref_at_the_commit_the_push_act_left()
    {
        var unit = Unit(Landed(pushed: true));

        Assert.Equal(NodeStates.Unknown, Applied(unit).Status.State);
        var reached = Applied(unit, new NodeGitReads(RemoteTip: _ => "t1"));
        Assert.Equal((NodeStates.Reached, NodeSource.Git), (reached.Status.State, reached.Status.By));
        Assert.Equal("landings.json#feature/x", reached.Status.Current!.Evidence!.Ref);
        Assert.Null(NodeObserving.Observe(unit, Push, Graph, 1, new NodeGitReads(RemoteTip: _ => "t0")).Outcome);
        Assert.Null(NodeObserving.Observe(Unit(Landed()), Push, Graph, 1).Outcome);
    }

    [Fact]
    public void Not_yet_is_no_outcome_and_only_the_push_acts_refusal_fails_it()
    {
        var observed = NodeObserving.Observe(Unit(), Push, Graph, 1);
        Assert.Equal(((string?)null, (NodeMove?)null), (observed.Outcome, observed.Move));
        Assert.False(string.IsNullOrWhiteSpace(observed.Said));
        Assert.Equal(NodeSource.Git, NodeKit.Row(NodeKinds.Push)!.FailureBy);
        Assert.Equal("push-refused", PushDoor.Refused);
        Assert.IsAssignableFrom<IStarts>(NodeKit.Of(NodeKinds.Push));
    }
}
