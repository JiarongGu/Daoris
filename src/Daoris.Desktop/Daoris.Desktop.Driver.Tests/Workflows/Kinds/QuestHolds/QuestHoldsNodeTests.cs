using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.HoldFixture;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The <c>quest.holds</c> node is reached once nothing of its quest's own holds the work, and kept active with its reason while something does.</summary>
public sealed class QuestHoldsNodeTests
{
    [Fact]
    public void The_quests_holds_are_reached_once_its_done_stands_unheld()
    {
        var node = Hold(NodeKinds.QuestHolds);
        var seen = Observe(QuestUnit(Quest("Done")), node, "q-rev");

        Assert.Equal((NodeStates.Reached, NodeSource.Holds), (seen.Outcome, seen.Source));
        Assert.Equal(("daoris/quest.holds@1", "q-rev"), (seen.Acceptance!.Evidence!.Schema, seen.Acceptance.SubjectRevision));
    }

    [Theory]
    [InlineData(null, "yes", "held for your yes")]
    [InlineData(EvidenceCodes.MissingHold, "evidence", "held for its evidence's verdict")]
    [InlineData(EvidenceCodes.Departed, "evidence", "held for its evidence's verdict")]
    public void A_held_done_keeps_it_active_with_why_unless_its_option_lets_that_hold_go(string? code, string option, string why)
    {
        var held = QuestUnit(Quest("Done", held: true, hold: code));

        var waits = Observe(held, Hold(NodeKinds.QuestHolds), "q-rev");
        Assert.Null(waits.Outcome);
        Assert.Null(waits.Move);
        Assert.Contains(why, waits.Said);

        Assert.Equal(NodeStates.Reached, Observe(held, Hold(NodeKinds.QuestHolds, $$"""{"{{option}}":false}"""), "q-rev").Outcome);
    }

    [Fact]
    public void A_done_held_for_its_review_alone_is_the_looks_and_a_quest_taken_back_waits()
    {
        Assert.Equal(NodeStates.Reached, Observe(QuestUnit(Quest("Done", held: true, hold: EvidenceCodes.Unreviewed)), Hold(NodeKinds.QuestHolds), "q-rev").Outcome);

        var back = Observe(QuestUnit(Quest("Taken")), Hold(NodeKinds.QuestHolds), "q-rev");
        Assert.Null(back.Outcome);
        Assert.Contains("taken back", back.Said);
    }

    [Fact]
    public void A_done_of_an_asks_quest_with_no_requirement_waits_unchecked_with_its_handback_to_read()
    {
        var asked = Quest("Done") with { From = "ask #a1b2c3" };

        var waits = Observe(QuestUnit(asked), Hold(NodeKinds.QuestHolds), "q-rev");
        Assert.Null(waits.Outcome);
        Assert.Contains("unchecked", waits.Said);
        Assert.Contains("handback", waits.Said);
        Assert.Contains("unchecked", waits.Holds);

        var required = asked with { Requirements = [new QuestRequirementView("use the media config", "the name is read from it")] };
        Assert.Equal(NodeStates.Reached, Observe(QuestUnit(required), Hold(NodeKinds.QuestHolds), "q-rev").Outcome);
        Assert.Equal(NodeStates.Reached, Observe(QuestUnit(asked), Hold(NodeKinds.QuestHolds, """{"yes":false}"""), "q-rev").Outcome);
    }

    private static readonly DateTimeOffset Then = new(2026, 11, 13, 9, 0, 0, TimeSpan.Zero);

    /// <summary>The run's branch as its record keeps it, held by the line at <paramref name="held"/> where that is given.</summary>
    private static WorkUnit BranchHeldAt(string? held, string by = EndBy.Merged) => Branch with
    {
        Records = new WorkUnitRecords(
            new GrownBranch("engine", "default", "daoris/s1", "main", "f0", null, Then)
            {
                Held = held is null ? null : new HeldFact(Then, Then, by, held, "main", "origin/main", "m1", "engine"),
            }, [], null),
    };

    private static NodeObservation ObserveWithBranch(WorkUnit branch, string tip) =>
        NodeObserving.Observe(QuestUnit(Quest("Done") with { From = "ask #a1b2c3" }), Hold(NodeKinds.QuestHolds), Bound(QuestUnit(Quest("Done")), Hold(NodeKinds.QuestHolds), "q-rev"),
            new NodeGitReads { Content = (_, _) => null }, branch, tip);

    [Fact]
    public void A_done_waiting_unchecked_is_passed_where_the_line_already_holds_the_runs_branch_tip()
    {
        var seen = ObserveWithBranch(BranchHeldAt("tip-1"), "tip-1");

        Assert.Equal((NodeStates.Reached, NodeSource.Holds), (seen.Outcome, seen.Source));
        Assert.Contains("merged outside the run", seen.Said);
        Assert.Contains("every commit of it is on `origin/main`", seen.Said);
        Assert.Null(seen.Holds);
    }

    [Theory]
    [InlineData(null, "tip-1")]
    [InlineData("tip-0", "tip-1")]
    [InlineData("tip-1", "tip-2")]
    public void A_branch_the_line_does_not_hold_at_its_tip_leaves_the_done_waiting(string? heldAt, string tip)
    {
        var waits = ObserveWithBranch(BranchHeldAt(heldAt), tip);

        Assert.Null(waits.Outcome);
        Assert.Contains("unchecked", waits.Holds);
    }

    [Fact]
    public void A_branch_that_never_moved_from_the_line_is_nothing_landed_and_leaves_the_done_waiting()
    {
        var waits = ObserveWithBranch(BranchHeldAt("tip-1", EndBy.Empty), "tip-1");

        Assert.Null(waits.Outcome);
        Assert.Contains("unchecked", waits.Holds);
    }

    /// <summary>A version that no longer holds a done for your yes, by its `quest.holds` taken away or its `yes` off, asks less of you, and says so.</summary>
    [Fact]
    public void Taking_the_quests_holds_away_or_their_yes_off_lowers_your_part()
    {
        static NodeGraph Graph(params NodeDeclaration[] holds) => new(
            [new("work", NodeKinds.Claimed, 1, NodeReach.Observed), .. holds, new("landing", NodeKinds.Land, 1, NodeReach.Performed)], [], ["work"], "landing");
        var held = Involvement.StepsOf(Graph(Hold(NodeKinds.QuestHolds)));

        Assert.Contains("it lands a done held for your yes", Involvement.Lowers(Involvement.StepsOf(Graph()), held));
        Assert.Contains("it lands a done held for your yes", Involvement.Lowers(Involvement.StepsOf(Graph(Hold(NodeKinds.QuestHolds, """{"yes":false}"""))), held));
        Assert.Empty(Involvement.Lowers(held, held));
        Assert.Empty(Involvement.Lowers(held, Involvement.StepsOf(Graph())));
    }

    /// <summary>The save: a hold twice, in a graph with no landing, or after the landing, is refused against the hold, by its kind's own rules.</summary>
    [Fact]
    public void The_save_refuses_a_hold_twice_unlanded_or_after_the_landing()
    {
        NodeDeclaration Node(string id, string kind) => new(id, kind, 1, kind == NodeKinds.Land ? NodeReach.Performed : NodeReach.Observed);
        var twice = new NodeGraph([Node("work", NodeKinds.Claimed), Node("h1", NodeKinds.QuestHolds), Node("h2", NodeKinds.QuestHolds), Node("landing", NodeKinds.Land)],
            [Wire("a", "work", "h1"), Wire("b", "h1", "h2"), Wire("c", "h2", "landing")], ["work"], "landing");
        Assert.Contains(NodeGraphs.Problems(twice), problem => problem is { Code: GraphRefusal.HoldTwice, Node: "h2" });

        var unlanded = new NodeGraph([Node("work", NodeKinds.Claimed), Node("h1", NodeKinds.QuestHolds)], [Wire("a", "work", "h1")], ["work"], "h1");
        Assert.Contains(NodeGraphs.Problems(unlanded), problem => problem is { Code: GraphRefusal.HoldUnlanded, Node: "h1" });

        var late = new NodeGraph([Node("work", NodeKinds.Claimed), Node("landing", NodeKinds.Land), Node("h1", NodeKinds.QuestHolds)],
            [Wire("a", "work", "landing"), Wire("b", "landing", "h1")], ["work"], "h1");
        Assert.Contains(NodeGraphs.Problems(late), problem => problem is { Code: GraphRefusal.HoldLate, Node: "h1" });
    }

    private static Wire Wire(string id, string from, string to) => new(id, WireKinds.After, new WireEnd(from), new WireEnd(to));
}
