using System.Text.Json;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PARKVIEW1 (the park's ask design §2, §4): a driven session's question to the person, one file per session under the home's sessions
/// folder, read as the connector's <c>question_ask</c> writes it and held to its bounds (the shared table
/// <c>fixtures/park-question.json</c>, which the page's reader of the route's answer reads too); gone as each run of its record opens and
/// bound to its park as each concludes; and a word pressed as one of its answers handed as that answer chosen.
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

    /// <summary>The shared table's rows (Fable's read, Q6), by name.</summary>
    private static readonly JsonArray Rows = (JsonNode.Parse(File.ReadAllText(Path.Combine(
        WorkspaceRoot.Folder, "src", "Daoris.Desktop", "Daoris.Desktop.Driver.Tests", "fixtures", "park-question.json")))!["rows"] as JsonArray)!;

    public static TheoryData<string> RowNames => [.. Rows.Select(row => row!["name"]!.GetValue<string>())];

    private static JsonObject RowOf(string name) => (JsonObject)Rows.First(row => row!["name"]!.GetValue<string>() == name)!;

    /// <summary>A row's file with its `widen`ed fields set to that many `x`.</summary>
    private static JsonObject Widened(JsonObject row)
    {
        var file = (JsonObject)row["file"]!.DeepClone();
        if (row["widen"] is JsonObject widen)
        {
            foreach (var (path, length) in widen)
            {
                var parts = path.Split('.');
                var at = parts.Length == 1 ? file : (JsonObject)file[parts[0]]![int.Parse(parts[1])]!;
                at[parts[^1]] = new string('x', length!.GetValue<int>());
            }
        }

        return file;
    }

    /// <summary>The question as `question_ask` writes it, the table's first row, at a moment.</summary>
    private void Asked(string session, string at)
    {
        var file = Widened((JsonObject)Rows[0]!);
        file["session"] = session;
        file["at"] = at;
        Put(session + QuestionFiles.Suffix, file.ToJsonString());
    }

    /// <summary>🔴 Each row of the shared table reads as it says: a question whole, or none past a bound the writer keeps (Q6).</summary>
    [Theory]
    [MemberData(nameof(RowNames))]
    public void Each_row_of_the_shared_table_reads_as_it_says(string name)
    {
        var row = RowOf(name);
        Put("s1" + QuestionFiles.Suffix, Widened(row).ToJsonString());

        var read = new QuestionFiles(_home).Read("s1");

        if (row["read"] is not JsonObject expected)
        {
            Assert.Null(read);
            return;
        }

        Assert.NotNull(read);
        string Words(JsonNode? node) => node is JsonValue value && value.TryGetValue<int>(out var length) ? new string('x', length) : node!.GetValue<string>();
        Assert.Equal(expected["id"]!.GetValue<string>(), read.Id);
        Assert.Equal(Words(expected["question"]), read.Question);
        Assert.Equal(
            [.. expected["choices"]!.AsArray().Select(choice => (choice!["label"]!.GetValue<string>(), choice["does"]!.GetValue<string>()))],
            read.Choices.Select(choice => (choice.Label, choice.Does)));
        Assert.Equal(expected["recommend"]?.GetValue<int>(), read.Recommend);
        Assert.Equal(expected["why"] is { } why ? Words(why) : null, read.Why);
        Assert.Equal(expected["by"]?.GetValue<string>(), read.By);
        Assert.Equal(expected["owner"]?.GetValue<string>(), read.Owner);
    }

    /// <summary>
    /// SUPERVISOR1a-i (the supervisor design §6.3): a park restored after its run's opening cleared its question gets it back from the
    /// decision's snapshot, with the same id, its choices to press.
    /// </summary>
    [Fact]
    public void A_restored_question_reads_back_with_its_id_and_choices()
    {
        var files = new QuestionFiles(_home);
        var asked = new ParkQuestion(
            "q-7", "s1", "Which report?", [new("The monthly one", "I write it."), new("The weekly one", "I write that.")], 2, "today", Opened,
            "It is due.");

        Assert.True(files.Restore(asked));
        var read = files.Read("s1")!;

        Assert.Equal(
            (asked.Id, asked.Question, asked.Recommend, asked.By, asked.Why, asked.At),
            (read.Id, read.Question, read.Recommend, read.By, read.Why, read.At));
        Assert.Equal(asked.Choices, read.Choices);
        Assert.False(files.Restore(asked with { Session = "../escape" }));
    }

    [Fact]
    public void A_file_that_does_not_read_and_an_id_that_is_not_one_are_none()
    {
        Asked("s1", "2026-10-22T09:05:00.0000000+00:00");
        Put("s2" + QuestionFiles.Suffix, "{ not json");
        var files = new QuestionFiles(_home);

        Assert.Equal(DateTimeOffset.Parse("2026-10-22T09:05:00Z"), files.Read("s1")!.At);
        Assert.Null(files.Read("s2"));
        Assert.Null(files.Read("nobody"));
        Assert.Null(files.Read("../escape"));
    }

    /// <summary>🔴 A run that parks keeps the question it asked since it opened: that is the question the park waits on.</summary>
    [Fact]
    public void A_run_that_parks_keeps_the_question_it_asked()
    {
        Asked("s1", "2026-10-22T09:05:00Z");

        Assert.Equal(QuestionSettle.Kept, new QuestionFiles(_home).Settle("s1", "awaiting-person", Opened));

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

        Assert.Equal(QuestionSettle.Gone, new QuestionFiles(_home).Settle("s1", "awaiting-person", Opened));

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

        Assert.Equal(QuestionSettle.Gone, new QuestionFiles(_home).Settle("s1", state, Opened));

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
        Assert.Equal(QuestionSettle.Gone, files.SettleRun("s1", "awaiting-person", Opened, events));
        Assert.Null(files.Read("s1"));

        Asked("s1", "2026-10-22T09:07:00Z");
        Assert.Equal(QuestionSettle.Kept, files.SettleRun("s1", "awaiting-person", Opened, events));
        Assert.NotNull(files.Read("s1"));
    }

    /// <summary>
    /// 🔴 Fable's read, Q3: the protocol door's echo of the prompt it was handed (`user_message_chunk`, an id-less word of the person's)
    /// hands the agent nothing new, so a question asked before the echo is still its last turn's.
    /// </summary>
    [Fact]
    public void The_protocol_door_s_echo_of_its_prompt_is_no_handing()
    {
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        events.Keep("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "target", Text = "Take quest #q1.", At = Opened.AddSeconds(1) }, say: null);
        events.Keep("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Take quest #q1.", At = Opened.AddMinutes(7) }, say: null);
        Asked("s1", "2026-10-22T09:05:00Z");

        Assert.Equal(Opened.AddSeconds(1), events.LastHandedAt("s1"));
        Assert.Equal(QuestionSettle.Kept, new QuestionFiles(_home).SettleRun("s1", "awaiting-person", Opened, events));
    }

    /// <summary>With nothing handed to it on its record, the run's opening is its last handing.</summary>
    [Fact]
    public void With_no_words_on_its_record_the_run_s_opening_binds_it()
    {
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        var files = new QuestionFiles(_home);

        Asked("s1", "2026-10-22T08:59:00Z");
        Assert.Equal(QuestionSettle.Gone, files.SettleRun("s1", "awaiting-person", Opened, events));

        Asked("s1", "2026-10-22T09:01:00Z");
        Assert.Equal(QuestionSettle.Kept, files.SettleRun("s1", "awaiting-person", Opened, events));
    }

    /// <summary>A record with no question settles to none, and an id that is not one touches nothing.</summary>
    [Fact]
    public void Nothing_to_settle_is_no_failure()
    {
        var files = new QuestionFiles(_home);

        Assert.Equal(QuestionSettle.None, files.Settle("s1", "awaiting-person", Opened));
        Assert.Equal(QuestionSettle.None, files.Settle("../escape", "awaiting-person", Opened));
        Assert.Equal(QuestionSettle.None, files.Clear("s1"));
    }

    /// <summary>
    /// 🔴 HELPERWAIT1b (Codex's round-3 #1 on COWORKUX1): a question on file that does not read is no proof none was asked, so the turn
    /// parks for the person (<see cref="QuestionFiles.AskedSince"/> is null), and the park keeps the file, a first run's conclusion and a
    /// resumed run's alike: nothing settles it away unread. The park still shows no half a file, since a read of it is none; a conclusion
    /// that does not park keeps nothing, as before.
    /// </summary>
    [Theory]
    [InlineData("first")]
    [InlineData("resumed")]
    public void An_unreadable_question_stays_with_the_park_it_ends_in(string run)
    {
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        if (run == "resumed")
        {
            // The resumed run's go-on: the person's words handed to it after it opened, so its question binds to after them.
            events.Keep("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Id = "w1", Text = "use the sheet", At = Opened.AddMinutes(2) }, say: null);
        }

        Put("s1" + QuestionFiles.Suffix, "{ \"id\": \"k1\", \"question\": ");
        var files = new QuestionFiles(_home);

        Assert.Null(files.AskedSince("s1", Opened));
        Assert.Equal(QuestionSettle.Kept, files.SettleRun("s1", "awaiting-person", Opened, events));
        Assert.True(File.Exists(PathOf("s1" + QuestionFiles.Suffix)));
        Assert.Null(files.Read("s1"));

        Assert.Equal(QuestionSettle.Gone, files.SettleRun("s1", "completed", Opened, events));
        Assert.False(File.Exists(PathOf("s1" + QuestionFiles.Suffix)));
    }

    /// <summary>
    /// 🔴 Fable's read, Q2: a run of its record opening takes the park's question away, so while it works no press another window still
    /// shows passes as an answer to the question it asks.
    /// </summary>
    [Fact]
    public void A_run_opening_clears_the_park_s_question()
    {
        Asked("s1", "2026-10-22T09:05:00Z");

        Assert.Equal(QuestionSettle.Gone, new QuestionFiles(_home).Clear("s1"));

        Assert.Null(new QuestionFiles(_home).Read("s1"));
    }

    /// <summary>
    /// 🔴 Fable's read, Q1: a reader that shares the file's delete, as every reader of it here does, never holds a settle back; one that
    /// does not share it holds the file past the delete's tries, which is answered <see cref="QuestionSettle.Stayed"/> for the caller
    /// to log, and the file is there to be settled again.
    /// </summary>
    [Fact]
    public void A_read_never_holds_the_delete_back_and_a_held_file_is_said_to_stay()
    {
        Asked("s1", "2026-10-22T08:55:00Z");
        var path = PathOf("s1" + QuestionFiles.Suffix);
        using (var shared = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
        {
            Assert.Equal(QuestionSettle.Gone, new QuestionFiles(_home).Settle("s1", "awaiting-person", Opened));
        }

        Assert.False(File.Exists(path));

        Asked("s1", "2026-10-22T08:55:00Z");
        using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.NotNull(new QuestionFiles(_home).Read("s1"));
            Assert.Equal(QuestionSettle.Stayed, new QuestionFiles(_home).Settle("s1", "awaiting-person", Opened));
            Assert.Equal(QuestionSettle.Stayed, new QuestionFiles(_home).Clear("s1"));
        }

        Assert.True(File.Exists(path));
        Assert.Equal(QuestionSettle.Gone, new QuestionFiles(_home).Clear("s1"));
    }

    /// <summary>
    /// 🔴 Fable's read, Q4: a press is composed as its label, then the person's words after a blank line; the label it begins with is
    /// the answer pressed, the longest first, and words that begin with none are no press.
    /// </summary>
    [Fact]
    public void A_press_is_the_answer_its_words_begin_with_and_is_handed_as_chosen()
    {
        var question = new ParkQuestion("q1", "s1", "Which?", [new("1,200", "a"), new("1,200 now", "b"), new("Later", "c")], null, null, Opened);

        Assert.Equal("1,200", QuestionWords.Pressed(question, "1,200"));
        Assert.Equal("1,200 now", QuestionWords.Pressed(question, "1,200 now\n\nand say so"));
        Assert.Equal("Later", QuestionWords.Pressed(question, "Later\n\nnot today"));
        Assert.Null(QuestionWords.Pressed(question, "Later on, maybe"));
        Assert.Null(QuestionWords.Pressed(question, "something else"));

        Assert.Equal("chose: 1,200\n\nonly the menu entry", QuestionWords.Handed("1,200\n\nonly the menu entry", "1,200"));
        Assert.Equal("chose: Later", QuestionWords.Handed("Later", "Later"));
        Assert.Equal("1,350 instead", QuestionWords.Handed("1,350 instead", "1,200"));
        Assert.Equal("plain words", QuestionWords.Handed("plain words", null));
    }

    /// <summary>
    /// 🔴 Fable's read, Q4: the word a press kept is marked with its answer by the event it was shown under, so the resumed run is
    /// handed it as that answer chosen, on both doors, and its record says the mark again where the session takes it; the person's
    /// other words go as they are.
    /// </summary>
    [Fact]
    public void A_pressed_word_is_handed_as_its_answer_chosen_and_recorded_so()
    {
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        events.Keep("s1", new SessionEvent
        {
            Kind = SessionEventKind.User, Origin = "person", Id = "w1", Text = "1,200\n\nonly the menu entry", Reaches = "resume", Choice = "1,200",
        }, say: null);
        events.Keep("s1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Id = "w2", Text = "and say so", Reaches = "resume" }, say: null);
        IReadOnlyList<SaidWordView> said =
        [
            new("w1", "1,200\n\nonly the menu entry", Opened, [], false),
            new("w2", "and say so", Opened, [], false),
        ];

        var words = Continuations.Marked(said, events.ChoicesOf("s1"));
        var ask = new ResumeAsk("conversation", words, "It goes on.");

        Assert.Equal(["1,200", null], words.Select(word => word.Choice));
        Assert.Equal("chose: 1,200\n\nonly the menu entry\n\nand say so", ask.Prompt);
        Assert.Equal(["chose: 1,200\n\nonly the menu entry", "and say so"], ask.Blocks);
        Assert.Equal(["1,200", null], ask.Opening().Where(e => e.Kind == SessionEventKind.User).Select(e => e.Choice));
        Assert.Empty(events.ChoicesOf("nobody"));
    }
}
