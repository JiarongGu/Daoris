using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A producer's landing opens or retargets the one pin-move quest of each consumer edge and writes nothing into the consumer; a pin move
/// that came out red holds the pin and files a break quest to the producer with the failing case. Nothing here names a kind
/// of repository: an edge is a consumer and a producer, whatever they are.
/// </summary>
public sealed class PinMovesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);
    private static readonly PinEdge Edge = new("web", "core");
    private static readonly string A = new('a', 40);
    private static readonly string B = new('b', 40);
    private static readonly string C = new('c', 40);

    private static QuestView Quest(string id, string status, int generation, string commit, string? note = null, DateTimeOffset? updated = null) =>
        new(id, "core", "web", "title", PinMoves.Words(Edge, commit).Body, status)
        {
            Request = PinMoves.KeyOf(Edge, generation), Note = note, Updated = updated ?? T0,
        };

    [Fact]
    public void A_landing_the_consumer_does_not_pin_opens_its_quest_at_generation_one_naming_the_commit_and_the_edge()
    {
        var act = PinMoves.Plan(Edge, pinned: A, tip: B, []);

        Assert.NotNull(act);
        Assert.Equal(PinMoveActs.Open, act.Act);
        Assert.Equal(B, act.Commit);
        Assert.Equal(PinMoves.KeyOf(Edge, 1), act.Key);
        Assert.Null(act.Quest);
        Assert.Contains(B, act.Body);
        Assert.Contains("core", act.Title);
        Assert.Contains("daoris.family.lock", act.Body);
    }

    [Fact]
    public void A_consumer_already_at_the_tip_is_owed_nothing()
    {
        Assert.Null(PinMoves.Plan(Edge, pinned: B, tip: B, []));
    }

    [Fact]
    public void A_second_landing_retargets_the_open_quest_and_never_opens_another()
    {
        var live = Quest("q1", "Open", 1, B);

        var act = PinMoves.Plan(Edge, pinned: A, tip: C, [live]);

        Assert.Equal(PinMoveActs.Retarget, act!.Act);
        Assert.Equal("q1", act.Quest);
        Assert.Equal(C, act.Commit);
        Assert.Contains(C, act.Body);
        Assert.DoesNotContain(B, act.Body);
    }

    [Fact]
    public void A_quest_a_session_has_taken_is_retargeted_too_and_one_already_at_the_tip_is_left()
    {
        var taken = Quest("q1", "Taken", 1, B);

        Assert.Equal(PinMoveActs.Retarget, PinMoves.Plan(Edge, A, C, [taken])!.Act);
        Assert.Null(PinMoves.Plan(Edge, A, B, [taken]));
    }

    [Fact]
    public void A_closed_quest_opens_the_next_generation_for_a_newer_landing_and_none_for_the_commit_it_settled()
    {
        var declined = Quest("q1", "Declined", 1, B, note: "the save test fails");
        var done = Quest("q2", "Done", 2, C);

        Assert.Equal(PinMoves.KeyOf(Edge, 2), PinMoves.Plan(Edge, A, C, [declined])!.Key);
        Assert.Null(PinMoves.Plan(Edge, A, B, [declined]));
        Assert.Equal(PinMoves.KeyOf(Edge, 3), PinMoves.Plan(Edge, A, "d".PadLeft(40, 'd'), [declined, done])!.Key);
    }

    [Fact]
    public void Quests_of_other_edges_and_other_requests_are_not_this_edges()
    {
        var other = new PinEdge("api", "core");
        var theirs = Quest("q1", "Open", 1, B) with { Request = PinMoves.KeyOf(other, 1) };
        var plain = Quest("q2", "Open", 1, B) with { Request = "run1/node/1" };
        var none = Quest("q3", "Open", 1, B) with { Request = null };

        Assert.Equal(PinMoveActs.Open, PinMoves.Plan(Edge, A, B, [theirs, plain, none])!.Act);
        Assert.Equal([], PinMoves.OfEdge(Edge, [theirs, plain, none]));
    }

    [Fact]
    public void The_commit_a_quest_asks_is_read_back_from_its_own_words_and_an_odd_body_asks_none()
    {
        Assert.Equal(B, PinMoves.CommitOf(Quest("q", "Open", 1, B)));
        Assert.Null(PinMoves.CommitOf(Quest("q", "Open", 1, B) with { Body = "free words" }));
    }

    [Fact]
    public void The_words_say_what_the_move_is_and_what_it_is_not_without_naming_a_kind_of_repository()
    {
        var words = PinMoves.Words(Edge, B);

        Assert.Contains("only", words.Body);
        Assert.Contains("hold", words.Body);
        Assert.Contains("decline", words.Body);
        foreach (var kind in new[] { "game", "engine", "plugin", "service", "library", "app" })
        {
            Assert.DoesNotContain(kind, words.Title + words.Body, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void A_declined_move_files_one_break_quest_to_the_producer_with_the_failing_case()
    {
        var declined = Quest("q1", "Declined", 1, B, note: "saves written before B no longer load", updated: T0.AddHours(2));

        var broke = Assert.Single(PinMoves.Breaks(Edge, [declined]));

        Assert.Equal(PinMoves.BreakKeyOf(Edge, 1), broke.Key);
        Assert.Equal("web", broke.From);
        Assert.Equal("core", broke.To);
        Assert.Contains("saves written before B no longer load", broke.Body);
        Assert.Contains(B, broke.Body);
        Assert.Contains("#q1", broke.Body);
    }

    [Fact]
    public void A_break_already_filed_is_not_filed_again_and_each_red_move_owes_its_own()
    {
        var first = Quest("q1", "Declined", 1, B, note: "red");
        var second = Quest("q2", "Declined", 2, C, note: "still red");
        var filed = new QuestView("b1", "web", "core", "t", "b", "Open") { Request = PinMoves.BreakKeyOf(Edge, 1) };

        Assert.Empty(PinMoves.Breaks(Edge, [first, filed]));
        Assert.Equal([PinMoves.BreakKeyOf(Edge, 2)], PinMoves.Breaks(Edge, [first, second, filed]).Select(each => each.Key));
    }

    [Fact]
    public void A_red_move_holds_the_pin_from_its_first_red_and_a_later_red_moves_the_commit_not_the_age()
    {
        var first = Quest("q1", "Declined", 1, B, updated: T0);
        var second = Quest("q2", "Declined", 2, C, updated: T0.AddDays(2));

        Assert.Equal(new FamilyHold("web", "core", B, T0, 1), PinMoves.Hold(Edge, [first], T0.AddDays(9)));
        Assert.Equal(new FamilyHold("web", "core", C, T0, 2), PinMoves.Hold(Edge, [first, second], T0.AddDays(9)));
    }

    [Fact]
    public void A_later_move_that_lands_ends_the_hold_and_the_next_red_starts_a_new_age()
    {
        var red = Quest("q1", "Declined", 1, B, updated: T0);
        var done = Quest("q2", "Done", 2, C, updated: T0.AddDays(1));
        var again = Quest("q3", "Declined", 3, "d".PadLeft(40, 'd'), updated: T0.AddDays(5));

        Assert.Null(PinMoves.Hold(Edge, [red, done], T0.AddDays(9)));
        Assert.Equal(1, PinMoves.Hold(Edge, [red, Quest("q2", "Open", 2, C)], T0.AddDays(9))!.Generation);
        Assert.Equal(T0.AddDays(5), PinMoves.Hold(Edge, [red, done, again], T0.AddDays(9))!.Since);
    }

    [Fact]
    public void A_red_move_with_no_time_ages_from_the_look_that_found_it()
    {
        var red = Quest("q1", "Declined", 1, B) with { Updated = null };

        Assert.Equal(T0.AddDays(4), PinMoves.Hold(Edge, [red], T0.AddDays(4))!.Since);
    }

    [Fact]
    public void No_hold_stands_for_an_edge_none_has_declined()
    {
        Assert.Null(PinMoves.Hold(Edge, [], T0));
        Assert.Null(PinMoves.Hold(Edge, [Quest("q1", "Open", 1, B), Quest("q2", "Taken", 2, C)], T0));
    }

    [Fact]
    public void The_person_is_told_only_past_the_edge_s_lag_bound_and_never_where_none_is_declared()
    {
        var held = new FamilyHold("web", "core", B, T0, 1);
        var lag = TimeSpan.FromDays(3);

        Assert.False(PinMoves.Overdue(held, lag, T0.AddDays(3)));
        Assert.True(PinMoves.Overdue(held, lag, T0.AddDays(3).AddMinutes(1)));
        Assert.False(PinMoves.Overdue(held, null, T0.AddDays(30)));
    }

    [Fact]
    public void A_hold_ages_from_its_first_red_and_says_how_long()
    {
        var held = new FamilyHold("web", "core", B, T0, 1);

        Assert.Equal(TimeSpan.FromHours(50), held.Age(T0.AddHours(50)));
    }
}
