using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LINESTOGETHER1d2: what the press reads of a member from the records, not from one record's flag: a part running or a start waiting
/// anywhere on its line of work, and a workflow with an <c>integrate</c> step still to come; and the coded note a brought-in member's
/// conversation keeps.
/// </summary>
public sealed class TogetherReadsTests
{
    private static SessionRecord Record(string id, string state, string? from = null, string? head = null) =>
        new(id, "engine", state) { From = from, Head = head };

    [Fact]
    public void A_part_running_later_on_the_members_line_makes_it_live_and_a_queued_start_is_a_start_waiting()
    {
        var ended = Record("s-member", "completed", head: "s-next");
        var records = new[] { ended, Record("s-next", "working", from: "s-member"), Record("s-other", "working") };

        Assert.Equal((true, false), TogetherPress.MemberReads(records, ended));
        Assert.Equal((false, true), TogetherPress.MemberReads([ended, Record("s-next", "queued", from: "s-member")], ended));
        Assert.Equal((false, false), TogetherPress.MemberReads([ended, Record("s-other", "working")], ended));
        Assert.Equal((true, false), TogetherPress.MemberReads([Record("s-member", "awaiting-person")], Record("s-member", "awaiting-person")));
    }

    private static WorkflowRunStep Step(string kind, string state) =>
        new(new WorkflowStep(kind, kind, WorkflowParticipation.Automatic, WorkflowExecutor.Daoris, null,
            new WorkflowSource("workflow", WorkflowLevels.Repository), [], WorkflowRuntime.Built, null), state, "");

    [Fact]
    public void A_workflow_steps_on_past_its_landing_while_an_integrate_step_is_not_done()
    {
        Assert.True(TogetherPress.StepsOn([Step(WorkflowKinds.Landing, WorkflowRunStates.Done), Step(NodeKinds.Integrate, WorkflowRunStates.NotReached)]));
        Assert.True(TogetherPress.StepsOn([Step(NodeKinds.Integrate, WorkflowRunStates.Working)]));
        Assert.False(TogetherPress.StepsOn([Step(NodeKinds.Integrate, WorkflowRunStates.Done)]));
        Assert.False(TogetherPress.StepsOn([Step(WorkflowKinds.Landing, WorkflowRunStates.NotReached)]));
    }

    [Fact]
    public void A_brought_in_member_keeps_a_coded_note_naming_where_its_work_went()
    {
        var onto = LandingRules.BroughtIntoNote(new TogetherBrought("tg1", "s-member", "trees/m", "abc1234def", "feature/x", false, DateTimeOffset.UnixEpoch));
        var part = Assert.Single(onto.Parts!);
        Assert.Equal(SessionEventKind.Note, onto.Kind);
        Assert.Equal((NoteCodes.LandingBroughtInto.Code, "feature/x", "tg1"), (part.Code, part.Value("branch"), part.Value("operation")));

        var line = LandingRules.BroughtIntoNote(new TogetherBrought("tg1", "s-member", "trees/m", "abc1234def", "main", true, DateTimeOffset.UnixEpoch));
        Assert.Equal(NoteCodes.LandingBroughtIntoLine.Code, Assert.Single(line.Parts!).Code);
        Assert.Contains("the line `main`", line.Text);
    }
}
