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

    [Fact]
    public void A_refused_turn_with_no_words_of_the_person_s_still_says_the_limit_with_none_held()
    {
        var held = LimitHeld.Of([NotTaken(), Cooling(NoteCodes.AccountCooling), Turn("error")])!;

        Assert.Empty(held.Words);
        Assert.Equal(3, held.Before);
    }
}
