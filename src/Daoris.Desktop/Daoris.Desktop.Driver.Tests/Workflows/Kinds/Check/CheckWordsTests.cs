using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a run's check step says of its verdict: a failure names the gate that failed, never only its code, and a reached command whose
/// classes only flaked (none red, exit 0) passes and says the flakes.
/// </summary>
public sealed class CheckWordsTests
{
    private static readonly NodeDeclaration Check = new("check", NodeKinds.Check, 1, NodeReach.Performed);

    [Fact]
    public void A_failed_check_step_names_the_gate_that_failed_not_only_its_code()
    {
        var step = new WorkflowRunStep(WorkflowGraphRuns.Cell(Check, null), WorkflowRunStates.Failed, WorkflowGraphDetails.Failed)
        {
            Node = NodeKinds.Check, Code = HeavyExecutor.GateFailed, Gate = "cli",
        };

        Assert.Equal("Failed: the gate `cli` failed; its words went to the work.", WorkflowRunWords.Said(step));
    }

    [Fact]
    public void A_reached_command_whose_classes_only_flaked_passes_and_says_the_flakes()
    {
        const string log = """reached-classes: {"ran":116,"replayed":318,"flaked":["web-unit|a.test.tsx","web-unit|b.test.tsx"],"mainRed":[],"red":[],"deferred":[]}""" + "\n";

        var result = ReachedVerdict.Judge("i-1", "0123456789abcdef0123456789abcdef01234567", 0, log, "reached.log", "reached-classes: ");

        Assert.Equal(VerifyVerdicts.Reached, result.Verdict);
        Assert.Equal(
            "the reached classes: 116 run, 318 replayed from passes of the same inputs, 0 red; passed on a second run alone: web-unit|a.test.tsx, web-unit|b.test.tsx",
            ReachedVerdict.Told(log, "reached-classes: "));
    }
}
