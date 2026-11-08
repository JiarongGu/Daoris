using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Which slot a start takes: the first choice that stands, in order, and the fallback when the pool is withheld or the count is spent.
/// </summary>
public sealed class SlotChoiceTests
{
    private const string Line = "s-1111";

    private static readonly DateTimeOffset Noon = new(2026, 11, 8, 12, 0, 0, TimeSpan.Zero);

    private static SlotCandidate Slot(int number, string line = "", DateTimeOffset? baseAt = null, DateTimeOffset? letGoAt = null) =>
        new(number, line) { BaseAt = baseAt, LetGoAt = letGoAt };

    private static SlotChoiceFacts Facts(int count, params SlotCandidate[] slots) => new(Line, slots) { Count = count };

    [Fact]
    public void Choice_1_the_line_s_own_slot_still_on_its_branch_goes_on_with_no_switch()
    {
        var chosen = SlotChoice.Choose(Facts(2, Slot(1, "s-2222") with { Free = true }, Slot(2, Line) with { OnItsBranch = true, Free = true }));

        Assert.Equal((SlotChoices.Own, 2), (chosen.Choice, chosen.Number));
    }

    [Fact]
    public void Choice_1_is_taken_beside_a_person_s_terminal_or_the_line_s_own_work_but_not_beside_anything_else()
    {
        var own = Slot(1, Line) with { OnItsBranch = true, HeldOnlyByLine = true };

        Assert.Equal((SlotChoices.Own, 1), Pair(SlotChoice.Choose(Facts(1, own))));
        Assert.NotEqual(SlotChoices.Own, SlotChoice.Choose(Facts(1, own with { HeldOnlyByLine = false })).Choice);
        Assert.NotEqual(SlotChoices.Own, SlotChoice.Choose(Facts(1, own with { OnItsBranch = false })).Choice);
        Assert.NotEqual(SlotChoices.Own, SlotChoice.Choose(Facts(1, own with { State = KeptStates.Stuck })).Choice);
    }

    [Fact]
    public void Choice_2_the_line_s_last_slot_when_free_and_judged_clean_though_another_line_worked_there()
    {
        var facts = Facts(3, Slot(1, "", baseAt: Noon) with { Free = true }, Slot(2, "s-2222") with { Free = true, MayComeBack = true }, Slot(3, "s-3333") with { Free = true });

        var chosen = SlotChoice.Choose(facts with { LastSlot = 2 });

        Assert.Equal((SlotChoices.Back, 2), (chosen.Choice, chosen.Number));
    }

    [Fact]
    public void Choice_2_is_passed_over_when_its_slot_is_held_or_unclean_or_its_present_line_has_a_start_due()
    {
        var detached = Slot(1, "", baseAt: Noon) with { Free = true };
        foreach (var last in new[]
        {
            Slot(2, "s-2222") with { Free = false },
            Slot(2, "s-2222") with { Free = true, Verdict = new SlotVerdict(5, SlotJudgement.Changed, "x") },
            Slot(2, "s-2222") with { Free = true, LineBusy = true },
        })
        {
            var chosen = SlotChoice.Choose(Facts(2, detached, last) with { LastSlot = 2 });
            Assert.Equal((SlotChoices.Detached, 1), (chosen.Choice, chosen.Number));
        }
    }

    [Fact]
    public void Choice_3_a_free_detached_slot_the_one_whose_last_line_s_base_is_newest_first()
    {
        var chosen = SlotChoice.Choose(Facts(3,
            Slot(1, "", baseAt: Noon.AddDays(-2)) with { Free = true },
            Slot(2, "", baseAt: Noon) with { Free = true },
            Slot(3, "", baseAt: Noon.AddDays(-1)) with { Free = true }));

        Assert.Equal((SlotChoices.Detached, 2), (chosen.Choice, chosen.Number));
    }

    [Fact]
    public void Choice_3_skips_a_detached_slot_that_is_held_unclean_retiring_or_stuck()
    {
        var chosen = SlotChoice.Choose(Facts(5,
            Slot(1, "", baseAt: Noon) with { Free = false },
            Slot(2, "", baseAt: Noon) with { Free = true, Verdict = new SlotVerdict(6, SlotJudgement.HeadMoved, "x") },
            Slot(3, "", baseAt: Noon) with { Free = true, State = KeptStates.Retiring },
            Slot(4, "", baseAt: Noon) with { Free = true, State = KeptStates.Stuck },
            Slot(5, "", baseAt: Noon.AddDays(-9)) with { Free = true }));

        Assert.Equal((SlotChoices.Detached, 5), (chosen.Choice, chosen.Number));
    }

    [Fact]
    public void Choice_4_a_free_slot_whose_line_may_come_back_the_one_let_go_longest_ago_first()
    {
        var chosen = SlotChoice.Choose(Facts(3,
            Slot(1, "s-2222", letGoAt: Noon) with { Free = true, MayComeBack = true },
            Slot(2, "s-3333", letGoAt: Noon.AddHours(-5)) with { Free = true, MayComeBack = true },
            Slot(3, "s-4444", letGoAt: Noon.AddHours(-9)) with { Free = true, MayComeBack = false }));

        Assert.Equal((SlotChoices.MayReturn, 2), (chosen.Choice, chosen.Number));
    }

    [Fact]
    public void Choice_4_never_takes_a_slot_whose_line_has_a_start_due_or_a_claim_or_that_is_held_or_unclean()
    {
        var may = Slot(1, "s-2222", letGoAt: Noon) with { Free = true, MayComeBack = true };

        Assert.Equal(SlotChoices.New, SlotChoice.Choose(Facts(2, may with { LineBusy = true })).Choice);
        Assert.Equal(SlotChoices.New, SlotChoice.Choose(Facts(2, may with { Free = false })).Choice);
        Assert.Equal(SlotChoices.New, SlotChoice.Choose(Facts(2, may with { Verdict = new SlotVerdict(5, SlotJudgement.LinesOwn, "x") })).Choice);
        Assert.Equal(SlotChoices.MayReturn, SlotChoice.Choose(Facts(2, may)).Choice);
    }

    [Fact]
    public void Choice_5_a_new_slot_at_the_next_number_where_the_repository_has_fewer_than_its_count()
    {
        var chosen = SlotChoice.Choose(Facts(3, Slot(1, "s-2222"), Slot(4, "s-3333")));

        Assert.Equal((SlotChoices.New, 5), (chosen.Choice, chosen.Number));
        Assert.Equal((SlotChoices.New, 1), Pair(SlotChoice.Choose(Facts(1))));
    }

    [Fact]
    public void Choice_6_a_tree_of_the_line_s_own_when_the_count_is_spent_or_unset()
    {
        var held = new[] { Slot(1, "s-2222"), Slot(2, "s-3333") };

        var spent = SlotChoice.Choose(Facts(2, held));
        var unset = SlotChoice.Choose(Facts(0));

        Assert.Equal((SlotChoices.Past, (int?)null), (spent.Choice, spent.Number));
        Assert.Equal(SlotChoices.Past, unset.Choice);
    }

    [Theory]
    [InlineData(SlotWithheld.Unsettled)]
    [InlineData(SlotWithheld.Submodules)]
    [InlineData(SlotWithheld.NoJob)]
    [InlineData(SlotWithheld.OldGit)]
    public void The_pool_withheld_chooses_nothing_of_it_even_with_the_line_s_own_slot_standing(SlotWithheld withheld)
    {
        var facts = Facts(3, Slot(1, Line) with { OnItsBranch = true, Free = true }) with { Withheld = withheld };

        var chosen = SlotChoice.Choose(facts);

        Assert.Equal((SlotChoices.Past, withheld), (chosen.Choice, chosen.Withheld));
    }

    [Fact]
    public void The_choices_come_in_order_so_an_earlier_one_beats_every_later()
    {
        var all = Facts(9,
            Slot(1, "s-2222", letGoAt: Noon) with { Free = true, MayComeBack = true },
            Slot(2, "", baseAt: Noon) with { Free = true },
            Slot(3, "s-3333") with { Free = true },
            Slot(4, Line) with { OnItsBranch = true, Free = true }) with { LastSlot = 3 };

        Assert.Equal(4, SlotChoice.Choose(all).Number);
        var without1 = all with { Slots = [.. all.Slots.Where(each => each.Number != 4)] };
        Assert.Equal((SlotChoices.Back, 3), Pair(SlotChoice.Choose(without1)));
        var without2 = without1 with { LastSlot = null };
        Assert.Equal((SlotChoices.Detached, 2), Pair(SlotChoice.Choose(without2)));
        var without3 = without2 with { Slots = [.. without2.Slots.Where(each => each.Number != 2)] };
        Assert.Equal((SlotChoices.MayReturn, 1), Pair(SlotChoice.Choose(without3)));
        var without4 = without3 with { Slots = [.. without3.Slots.Where(each => each.Number != 1)] };
        Assert.Equal(SlotChoices.New, SlotChoice.Choose(without4).Choice);
    }

    private static (SlotChoices Choice, int? Number) Pair(SlotChosen chosen) => (chosen.Choice, chosen.Number);
}
