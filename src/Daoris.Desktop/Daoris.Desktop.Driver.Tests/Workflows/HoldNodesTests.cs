using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDNODE1k (D145's and D173's notes of 2026-11-01): what a landing's start judged outside the graph is two steps of it, <c>quest.holds</c> and
/// <c>content.holds</c>, observed by Daoris at the bound tip, each with its options, reached once nothing it judges holds and kept active with its
/// reason while something does; an older build reads either as no kind and holds the graph whole.
/// </summary>
public sealed class HoldNodesTests
{
    private const string Run = "run:r";
    private static readonly NodeScope Scope = new("graph-1", "run-1");

    private static NodeDeclaration Hold(string kind, string? options = null) => new(kind == NodeKinds.QuestHolds ? "holds" : "content", kind, 1, NodeReach.Observed)
    {
        Options = options is null ? new Dictionary<string, string>()
            : JsonDocument.Parse(options).RootElement.EnumerateObject().ToDictionary(option => option.Name, option => option.Value.GetRawText()),
    };

    private static QuestView Quest(string status, bool held = false, string? hold = null) =>
        new("q1", "engine", "engine", "Fix the gap", "Body.", status) { Held = held, Hold = hold };

    private static WorkUnit QuestUnit(QuestView quest) =>
        new(QuestUnits.IdOf(quest.Id), WorkUnitKind.Quest, new WorkOwner("engine", "default", WorkRecord.Quests, WorkRecord.ServiceWriter),
            new WorkSubject($"#{quest.Id}", "q-rev"), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), [])
        {
            Records = new WorkUnitRecords(null, [], quest),
        };

    private static readonly WorkUnit Branch = new("unit-b1", WorkUnitKind.Branch, new WorkOwner("engine", "default", SessionBranches.FileName, WorkRecord.DriverWriter),
        new WorkSubject("daoris/s1", "tip-1"), new MilestoneEnd(WorkEndState.NotEnded, null, null, null, null, "open"), []);

    private static NodeAcceptance Bound(WorkUnit unit, NodeDeclaration node, string? revision) =>
        new(Run, revision, Scope.GraphDigest, node.Id, node.Version, 1) { Subject = unit.Id };

    private static NodeObservation Observe(WorkUnit unit, NodeDeclaration node, string? revision, HoldContent? content = null) =>
        BuiltInNodes.Observe(unit, node, Bound(unit, node, revision), new NodeGitReads { Content = (_, _) => content });

    [Fact]
    public void Both_are_built_in_observed_kinds_reached_by_what_Daoris_reads_and_failed_by_nothing()
    {
        foreach (var kind in new[] { NodeKinds.QuestHolds, NodeKinds.ContentHolds })
        {
            var authority = NodeAuthorities.BuiltIn[kind];
            Assert.Equal((NodeReach.Observed, NodeSource.Holds, (string?)null, true, false), (authority.Reach, authority.ReachBy, authority.FailureBy, authority.PersonSkips, authority.MayEnd));
            Assert.Contains(kind, NodeKinds.BuiltIn);
            Assert.Null(NodeDeclarations.Problem(Hold(kind)));
        }
    }

    [Theory]
    [InlineData(NodeKinds.QuestHolds, """{"yes":false,"evidence":true}""", null)]
    [InlineData(NodeKinds.QuestHolds, """{"yes":"no"}""", "yes")]
    [InlineData(NodeKinds.QuestHolds, """{"evidence":1}""", "evidence")]
    [InlineData(NodeKinds.ContentHolds, """{"line":false,"proof":false}""", null)]
    [InlineData(NodeKinds.ContentHolds, """{"proof":"off"}""", "proof")]
    public void Each_option_is_true_or_false(string kind, string options, string? refused) =>
        Assert.Equal(refused, NodeDeclarations.Problem(Hold(kind, options)));

    /// <summary>An older build's table has neither row, so it reads each as no kind and holds the graph whole (NodeContract's unknown kind).</summary>
    [Fact]
    public void An_older_build_whose_table_omits_the_holds_reads_them_as_no_kind()
    {
        var older = NodeAuthorities.BuiltIn.Where(pair => !NodeKinds.IsHold(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);

        Assert.Equal(NodeRefusal.UnknownKind, NodeDeclarations.Problem(Hold(NodeKinds.QuestHolds), older));
        Assert.Equal(NodeRefusal.UnknownKind, NodeDeclarations.Problem(Hold(NodeKinds.ContentHolds), older));
        Assert.Null(NodeAuthorities.Of(Hold(NodeKinds.ContentHolds), older));
    }

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
    public void What_the_line_holds_is_reached_where_the_work_holds_a_commit_nothing_of_yours_holds()
    {
        var node = Hold(NodeKinds.ContentHolds);
        var seen = Observe(Branch, node, "tip-1", new HoldContent(null, Unlanded: 2, Ahead: 2));

        Assert.Equal((NodeStates.Reached, NodeSource.Holds), (seen.Outcome, seen.Source));
        Assert.Equal("tip-1", seen.Acceptance!.Evidence!.Revision);
    }

    [Fact]
    public void Unread_at_the_bound_commit_it_waits_and_is_never_reached_on_a_guess()
    {
        var unread = Observe(Branch, Hold(NodeKinds.ContentHolds), "tip-1");

        Assert.Null(unread.Outcome);
        Assert.Contains("not read", unread.Said);
    }

    [Theory]
    [InlineData(0, 0, "nothing to land")]
    [InlineData(0, 3, "a branch of yours already holds")]
    public void Dds_proof_keeps_it_active_where_no_commit_is_left_to_land_unless_proof_is_off(int unlanded, int ahead, string why)
    {
        var read = new HoldContent(null, unlanded, ahead);

        var waits = Observe(Branch, Hold(NodeKinds.ContentHolds), "tip-1", read);
        Assert.Null(waits.Outcome);
        Assert.Contains(why, waits.Said);

        Assert.Equal(NodeStates.Reached, Observe(Branch, Hold(NodeKinds.ContentHolds, """{"proof":false}"""), "tip-1", read).Outcome);
    }

    [Fact]
    public void Work_the_line_holds_by_content_keeps_it_active_in_the_heads_words_unless_line_is_off()
    {
        var read = new HoldContent("`main` already holds this work by content (a squash merge).", Unlanded: 1, Ahead: 1);

        var waits = Observe(Branch, Hold(NodeKinds.ContentHolds), "tip-1", read);
        Assert.Null(waits.Outcome);
        Assert.Contains("already holds this work by content", waits.Said);

        Assert.Equal(NodeStates.Reached, Observe(Branch, Hold(NodeKinds.ContentHolds, """{"line":false}"""), "tip-1", read).Outcome);
    }

    /// <summary>Under Current a door judges both where it lands, so a landing its record keeps reaches them, as an opinion's landed gate does.</summary>
    [Fact]
    public void A_landing_recorded_reaches_both_where_no_run_binds_them()
    {
        var landed = Branch with
        {
            Records = new WorkUnitRecords(null, [new LandedBranch("engine", "default", "feature/q1", "main", "tip-1", "s1", "q1", "Fix the gap", DateTimeOffset.UnixEpoch)], null),
        };

        foreach (var kind in new[] { NodeKinds.QuestHolds, NodeKinds.ContentHolds })
        {
            var node = Hold(kind);
            var seen = BuiltInNodes.Observe(landed, node, new NodeAcceptance(landed.Id, "tip-1", Scope.GraphDigest, node.Id, 1, 1));
            Assert.Equal(NodeStates.Reached, seen.Outcome);
        }
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

    /// <summary>The words the person reads of each kind, its options' rows among the step palette's fields.</summary>
    [Fact]
    public void Each_kind_has_its_option_fields()
    {
        Assert.Equal(["yes", "evidence"], NodeOptionFields.Of(NodeKinds.QuestHolds).Select(field => field.Name));
        Assert.Equal(["line", "proof"], NodeOptionFields.Of(NodeKinds.ContentHolds).Select(field => field.Name));
    }
}
