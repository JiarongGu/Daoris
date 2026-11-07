using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A run's <c>land</c> is the one way to land, so a door whose work no run's land performs lands nothing in place. It
/// answers the gate's own refusal where a part holds, else why the work's process lands nothing, as <see cref="LandingCode.Refused"/>; the
/// terminal's press says the same, exit 1.
/// </summary>
public sealed class RunLandHoldsTests
{
    [Fact]
    public void Work_no_workflow_governs_is_held_with_the_workflows_own_sentence_and_never_landed()
    {
        var process = WorkflowProcess.Declared(DriverConfig.Empty, "engine", null) with { Problem = WorkflowProcess.NoWorkflow("engine") };
        var gate = LandingGate.Open("engine") with { Workflow = WorkflowGate.Judge(process) };

        var held = RunLandHolds.Of(gate);

        Assert.Null(gate.Refusal);
        Assert.False(held.Landed);
        Assert.Equal(LandingCode.Refused, held.Refusal);
        Assert.Equal("No workflow is chosen for `engine`, so nothing runs or lands its work: Set up… chooses one. Nothing was landed.", held.Message);
    }

    [Fact]
    public void A_gate_that_holds_answers_its_own_refusal()
    {
        var process = WorkflowProcess.Declared(DriverConfig.Empty, "engine", null) with { Unread = "its chain could not be read." };
        var gate = LandingGate.Open("engine") with { Workflow = WorkflowGate.Judge(process) };

        Assert.NotNull(gate.Refusal);
        Assert.Equal(gate.Refusal!.Message, RunLandHolds.Of(gate).Message);
        Assert.Equal(gate.Refusal.Refusal, RunLandHolds.Of(gate).Refusal);
    }

    [Fact]
    public void A_gate_read_with_no_process_is_held_as_work_no_workflow_governs()
    {
        var held = RunLandHolds.Of(LandingGate.Open("engine"));

        Assert.False(held.Landed);
        Assert.Equal("No workflow's run governs this work, so nothing lands it: Set up… chooses one. Nothing was landed.", held.Message);
    }

    [Fact]
    public void The_terminals_press_on_work_no_run_lands_says_the_hold_and_exits_1()
    {
        var process = WorkflowProcess.Declared(DriverConfig.Empty, "engine", null) with { Problem = WorkflowProcess.NoWorkflow("engine") };
        var gate = LandingGate.Open("engine") with { Workflow = WorkflowGate.Judge(process) };

        var said = TreesWords.AcceptedForPass(Path.Combine(AppContext.BaseDirectory, "no-home"), gate);

        Assert.Equal(1, said.Code);
        Assert.Equal([$"trees: {RunLandHolds.Of(gate).Message}"], said.Out);
    }
}
