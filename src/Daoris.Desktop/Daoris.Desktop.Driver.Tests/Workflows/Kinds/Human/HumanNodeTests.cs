using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The person's task (NODEKIT1 HUMAN; D245's second note): a <c>human</c> node a run reaches waits on the person, who presses <i>Done</i>, or
/// <i>Approve</i> or <i>Reject</i> where it is an approval, with a note if they like, on the run's node door; the run goes on along the <c>when</c>
/// wire of the outcome pressed. What waits is read for <i>What needs you</i> and the press clears it; a press on a node that is not waiting is
/// refused. Each seen failing first.
/// </summary>
public sealed class HumanNodeTests : NodeRunFixture
{
    private const string TaskGraph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"release","kind":"human","version":1,"reach":"pressed","outcomes":["done"]},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"}],
         "wires":[{"id":"c>r","kind":"after","from":"claimed","to":"release"},
                  {"id":"r>h","kind":"when","from":"release","to":"handed","outcome":"done"}]}
        """;

    private const string ApprovalGraph = """
        {"schemaVersion":2,"entries":["claimed"],"end":"handed",
         "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed"},
                  {"id":"sign","kind":"human","version":1,"reach":"pressed","outcomes":["approved"]},
                  {"id":"handed","kind":"land","version":1,"reach":"performed"},
                  {"id":"redo","kind":"human","version":1,"reach":"pressed","outcomes":["done"]}],
         "wires":[{"id":"c>s","kind":"after","from":"claimed","to":"sign"},
                  {"id":"s>h","kind":"when","from":"sign","to":"handed","outcome":"approved"},
                  {"id":"s>r","kind":"rejected","from":"sign","to":"redo"}]}
        """;

    /// <summary>The run's work done on its branch, as the landing binds it.</summary>
    private void Read()
    {
        _branchTips["daoris/s1"] = Tip1;
        _units = [RunHome.QuestUnit(RunHome.Quest(RunId, "Done")), RunHome.Branch("unit-b1", "daoris/s1", Opened, [RunId])];
    }

    private NodeWorded Press(string node, string act, string? note = null) => RunNodeWords.Word(Files, RunId, node, act, note, () => _now);

    private static NodeDeclaration Declared(params string[] outcomes) => new("t", NodeKinds.Human, 1, NodeReach.Pressed) { Outcomes = outcomes };

    [Fact]
    public async Task A_run_reaching_a_task_waits_on_the_person_and_their_done_goes_on_along_its_when_wire()
    {
        BoundTo(TaskGraph);
        Read();
        var runtime = Runtime();
        await RunPassAsync(runtime);

        Assert.Equal(NodeStates.Active, RunStatus("release").State);
        Assert.Equal(NodeStates.Blocked, RunStatus("handed").State);
        Assert.Equal([NodeWordActs.Done, NodeWordActs.Skip, NodeWordActs.Stop], RunNodeWords.Acts(Declared("done") with { Id = "release" }, RunStatus("release")));

        Assert.True(Press("release", NodeWordActs.Done, "tagged v1.4 by hand").Done);
        Read();
        await RunPassAsync(runtime, whole: false);

        var done = RunStatus("release");
        Assert.Equal((NodeStates.Reached, NodeSource.Person, "done", "tagged v1.4 by hand"), (done.State, done.By, done.Acceptance!.Outcome, done.Words));
        Assert.Equal((NodeStates.Active, "unit-b1"), (RunStatus("handed").State, RunStatus("handed").Binding!.Subject));
    }

    [Theory]
    [InlineData(NodeWordActs.Approve, null, NodeStates.Reached, "approved", "handed", "redo")]
    [InlineData(NodeWordActs.Reject, "the notes miss the migration", NodeStates.Rejected, null, "redo", "handed")]
    public async Task An_approval_goes_on_along_the_when_wire_of_its_approval_and_its_refusal_along_the_rejected_wire(
        string act, string? note, string state, string? outcome, string next, string other)
    {
        BoundTo(ApprovalGraph);
        Read();
        var runtime = Runtime();
        await RunPassAsync(runtime);
        Assert.Equal([NodeWordActs.Approve, NodeWordActs.Reject, NodeWordActs.Skip, NodeWordActs.Stop],
            RunNodeWords.Acts(Declared("approved") with { Id = "sign" }, RunStatus("sign")));

        Assert.True(Press("sign", act, note).Done);
        Read();
        await RunPassAsync(runtime, whole: false);

        Assert.Equal((state, outcome, note), (RunStatus("sign").State, RunStatus("sign").Acceptance?.Outcome, RunStatus("sign").Words));
        Assert.Equal(NodeStates.Active, RunStatus(next).State);
        Assert.NotEqual(NodeStates.Active, RunStatus(other).State);
    }

    [Theory]
    [InlineData(true, "done")]
    [InlineData(true, "approved")]
    [InlineData(true)]
    [InlineData(false, "approved", "rejected")]
    [InlineData(false, "rejected")]
    public void A_task_declares_done_and_an_approval_approved_and_the_refusal_is_no_outcome(bool taken, params string[] outcomes)
    {
        Assert.Equal(taken, ((BuiltInNode)NodeKit.Of(NodeKinds.Human)!).TakesOutcomes(outcomes) || outcomes.Length == 0);
    }

    [Fact]
    public async Task A_press_is_refused_where_the_node_waits_on_no_such_press()
    {
        BoundTo(ApprovalGraph);
        Read();

        // Not reached yet: nothing waits on the person.
        Assert.Equal(RunNodeWords.NotWaiting, Press("sign", NodeWordActs.Approve).Refused);
        var runtime = Runtime();
        await RunPassAsync(runtime);

        // Waiting, an approval takes no Done, and only a person's task takes any of the three.
        Assert.Equal(RunNodeWords.NotWaiting, Press("sign", NodeWordActs.Done).Refused);
        Assert.Equal(RunNodeWords.NotTask, Press("handed", NodeWordActs.Approve).Refused);

        Assert.True(Press("sign", NodeWordActs.Approve).Done);
        Assert.Equal(RunNodeWords.Worded, Press("sign", NodeWordActs.Reject).Refused);
        Read();
        await RunPassAsync(runtime, whole: false);
        Assert.Equal(RunNodeWords.NotWaiting, Press("sign", NodeWordActs.Reject).Refused);
    }

    [Fact]
    public async Task A_waiting_task_is_read_for_What_needs_you_and_the_press_clears_it()
    {
        BoundTo(ApprovalGraph);
        Read();
        var runtime = Runtime();
        Assert.Empty(runtime.HumanTasks());
        await RunPassAsync(runtime);

        var task = Assert.Single(runtime.HumanTasks());
        Assert.Equal((RunId, "sign", RunHome.Repository, RunId), (task.Run, task.Node, task.Repository, task.Work));
        Assert.Equal([NodeWordActs.Approve, NodeWordActs.Reject], task.Acts);

        // A refusal is a rejection, so it takes the person's words as a review's changes do.
        Assert.False(Press("sign", NodeWordActs.Reject).Done);
        Assert.True(Press("sign", NodeWordActs.Reject, "the notes miss it").Done);
        Assert.Empty(runtime.HumanTasks());
    }

    [Fact]
    public void The_terminal_says_each_press_with_a_note_if_given()
    {
        foreach (var act in new[] { NodeWordActs.Done, NodeWordActs.Approve, NodeWordActs.Reject })
        {
            Assert.True(WorkflowNodeCommand.Asks([act, RunId, "sign"]));
            Assert.Equal(new WorkflowNodeAsk(act, RunId, "sign", null), WorkflowNodeCommand.Read([act, "#" + RunId, "sign"], out _));
        }

        Assert.Equal(new WorkflowNodeAsk(NodeWordActs.Reject, RunId, "sign", "the notes miss it"), WorkflowNodeCommand.Read(["reject", RunId, "sign", "the notes miss it"], out _));
        Assert.Contains("workflow done <run> <node>", WorkflowNodeCommand.Usage, StringComparison.Ordinal);
    }

    [Fact]
    public void A_person_s_task_declares_a_tasks_outcome_or_an_approvals_and_no_other_and_the_refusal_is_no_outcome()
    {
        Assert.Null(NodeDeclarations.Problem(Declared()));
        Assert.Null(NodeDeclarations.Problem(Declared("done")));
        Assert.Null(NodeDeclarations.Problem(Declared("approved")));
        foreach (var other in new[] { new[] { "merged", "closed" }, ["rejected", "approved"], ["approved", "rejected"], ["rejected"], ["done", "approved"] })
        {
            Assert.Equal("outcomes", NodeDeclarations.Problem(Declared(other)));
        }

        Assert.Equal([NodeWordActs.Done], HumanNode.Presses(Declared()));
        Assert.Equal([NodeWordActs.Done], HumanNode.Presses(Declared("done")));
        Assert.Equal([NodeWordActs.Approve, NodeWordActs.Reject], HumanNode.Presses(Declared("approved")));
    }
}
