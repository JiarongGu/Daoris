using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-NODE1 (the fabric events design §E2, generalizing §D4 and §B5's lapse): an acceptance binds the unit, its revision, the graph,
/// the node's version, its inputs, its prerequisites and the attempt; a changed dependency invalidates every acceptance downstream,
/// transitively, across units, and keeps each as history.
/// </summary>
public sealed class NodeAcceptancesTests
{
    private static NodePort Output(NodeAcceptance from, string name) => new($"acme/{name}@1", $"{from.Node}.{name}", from.SubjectRevision, from.Id);

    /// <summary>The id is the binding: the same binding is the same acceptance, any field of it another; the evidence is not the binding.</summary>
    [Fact]
    public void An_acceptance_is_named_by_its_binding()
    {
        var accepted = new NodeAcceptance("unit-a", "tip-1", "graph-1", "line", 1, 1);

        Assert.Equal(accepted.Id, (accepted with { Evidence = new NodePort("daoris/line@1", "session-branches.json#daoris/s1", "tip-1", null) }).Id);
        Assert.Equal(accepted.Id, new NodeAcceptance("unit-a", "tip-1", "graph-1", "line", 1, 1).Id);
        Assert.NotEqual(accepted.Id, (accepted with { SubjectRevision = "tip-2" }).Id);
        Assert.NotEqual(accepted.Id, (accepted with { GraphDigest = "graph-2" }).Id);
        Assert.NotEqual(accepted.Id, (accepted with { NodeVersion = 2 }).Id);
        Assert.NotEqual(accepted.Id, (accepted with { Attempt = 2 }).Id);
        Assert.NotEqual(accepted.Id, (accepted with { Unit = "unit-b" }).Id);
        Assert.NotEqual(accepted.Id, (accepted with { PrerequisiteReceipts = ["acc-x"] }).Id);
        Assert.NotEqual(accepted.Id, (accepted with { Inputs = new Dictionary<string, NodePort> { ["release"] = new("acme/artifact@1", "build.artifact", "tip-1", "acc-y") } }).Id);
        Assert.StartsWith("acc-", accepted.Id, StringComparison.Ordinal);
    }

    /// <summary>What no longer binds says why: the unit's revision moved, the graph changed, the node's version changed.</summary>
    [Fact]
    public void An_acceptance_lapses_when_its_binding_changes()
    {
        var accepted = new NodeAcceptance("unit-a", "tip-1", "graph-1", "line", 1, 1);

        Assert.Null(NodeAcceptances.Lapsed(accepted, "tip-1", "graph-1", 1));
        Assert.Equal(NodeLapse.Revision, NodeAcceptances.Lapsed(accepted, "tip-2", "graph-1", 1));
        Assert.Equal(NodeLapse.Revision, NodeAcceptances.Lapsed(accepted, null, "graph-1", 1));
        Assert.Equal(NodeLapse.Graph, NodeAcceptances.Lapsed(accepted, "tip-1", "graph-2", 1));
        Assert.Equal(NodeLapse.Version, NodeAcceptances.Lapsed(accepted, "tip-1", "graph-1", 2));
    }

    /// <summary>
    /// A new tip on one unit invalidates that unit's acceptances, and through their prerequisites and inputs every acceptance downstream,
    /// on another unit too, however far; an acceptance that depends on none of them stands. Each says why, and none is removed.
    /// </summary>
    [Fact]
    public void A_changed_revision_invalidates_every_acceptance_downstream_transitively()
    {
        var line = new NodeAcceptance("unit-a", "tip-1", "graph-1", "line", 1, 1);
        var qa = new NodeAcceptance("unit-a", "tip-1", "graph-1", "qa", 1, 1) { PrerequisiteReceipts = [line.Id] };
        var deploy = new NodeAcceptance("unit-b", "rev-7", "graph-2", "deploy", 1, 2)
        {
            Inputs = new Dictionary<string, NodePort> { ["approval"] = Output(qa, "evidence") },
        };
        var close = new NodeAcceptance("unit-b", "rev-7", "graph-2", "close", 1, 1) { PrerequisiteReceipts = [deploy.Id] };
        var other = new NodeAcceptance("unit-b", "rev-7", "graph-2", "docs", 1, 1);
        IReadOnlyList<NodeAcceptance> all = [close, other, deploy, qa, line];

        var invalidated = NodeAcceptances.Invalidate(all, NodeAcceptances.Moved("unit-a", "tip-2"));

        Assert.Equal(
            [
                new NodeInvalidation(qa.Id, NodeLapse.Revision, null),
                new NodeInvalidation(line.Id, NodeLapse.Revision, null),
                new NodeInvalidation(deploy.Id, NodeLapse.Upstream, qa.Id),
                new NodeInvalidation(close.Id, NodeLapse.Upstream, deploy.Id),
            ],
            invalidated);
        Assert.Equal(5, all.Count);
    }

    /// <summary>An upstream acceptance invalidated alone (a lost witness, a superseded answer) takes its consumers with it, and only them.</summary>
    [Fact]
    public void An_invalidated_upstream_invalidates_only_its_consumers()
    {
        var build = new NodeAcceptance("unit-a", "tip-1", "graph-1", "build", 1, 1);
        var test = new NodeAcceptance("unit-a", "tip-1", "graph-1", "test", 1, 1) { PrerequisiteReceipts = [build.Id] };
        var deploy = new NodeAcceptance("unit-a", "tip-1", "graph-1", "deploy", 1, 1)
        {
            Inputs = new Dictionary<string, NodePort> { ["release"] = Output(build, "artifact") },
        };
        var lint = new NodeAcceptance("unit-a", "tip-1", "graph-1", "lint", 1, 1);

        var invalidated = NodeAcceptances.Invalidate([build, test, deploy, lint], each => each.Id == test.Id ? "witness-lost" : null);

        Assert.Equal([new NodeInvalidation(test.Id, "witness-lost", null)], invalidated);
        Assert.Equal(
            [new NodeInvalidation(build.Id, NodeLapse.Version, null), new NodeInvalidation(test.Id, NodeLapse.Upstream, build.Id),
             new NodeInvalidation(deploy.Id, NodeLapse.Upstream, build.Id)],
            NodeAcceptances.Invalidate([build, test, deploy, lint], each => each.Node == "build" ? NodeLapse.Version : null));
    }

    /// <summary>A reached node whose acceptance is invalidated moves to <c>invalidated</c> by the one table, keeping its acceptance as history.</summary>
    [Fact]
    public void A_reached_node_whose_acceptance_is_invalidated_is_invalidated_by_the_table()
    {
        var node = new NodeDeclaration("qa", NodeKinds.Human, 1, NodeReach.Pressed);
        var line = new NodeAcceptance("unit-a", "tip-1", "graph-1", "line", 1, 1);
        var qa = new NodeAcceptance("unit-a", "tip-1", "graph-1", "qa", 1, 1) { PrerequisiteReceipts = [line.Id] };
        var status = new NodeStatus("unit-a", "qa", NodeStates.Reached) { Attempt = 1, Tries = 1, Acceptance = qa };

        var invalidated = NodeAcceptances.Invalidate([line, qa], each => each.Id == line.Id ? "superseded" : null);
        Assert.Contains(invalidated, each => each.Acceptance == qa.Id);

        var moved = NodeLifecycle.Apply(node, status, new NodeMove(NodeStates.Invalidated, NodeCause.Daoris));
        Assert.Equal((NodeStates.Invalidated, qa.Id), (moved.Status.State, moved.Status.Acceptance?.Id));
        Assert.Null(moved.Status.Current);
    }

    /// <summary>
    /// FABRIC-NODE1c, defect 5: an issued id never changes. The inputs, receipts and outputs an acceptance was made of are copied, so
    /// changing the collections that were supplied changes nothing of it.
    /// </summary>
    [Fact]
    public void An_issued_acceptance_keeps_its_id_when_what_was_supplied_changes()
    {
        var inputs = new Dictionary<string, NodePort> { ["release"] = new("acme/artifact@1", "build.artifact", "tip-1", "acc-y") };
        var receipts = new List<string> { "acc-x" };
        var outputs = new Dictionary<string, NodePort> { ["preview"] = new("acme/preview@1", "deploy.preview", "tip-1", null) };
        var accepted = new NodeAcceptance("unit-a", "tip-1", "graph-1", "deploy", 1, 1) { Inputs = inputs, PrerequisiteReceipts = receipts, Outputs = outputs };
        var issued = accepted.Id;

        inputs["release"] = new("acme/artifact@1", "build.other", "tip-2", "acc-z");
        inputs["extra"] = new("acme/artifact@1", "build.extra", "tip-2", "acc-z");
        receipts.Add("acc-w");
        outputs.Clear();

        Assert.Equal(issued, accepted.Id);
        Assert.Equal(new[] { "acc-x" }, accepted.PrerequisiteReceipts);
        Assert.Equal(("build.artifact", 1), (accepted.Inputs["release"].Ref, accepted.Inputs.Count));
        Assert.Single(accepted.Outputs);
        Assert.Equal(issued, (accepted with { Evidence = null }).Id);
    }

    /// <summary>
    /// FABRIC-NODE1c, defect 6: one immutable result per binding. The evidence and outputs are not in the id, so a replay of the same
    /// result is kept once, and a second, different one is refused, never overwritten. Another attempt is another binding.
    /// </summary>
    [Fact]
    public void A_binding_keeps_one_result_and_refuses_a_second_different_one()
    {
        var binding = new NodeAcceptance("unit-a", "tip-1", "graph-1", "deploy", 1, 1);
        NodeAcceptance Result(string receipt) => binding with { Evidence = new NodePort("acme/receipt@1", receipt, "tip-1", binding.Id) };
        var first = Result("deploy#r1");

        var kept = NodeAcceptances.Keep([], first);
        Assert.Equal(((string?)null, 1), (kept.Refused, kept.Kept.Count));

        var replay = NodeAcceptances.Keep(kept.Kept, Result("deploy#r1"));
        Assert.Equal(((string?)null, 1), (replay.Refused, replay.Kept.Count));

        var other = NodeAcceptances.Keep(kept.Kept, Result("deploy#r2"));
        Assert.Equal(NodeRefusal.OtherResult, other.Refused);
        Assert.Same(first, Assert.Single(other.Kept));

        var otherOutputs = NodeAcceptances.Keep(kept.Kept,
            first with { Outputs = new Dictionary<string, NodePort> { ["preview"] = new("acme/preview@1", "deploy.preview", "tip-1", binding.Id) } });
        Assert.Equal(NodeRefusal.OtherResult, otherOutputs.Refused);

        var next = NodeAcceptances.Keep(kept.Kept, Result("deploy#r2") with { Attempt = 2 });
        Assert.Equal(((string?)null, 2), (next.Refused, next.Kept.Count));
    }
}
