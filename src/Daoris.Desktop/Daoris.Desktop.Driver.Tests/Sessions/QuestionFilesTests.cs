using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PARKVIEW1 (the park's ask design §2): a driven session's question to the person, one file per session under the home's sessions
/// folder, read as the connector's <c>question_ask</c> writes it (the service's <c>McpQuestionToolTests</c> hold the same shape), and
/// bound to its park by the driver at each run's conclusion: kept only where the run parks and the question was asked since the run
/// opened, deleted otherwise, so a question on file while the record is parked is that park's.
/// </summary>
public sealed class QuestionFilesTests : IDisposable
{
    private static readonly DateTimeOffset Opened = DateTimeOffset.Parse("2026-10-22T09:00:00Z");

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "questions", Guid.NewGuid().ToString("N")[..8]);

    public QuestionFilesTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private string PathOf(string name) => DaorisHome.Entry(_home, HomeEntry.Sessions, name);

    private void Put(string name, string text) => File.WriteAllText(PathOf(name), text);

    /// <summary>The question as `question_ask` writes it, at a moment.</summary>
    private void Asked(string session, string at) => Put(session + QuestionFiles.Suffix, $$"""
        {
          "id": "a1b2c3d4",
          "session": "{{session}}",
          "question": "Which quarterly target should the dashboard tile show?",
          "choices": [
            { "label": "1,200", "does": "Write 1,200, the planning sheet's value." },
            { "label": "1,350", "does": "Write 1,350, the stand-up note's value." }
          ],
          "recommend": 1,
          "by": "before Friday's release",
          "at": "{{at}}"
        }
        """);

    [Fact]
    public void A_question_is_read_as_question_ask_writes_it_and_an_unreadable_one_is_none()
    {
        Asked("s1", "2026-10-22T09:05:00.0000000+00:00");
        Put("s2" + QuestionFiles.Suffix, "{ not json");
        Put("s3" + QuestionFiles.Suffix, """{ "id": "c1", "session": "s3" }""");
        Put("s4" + QuestionFiles.Suffix, """{ "id": "c2", "question": "Free?", "choices": [{ "does": "no label" }, { "label": "Yes" }], "recommend": 5 }""");
        var files = new QuestionFiles(_home);

        var question = files.Read("s1")!;

        Assert.Equal(("a1b2c3d4", "s1", "Which quarterly target should the dashboard tile show?", 1, "before Friday's release"),
            (question.Id, question.Session, question.Question, question.Recommend, question.By));
        Assert.Equal([("1,200", "Write 1,200, the planning sheet's value."), ("1,350", "Write 1,350, the stand-up note's value.")],
            question.Choices.Select(choice => (choice.Label, choice.Does)));
        Assert.Equal(DateTimeOffset.Parse("2026-10-22T09:05:00Z"), question.At);
        Assert.Null(files.Read("s2"));
        Assert.Null(files.Read("s3"));
        // An answer with no label is no answer, one with no words for what it does says none, and a recommendation that names no
        // answer kept is none.
        var free = files.Read("s4")!;
        Assert.Equal([("Yes", "")], free.Choices.Select(choice => (choice.Label, choice.Does)));
        Assert.Null(free.Recommend);
        Assert.Null(files.Read("nobody"));
        Assert.Null(files.Read("../escape"));
    }

    /// <summary>🔴 A run that parks keeps the question it asked since it opened: that is the question the park waits on.</summary>
    [Fact]
    public void A_run_that_parks_keeps_the_question_it_asked()
    {
        Asked("s1", "2026-10-22T09:05:00Z");

        Assert.True(new QuestionFiles(_home).Settle("s1", "awaiting-person", Opened));

        Assert.NotNull(new QuestionFiles(_home).Read("s1"));
    }

    /// <summary>
    /// 🔴 A run that parks again without asking leaves no question from an earlier run: the one on file was asked before this run
    /// opened, already answered by what sent the session on, so it goes.
    /// </summary>
    [Fact]
    public void A_run_that_parks_without_asking_leaves_no_earlier_question()
    {
        Asked("s1", "2026-10-22T08:55:00Z");

        Assert.False(new QuestionFiles(_home).Settle("s1", "awaiting-person", Opened));

        Assert.False(File.Exists(PathOf("s1" + QuestionFiles.Suffix)));
    }

    /// <summary>🔴 A run that does not park keeps no question, whatever it asked: nothing waits on the person's answer to it.</summary>
    [Theory]
    [InlineData("completed")]
    [InlineData("failed")]
    [InlineData("stopped")]
    [InlineData("declined")]
    [InlineData("stood-down")]
    public void A_run_that_does_not_park_keeps_no_question(string state)
    {
        Asked("s1", "2026-10-22T09:05:00Z");

        Assert.False(new QuestionFiles(_home).Settle("s1", state, Opened));

        Assert.False(File.Exists(PathOf("s1" + QuestionFiles.Suffix)));
    }

    /// <summary>
    /// 🔴 A run that went on in the same record with the person's words (MSG1b: held while it worked, then its next prompt, on either
    /// door) binds the question to its last turn: one asked before those words were handed to it goes, since they may have answered
    /// it, and one asked after them is the park's.
    /// </summary>
    [Fact]
    public void The_question_binds_to_the_run_s_last_handing_of_words()
    {
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        events.Keep("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "Take quest #q1.", At = Opened.AddSeconds(1) }, say: null);
        events.Keep("s1", new SessionEvent { Kind = SessionEventKind.Message, Text = "Which target?", At = Opened.AddMinutes(4) }, say: null);
        // The person's word, said while it worked, then written to its harness as its next prompt after the question.
        events.Keep("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Id = "w1", Text = "use the sheet", At = Opened.AddMinutes(2) }, say: null);
        events.Keep("s1", new SessionEvent { Kind = SessionEventKind.Receipt, Id = "w1", Receipt = Receipts.Sent, At = Opened.AddMinutes(6) }, say: null);
        events.Keep("s1", new SessionEvent { Kind = SessionEventKind.Message, Text = "Done with that; parked.", At = Opened.AddMinutes(8) }, say: null);
        var files = new QuestionFiles(_home);

        Asked("s1", "2026-10-22T09:05:00Z");
        Assert.False(files.SettleRun("s1", "awaiting-person", Opened, events));
        Assert.Null(files.Read("s1"));

        Asked("s1", "2026-10-22T09:07:00Z");
        Assert.True(files.SettleRun("s1", "awaiting-person", Opened, events));
        Assert.NotNull(files.Read("s1"));
    }

    /// <summary>With nothing handed to it on its record, the run's opening is its last handing.</summary>
    [Fact]
    public void With_no_words_on_its_record_the_run_s_opening_binds_it()
    {
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        var files = new QuestionFiles(_home);

        Asked("s1", "2026-10-22T08:59:00Z");
        Assert.False(files.SettleRun("s1", "awaiting-person", Opened, events));

        Asked("s1", "2026-10-22T09:01:00Z");
        Assert.True(files.SettleRun("s1", "awaiting-person", Opened, events));
    }

    /// <summary>A record with no question settles to none, and an id that is not one touches nothing.</summary>
    [Fact]
    public void Nothing_to_settle_is_no_failure()
    {
        var files = new QuestionFiles(_home);

        Assert.False(files.Settle("s1", "awaiting-person", Opened));
        Assert.False(files.Settle("../escape", "awaiting-person", Opened));
    }

    /// <summary>An unreadable question is no question to keep: it goes, so a park never shows half a file.</summary>
    [Fact]
    public void An_unreadable_question_goes_at_the_settle()
    {
        Put("s1" + QuestionFiles.Suffix, "{ not json");

        Assert.False(new QuestionFiles(_home).Settle("s1", "awaiting-person", Opened));

        Assert.False(File.Exists(PathOf("s1" + QuestionFiles.Suffix)));
    }
}
