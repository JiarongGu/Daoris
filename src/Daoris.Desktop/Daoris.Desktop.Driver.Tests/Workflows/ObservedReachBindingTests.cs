using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A door kind keeps no attempt journal, so its node leaves <c>active</c> only by its reading's reach, which the one table accepts only on the
/// node's own binding. A <c>push</c> or a <c>tidy</c> bound at the commit its unit read, where its landing's recorded tip reads another, was read
/// as reached at the landing's tip and refused for good: the reach is made at the binding's commit and its evidence names the landing's.
/// </summary>
public sealed class ObservedReachBindingTests
{
    private const string Unit = "run:r1";
    private static readonly DateTimeOffset Then = new(2026, 10, 17, 9, 0, 0, TimeSpan.Zero);

    private static LandedBranch Landing(ChainTidied? tidied = null) =>
        new("engine", "default", "daoris/s-0001", "dev", "t2", "s-0001", "q1", "Fix", Then)
        {
            Pushed = true, PushedTip = "t2", Units = [Unit], Tidied = tidied,
        };

    private static WorkUnit UnitOf(LandedBranch landing) =>
        Assert.Single(WorkInventory.Units(new InventoryFacts(
            [new GrownBranch("engine", "default", "daoris/s-0001", "dev", "f0", null, Then) { Unit = Unit }], [landing], [], [], new HashSet<string>(), _ => false, Then.AddDays(1))));

    private static NodeAcceptance BoundAt(string node, string revision) => new(Unit, revision, "graph-1", node, 1, 1);

    [Fact]
    public void A_tidy_the_landing_record_holds_is_reached_on_its_own_binding_whatever_tip_the_landing_reads()
    {
        var landing = Landing(new ChainTidied("t2", Then) { Parts = [new("daoris/s-0001", "p1") { KeptOn = "refs/daoris/discarded/daoris/s-0001" }] });
        var tidy = new NodeDeclaration("tidy", NodeKinds.Tidy, 1, NodeReach.Performed);

        var observed = NodeObserving.Observe(UnitOf(landing), tidy, BoundAt("tidy", "t1"));

        Assert.Equal(NodeStates.Reached, observed.Outcome);
        Assert.Equal("t1", observed.Acceptance!.SubjectRevision);
        Assert.Equal("t2", observed.Acceptance.Evidence!.Revision);
    }

    [Fact]
    public void A_push_the_remote_holds_is_reached_on_its_own_binding_whatever_tip_the_landing_reads()
    {
        var push = new NodeDeclaration("push", NodeKinds.Push, 1, NodeReach.Performed);

        var observed = NodeObserving.Observe(UnitOf(Landing()), push, BoundAt("push", "t1"), new NodeGitReads(RemoteTip: _ => "t2"));

        Assert.Equal(NodeStates.Reached, observed.Outcome);
        Assert.Equal("t1", observed.Acceptance!.SubjectRevision);
        Assert.Equal("t2", observed.Acceptance.Evidence!.Revision);
    }
}
