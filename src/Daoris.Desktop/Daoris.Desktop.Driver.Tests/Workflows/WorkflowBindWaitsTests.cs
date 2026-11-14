using Daoris.Contracts.Quests;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A first turn's end waits on its run as a later turn's does, and names the commit its submission handed.</summary>
public sealed class WorkflowBindWaitsTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-15T10:00:00Z");

    private static WorkflowBind Bound(string workflow) => new(
        new QuestView("q1", "Asker", "Owner", "Do the thing", "Here is why.", "Taken"), WorkflowBindCame.Bound, "q1",
        new WorkflowRunBinding("q1", "Owner", "ws", workflow, WorkflowLevels.Task, At), null);

    [Fact]
    public void A_first_turn_waits_on_its_run_at_the_commit_its_submission_handed()
    {
        var handed = new QuestView("q1", "Asker", "Owner", "Do the thing", "Here is why.", "Taken")
        {
            Submission = new QuestSubmissionView("m1", 1, At, "Built.", Commit: "abc1234"),
        };

        Assert.Equal(new StepWait("q1", Tip: "abc1234"), Bound("merge-after-accept").WaitsAfter(handed));
        Assert.Equal(new StepWait("q1"), Bound("merge-after-accept").WaitsAfter(null));
    }

    [Fact]
    public void A_run_under_no_chosen_workflow_waits_on_nothing()
    {
        Assert.Null(Bound(WorkflowSelection.Current).WaitsAfter(null));
    }
}
