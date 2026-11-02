using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COMMS1d (the comms design §3.4): the exchanges of a line, read from the look's snapshot alone. Delivery and outcome stay apart, the
/// read is equal across a restart, a source not read is said, and a head after a hand-over holds its predecessor's open exchanges.
/// </summary>
public sealed class ExchangesTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-11-02T10:00:00Z");

    private static HelperAsk Ask(string id, string asker = "s1", string by = HelperAsk.ByAgent) =>
        new(id, asker, "Why?", "codex-acp", by, At);

    private static HelperState State(string id, string state, string? handed = null) =>
        new(id, state, null, "h1", At) { Handed = handed, Agent = "codex-acp" };

    private static ExchangeLook Look(
        IReadOnlyList<HelperAsk> asks, params HelperState[] states) =>
        new()
        {
            Helpers = asks,
            HelperState = id => states.FirstOrDefault(state => state.Id == id),
            Pieces = [],
            PieceState = _ => null,
            Questions = [],
            Said = [],
            HandOvers = [],
            HandOverState = _ => null,
            Quests = [],
            Opinions = [],
            GoAheads = [],
        };

    [Fact]
    public void An_answer_kept_but_not_handed_is_delivered_and_answered_and_still_held()
    {
        var look = Look([Ask("a1"), Ask("a2")], State("a1", HelperStates.Answered), State("a2", HelperStates.Answered, handed: "w1"));

        var read = Exchanges.Of(look, "s1").ToDictionary(exchange => exchange.Id);

        Assert.Equal((Exchanges.Taken, "answered", true), (read["helper:a1"].Delivery, read["helper:a1"].Outcome, read["helper:a1"].Holds));
        Assert.Equal((Exchanges.Taken, "taken-in", false), (read["helper:a2"].Delivery, read["helper:a2"].Outcome, read["helper:a2"].Holds));
        Assert.All(read.Values, exchange => Assert.Equal((Exchanges.Yes, Exchanges.Read), (exchange.Available, exchange.Source)));
    }

    [Fact]
    public void A_refused_ask_was_not_reached_and_holds_nothing_and_a_persons_ask_never_holds()
    {
        var look = Look([Ask("a1"), Ask("a2", by: HelperAsk.ByPerson)], State("a1", HelperStates.Refused));

        var read = Exchanges.Of(look, "s1").ToDictionary(exchange => exchange.Id);

        Assert.Equal((Exchanges.NotReached, "refused", false), (read["helper:a1"].Delivery, read["helper:a1"].Outcome, read["helper:a1"].Holds));
        Assert.False(read["helper:a2"].Holds);
    }

    [Fact]
    public void The_read_is_equal_across_a_restart()
    {
        var look = Look([Ask("a1"), Ask("a2")], State("a1", HelperStates.Started));

        Assert.Equal(Exchanges.Of(look, "s1"), Exchanges.Of(Look([Ask("a1"), Ask("a2")], State("a1", HelperStates.Started)), "s1"));
    }

    [Fact]
    public void A_source_not_read_is_said_never_empty()
    {
        var look = Look([Ask("a1")]) with { Questions = null, GoAheads = null };

        var read = Exchanges.Of(look, "s1");

        Assert.True(Exchanges.AnyUnread(read));
        Assert.All(
            read.Where(exchange => exchange.Source == Exchanges.Unread),
            exchange => Assert.Equal((Exchanges.NotAvailable, false), (exchange.Available, exchange.Holds)));
        Assert.Equal(["go-ahead:unread", "question:unread"], read.Where(exchange => exchange.Source == Exchanges.Unread).Select(exchange => exchange.Id).Order());
    }

    [Fact]
    public void An_answered_question_is_read_from_the_said_words_question_id_after_its_file_is_gone()
    {
        var look = Look([]) with
        {
            Questions = [],
            Said = [new ExchangeSaid("s1", "q7", At.AddMinutes(2), "chose: yes")],
        };

        var read = Assert.Single(Exchanges.Of(look, "s1"));

        Assert.Equal(("question:q7", "answered", false), (read.Id, read.Outcome, read.Holds));
        Assert.Equal(At.AddMinutes(2), read.Answer!.At);
    }

    [Fact]
    public void A_question_on_file_holds_unless_asked_before_the_run_last_handed_words()
    {
        var question = new ParkQuestion("q1", "s1", "Which?", [], null, null, At);

        var open = Assert.Single(Exchanges.Of(Look([]) with { Questions = [question] }, "s1"));
        var stale = Assert.Single(Exchanges.Of(Look([]) with { Questions = [question], QuestionsSince = At.AddMinutes(1) }, "s1"));

        Assert.True(open.Holds);
        Assert.Equal(("stale", false), (stale.Outcome, stale.Holds));
    }

    [Fact]
    public void A_go_ahead_the_line_asked_holds_until_the_person_answers_it()
    {
        var waiting = new GoAheadView(1, "write", "prod", "config", [new GoAheadRequestView("s1", null, At, "why")]);
        var answered = waiting with { Number = 2, Answer = new GoAheadAnswerView(true, null, At) };

        var read = Exchanges.Of(Look([]) with { Ask = "k1", GoAheads = [waiting, answered] }, "s1").ToDictionary(exchange => exchange.Id);

        Assert.True(read["go-ahead:k1/1"].Holds);
        Assert.Equal(("approved", false), (read["go-ahead:k1/2"].Outcome, read["go-ahead:k1/2"].Holds));
    }

    [Fact]
    public void A_hand_over_head_holds_its_predecessors_open_exchanges()
    {
        var records = new Dictionary<string, PriorSession>
        {
            ["s1"] = new("s1", null, "completed"),
            ["s2"] = new("s2", null, "working") { From = "s1" },
        };
        var look = Look([Ask("a1", asker: "s1"), Ask("a2", asker: "s9")], State("a1", HelperStates.Started)) with
        {
            Records = records,
            HandOvers = [new HandOverAsk("ho1", "s1", "note", At)],
            HandOverState = session => session == "s1" ? new HandOverAskState("ho1", HandOverAskStates.Taken, null, "codex-acp", At) : null,
        };

        var head = Exchanges.Of(look, "s2");
        var alone = Exchanges.Of(look with { Records = null }, "s2");

        Assert.Equal(["hand-over:ho1", "helper:a1"], head.Select(exchange => exchange.Id).Order());
        Assert.True(head.Single(exchange => exchange.Id == "helper:a1").Holds);
        Assert.Equal(("taken-in", false), (head.Single(exchange => exchange.Id == "hand-over:ho1").Outcome, head.Single(exchange => exchange.Id == "hand-over:ho1").Holds));
        Assert.DoesNotContain(alone, exchange => exchange.Id == "helper:a1");
    }

    [Fact]
    public void Its_own_helper_alone_ends_the_turn_on_the_helper()
    {
        var question = new ParkQuestion("q1", "s1", "Which?", [], null, null, At);
        var helper = Look([Ask("a1")]);

        Assert.True(DriverHelpers.OwnHelperHolds(Exchanges.Of(helper, "s1"), "s1"));
        Assert.False(DriverHelpers.OwnHelperHolds(Exchanges.Of(helper with { Questions = [question] }, "s1"), "s1"));
        Assert.False(DriverHelpers.OwnHelperHolds(Exchanges.Of(helper with { Questions = null }, "s1"), "s1"));
        Assert.False(DriverHelpers.OwnHelperHolds(Exchanges.Of(helper with { GoAheads = null }, "s1"), "s1"));
        Assert.False(DriverHelpers.OwnHelperHolds(Exchanges.Of(Look([Ask("a1", asker: "s2")]), "s1"), "s1"));
    }
}
