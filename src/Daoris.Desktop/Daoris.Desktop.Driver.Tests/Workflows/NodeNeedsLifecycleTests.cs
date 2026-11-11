using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A failure's <c>needs</c> on the node's status.</summary>
public sealed class NodeNeedsLifecycleTests
{
    private const string Unit = "run:r1";

    [Fact]
    public void A_failures_needs_is_kept_with_its_code_and_gone_when_the_node_starts_again()
    {
        var node = new NodeDeclaration("n1", "acme.deploy/deploy", 1, NodeReach.Performed) { Perform = new NodePerform(true, false, []) };
        var active = new NodeStatus(Unit, "n1", NodeStates.Active)
        {
            Attempt = 1, Tries = 1, Binding = new NodeAcceptance(Unit, "tip-1", "graph-1", "n1", 1, 1),
        };

        var failed = NodeLifecycle.Apply(node, active, new NodeMove(NodeStates.Failed, NodeCause.Authority)
        {
            Source = NodeSource.Plugin, Attempt = 1, Code = "signed-out", Needs = "login",
        });
        Assert.Null(failed.Refused);
        Assert.Equal(("signed-out", "login"), (failed.Status.Code, failed.Status.Needs));

        var ready = NodeLifecycle.Apply(node, failed.Status, new NodeMove(NodeStates.Ready, NodeCause.Person));
        Assert.Null(ready.Refused);
        Assert.Null(ready.Status.Needs);
    }
}
