using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>STAFFPOLICY1e: the role policy's met condition as a fact of the one course decision, and what each action answers.</summary>
public sealed class CoursePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 14, 0, 0, TimeSpan.Zero);

    private static CourseFacts Facts(StaffAction action, Func<CoursePolicy, CoursePolicy>? shape = null, CourseHead? head = null)
    {
        var policy = new CoursePolicy(action, Now, Met: "claude-code opus high", Next: "claude-code sonnet high");
        return new CourseFacts(
            head ?? new CourseHead("failed", Role: "build", Choice: "1", Adapter: "claude-code", QuestStatus: "Taken", Waiting: [new CourseWord("w1", CourseAuthor.Person)]),
            CourseBoundary.ProcessExited, Failure: new CourseFailure(PastBound: true), Policy: shape?.Invoke(policy) ?? policy);
    }

    private static StaffAction Action(string condition, string @do, int? upTo = null, bool bound = false) => new(condition, @do, null, upTo, false, bound);

    [Theory]
    [InlineData(StaffActions.Next)]
    [InlineData(StaffActions.Weighted)]
    public void A_transient_end_moves_to_the_walks_next_choice_in_a_new_briefed_part_with_the_persons_words(string action)
    {
        var course = CourseDecision.Decide(Facts(Action(StaffConditions.Transient, action)), CourseTrigger.TransientPastBound);

        var move = Assert.IsType<LineCourse.Move>(course);
        Assert.Equal((CourseFromWhy.Transient, CourseFromBy.Daoris, CourseHow.Brief, CourseWhen.Now), (move.Why, move.By, move.How, move.When));
        Assert.Equal(new CourseWhere(CourseAccountSource.Settled, "build", null), move.Where);
        Assert.Equal(["w1"], move.Words.Ids);
        Assert.Equal("transient", CourseFrom.Code(move.Why));
    }

    [Theory]
    [InlineData(StaffConditions.Failed, CourseFromWhy.Failed)]
    [InlineData(StaffConditions.Quiet, CourseFromWhy.Quiet)]
    public void A_failed_or_quiet_end_moves_on_a_carry_on_with_its_own_reason(string condition, CourseFromWhy why)
    {
        var facts = Facts(Action(condition, StaffActions.Next)) with { Failure = null, CarryOn = CourseCarryOn.CutOff };

        var move = Assert.IsType<LineCourse.Move>(CourseDecision.Decide(facts, CourseTrigger.CarryOn));

        Assert.Equal(why, move.Why);
    }

    [Theory]
    [InlineData(StaffActions.Hold)]
    [InlineData(StaffActions.Retry)]
    public void Hold_and_retry_answer_what_the_line_answers_with_no_policy(string action)
    {
        var facts = Facts(Action(StaffConditions.Transient, action));

        Assert.Equal(Shape(CourseDecision.Decide(facts with { Policy = null }, CourseTrigger.TransientPastBound)), Shape(CourseDecision.Decide(facts, CourseTrigger.TransientPastBound)));
    }

    // Records holding lists compare by reference, so an answer is compared by its kind and its reason.
    private static string Shape(LineCourse course) => course switch
    {
        LineCourse.Hold hold => $"hold {hold.Why} {hold.Code} {string.Join(",", hold.Doors)}",
        LineCourse.Wait wait => $"wait {wait.Why} {wait.Until:o}",
        LineCourse.Move move => $"move {move.Why} {move.Where}",
        _ => course.GetType().Name,
    };

    [Fact]
    public void Ask_holds_for_the_person_with_the_door_that_goes_on_now_and_one_approval_releases_one_move()
    {
        var asking = Facts(Action(StaffConditions.Transient, StaffActions.Ask));

        var hold = Assert.IsType<LineCourse.Hold>(CourseDecision.Decide(asking, CourseTrigger.TransientPastBound));
        Assert.Equal((CourseHoldWhy.PolicyAsk, "transient"), (hold.Why, hold.Code));
        Assert.Equal([CourseDoor.GoOnNow], hold.Doors);

        var approved = CourseDecision.Decide(Facts(Action(StaffConditions.Transient, StaffActions.Ask), policy => policy with { Approved = true }), CourseTrigger.TransientPastBound);
        Assert.IsType<LineCourse.Move>(approved);
    }

    [Fact]
    public void The_bound_turns_a_move_into_a_hold_typed_as_the_bound_and_an_approval_releases_one_move()
    {
        var bound = Action(StaffConditions.Failed, StaffActions.Ask, bound: true);
        var facts = Facts(bound) with { Failure = null, CarryOn = CourseCarryOn.CutOff };

        var hold = Assert.IsType<LineCourse.Hold>(CourseDecision.Decide(facts, CourseTrigger.CarryOn));
        Assert.Equal((CourseHoldWhy.MovesBound, "moves-bound"), (hold.Why, hold.Code));

        var released = facts with { Policy = facts.Policy! with { Approved = true } };
        Assert.IsType<LineCourse.Move>(CourseDecision.Decide(released, CourseTrigger.CarryOn));
    }

    [Fact]
    public void A_limit_that_waits_holds_for_the_met_choices_reset_while_another_is_ready_and_walks_at_up_to()
    {
        var until = Now.AddHours(2);
        var head = new CourseHead("failed", EndCode: "ended.limit", Limited: true, Role: "build", Choice: "1", Adapter: "claude-code", Protocol: true, QuestStatus: "Taken");
        CourseFacts Limit(Func<CoursePolicy, CoursePolicy> shape) =>
            Facts(Action(StaffConditions.Limit, StaffActions.Wait, upTo: 30), shape, head) with { Failure = null };

        var waiting = Assert.IsType<LineCourse.Wait>(CourseDecision.Decide(Limit(policy => policy with { Until = until, Since = Now.AddMinutes(-10) }), CourseTrigger.LimitEnd));
        Assert.Equal((until, CourseWaitWhy.PolicyWait), (waiting.Until, waiting.Why));

        // 30 minutes after it began to wait the line walks by the role's rule, as the limit's own answer does.
        var expired = CourseDecision.Decide(Limit(policy => policy with { Until = until, Since = Now.AddMinutes(-30) }), CourseTrigger.LimitEnd);
        Assert.IsNotType<LineCourse.Wait>(expired);
        Assert.Equal(Shape(CourseDecision.Decide(Limit(policy => policy with { Until = until, Since = Now.AddMinutes(-30) }) with { Policy = null }, CourseTrigger.LimitEnd)), Shape(expired));
    }

    [Fact]
    public void A_presss_trigger_and_a_pick_keep_todays_answer()
    {
        var facts = Facts(Action(StaffConditions.Transient, StaffActions.Next));

        Assert.Equal(
            Shape(CourseDecision.Decide(facts with { Policy = null, Press = new CoursePress(CoursePressKind.TryAgain) }, CourseTrigger.TransientPastBound)),
            Shape(CourseDecision.Decide(facts with { Press = new CoursePress(CoursePressKind.TryAgain) }, CourseTrigger.TransientPastBound)));
        Assert.IsNotType<LineCourse.Move>(CourseDecision.Decide(facts, CourseTrigger.Answer));
    }
}
