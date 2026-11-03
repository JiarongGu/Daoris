using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// COMMS1e (the comms design §5.2, §13 point 7): a person's press of an offered choice names its question, which the say door keeps on
/// the word and checks inside its one step against the question on file. Their free words name none and are never read as a choice.
/// </summary>
public sealed class PersonPressTests : GoAheadFixture
{
    private string Home => Path.Combine(_root, "home");

    private SessionLedger Ledger => new(_quests, _sessions, null, _asks, Home);

    private async Task<Session> Parked(string question = "q-1")
    {
        var (_, quest) = await Asked($"{Sentence} for the first report");
        var session = await Working(quest, "first");
        await _ledger.AdvanceAsync(session.Id, "awaiting-person", "Which report?", null, null, Now);
        File(session.Id, question);
        return session;
    }

    private void File(string session, string question)
    {
        var path = QuestionBox.PathOf(Home, session);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, $$"""
            { "id": "{{question}}", "session": "{{session}}", "question": "Which report?",
              "choices": [ { "label": "The monthly one", "does": "I push." }, { "label": "The weekly one", "does": "I write it." } ],
              "recommend": 1, "by": "now" }
            """);
    }

    private Task<SessionSayOutcome> Press(string session, string? question, string key = "press-1", string text = "The monthly one") =>
        Ledger.SayAsync(session, text, null, Now.AddMinutes(1), key: key, question: question);

    [Fact]
    public async Task A_press_of_the_current_question_is_kept_with_its_id_as_the_persons_word()
    {
        var parked = await Parked();

        var kept = await Press(parked.Id, "q-1");

        Assert.Equal(SessionSayRefusal.None, kept.Refusal);
        var word = Assert.Single((await _sessions.FindAsync(parked.Id))!.Said);
        Assert.Equal(("q-1", true, null), (word.Question, word.Persons, word.Choice));
    }

    [Fact]
    public async Task A_press_of_an_older_question_is_refused_and_a_newer_one_is_kept()
    {
        var parked = await Parked();
        File(parked.Id, "q-2");

        var stale = await Press(parked.Id, "q-1");
        Assert.Equal(SessionSayRefusal.QuestionChanged, stale.Refusal);
        Assert.Contains("q-2", stale.Message);
        Assert.Empty((await _sessions.FindAsync(parked.Id))!.Said);

        Assert.Equal(SessionSayRefusal.None, (await Press(parked.Id, "q-2", key: "press-2")).Refusal);
    }

    [Fact]
    public async Task A_double_press_is_taken_once_even_after_the_question_moved_on()
    {
        var parked = await Parked();
        var first = await Press(parked.Id, "q-1");
        File(parked.Id, "q-2");

        var again = await Press(parked.Id, "q-1");

        Assert.Equal((SessionSayRefusal.None, true, first.Word!.Id), (again.Refusal, again.Again, again.Word!.Id));
        Assert.Single((await _sessions.FindAsync(parked.Id))!.Said);
    }

    [Fact]
    public async Task Free_words_name_no_question_and_are_kept_whatever_is_filed_and_a_decision_or_choice_is_still_the_supervisors()
    {
        var parked = await Parked();
        File(parked.Id, "q-9");

        var kept = await Press(parked.Id, null, text: "The monthly one, but call me first");

        Assert.Equal(SessionSayRefusal.None, kept.Refusal);
        var word = Assert.Single((await _sessions.FindAsync(parked.Id))!.Said);
        Assert.Equal((null, null), (word.Question, word.Choice));
        Assert.Equal(SessionSayRefusal.From, (await Ledger.SayAsync(parked.Id, "mine", null, Now, key: "k2", choice: "x", question: "q-9")).Refusal);
        Assert.Equal(SessionSayRefusal.From, (await Ledger.SayAsync(parked.Id, "mine", null, Now, key: "k3", decision: "d")).Refusal);
    }

    [Fact]
    public async Task A_press_held_across_a_hand_over_is_refused_when_forwarded_to_a_head_with_another_question()
    {
        var parked = await Parked();
        File(parked.Id, "q-head");

        Assert.Equal(SessionSayRefusal.QuestionChanged, (await Press(parked.Id, "q-held")).Refusal);
        Assert.Equal(SessionSayRefusal.NoHome, (await _ledger.SayAsync(parked.Id, "x", null, Now, question: "q-head")).Refusal);
    }
}
