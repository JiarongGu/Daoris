using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A door whose work no run's <c>land</c> performs lands nothing, and answers the gate's refusal or why the process lands nothing (<see cref="LandingCode.Refused"/>), exit 1 at the terminal.
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
}
