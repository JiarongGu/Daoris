using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HELPERWAIT1 (D163's and D188's notes): the rules a driven session waiting on its own helper is read by, in process. An ask is its own
/// while its agent made it, and open until it is refused or its answer is handed; a session ended waiting on its helper is completed by
/// that code, with nothing waiting on it; and a go-ahead it asked and the person has not answered is a real question, never a wait on a helper.
/// </summary>
public sealed class HelperWaitsTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-23T10:00:00Z");

    private static HelperAsk Ask(string id = "a1", string asker = "s1", string by = HelperAsk.ByAgent, int minute = 0) =>
        new(id, asker, "Why does the report drop its last day?", "codex-acp", by, At.AddMinutes(minute));

    private static Func<string, HelperState?> States(params HelperState[] states) =>
        id => states.FirstOrDefault(state => state.Id == id);

    private static HelperState State(string id, string state, string? handed = null, string? code = null) =>
        new(id, state, code, "h1", At) { Handed = handed, Agent = "codex-acp" };

    private static NotePart Part(NoteCode code) => code.Part("a line.", []);

    [Fact]
    public void An_ask_is_open_while_its_agent_made_it_and_it_is_neither_refused_nor_handed()
    {
        var asks = new[]
        {
            Ask("a1"), Ask("a2"), Ask("a3"), Ask("a4"), Ask("a5"), Ask("a6", by: HelperAsk.ByPerson), Ask("a7", asker: "s2"),
        };
        var states = States(
            State("a2", HelperStates.Started), State("a3", HelperStates.Answered), State("a4", HelperStates.Answered, handed: "w1"),
            State("a5", HelperStates.Refused, code: HelperWhy.NoAnswer));

        Assert.Equal(["a1", "a2", "a3"], HelperWaits.Open("s1", asks, states).Select(ask => ask.Id));
        Assert.Empty(HelperWaits.Open("s9", asks, states));
    }

    [Fact]
    public void A_session_waits_on_its_helper_while_it_ended_so_and_nothing_waits_on_it()
    {
        var waiting = new PriorSession("s1", "trees/s-1", "completed") { Quest = "q1", NoteParts = [Part(NoteCodes.EndedAwaitsHelper)], Said = [] };

        Assert.True(HelperWaits.Waits(waiting));
        Assert.False(HelperWaits.Waits(waiting with { Said = [new SaidWordView("w1", "the answer", At, [], true) { Helper = "h1" }] }));
        Assert.False(HelperWaits.Waits(waiting with { NoteParts = [Part(NoteCodes.EndedAwaits)] }));
        Assert.False(HelperWaits.Waits(waiting with { State = "working" }));
        Assert.False(HelperWaits.Waits(waiting with { Session = "laptop/s1" }));
    }

    /// <summary>A go-ahead the session asked that the person has not answered is a real question; one answered, or another's, is not.</summary>
    [Fact]
    public void A_go_ahead_it_asked_and_the_person_has_not_answered_is_a_real_question()
    {
        GoAheadView GoAhead(string session, bool answered) =>
            new(1, "push", "origin", "main", [new GoAheadRequestView(session, "q1", At, "to land it")])
            {
                Answer = answered ? new GoAheadAnswerView(true, null, At) : null,
            };

        Assert.True(HelperWaits.AskedGoAhead("s1", new AskWords("a1", []) { GoAheads = [GoAhead("s1", answered: false)] }));
        Assert.False(HelperWaits.AskedGoAhead("s1", new AskWords("a1", []) { GoAheads = [GoAhead("s1", answered: true)] }));
        Assert.False(HelperWaits.AskedGoAhead("s1", new AskWords("a1", []) { GoAheads = [GoAhead("s2", answered: false)] }));
        Assert.False(HelperWaits.AskedGoAhead("s1", AskWords.Unread("a1")));
        Assert.False(HelperWaits.AskedGoAhead("s1", null));
    }

    /// <summary>
    /// The reason a session waiting on its helper comes back to the person: its newest refused ask's code and agent, or no answer where
    /// nothing of its own is on file any more.
    /// </summary>
    [Fact]
    public void The_reason_it_comes_back_is_its_newest_refused_asks()
    {
        var asks = new[] { Ask("a1", minute: 0), Ask("a2", minute: 3), Ask("a3", asker: "s2", minute: 9) };
        var states = States(
            State("a1", HelperStates.Refused, code: HelperWhy.Declined) with { Agent = null },
            State("a2", HelperStates.Refused, code: HelperWhy.NoAnswer),
            State("a3", HelperStates.Refused, code: HelperWhy.Off));

        var why = HelperWaits.Why("s1", asks, states);
        Assert.Equal(("helper.refused-by", "codex-acp", "no-answer"), (why.Parts[0].Code, why.Parts[0].Value("agent"), why.Parts[0].Value("why")));
        Assert.Equal(("helper.refused", "no-answer"), (HelperWaits.Why("s9", asks, states).Parts[0].Code, HelperWaits.Why("s9", asks, states).Parts[0].Value("why")));
    }
}
