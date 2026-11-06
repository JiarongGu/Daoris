using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.ObservedFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>FABRIC-NODE1 (§C2, §C7; FABRIC-CUTOVER1a): <c>land</c> is the work handed on, read from the landing record or the merge the session branch keeps.</summary>
public sealed class LandNodeObservationTests
{
    /// <summary>The landing is <c>handed</c>, reached by its own authority with an acceptance bound to the commit its evidence names and pointing at its record.</summary>
    [Fact]
    public void Land_is_reached_from_the_landing_record_alone()
    {
        var unit = Unit(Grown(Held()), Landed(pushed: true, held: Held()));

        var moved = Applied(unit, NodeKinds.Land);

        Assert.Equal((NodeKinds.Land, (string?)null, NodeStates.Reached, NodeSource.Landing), (NodeKinds.Land, moved.Refused, moved.Status.State, moved.Status.By));
        var accepted = moved.Status.Current!;
        Assert.Equal(("unit-a", "t1", Graph, NodeKinds.Land, 1, 1), (accepted.Unit, accepted.SubjectRevision, accepted.GraphDigest, accepted.Node, accepted.NodeVersion, accepted.Attempt));
        Assert.Equal(("landings.json#feature/x", "t1", accepted.Id), (accepted.Evidence?.Ref, accepted.Evidence?.Revision, accepted.Evidence?.Acceptance));
        Assert.DoesNotContain("engine", accepted.Evidence!.Ref, StringComparison.Ordinal);
    }

    /// <summary>Not yet is pending, never failed: a branch nothing handed on is observed with no outcome, and one merged into the line is reached by its record.</summary>
    [Fact]
    public void Not_yet_is_no_outcome_and_a_merge_is_reached_by_the_session_branch_record()
    {
        var observed = NodeObserving.Observe(Unit(Grown()), Node(NodeKinds.Land), Graph, 1);
        Assert.Equal(((string?)null, (NodeMove?)null), (observed.Outcome, observed.Move));
        Assert.False(string.IsNullOrWhiteSpace(observed.Said));

        var merged = Applied(Unit(Grown(Held())), NodeKinds.Land);
        Assert.Equal((NodeStates.Reached, "session-branches.json#daoris/s1"), (merged.Status.State, merged.Status.Current?.Evidence?.Ref));
    }

    /// <summary>
    /// FABRIC-CUTOVER1a (the cutover design §2.4.4, §11.2): on a run's subject <c>handed</c> is its session's work handed on, reached where the
    /// session branch's landing holds the tip it is bound to, by its record: made at it, or squashing it. Its acceptance keeps that tip, the one
    /// its binding lapses on, so the one table takes the reach; its evidence names the landing's tip, which what follows binds. A landing
    /// squashing another tip is not this work's; a branch unit of Current's landing cycle reads as today, on the landing's tip.
    /// </summary>
    [Fact]
    public void Handed_on_a_runs_subject_is_reached_by_its_landing_squashing_the_bound_tip()
    {
        var squash = Landed() with { Tip = "s1", Squashed = "t1" };
        var unit = Unit(Grown(), squash);
        var bound = new NodeAcceptance("run:r", "t1", Graph, NodeKinds.Land, 1, 1) { Subject = unit.Id };

        var seen = NodeObserving.Observe(unit, Node(NodeKinds.Land), bound);
        Assert.Equal((NodeStates.Reached, bound.Id, "s1"), (seen.Outcome, seen.Acceptance?.Id, seen.Acceptance?.Evidence?.Revision));
        var moved = NodeLifecycle.Apply(Node(NodeKinds.Land),
            new NodeStatus("run:r", NodeKinds.Land, NodeStates.Active) { Attempt = 1, Tries = 1, Binding = bound }, seen.Move!);
        Assert.Equal(((string?)null, NodeStates.Reached), (moved.Refused, moved.Status.State));

        foreach (var other in new[] { squash with { Squashed = "t0" }, squash with { Squashed = null } })
        {
            Assert.Null(NodeObserving.Observe(Unit(Grown(), other), Node(NodeKinds.Land), bound).Move);
        }

        Assert.Equal(("s1", "s1"), (NodeObserving.Observe(unit, Node(NodeKinds.Land), Graph, 1).Acceptance?.SubjectRevision,
            NodeObserving.Observe(unit, Node(NodeKinds.Land), Graph, 1).Acceptance?.Evidence?.Revision));
    }

    /// <summary>
    /// FABRIC-NODE1c, defect 4: an observation takes the binding Daoris evaluated, its inputs and receipts with it. A QA wired before
    /// <c>handed</c> is in <c>handed</c>'s acceptance, so invalidating the QA reaches it. A binding of another unit, node or version is no
    /// observation, and evidence at another tip than the binding's is not this binding's reach.
    /// </summary>
    [Fact]
    public void An_observation_takes_its_evaluated_binding_so_an_upstream_invalidation_reaches_it()
    {
        var unit = Unit(Grown(Held()), Landed(held: Held()));
        var qa = new NodeAcceptance(unit.Id, "t1", Graph, "qa", 1, 1);
        var binding = Binding(unit, NodeKinds.Land) with { PrerequisiteReceipts = [qa.Id] };

        var observed = NodeObserving.Observe(unit, Node(NodeKinds.Land), binding);
        Assert.Equal(new[] { qa.Id }, observed.Acceptance?.PrerequisiteReceipts);
        var moved = NodeLifecycle.Apply(Node(NodeKinds.Land), Observing(unit, NodeKinds.Land) with { Binding = binding }, observed.Move!);
        Assert.Equal(((string?)null, NodeStates.Reached, binding.Id), (moved.Refused, moved.Status.State, moved.Status.Current?.Id));
        Assert.Contains(NodeAcceptances.Invalidate([qa, moved.Status.Current!], each => each.Id == qa.Id ? NodeLapse.Revision : null),
            each => each.Acceptance == binding.Id && each.Upstream == qa.Id);

        var newer = binding with { SubjectRevision = "t2" };
        var stale = NodeLifecycle.Apply(Node(NodeKinds.Land), Observing(unit, NodeKinds.Land) with { Binding = newer },
            NodeObserving.Observe(unit, Node(NodeKinds.Land), newer).Move!);
        Assert.Equal(NodeRefusal.NotBound, stale.Refused);

        Assert.Equal(NodeRefusal.NotBound, NodeObserving.Observe(unit, Node(NodeKinds.Land), binding with { Unit = "unit-b" }).Refused);
        Assert.Equal(NodeRefusal.NotBound, NodeObserving.Observe(unit, Node(NodeKinds.Land), binding with { Node = NodeKinds.Line }).Refused);
        Assert.Equal(NodeRefusal.NotBound, NodeObserving.Observe(unit, Node(NodeKinds.Land), binding with { NodeVersion = 2 }).Refused);
    }
}
