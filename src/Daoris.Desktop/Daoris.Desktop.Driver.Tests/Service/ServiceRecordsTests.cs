using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The records the service client returns, now the service's (FOUNDATION1 mod-iv): each reads what it read, and the words the higher
/// subdomains spell (an author's mark, a session's kind, a pressed answer) are the service's own spellings, which theirs answer through.
/// </summary>
public sealed class ServiceRecordsTests
{
    [Fact]
    public void An_authors_mark_and_a_sessions_kind_are_spelled_once_and_the_planners_and_the_words_answer_through_it()
    {
        Assert.Equal(SaidFrom.Daoris, DaorisWords.From);
        Assert.Equal(SaidFrom.Supervisor, SupervisorHanded.From);
        Assert.Equal(SessionKinds.Driven, ActiveSessions.DrivenKind);
        Assert.Equal(SessionKinds.Chat, ActiveSessions.ChatKind);
        Assert.Equal("driven", new SessionView("s", "game").Kind);
    }

    [Fact]
    public void A_word_tells_its_author_by_its_mark_and_is_handed_as_a_press_marks_it()
    {
        var at = DateTimeOffset.UnixEpoch;
        var person = new SaidWordView("1", "go on", at, [], false);
        var daoris = person with { From = SaidFrom.Daoris };
        var supervisor = person with { From = SaidFrom.Supervisor };
        Assert.True(person.Persons);
        Assert.True(daoris.Daoris);
        Assert.False(daoris.Persons);
        Assert.True(supervisor.Supervisors);

        var pressed = person with { Text = "Yes\n\nbut quickly", Choice = "Yes" };
        Assert.Equal(PressedWords.Chose + "Yes\n\nbut quickly", pressed.Handed);
        Assert.Equal(QuestionWords.Handed(pressed.Text, pressed.Choice), pressed.Handed);
        Assert.Equal("go on", person.Handed);
    }

    [Fact]
    public void A_session_that_ended_on_the_stewards_stop_is_told_so_by_its_notes_code()
    {
        var stopped = new PriorSession("s", null, "stopped") { NoteParts = [new NotePart { Code = RecordNotes.EndedStoppedSteward }] };
        Assert.True(stopped.PersonStopped);
        Assert.True(stopped.StewardStopped);
        Assert.False((stopped with { NoteParts = [new NotePart { Code = RecordNotes.EndedTransient }] }).StewardStopped);
    }
}
