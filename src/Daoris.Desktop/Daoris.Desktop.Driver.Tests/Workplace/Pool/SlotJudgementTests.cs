using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What makes a slot safe to take for another line, step by step: the first step that fails answers, so the answer names one cause, and a
/// slot that passes every step is judged clean.
/// </summary>
public sealed class SlotJudgementTests
{
    private static SlotJudgementFacts Clean => new();

    [Fact]
    public void A_slot_passing_every_step_is_clean()
    {
        var verdict = SlotJudgement.Judge(Clean);

        Assert.True(verdict.Clean);
        Assert.Equal((0, ""), (verdict.Step, verdict.Code));
    }

    [Theory]
    [InlineData("HeldByLine")]
    [InlineData("Claimed")]
    [InlineData("Visited")]
    [InlineData("LandingUnderWay")]
    [InlineData("StandingMove")]
    [InlineData("LandDue")]
    [InlineData("StartDue")]
    [InlineData("HelperWait")]
    public void Step_1_a_slot_something_holds_is_never_taken(string held)
    {
        var verdict = SlotJudgement.Judge(Hold(Clean, held));

        Assert.Equal((1, SlotJudgement.Held), (verdict.Step, verdict.Code));
        Assert.Contains(held switch
        {
            "HeldByLine" => "line",
            "Claimed" => "claim",
            "Visited" => "visit",
            "LandingUnderWay" => "landing",
            "StandingMove" => "move",
            "LandDue" => "land",
            "StartDue" => "start",
            _ => "helper",
        }, verdict.Why, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Step_2_git_must_list_the_slot_at_its_folder_and_bind_it_to_no_other_checkout()
    {
        Assert.Equal((2, SlotJudgement.Unlisted), Of(Clean with { Listed = false }));
        Assert.Equal((2, SlotJudgement.Bound), Of(Clean with { BoundElsewhere = true }));
    }

    [Theory]
    [InlineData(SlotLocks.Free, true)]
    [InlineData(SlotLocks.SessionNoRecord, true)]
    [InlineData(SlotLocks.None, true)]
    [InlineData(SlotLocks.Other, false)]
    public void Step_2_the_lock_is_the_pool_s_free_reason_a_session_s_whose_record_no_longer_holds_it_or_none(SlotLocks locked, bool passes)
    {
        var verdict = SlotJudgement.Judge(Clean with { Lock = locked });

        Assert.Equal(passes, verdict.Clean);
        if (!passes) Assert.Equal((2, SlotJudgement.Locked), (verdict.Step, verdict.Code));
    }

    [Fact]
    public void Step_3_a_link_under_the_trees_a_path_git_is_told_to_ignore_or_a_folder_unread_holds_the_slot()
    {
        Assert.Equal((3, SlotJudgement.Linked), Of(Clean with { LinkUnderTrees = true }));
        Assert.Equal((3, SlotJudgement.Linked), Of(Clean with { PathHidden = true }));
        Assert.Equal((3, SlotJudgement.Unread), Of(Clean with { Unread = true }));
    }

    [Fact]
    public void Step_3_only_a_link_a_placement_record_holds_is_carried_and_any_other_holds_the_slot()
    {
        Assert.True(SlotJudgement.Judge(Clean with { LinksNotRecorded = 0 }).Clean);
        Assert.Equal((3, SlotJudgement.Linked), Of(Clean with { LinksNotRecorded = 1 }));
    }

    [Fact]
    public void Step_3_a_repository_inside_the_slot_holds_it_wherever_it_lies()
    {
        Assert.Equal((3, SlotJudgement.RepositoryInside), Of(Clean with { RepositoryInside = true }));
    }

    [Fact]
    public void Step_4_a_git_operation_half_done_holds_the_slot()
    {
        Assert.Equal((4, SlotJudgement.HalfDone), Of(Clean with { OperationHalfDone = true }));
    }

    [Theory]
    [InlineData("TrackedChange", SlotJudgement.Changed)]
    [InlineData("UntrackedPath", SlotJudgement.Changed)]
    [InlineData("LineOwnIgnored", SlotJudgement.LinesOwn)]
    public void Step_5_nothing_outside_a_ref_and_nothing_of_the_line_s_own_is_in_the_slot(string found, string code)
    {
        var verdict = SlotJudgement.Judge(found switch
        {
            "TrackedChange" => Clean with { TrackedChange = true },
            "UntrackedPath" => Clean with { UntrackedPath = true },
            _ => Clean with { LineOwnIgnored = true },
        });

        Assert.Equal((5, code), (verdict.Step, verdict.Code));
    }

    [Fact]
    public void Step_6_the_head_is_where_the_line_let_it_go_and_a_ref_holds_it()
    {
        Assert.True(SlotJudgement.Judge(Clean with { Head = "abc", Expected = "abc", RefHolds = true }).Clean);
        Assert.Equal((6, SlotJudgement.HeadMoved), Of(Clean with { Head = "def", Expected = "abc" }));
        Assert.Equal((6, SlotJudgement.HeadUnheld), Of(Clean with { Head = "abc", Expected = "abc", RefHolds = false }));
    }

    [Fact]
    public void A_slot_with_no_recorded_tip_is_changed_since_its_line_let_it_go()
    {
        Assert.Equal((6, SlotJudgement.HeadMoved), Of(Clean with { Head = "abc", Expected = "" }));
    }

    [Fact]
    public void The_earliest_failing_step_answers()
    {
        var everything = Clean with
        {
            Claimed = true, Listed = false, LinkUnderTrees = true, OperationHalfDone = true, TrackedChange = true, Head = "x", Expected = "y",
        };

        Assert.Equal(1, SlotJudgement.Judge(everything).Step);
        Assert.Equal(2, SlotJudgement.Judge(everything with { Claimed = false }).Step);
        Assert.Equal(3, SlotJudgement.Judge(everything with { Claimed = false, Listed = true }).Step);
        Assert.Equal(4, SlotJudgement.Judge(everything with { Claimed = false, Listed = true, LinkUnderTrees = false }).Step);
        Assert.Equal(5, SlotJudgement.Judge(everything with { Claimed = false, Listed = true, LinkUnderTrees = false, OperationHalfDone = false }).Step);
        Assert.Equal(6, SlotJudgement.Judge(everything with { Claimed = false, Listed = true, LinkUnderTrees = false, OperationHalfDone = false, TrackedChange = false }).Step);
    }

    private static (int Step, string Code) Of(SlotJudgementFacts facts)
    {
        var verdict = SlotJudgement.Judge(facts);
        return (verdict.Step, verdict.Code);
    }

    private static SlotJudgementFacts Hold(SlotJudgementFacts facts, string held) => held switch
    {
        "HeldByLine" => facts with { HeldByLine = true },
        "Claimed" => facts with { Claimed = true },
        "Visited" => facts with { Visited = true },
        "LandingUnderWay" => facts with { LandingUnderWay = true },
        "StandingMove" => facts with { StandingMove = true },
        "LandDue" => facts with { LandDue = true },
        "StartDue" => facts with { StartDue = true },
        _ => facts with { HelperWait = true },
    };
}
