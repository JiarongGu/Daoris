using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a run's check step says of its verdict: a failure names the gate that failed, never only its code, and a reached command whose
/// classes only flaked (none red, exit 0) passes and says the flakes.
/// </summary>
public sealed class CheckWordsTests
{
    private static readonly NodeDeclaration Check = new("check", NodeKinds.Check, 1, NodeReach.Performed);

    private static WorkflowRunStep Failed(WorkflowRunTold? told) =>
        new(WorkflowGraphRuns.Cell(Check, null), WorkflowRunStates.Failed, WorkflowGraphDetails.Failed)
        {
            Node = NodeKinds.Check, Code = HeavyExecutor.GateFailed, Gate = "cli", Told = told,
        };

    [Fact]
    public void A_failed_check_step_names_the_gate_and_where_its_words_went_or_why_they_did_not()
    {
        Assert.Equal("Failed: the gate `cli` failed; its words went to session `s1` as its next turn.", WorkflowRunWords.Said(Failed(new("s1", null))));
        Assert.Equal("Failed: the gate `cli` failed; its words were not told to the work: no session of its work is known here.",
            WorkflowRunWords.Said(Failed(new(null, "no session of its work is known here"))));
        Assert.Equal("Failed: the gate `cli` failed; its words are not told to the work yet.", WorkflowRunWords.Said(Failed(null)));
        Assert.Equal("Failed: the gate `cli` failed; its rounds back to the work are spent, so it waits on you, and its words went to session `s1` as its next turn.",
            WorkflowRunWords.Said(Failed(new("s1", null) { Spent = true })));
        Assert.Equal("Failed: the gate `cli` failed; its rounds back to the work are spent, so the role `supervisor` holds it, and its words went to session `s1` as its next turn.",
            WorkflowRunWords.Said(Failed(new("s1", null) { Spent = true, Decided = NodeTold.RoleHeld, Role = "supervisor" })));
        Assert.Contains("came to you as Daoris's final exit", WorkflowRunWords.Said(Failed(new("s1", null) { Spent = true, Decided = NodeTold.Exit })), StringComparison.Ordinal);
    }

    [Fact]
    public void Where_a_failed_check_s_words_went_is_read_from_its_attempt_s_telling_on_the_run_s_record()
    {
        var at = DateTimeOffset.UnixEpoch;
        var record = new NodeRecord("run:r") { Told = [new NodeTold("check", 2, "s1", null, at), new NodeTold("check", 3, null, "the say door refused", at)] };

        Assert.Equal(new WorkflowRunTold("s1", null), WorkflowRunTold.Of(record, "check", 2));
        Assert.Equal(new WorkflowRunTold(null, "the say door refused"), WorkflowRunTold.Of(record, "check", 3));
        Assert.Equal(new WorkflowRunTold(null, null), WorkflowRunTold.Of(record, "check", 4));
        Assert.Equal(record.Told, NodeRecordFiles.Parse(NodeRecordFiles.ToJson(record))!.Told);
    }

    [Fact]
    public void A_red_past_the_bound_tells_the_work_its_words_and_that_the_person_sees_it_too()
    {
        var words = SpentChecks.Words("check", HeavyExecutor.GateFailed, "the gate 'reached' failed with exit 1", 3, NodeTold.OwnerHeld, null);

        Assert.StartsWith("Daoris: `check` failed (`gate-failed`), so your work goes back to you: \n\nthe gate 'reached' failed with exit 1", words);
        Assert.Contains("red round 3, past the 3 Daoris sends back on its own", words);
        Assert.EndsWith("give up with what you tried.", words);
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
