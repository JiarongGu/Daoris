using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A per-run decision: a <c>task</c> step declaring its own outcomes is answered by <i>decide</i>, proposed by a named holder with a reason
/// and confirmed by the owner; the route its answer closes is passed on the record with both holders, what follows goes on, and a changed
/// answer before the run ends reopens a passed step. Each seen failing first.
/// </summary>
public sealed class TaskDecisionTests : NodeRunFixture
{
    private const string DecisionGraph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"decide","kind":"task","version":1,"reach":"pressed","outcomes":["needed","not-needed"]},
                  {"id":"config","kind":"task","version":1,"reach":"pressed","outcomes":["done"]},
                  {"id":"check","kind":"task","version":1,"reach":"pressed","outcomes":["done"]},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>d","kind":"after","from":"claimed","to":"decide"},
                  {"id":"d>g","kind":"when","from":"decide","to":"config","outcome":"needed"},
                  {"id":"d>k","kind":"after","from":"decide","to":"check"},
                  {"id":"g>k","kind":"after","from":"config","to":"check"},
                  {"id":"k>h","kind":"when","from":"check","to":"handed","outcome":"done"}]}
        """;

    private static readonly NodeDeclaration Decision = new("decide", NodeKinds.Task, 1, NodeReach.Pressed) { Outcomes = ["needed", "not-needed"] };

    private void Read()
    {
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
    }

    private NodeWorded Decide(string outcome, string? why, string? holder = null) =>
        RunNodeWords.Word(Files, RunId, "decide", NodeWordActs.Decide, why, () => _now, outcome: outcome, holder: holder);

    private async Task<NodeRuntime> ProposedNotNeededAsync()
    {
        BoundTo(DecisionGraph);
        Read();
        var runtime = Runtime();
        await RunPassAsync(runtime);
        Assert.Equal(NodeStates.Active, RunStatus("decide").State);
        Assert.Equal(["decide:needed", "decide:not-needed", NodeWordActs.Skip, NodeWordActs.Stop], RunNodeWords.Acts(Decision, RunStatus("decide")));

        Assert.True(Decide("not-needed", "no setting changes in this release", holder: "release manager").Done);
        Read();
        await RunPassAsync(runtime, whole: false);
        Read();
        await RunPassAsync(runtime, whole: false);
        return runtime;
    }

    [Fact]
    public async Task Not_needed_passes_the_config_step_with_both_holders_on_the_record_and_what_follows_goes_on()
    {
        var runtime = await ProposedNotNeededAsync();

        // The proposal stands as the answer, held for the owner's word, and passes the step its answer closed.
        Assert.Equal((NodeStates.Reached, "not-needed"), (RunStatus("decide").State, RunStatus("decide").Acceptance!.Outcome));
        var passed = RunStatus("config");
        Assert.Equal((NodeStates.Skipped, NodeSource.Decision, "Not needed: proposed by release manager (no setting changes in this release)"),
            (passed.State, passed.By, passed.Words));
        Assert.Equal(NodeStates.Active, RunStatus("check").State);

        // The owner confirms it: the passed step names both holders, and nothing that follows starts again.
        var check = RunStatus("check").Attempt;
        Assert.True(Decide("not-needed", null).Done);
        Read();
        await RunPassAsync(runtime, whole: false);
        Assert.Equal("Not needed: proposed by release manager (no setting changes in this release), confirmed by the owner", RunStatus("config").Words);
        Assert.Equal(NodeStates.Skipped, RunStatus("config").State);
        Assert.Equal((NodeStates.Active, check), (RunStatus("check").State, RunStatus("check").Attempt));
    }

    [Fact]
    public async Task A_changed_answer_before_the_run_ends_reopens_a_passed_step_and_needed_starts_it()
    {
        var runtime = await ProposedNotNeededAsync();
        Assert.Equal(NodeStates.Skipped, RunStatus("config").State);

        Assert.True(Decide("needed", "the new version renames a setting").Done);
        Read();
        await RunPassAsync(runtime, whole: false);

        Assert.Equal(("needed", "Needed: proposed and confirmed by the owner (the new version renames a setting)"),
            (RunStatus("decide").Acceptance!.Outcome, RunStatus("decide").Words));
        Assert.Equal(NodeStates.Active, RunStatus("config").State);
        Assert.NotEqual(NodeStates.Active, RunStatus("check").State);
        Assert.Equal(["decide:needed", "decide:not-needed"], RunNodeWords.Acts(Decision, RunStatus("decide")));

        // A holder's proposal gives way once the owner answered.
        Assert.Equal(RunNodeWords.NotWaiting, Decide("not-needed", "changed my mind", holder: "release manager").Refused);
    }

    [Fact]
    public async Task A_decide_names_one_of_its_outcomes_and_the_record_round_trips_it_with_its_holder()
    {
        BoundTo(DecisionGraph);
        Read();
        await RunPassAsync(Runtime());

        Assert.Equal(RunNodeWords.NotOutcome, Decide("maybe", null).Refused);
        Assert.Equal(RunNodeWords.NotTask, RunNodeWords.Word(Files, RunId, "handed", NodeWordActs.Decide, null, () => _now, outcome: "needed").Refused);
        Assert.True(Decide("not-needed", "nothing to change", holder: "release manager").Done);

        var kept = Assert.Single(Files.Read(RunUnit)!.Pending);
        Assert.Equal((NodeWordActs.Decide, "not-needed", "release manager", "nothing to change"), (kept.Act, kept.Outcome, kept.Holder, kept.Words));
    }

    [Fact]
    public void A_save_refuses_a_decision_none_of_whose_outcomes_routes_a_wire()
    {
        var kind = NodeKit.Of(NodeKinds.Task)!;
        var after = new NodeDeclaration("after", NodeKinds.Task, 1, NodeReach.Pressed) { Outcomes = ["done"] };
        NodeGraph Graph(params Wire[] wires) => new([Decision, after], wires, ["decide"], "after");

        var routed = Graph(new Wire("w", WireKinds.When, new WireEnd("decide"), new WireEnd("after")) { Outcome = "needed" });
        Assert.Empty(kind.Problems(routed, Decision));

        var unrouted = Graph(new Wire("w", WireKinds.After, new WireEnd("decide"), new WireEnd("after")));
        Assert.Equal(GraphRefusal.DecisionUnwired, Assert.Single(kind.Problems(unrouted, Decision)).Code);
        Assert.Empty(kind.Problems(unrouted, after));
    }

    [Fact]
    public void A_decision_declares_two_or_more_outcomes_of_its_own()
    {
        var human = (BuiltInNode)NodeKit.Of(NodeKinds.Task)!;
        Assert.True(human.TakesOutcomes(["needed", "not-needed"]));
        Assert.False(human.TakesOutcomes(["needed"]));
        Assert.False(human.TakesOutcomes(["approved", "rejected"]));
        Assert.False(human.TakesOutcomes(["done", "approved"]));
        Assert.Equal([NodeWordActs.Decide], TaskNode.Presses(Decision));
    }
}
