using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The note codes the service client's reading of a record judges by: the service owns their spelling, the events'
/// vocabulary (<see cref="NoteCodes"/>) declares them for the page, and the two are held equal here, so a code renamed in one is a
/// failure and never a strike read wrongly. The note's shapes, now the service's, keep the members their callers spell.
/// </summary>
public sealed class RecordNotesTests
{
    [Fact]
    public void Each_code_the_client_judges_by_is_the_vocabularys_declaration()
    {
        Assert.Equal(EndNoteCodes.UpdateClosed.Code, RecordNotes.UpdateClosed);
        Assert.Equal(EndNoteCodes.EndedTransient.Code, RecordNotes.EndedTransient);
        Assert.Equal(EndNoteCodes.EndedChoiceNotOffered.Code, RecordNotes.EndedChoiceNotOffered);
        Assert.Equal(EndNoteCodes.EndedStoppedSteward.Code, RecordNotes.EndedStoppedSteward);
        Assert.Equal(EndNoteCodes.EndedAwaitsHelper.Code, RecordNotes.EndedAwaitsHelper);
        Assert.Equal(EndNoteCodes.StoppedCheckpointStoppedSteward.Code, RecordNotes.StoppedCheckpointStoppedSteward);
        Assert.Equal(AccountNoteCodes.AccountsOwn.Select(code => code.Code), RecordNotes.AccountsOwn);
    }

    [Fact]
    public void A_coded_note_is_composed_from_a_code_as_it_was_and_a_conclusion_carries_it()
    {
        var noted = Noted.Of(EndNoteCodes.EndedTransient, "its provider kept failing.", ("count", 3), ("minutes", 10));
        var part = Assert.Single(noted.Parts);
        Assert.Equal(EndNoteCodes.EndedTransient.Code, part.Code);
        Assert.Equal("its provider kept failing.", part.Text);
        Assert.Equal(3, part.Value("count"));

        var conclusion = SessionConclusion.Of("failed", noted);
        Assert.Equal("failed", conclusion.State);
        Assert.Equal(noted.Note, conclusion.Note);
        Assert.Same(noted.Parts, conclusion.Parts);
        Assert.Equal(noted.Note, conclusion.AsNoted().Note);
    }

    [Fact]
    public void A_registration_outcome_is_said_in_the_looks_words_through_the_events_member()
    {
        var followed = new RegistrationFollowed("game", RegistryOutcome.Registered, "registered", Sent: true);
        var said = new LookSaid(null, () => null);
        Assert.Equal(RegistrationFollow.EventLine(followed), RegistrationFollow.EventLine(followed, said));
        Assert.Equal("registry  game: registered", RegistrationFollow.EventLine(followed));
    }
}
