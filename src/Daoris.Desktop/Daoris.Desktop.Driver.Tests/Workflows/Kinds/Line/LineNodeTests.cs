using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.ObservedFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary><c>line</c> is the kept end fact, git's proof that the line holds the work, observed and never skipped.</summary>
public sealed class LineNodeTests
{
    /// <summary>The kept end fact is <c>line</c>, reached by git with an acceptance bound to the commit its evidence names and pointing at its record, never a path.</summary>
    [Fact]
    public void Line_is_reached_from_the_kept_end_fact_alone()
    {
        var unit = Unit(Grown(Held()), Landed(pushed: true, held: Held()));

        var moved = Applied(unit, NodeKinds.Line);

        Assert.Equal((NodeKinds.Line, (string?)null, NodeStates.Reached, NodeSource.Git), (NodeKinds.Line, moved.Refused, moved.Status.State, moved.Status.By));
        var accepted = moved.Status.Current!;
        Assert.Equal(("unit-a", "t1", Graph, NodeKinds.Line, 1, 1), (accepted.Unit, accepted.SubjectRevision, accepted.GraphDigest, accepted.Node, accepted.NodeVersion, accepted.Attempt));
        Assert.Equal(("landings.json#feature/x", "t1", accepted.Id), (accepted.Evidence?.Ref, accepted.Evidence?.Revision, accepted.Evidence?.Acceptance));
        Assert.DoesNotContain("engine", accepted.Evidence!.Ref, StringComparison.Ordinal);
    }

    /// <summary>Not yet is pending, never failed: a branch nothing held is observed with no outcome, and an unknown owner is unknown.</summary>
    [Fact]
    public void Not_yet_is_no_outcome_and_an_unknown_owner_is_unknown()
    {
        var observed = NodeObserving.Observe(Unit(Grown()), Node(NodeKinds.Line), Graph, 1);
        Assert.Equal(((string?)null, (NodeMove?)null), (observed.Outcome, observed.Move));
        Assert.False(string.IsNullOrWhiteSpace(observed.Said));

        var unowned = Assert.Single(WorkInventory.Units(new InventoryFacts(
            [], [], [new SessionTree(Path.GetFullPath(Path.Combine("node-trees", "stray")), "default", "engine", "feature/stray")], [],
            new HashSet<string>(), _ => false, Then)));
        Assert.Equal(NodeStates.Unknown, Applied(unowned, NodeKinds.Line).Status.State);
    }

    /// <summary>A line reads only a branch unit.</summary>
    [Fact]
    public void A_line_on_a_quest_unit_is_refused()
    {
        var quest = Assert.Single(QuestUnits.Read([new QuestView("q1", "game", "engine", "The work", "Body.", "Done")], []));

        Assert.Equal(NodeRefusal.NotThisUnit, NodeObserving.Observe(quest, Node(NodeKinds.Line), Graph, 1).Refused);
    }
}
