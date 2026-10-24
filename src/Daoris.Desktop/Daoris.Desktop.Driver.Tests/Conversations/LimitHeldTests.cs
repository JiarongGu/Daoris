using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LIMITGOON1 (the limit-goes-on design §2 item 2): what a conversation whose last turn its account's limit refused holds, read from its
/// record's events alone. The person's words of the refused turns since the last turn that ended otherwise, oldest first; never Daoris's
/// own words, another agent's, or a brief; nothing where the last turn ended otherwise.
/// </summary>
public sealed class LimitHeldTests
{
    private static SessionEvent Person(string text) => new() { Kind = SessionEventKind.User, Origin = "person", Text = text };

    private static SessionEvent Agent(string text) => new() { Kind = SessionEventKind.Message, Text = text };

    private static SessionEvent Turn(string stop) => new() { Kind = SessionEventKind.Turn, StopReason = stop };

    private static SessionEvent Cooling(NoteCode code) => new()
    {
        Kind = SessionEventKind.Note, Text = "The `stub` account this conversation runs on is cooling…", Parts = [new NotePart { Code = code.Code }],
    };

    private static SessionEvent NotTaken() => new() { Kind = SessionEventKind.Note, Text = "the turn could not be taken: …" };

    private static IReadOnlyList<SessionEvent> Refused(string text, NoteCode? code = null) =>
        [Person(text), NotTaken(), Cooling(code ?? NoteCodes.AccountCooling), Turn("error")];

    [Fact]
    public void The_words_of_every_turn_refused_by_the_limit_since_the_last_answered_one_are_held_oldest_first()
    {
        IReadOnlyList<SessionEvent> events =
        [
            Person("Earlier, answered."), Agent("Done."), Turn("end_turn"),
            .. Refused("Make the port configurable."),
            .. Refused("Did you hear me?", NoteCodes.AccountCoolingNoWindow),
        ];

        var held = LimitHeld.Of(events)!;

        Assert.Equal(["Make the port configurable.", "Did you hear me?"], held.Words.Select(word => word.Text));
        // The brief is composed from what stands before the first of them.
        Assert.Equal(3, held.Before);
    }

    [Fact]
    public void A_last_turn_that_ended_otherwise_holds_nothing_and_a_turn_answered_after_a_refusal_clears_it()
    {
        Assert.Null(LimitHeld.Of([Person("Hello."), Agent("Hi."), Turn("end_turn")]));
        Assert.Null(LimitHeld.Of([.. Refused("Make the port configurable."), Person("Again."), Agent("Done."), Turn("end_turn")]));
        // A turn refused otherwise, its account not cooling, is no limit.
        Assert.Null(LimitHeld.Of([Person("Hello."), NotTaken(), Turn("error")]));
        // The person's stop ends a turn that was not refused.
        Assert.Null(LimitHeld.Of([Person("Hello."), Turn("cancelled")]));
        Assert.Null(LimitHeld.Of([]));

        var after = LimitHeld.Of([.. Refused("First."), Person("Second."), Agent("Done."), Turn("end_turn"), .. Refused("Third.")])!;
        Assert.Equal(["Third."], after.Words.Select(word => word.Text));
        Assert.Equal(7, after.Before);
    }

    [Fact]
    public void Only_the_person_s_words_are_held_never_Daoris_s_another_agent_s_or_a_brief()
    {
        IReadOnlyList<SessionEvent> events =
        [
            new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "Daoris's brief of the line." },
            new SessionEvent { Kind = SessionEventKind.User, Origin = "target", From = "daoris", Text = "A step's go-on." },
            new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Helper = "h1", Text = "Another agent says five." },
            Person("The person's own."),
            NotTaken(), Cooling(NoteCodes.AccountCoolingWindow), Turn("error"),
        ];

        var held = LimitHeld.Of(events)!;

        Assert.Equal(["The person's own."], held.Words.Select(word => word.Text));
        Assert.Equal(3, held.Before);
    }

    private static SessionEvent Wait() => new() { Kind = SessionEventKind.Transient, Next = new DateTimeOffset(2026, 10, 24, 9, 1, 0, TimeSpan.Zero) };

    private static SessionEvent Try(int number) => new() { Kind = SessionEventKind.Transient, Try = number };

    /// <summary>
    /// 🔴 AGENTERRORS1b (D210, the agent-errors design §6.1, round 3's R2-C2 and R3-O6): a limit that ends a transient episode holds only the
    /// words no try carried. The failed turn's own words went on the wire with the prompt the provider's failure answered, and so did the
    /// words each try carried; none is held to be sent again at the wake or the move, however the episode ended. The episode's quiet events
    /// part them from the words asked after.
    /// </summary>
    [Fact]
    public void A_limit_ending_a_transient_episode_holds_no_word_the_failed_turn_or_a_try_carried()
    {
        IReadOnlyList<SessionEvent> events =
        [
            Person("Earlier, answered."), Agent("Done."), Turn("end_turn"),
            // The failed turn's own words, then the episode: a wait, a try that carried a word the agent took before it failed, a wait, a try.
            Person("Make the port configurable."),
            Wait(), Try(1), Person("Use 9090."), Wait(), Try(2),
            NotTaken(), Cooling(NoteCodes.AccountCooling), Turn("error"),
        ];

        var held = LimitHeld.Of(events)!;

        Assert.Empty(held.Words);
        Assert.Equal(events.Count, held.Before);
    }

    /// <summary>The words asked after the episode's last quiet event are no try's: a limit holds them, as it holds a refused turn's words.</summary>
    [Fact]
    public void A_word_asked_after_the_episode_s_last_event_is_still_held_when_a_limit_refuses_it()
    {
        IReadOnlyList<SessionEvent> events =
        [
            Person("Make the port configurable."), Wait(), Try(1), Wait(), Try(2),
            Person("And add a test."), NotTaken(), Cooling(NoteCodes.AccountCooling), Turn("error"),
        ];

        var held = LimitHeld.Of(events)!;

        Assert.Equal(["And add a test."], held.Words.Select(word => word.Text));
        Assert.Equal(5, held.Before);
    }

    /// <summary>An episode a try answered ends no limit, and its quiet events change nothing for the next turn: held words are the next turn's alone.</summary>
    [Fact]
    public void An_episode_a_try_answered_holds_nothing_and_leaves_the_next_refused_turn_its_own_words()
    {
        IReadOnlyList<SessionEvent> events =
        [
            Person("Make the port configurable."), Wait(), Try(1), Agent("Done."), Turn("end_turn"),
            .. Refused("Now the tests."),
        ];

        var held = LimitHeld.Of(events)!;

        Assert.Equal(["Now the tests."], held.Words.Select(word => word.Text));
        Assert.Equal(5, held.Before);
        Assert.Null(LimitHeld.Of(events.Take(5).ToList()));
    }

    [Fact]
    public void A_refused_turn_with_no_words_of_the_person_s_still_says_the_limit_with_none_held()
    {
        var held = LimitHeld.Of([NotTaken(), Cooling(NoteCodes.AccountCooling), Turn("error")])!;

        Assert.Empty(held.Words);
        Assert.Equal(3, held.Before);
    }
}
