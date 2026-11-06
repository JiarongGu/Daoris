using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.ObservedFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>FABRIC-NODE1 (the fabric events design §E4): what every observing kind shares, the dispatch to the kind's reading and its refusals.</summary>
public sealed class NodeObservingTests
{
    /// <summary>An observer reads only its own unit's kind, and only the observed kinds: a pressed, performed or waited node is its row's.</summary>
    [Fact]
    public void Only_an_observed_built_in_on_its_own_kind_of_unit_is_observed()
    {
        var quest = Assert.Single(QuestUnits.Read([new QuestView("q1", "game", "engine", "The work", "Body.", "Done")], []));
        var branch = Unit(Grown(Held()));

        Assert.Equal(NodeRefusal.NotThisUnit, NodeObserving.Observe(quest, Node(NodeKinds.Line), Graph, 1).Refused);
        Assert.Equal(NodeRefusal.NotThisUnit, NodeObserving.Observe(branch, Node(NodeKinds.Claimed), Graph, 1).Refused);
        // A person's task observes itself pending: only their press reaches it.
        var task = NodeObserving.Observe(branch, new NodeDeclaration("n", NodeKinds.Human, 1, NodeReach.Pressed), Graph, 1);
        Assert.Equal(((string?)null, (string?)null), (task.Refused, task.Outcome));
        foreach (var kind in new[] { NodeKinds.QuestClose, NodeKinds.WaitUnit, NodeKinds.All, "acme.deploy/deploy" })
        {
            var refused = NodeObserving.Observe(branch, new NodeDeclaration("n", kind, 1, NodeReach.Performed), Graph, 1);
            Assert.Equal((kind, NodeRefusal.NotObserved, (NodeMove?)null), (kind, refused.Refused, refused.Move));
        }
    }
}
