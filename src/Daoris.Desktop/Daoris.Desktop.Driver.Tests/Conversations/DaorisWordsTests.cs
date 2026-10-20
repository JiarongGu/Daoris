using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COWORK1b (D188's COWORK1b note; the message-urgency design §9, trap 6): Daoris's own words to a session are marked as Daoris's,
/// read back so, and never taken for the person's: in the record a resumed run opens with, in the record of a word held while a
/// session works, and in the lines that say whose words a session goes on with. A host from before the mark keeps none, and its
/// word reads as the person's, as every word did.
/// </summary>
public sealed class DaorisWordsTests
{
    private static readonly DateTimeOffset At = DateTimeOffset.Parse("2026-10-20T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static SaidWordView Word(string json) => ServiceClient.ReadWord(JsonDocument.Parse(json).RootElement)!;

    private static SaidWordView Persons() => new("w0", "Also add the changelog line.", At, [], Reopens: true);

    private static SaidWordView Daoris() =>
        new("w1", "Daoris: the person reopened it.", At, [], Reopens: true) { From = DaorisWords.From, Key = "reopen/q1/desk/44" };

    [Fact]
    public void A_word_marked_daoris_reads_as_daoris_with_its_key_and_an_older_hosts_word_as_the_persons()
    {
        var daoris = Word("""{ "id": "w1", "text": "Daoris: reopened.", "at": "2026-10-20T10:00:00Z", "files": [], "reopens": true, "from": "daoris", "key": "reopen/q1/desk/44" }""");
        var older = Word("""{ "id": "w2", "text": "Daoris: reopened.", "at": "2026-10-20T10:01:00Z", "files": [], "reopens": true }""");

        Assert.Equal(("daoris", "reopen/q1/desk/44", false, true), (daoris.From, daoris.Key, daoris.Persons, daoris.Daoris));
        Assert.Equal(((string?)null, (string?)null, true, false), (older.From, older.Key, older.Persons, older.Daoris));
    }

    [Fact]
    public void A_resumed_run_opens_with_daoris_words_as_daoris_and_never_as_the_persons()
    {
        var ask = new ResumeAsk("conv-1", [Persons(), Daoris()], "— your words are the next turn of its own conversation.");

        var said = ask.Opening().Where(each => each.Kind == SessionEventKind.User).ToList();

        Assert.Equal([("person", null), ("target", "daoris")], said.Select(each => (each.Origin, each.From)));
        Assert.Equal(["w0", "w1"], said.Select(each => each.Id));
    }

    [Fact]
    public void A_daoris_word_held_for_a_running_session_is_recorded_as_daoris()
    {
        var daoris = ChatMessage.Words(new ChatMessage("Daoris: reopened.", []) { Id = "said-1", Daoris = new DaorisWord("reopen/q1/desk/44") });
        var person = ChatMessage.Words(new ChatMessage("And the readme.", []) { Id = "said-2" });

        Assert.Equal(("target", "daoris", "said-1"), (daoris.Origin, daoris.From, daoris.Id));
        Assert.Equal(("person", (string?)null), (person.Origin, person.From));
    }

    [Fact]
    public void The_lines_a_session_goes_on_with_say_daoris_words_and_never_your_words()
    {
        var opening = Continuations.OpeningNoted("claude-code", null, null, answer: false, persons: false, daoris: true);
        Assert.Equal("— Daoris's words are the next turn of its own conversation, resumed on `claude-code`.", opening.Note);
        Assert.Equal(ResumedWhy.Daoris, opening.Parts[0].Value("why"));
        // Beside the person's words, the person's line stands, as it does beside a helper's answer.
        Assert.Equal(
            ResumedWhy.Words,
            Continuations.OpeningNoted("claude-code", null, null, answer: false, persons: true, daoris: true).Parts[0].Value("why"));
        Assert.Equal(
            "— Daoris's words are the next turn of its own conversation, resumed on `claude-code`.",
            ChatGoOn.OpeningNote([Daoris()], "claude-code", null, null).Note);

        Assert.Equal(Continuations.GoingOnWithDaoris, ChatGoOn.GoingOnNote([Daoris()]).Note);
        Assert.Equal(Continuations.GoingOn, ChatGoOn.GoingOnNote([Persons(), Daoris()]).Note);

        var native = NativeWords.Opening("claude-code", [new ChatMessage("Daoris: reopened.", []) { Id = "said-1", Daoris = new DaorisWord("k") }]);
        Assert.Equal(["resumed.native-daoris"], NoteAssert.Codes(native[0].Parts));
        Assert.Equal("— what Daoris told it while it worked is the next turn of its own conversation, resumed on `claude-code`.", native[0].Text);
        Assert.Equal(
            ["resumed.native"],
            NoteAssert.Codes(NativeWords.Opening("claude-code", [new ChatMessage("And the readme.", []) { Id = "said-2" }])[0].Parts));
    }

    /// <summary>The keys Daoris's facts give their words (the design §6): the quest and the reopen's machine and sequence, or the step and the submission it grew from.</summary>
    [Fact]
    public void A_facts_key_names_its_quest_and_submission_and_reads_back()
    {
        var reopen = DaorisKeys.Reopen("q1", "desk", 44);
        var goneOn = DaorisKeys.GoneOn("q2", new QuestSubmissionRef("a@one", 43));

        Assert.Equal(("reopen/q1/desk/44", "gone-on/q2/a@one/43"), (reopen, goneOn));
        Assert.Equal(new DaorisFact(DaorisKeys.ReopenKind, "q1", "desk", 44), DaorisKeys.Read(reopen));
        Assert.Equal(new DaorisFact(DaorisKeys.GoneOnKind, "q2", "a@one", 43), DaorisKeys.Read(goneOn));
        Assert.Null(DaorisKeys.Read("someone-else's/key"));
        Assert.Null(DaorisKeys.Read(null));
    }
}
