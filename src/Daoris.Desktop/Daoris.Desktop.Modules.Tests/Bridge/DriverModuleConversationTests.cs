using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A conversation over the bridge (`ConversationRoutes.cs`, MOD5): its record, its start, the
/// person's messages and files, finishing, stopping a turn, and its queue. The rows where a real process
/// holds the session are <see cref="DriverModuleConversationProcessTests"/>'s.
/// </summary>
public sealed class DriverModuleConversationTests : DriverModuleBridge
{
    /// <summary>
    /// MSG1d (D137 §2.2, §5.3): before the loop's service answers, words to a session nothing here runs have nowhere to be
    /// kept, which is a sentence the person can wait out, never a quiet false.
    /// </summary>
    [Fact]
    public async Task Words_to_a_session_nothing_here_runs_before_the_loop_is_up_say_so()
    {
        var refusal = await RefusalAsync(Module(), "SESSION_INPUT", new { id = "nothing-here", text = "hello" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
    }

    /// <summary>
    /// MSG1d (D137 §2.2, §3.1, §5.3): words to a session of this machine's that ended go to the service's say door, which
    /// keeps them on its record; the conversation shows them at once as the person's, under the id the record gave them,
    /// with the reach <c>resume</c> and the door they were said at; the loop is nudged to take them up now; and the page is
    /// told they are held. They are kept on the ask once a session takes them, so nothing goes to the added door.
    /// </summary>
    [Theory]
    [InlineData("completed")]
    [InlineData("declined")]
    [InlineData("failed")]
    [InlineData("stopped")]
    [InlineData("awaiting-person")]
    public async Task Words_to_a_session_that_ended_or_parked_are_kept_on_its_record_shown_at_once_and_the_loop_nudged(string state)
    {
        var ledger = new Ledger { Records = Records(Record("s1", state)) };
        var (loop, module) = await UpAsync(ledger);

        var sent = await AnswerAsync(module, "SESSION_INPUT", new { id = "s1", text = "also the changelog" });

        Assert.True(sent.GetProperty("sent").GetBoolean());
        Assert.Equal("resume", sent.GetProperty("reaches").GetString());
        Assert.Equal(JsonValueKind.Null, sent.GetProperty("why").ValueKind);
        var (_, path, body) = Assert.Single(ledger.Posts);
        Assert.Equal("/api/sessions/s1/say", path);
        using (var said = JsonDocument.Parse(body)) Assert.Equal("also the changelog", said.RootElement.GetProperty("text").GetString());
        var shown = Assert.Single(loop.Events.Page("s1").Events);
        Assert.Equal(
            (SessionEventKind.User, "person", "w1", "also the changelog", "resume", "screen"),
            (shown.Kind, shown.Origin, shown.Id, shown.Text, shown.Reaches, shown.Door));
        Assert.Equal(1, loop.Nudges);
    }

    /// <summary>
    /// ANSWER2 (D131's ANSWER2 note): the person's answer to a parked driven session through the box moves nothing. The say
    /// door keeps it on the parked record and the loop is nudged; the driver's next look is what moves the record to working,
    /// once (the driver's <c>AnswerGoesOnOnceTests</c>). The install's failures read as if the answer had moved it first: the
    /// second move was a second look taking the same record up.
    /// </summary>
    [Fact]
    public async Task An_answer_to_a_park_through_the_box_moves_nothing_and_nudges_the_loop()
    {
        var ledger = new Ledger
        {
            Records = Records(Record("s1", "awaiting-person")),
            Say = id => (HttpStatusCode.OK, $$$"""
                {"session":{"id":"{{{id}}}","state":"awaiting-person"},"message":"Kept for session `{{{id}}}` to go on with.",
                 "said":{"id":"w1","text":"Port 8080.","at":"2026-10-08T20:43:08+00:00","files":[],"reopens":false}}
                """),
        };
        var (loop, module) = await UpAsync(ledger);

        var sent = await AnswerAsync(module, "SESSION_INPUT", new { id = "s1", text = "Port 8080." });

        Assert.Equal((true, "resume"), (sent.GetProperty("sent").GetBoolean(), sent.GetProperty("reaches").GetString()));
        Assert.Equal("/api/sessions/s1/say", Assert.Single(ledger.Posts).Path);
        Assert.Empty(ledger.Moves);
        Assert.Equal(1, loop.Nudges);
    }

    /// <summary>
    /// MSG1d (D137 §2.2): what never goes on is refused by its code, and nothing is posted: a teammate's record (whose
    /// process and conversation are on their machine), an intake, a session that stood down, a session whose quest went on in
    /// a later session here, and a record nothing holds. Ask Daoris's own conversation goes on in itself since ASKHIST1
    /// (<see cref="DriverModuleHelpHistoryTests"/>).
    /// </summary>
    [Theory]
    [InlineData("laptop/s1", """{"id":"laptop/s1","repository":"engine","state":"completed","quest":"q1","kind":"driven","created":"2026-10-03T08:00:00Z"}""", "teammate")]
    [InlineData("s1", """{"id":"s1","repository":"ask #a1","state":"completed","ask":"a1","kind":"driven","created":"2026-10-03T08:00:00Z"}""", "intake")]
    [InlineData("s1", """{"id":"s1","repository":"engine","state":"stood-down","quest":"q1","kind":"driven","created":"2026-10-03T08:00:00Z"}""", "stood-down")]
    [InlineData("s1", """{"id":"s1","repository":"engine","state":"failed","quest":"q1","kind":"driven","created":"2026-10-03T08:00:00Z"},{"id":"s2","repository":"engine","state":"completed","quest":"q1","kind":"driven","created":"2026-10-03T09:00:00Z"}""", "superseded")]
    [InlineData("s9", """{"id":"s1","repository":"engine","state":"completed","quest":"q1","kind":"driven","created":"2026-10-03T08:00:00Z"}""", "not-found")]
    public async Task What_never_goes_on_is_refused_by_its_code_and_nothing_is_kept(string id, string records, string why)
    {
        var ledger = new Ledger { Records = $"[{records}]" };
        var (loop, module) = await UpAsync(ledger);

        var sent = await AnswerAsync(module, "SESSION_INPUT", new { id, text = "one more thing" });
        var queue = await AnswerAsync(module, "SESSION_QUEUE", new { id });

        Assert.False(sent.GetProperty("sent").GetBoolean());
        Assert.Equal((JsonValueKind.Null, why), (sent.GetProperty("reaches").ValueKind, sent.GetProperty("why").GetString()));
        Assert.Equal((JsonValueKind.Null, why), (queue.GetProperty("reaches").ValueKind, queue.GetProperty("why").GetString()));
        Assert.Empty(ledger.Posts);
        Assert.Empty(loop.Events.Page(id.Replace('/', '-')).Events);
        Assert.Equal(0, loop.Nudges);
    }

    /// <summary>
    /// MSG1d: the service judges the record again as it keeps the words, and its refusal is the answer, by the code the
    /// page words: its word for a teammate's record is the page's <c>teammate</c>.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Conflict, "stood-down", "stood-down")]
    [InlineData(HttpStatusCode.Conflict, "not-ours", "teammate")]
    [InlineData(HttpStatusCode.Conflict, "intake", "intake")]
    [InlineData(HttpStatusCode.NotFound, "not-found", "not-found")]
    public async Task The_say_doors_refusal_is_the_answer_by_its_code(HttpStatusCode status, string refusal, string why)
    {
        var ledger = new Ledger
        {
            Records = Records(Record("s1", "completed")),
            Say = _ => (status, $$"""{"error":"Refused.","refusal":"{{refusal}}"}"""),
        };
        var (loop, module) = await UpAsync(ledger);

        var sent = await AnswerAsync(module, "SESSION_INPUT", new { id = "s1", text = "one more thing" });

        Assert.Equal((false, why), (sent.GetProperty("sent").GetBoolean(), sent.GetProperty("why").GetString()));
        Assert.Empty(loop.Events.Page("s1").Events);
    }

    /// <summary>
    /// MSG1d (D137 §2.1, D90, D136): words said as a session winds up are no longer refused. Its record still runs, so the
    /// say door refuses them <c>running</c>; they are held, the page is told so, and once the record moves through this
    /// machine's client the words are kept on it, shown in its conversation and the loop nudged.
    /// </summary>
    [Fact]
    public async Task Words_said_as_a_session_winds_up_are_held_until_its_record_ends_then_kept()
    {
        var ended = false;
        var ledger = new Ledger
        {
            Records = Records(Record("s1", "working")),
            Say = id => ended
                ? (HttpStatusCode.OK, Kept(id, "w7", "and the readme"))
                : (HttpStatusCode.Conflict, """{"error":"Session `s1` is working.","refusal":"running"}"""),
        };
        var (loop, module) = await UpAsync(ledger);

        var sent = await AnswerAsync(module, "SESSION_INPUT", new { id = "s1", text = "and the readme" });

        Assert.True(sent.GetProperty("sent").GetBoolean());
        Assert.Equal("resume", sent.GetProperty("reaches").GetString());
        await UntilAsync(() => ledger.Posts.Count >= 1);
        Assert.Empty(loop.Events.Page("s1").Events);

        ended = true;
        await loop.Service!.AdvanceAsync("s1", "completed", note: "the quest reached done.");

        await UntilAsync(() => loop.Events.Page("s1").Events.Count == 1);
        var shown = Assert.Single(loop.Events.Page("s1").Events);
        Assert.Equal(("w7", "resume", "screen"), (shown.Id, shown.Reaches, shown.Door));
        Assert.Equal(1, loop.Nudges);
    }

    /// <summary>
    /// MSG1d4 (D137's MSG1d note): words held as a session winds up survive a restart. The shell keeps them in a small file
    /// under the home until the record keeps them; a new loop's judge reads them back and tries them as before, so once the
    /// record ends they are kept, shown at the door they were said at, and the file holds nothing more.
    /// </summary>
    [Fact]
    public async Task Words_held_as_a_session_winds_up_survive_a_restart()
    {
        var ended = false;
        var ledger = new Ledger
        {
            Records = Records(Record("s1", "working")),
            Say = id => ended
                ? (HttpStatusCode.OK, Kept(id, "w7", "and the readme"))
                : (HttpStatusCode.Conflict, """{"error":"Session `s1` is working.","refusal":"running"}"""),
        };
        var (before, module) = await UpAsync(ledger);

        var sent = await AnswerAsync(module, "SESSION_INPUT", new { id = "s1", text = "and the readme" });

        Assert.Equal("resume", sent.GetProperty("reaches").GetString());
        Assert.True(File.Exists(SessionWords.HeldPath(Home)));

        // The shell closes with the words still held, and a new one comes up on the same home.
        before.Stop();
        var (after, _) = await UpAsync(ledger);
        ended = true;
        await after.Service!.AdvanceAsync("s1", "completed", note: "the quest reached done.");

        await UntilAsync(() => after.Events.Page("s1").Events.Count == 1);
        var shown = Assert.Single(after.Events.Page("s1").Events);
        Assert.Equal(("w7", "resume", "screen"), (shown.Id, shown.Reaches, shown.Door));
        Assert.Equal(1, after.Nudges);
        await UntilAsync(() => !File.Exists(SessionWords.HeldPath(Home)));
    }

    /// <summary>
    /// A file of held words that does not read holds nothing (MSG1d4): the loop comes up as before, and the next words held
    /// replace it, written whole beside it and renamed.
    /// </summary>
    [Fact]
    public async Task A_file_of_held_words_that_does_not_read_holds_nothing()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SessionWords.HeldPath(Home))!);
        File.WriteAllText(SessionWords.HeldPath(Home), "{ not json");
        var ledger = new Ledger
        {
            Records = Records(Record("s1", "working")),
            Say = _ => (HttpStatusCode.Conflict, """{"error":"Session `s1` is working.","refusal":"running"}"""),
        };
        var (loop, module) = await UpAsync(ledger);

        var sent = await AnswerAsync(module, "SESSION_INPUT", new { id = "s1", text = "and the readme" });

        Assert.True(sent.GetProperty("sent").GetBoolean());
        var written = File.ReadAllText(SessionWords.HeldPath(Home));
        Assert.DoesNotContain("\r", written);
        using var held = JsonDocument.Parse(written);
        var word = Assert.Single(held.RootElement.GetProperty("held").EnumerateArray());
        Assert.Equal(("s1", "and the readme", "screen"), (word.GetProperty("session").GetString(), word.GetProperty("text").GetString(), word.GetProperty("door").GetString()));
        loop.Stop();
    }

    /// <summary>
    /// 🔴 HANDWORDS1 (D163 point 1, D165 point 3): a conversation handed over or moved went on in another, which takes its words. A
    /// word to it is refused <c>continued</c>, naming the latest of the conversations that continue it, as the queue says it too;
    /// nothing is kept on its record, so nothing could restart it in the tree its successor holds.
    /// </summary>
    [Fact]
    public async Task Words_to_a_conversation_that_went_on_are_refused_naming_the_one_that_continues_it()
    {
        var ledger = new Ledger { Records = Records(Chat("c1", "completed"), Chat("c2", "completed", from: "c1"), Chat("c3", "working", from: "c2")) };
        var (loop, module) = await UpAsync(ledger);

        var sent = await AnswerAsync(module, "SESSION_INPUT", new { id = "c1", text = "one more thing" });
        var queue = await AnswerAsync(module, "SESSION_QUEUE", new { id = "c1" });

        Assert.Equal((false, WordsNever.Continued, "c3"), (sent.GetProperty("sent").GetBoolean(), sent.GetProperty("why").GetString(), sent.GetProperty("to").GetString()));
        Assert.Equal((WordsNever.Continued, "c3"), (queue.GetProperty("why").GetString(), queue.GetProperty("to").GetString()));
        var reach = queue.GetProperty("reach");
        Assert.Equal((WordsNever.Continued, "c3"), (reach.GetProperty("why").GetString(), reach.GetProperty("to").GetString()));
        Assert.Empty(ledger.Posts);
        Assert.Empty(loop.Events.Page("c1").Events);
        Assert.Equal(0, loop.Nudges);
    }

    /// <summary>
    /// 🔴 HANDWORDS1: words said while a conversation is handed over or moved (its box kept until the take is done) are held here, never
    /// kept on the record that is ending, even once it has ended; when the take is told done they go to the conversation that
    /// continues it, kept and shown there, and nothing is held any more.
    /// </summary>
    [Fact]
    public async Task Words_said_while_a_conversation_is_handed_over_go_to_the_one_that_continues_it()
    {
        var ledger = new Ledger { Records = Records(Chat("c1", "working")) };
        var (loop, module) = await UpAsync(ledger);
        new HandOverBox(loop.Home).Write("c1", new HandOverRequest("talk", "account-2", HandOverWhy.Curve, HandOverBy.Daoris, DateTimeOffset.UtcNow));

        var sent = await AnswerAsync(module, "SESSION_INPUT", new { id = "c1", text = "and the readme" });

        Assert.Equal((true, "resume"), (sent.GetProperty("sent").GetBoolean(), sent.GetProperty("reaches").GetString()));
        Assert.Empty(ledger.Posts);

        // The take ends the old conversation and opens the one that continues it; until its box is gone the words still wait.
        ledger.Records = Records(Chat("c1", "completed"), Chat("c2", "working", from: "c1"));
        await loop.Service!.AdvanceAsync("c1", "completed", note: "the conversation went on in c2.");
        await Task.Delay(300);
        Assert.Empty(ledger.Posts);

        new HandOverBox(loop.Home).Clear("c1");
        loop.Words.OnTakeEnded("c1");

        await UntilAsync(() => ledger.Posts.Count >= 1);
        var (_, path, body) = Assert.Single(ledger.Posts);
        Assert.Equal("/api/sessions/c2/say", path);
        using (var said = JsonDocument.Parse(body)) Assert.Equal("and the readme", said.RootElement.GetProperty("text").GetString());
        await UntilAsync(() => loop.Events.Page("c2").Events.Count == 1);
        Assert.Equal(("w1", "resume", "screen"), (loop.Events.Page("c2").Events[0].Id, loop.Events.Page("c2").Events[0].Reaches, loop.Events.Page("c2").Events[0].Door));
        Assert.Empty(loop.Events.Page("c1").Events);
        await UntilAsync(() => !File.Exists(SessionWords.HeldPath(Home)));
    }

    /// <summary>
    /// MSG1d (D137 §5.3): what a word said now would do, for the page to offer the box by: a session that ended or parked
    /// goes on with it, and a driven session's inbox answers its door's reach, which is not known until the door is.
    /// </summary>
    [Fact]
    public async Task The_queue_says_what_a_word_said_now_would_do()
    {
        var ledger = new Ledger { Records = Records(Record("s1", "completed"), Record("s2", "awaiting-person", quest: "q2")) };
        var (loop, module) = await UpAsync(ledger);
        var opening = loop.Processes.OpenInbox("d1");
        var holding = loop.Processes.OpenInbox("d2");
        var sending = loop.Processes.OpenInbox("d3");
        holding.Attach(interrupt: null);
        sending.Attach(() => Task.CompletedTask, _ => Task.FromResult("end_turn"));

        foreach (var (id, reaches) in new[] { ("s1", "resume"), ("s2", "resume"), ("d2", "turn-end"), ("d3", "next-step") })
        {
            var queue = await AnswerAsync(module, "SESSION_QUEUE", new { id });
            Assert.Equal((id, reaches), (id, queue.GetProperty("reaches").GetString()));
            Assert.Equal(JsonValueKind.Null, queue.GetProperty("why").ValueKind);
        }

        var unknown = await AnswerAsync(module, "SESSION_QUEUE", new { id = "d1" });
        Assert.True(unknown.GetProperty("listening").GetBoolean());
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null), (unknown.GetProperty("reaches").ValueKind, unknown.GetProperty("why").ValueKind));
        GC.KeepAlive(opening);
    }

    /// <summary>
    /// QUEUEEDIT1a (D76): the queue names each held word by its id, and <c>SESSION_QUEUE_EDIT</c> takes one back or rewords it:
    /// with no text it removes, with some it replaces the words in place, and either is told to every list as the queue moved.
    /// A word a turn took is a normal answer, <c>done: false</c> and <c>why: taken</c>, never an error.
    /// </summary>
    [Fact]
    public async Task A_held_word_is_removed_or_reworded_by_its_id_and_a_taken_one_answers_taken()
    {
        var ledger = new Ledger { Records = Records(Record("s1", "completed")) };
        var (loop, module) = await UpAsync(ledger);
        var inbox = loop.Processes.OpenInbox("d2");
        inbox.Attach(interrupt: null);
        inbox.Hold(new ChatMessage("one", []));
        inbox.Hold(new ChatMessage("two", []));

        var queue = await AnswerAsync(module, "SESSION_QUEUE", new { id = "d2" });
        var ids = queue.GetProperty("queued").EnumerateArray().Select(said => said.GetProperty("id").GetString()!).ToArray();
        Assert.Equal(2, ids.Distinct().Count());

        var reworded = await AnswerAsync(module, "SESSION_QUEUE_EDIT", new { session = "d2", id = ids[0], text = "one, said better" });
        var removed = await AnswerAsync(module, "SESSION_QUEUE_EDIT", new { session = "d2", id = ids[1] });
        var again = await AnswerAsync(module, "SESSION_QUEUE_EDIT", new { session = "d2", id = ids[1] });
        var unknown = await AnswerAsync(module, "SESSION_QUEUE_EDIT", new { session = "nothing-here", id = "said-1", text = "words" });

        Assert.True(reworded.GetProperty("done").GetBoolean());
        Assert.True(removed.GetProperty("done").GetBoolean());
        Assert.False(again.GetProperty("done").GetBoolean());
        Assert.Equal("taken", again.GetProperty("why").GetString());
        Assert.Equal("taken", unknown.GetProperty("why").GetString());
        var after = await AnswerAsync(module, "SESSION_QUEUE", new { id = "d2" });
        var now = after.GetProperty("queued").EnumerateArray().ToArray();
        Assert.Equal(["one, said better"], now.Select(said => said.GetProperty("text").GetString()));
        Assert.NotEqual(ids[0], now[0].GetProperty("id").GetString());
    }

    /// <summary>QUEUEEDIT1a: words reworded to nothing are refused in a sentence: removing is the verb with no text.</summary>
    [Fact]
    public async Task Rewording_a_held_word_to_nothing_is_refused()
    {
        var (loop, module) = await UpAsync(new Ledger { Records = Records(Record("s1", "completed")) });
        var inbox = loop.Processes.OpenInbox("d2");
        inbox.Attach(interrupt: null);
        inbox.Hold(new ChatMessage("one", []));
        var id = inbox.State.Queued[0].Id!;

        var refusal = await RefusalAsync(module, "SESSION_QUEUE_EDIT", new { session = "d2", id, text = "  " });

        Assert.Contains(Refusals.DriverRefused, refusal);
        Assert.Equal(["one"], inbox.State.Queued.Select(message => message.Text));
    }

    /// <summary>
    /// SAIDBACK1b (D76, D137 §2.4): the person's words waiting on a record that ended or parked are its queue, each by the id
    /// the record gave it, so the composer's *Edit* and *Remove* reach them. Never another agent's words waiting there (a second
    /// opinion's findings, a helper's answer), and none on a record that runs again, whose run is already taking them.
    /// </summary>
    [Theory]
    [InlineData("completed")]
    [InlineData("awaiting-person")]
    public async Task The_persons_words_waiting_on_a_record_that_ended_or_parked_are_its_queue(string state)
    {
        var ledger = new Ledger
        {
            Records = Records(
                Waiting(
                    "s1", state, Word("w1", "use the blue one", files: "\"plan.md\""), Word("w2", "the findings", mark: ",\"by\":\"o1\""),
                    Word("w3", "a helper's answer", mark: ",\"helper\":\"h1\"")),
                WaitingIn("q2", "s2", "working", Word("w4", "on its way"))),
        };
        var (_, module) = await UpAsync(ledger);

        var queue = await AnswerAsync(module, "SESSION_QUEUE", new { id = "s1" });
        var running = await AnswerAsync(module, "SESSION_QUEUE", new { id = "s2" });

        var word = Assert.Single(queue.GetProperty("queued").EnumerateArray());
        Assert.Equal(
            ("w1", "use the blue one", "plan.md"),
            (word.GetProperty("id").GetString(), word.GetProperty("text").GetString(), Assert.Single(word.GetProperty("files").EnumerateArray()).GetString()));
        Assert.False(queue.GetProperty("taking").GetBoolean());
        Assert.Equal("resume", queue.GetProperty("reaches").GetString());
        Assert.Empty(running.GetProperty("queued").EnumerateArray());
    }

    /// <summary>
    /// SAIDBACK1b (D76): a word waiting on a record, which nothing here holds, is taken back or reworded through the service's
    /// take-back door (SAIDBACK1a). The conversation settles the old word with QUEUEEDIT1a's withdrawn line and shows a reword's
    /// new words as the person's under their new id, and the queue is told again, whole, with the record's words as they stand.
    /// </summary>
    [Fact]
    public async Task A_word_waiting_on_a_record_is_taken_back_or_reworded_through_the_service()
    {
        var ledger = new Ledger { Records = Records(Waiting("s1", "completed", Word("w1", "use the blue one"), Word("w2", "and the red"))) };
        ledger.TakeBack = (id, body) =>
        {
            using var asked = JsonDocument.Parse(body);
            if (!asked.RootElement.TryGetProperty("text", out _))
            {
                ledger.Records = Records(Waiting(id, "completed", Word("w2", "and the red")));
                return (HttpStatusCode.OK, $$$"""{"done":true,"message":"Took word `w1` back from session `{{{id}}}`: 1 still wait.","session":{"id":"{{{id}}}","state":"completed"}}""");
            }

            ledger.Records = Records(Waiting(id, "completed", Word("w3", "use the green one")));
            return (HttpStatusCode.OK, $$$"""
                {"done":true,"message":"Reworded word `w2` on session `{{{id}}}`.","session":{"id":"{{{id}}}","state":"completed"},
                 "said":{{{Word("w3", "use the green one")}}}}
                """);
        };
        var (loop, module) = await UpAsync(ledger);

        var removed = await AnswerAsync(module, "SESSION_QUEUE_EDIT", new { session = "s1", id = "w1" });
        var reworded = await AnswerAsync(module, "SESSION_QUEUE_EDIT", new { session = "s1", id = "w2", text = "use the green one" });

        Assert.True(removed.GetProperty("done").GetBoolean());
        Assert.True(reworded.GetProperty("done").GetBoolean());
        Assert.Equal(
            [("/api/sessions/s1/take-back", "w1", null), ("/api/sessions/s1/take-back", "w2", "use the green one")],
            ledger.Posts.Select(post =>
            {
                using var said = JsonDocument.Parse(post.Body);
                var text = said.RootElement.TryGetProperty("text", out var words) ? words.GetString() : null;
                return (post.Path, said.RootElement.GetProperty("word").GetString(), text);
            }));
        var shown = loop.Events.Page("s1").Events;
        Assert.Equal(
            [(SessionEventKind.Note, SessionEventCodes.Withdrawn, "w1"), (SessionEventKind.Note, SessionEventCodes.Withdrawn, "w2")],
            shown.Where(each => each.Kind == SessionEventKind.Note).Select(each => (each.Kind, each.Code, Assert.Single(each.Words!))));
        // WITHDRAWNLINE1: one note shape for every door: the library's, the one the driver's terminal keeps.
        Assert.All(
            shown.Where(each => each.Kind == SessionEventKind.Note),
            each => Assert.Equal(SessionNotes.Withdrawn(each.Words![0]).Text, each.Text));
        var renamed = Assert.Single(shown, each => each.Kind == SessionEventKind.User);
        Assert.Equal(("w3", "use the green one", "resume", "screen"), (renamed.Id, renamed.Text, renamed.Reaches, renamed.Door));
        await UntilAsync(() => QueuedWords("s1") is ["w3"]);
    }

    /// <summary>
    /// SAIDBACK1b: a word the service no longer holds is <c>taken</c>, an answer and never an error, as a live queue's is: gone
    /// from the record, or the record itself gone. The page is told the queue again, so a list it showed stale is read anew.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.OK, """{"done":false,"why":"taken","message":"Word `w1` no longer waits on session `s1`."}""")]
    [InlineData(HttpStatusCode.NotFound, """{"error":"No session `s1`.","refusal":"not-found"}""")]
    public async Task A_word_the_service_no_longer_holds_answers_taken(HttpStatusCode status, string answer)
    {
        var ledger = new Ledger { Records = Records(Record("s1", "completed")) };
        ledger.TakeBack = (_, _) => (status, answer);
        var (_, module) = await UpAsync(ledger);

        var edit = await AnswerAsync(module, "SESSION_QUEUE_EDIT", new { session = "s1", id = "w1" });

        Assert.Equal((false, "taken"), (edit.GetProperty("done").GetBoolean(), edit.GetProperty("why").GetString()));
        Assert.Equal("/api/sessions/s1/take-back", Assert.Single(ledger.Posts).Path);
        await UntilAsync(() => QueuedWords("s1") is []);
    }

    /// <summary>SAIDBACK1b: what never goes on refuses a take-back in the service's sentence, and the old word stays shown.</summary>
    [Fact]
    public async Task A_take_back_the_service_refuses_is_refused_in_its_sentence()
    {
        var ledger = new Ledger { Records = Records(Waiting("s1", "completed", Word("w1", "use the blue one"))) };
        ledger.TakeBack = (_, _) => (HttpStatusCode.Conflict, """{"error":"Session `s1` stood down: #q1 is someone else's.","refusal":"stood-down"}""");
        var (loop, module) = await UpAsync(ledger);

        var refusal = await RefusalAsync(module, "SESSION_QUEUE_EDIT", new { session = "s1", id = "w1" });

        Assert.Contains(Refusals.DriverRefused, refusal);
        Assert.Contains("Session `s1` stood down: #q1 is someone else's.", refusal);
        Assert.Empty(loop.Events.Page("s1").Events);
    }

    /// <summary>SAIDBACK1b: before the loop's service answers, nothing holds a record's words for this machine: <c>taken</c>.</summary>
    [Fact]
    public async Task A_word_asked_back_before_the_loop_is_up_answers_taken()
    {
        var edit = await AnswerAsync(Module(), "SESSION_QUEUE_EDIT", new { session = "s1", id = "w1" });

        Assert.Equal((false, "taken"), (edit.GetProperty("done").GetBoolean(), edit.GetProperty("why").GetString()));
    }

    /// <summary>
    /// SAIDBACK1b: words kept on a record that ended are told in its queue at once, so the box's list offers *Edit* and *Remove*
    /// on them without the page asking again.
    /// </summary>
    [Fact]
    public async Task Words_kept_on_a_record_that_ended_are_told_in_its_queue()
    {
        var ledger = new Ledger { Records = Records(Record("s1", "completed")) };
        ledger.Say = id =>
        {
            ledger.Records = Records(Waiting(id, "completed", Word("w1", "also the changelog")));
            return (HttpStatusCode.OK, Kept(id));
        };
        var (_, module) = await UpAsync(ledger);

        await AnswerAsync(module, "SESSION_INPUT", new { id = "s1", text = "also the changelog" });

        await UntilAsync(() => QueuedWords("s1") is ["w1"]);
    }

    /// <summary>The ids of the words in the last <c>SESSION_QUEUED</c> told for this session, or null where none was told.</summary>
    private string[]? QueuedWords(string session)
    {
        string[]? last = null;
        foreach (var message in Raised.Where(m => m.Type == "SESSION_QUEUED"))
        {
            var payload = JsonSerializer.SerializeToElement(message.Payload);
            if (payload.GetProperty("Session").GetString() != session) continue;
            last = [.. payload.GetProperty("Queued").EnumerateArray().Select(word => word.GetProperty("Id").GetString()!)];
        }

        return last;
    }

    /// <summary>
    /// MSG1d (D136, D137 §5.3): a word to a driven session whose inbox is open is held there as before, and the answer says
    /// when it reaches the session: at its next step where the door sends words during a turn.
    /// </summary>
    [Fact]
    public async Task A_word_a_driven_sessions_inbox_holds_says_when_it_reaches_the_session()
    {
        var loop = Loop();
        var inbox = loop.Processes.OpenInbox("s1");
        inbox.Attach(() => Task.CompletedTask, _ => Task.FromResult("end_turn"));

        var sent = await AnswerAsync(new DriverModule(Bus, loop), "SESSION_INPUT", new { id = "s1", text = "the level file moved" });

        Assert.True(sent.GetProperty("sent").GetBoolean());
        Assert.Equal("next-step", sent.GetProperty("reaches").GetString());
        Assert.Equal("the level file moved", inbox.TakeOrClose()?.Text);
    }

    /// <summary>CONV4c: the payload's files read back as the names and bytes the page sent; none is none.</summary>
    [Fact]
    public void A_messages_files_are_read_from_the_payload_as_names_and_bytes()
    {
        using var payload = JsonDocument.Parse("""
            {"id":"s1","text":"look","files":[{"name":"run.log","content":"ZXhpdCAz"},{"name":"shot.png","content":""}]}
            """);
        using var bare = JsonDocument.Parse("""{"id":"s1","text":"look"}""");

        var files = ConversationRoutes.FilesOf(payload.RootElement);

        Assert.Equal(["run.log", "shot.png"], files.Select(file => file.Name));
        Assert.Equal("exit 3", System.Text.Encoding.UTF8.GetString(files[0].Content));
        Assert.Empty(files[1].Content);
        Assert.Empty(ConversationRoutes.FilesOf(bare.RootElement));
    }

    /// <summary>
    /// CONV4c: a message's files arrive as bytes the way a quest's do, base64 in the payload — and bytes
    /// that are not base64 are refused in a sentence, never kept as something else.
    /// </summary>
    [Fact]
    public async Task A_file_that_is_not_base64_is_refused_in_a_sentence()
    {
        var refusal = await RefusalAsync(Module(), "SESSION_INPUT", new
        {
            id = "nothing-here", text = "look", files = new[] { new { name = "a.png", content = "not base64 at all!" } },
        });

        Assert.Contains(Refusals.DriverRefused, refusal);
        Assert.Contains("`a.png` did not arrive as a file's bytes", refusal);
    }

    /// <summary>
    /// CONV4a: stopping a turn nothing here holds stops nothing and withdraws nothing — an answer, as
    /// the page asking a moment late deserves, never an error.
    /// </summary>
    [Fact]
    public async Task Stopping_a_turn_nothing_here_holds_answers_that_nothing_ran()
    {
        var stop = await AnswerAsync(Module(), "CANCEL_TURN", new { id = "nothing-here" });

        Assert.False(stop.GetProperty("cancelled").GetBoolean());
        Assert.Empty(stop.GetProperty("withdrawn").EnumerateArray());
    }

    /// <summary>
    /// STEER2a, COWORK1a (D188 point 3): <c>SESSION_INPUT</c> at <c>now</c>, as <c>level: "now"</c> or an older page's
    /// <c>now: true</c>, answers what the session's door did, as <c>{ sent, reaches, why, to, now }</c>: steered into the running
    /// turn (<c>now</c>), or held for the turn's end, <c>now: false</c> with <c>no-interrupt</c>, whether or not the door could stop
    /// the turn: no word stops one. Without a level the words go as they always did, and the answer says now was not asked.
    /// </summary>
    [Fact]
    public async Task Send_now_to_a_driven_session_answers_what_its_door_did()
    {
        var (loop, module) = await UpAsync(new Ledger());
        var stopped = 0;
        var steered = new List<string>();
        loop.Processes.OpenInbox("d1").Attach(() => { stopped++; return Task.CompletedTask; });
        loop.Processes.OpenInbox("d2").Attach(interrupt: null);
        var steering = loop.Processes.OpenInbox("d3");
        steering.Attach(
            () => { stopped++; return Task.CompletedTask; },
            _ => new TaskCompletionSource<string>().Task,
            message => { steered.Add(message.Text); return Task.FromResult("injected"); });
        steering.Flow();

        foreach (var (id, reaches, now, why) in new[]
                 {
                     ("d1", "turn-end", false, SendNowWhy.NoInterrupt), ("d2", "turn-end", false, SendNowWhy.NoInterrupt),
                     ("d3", "now", true, (string?)null),
                 })
        {
            var sent = await AnswerAsync(module, "SESSION_INPUT", new { id, text = "start with the tests", now = true });
            var leveled = await AnswerAsync(module, "SESSION_INPUT", new { id, text = "and the readme", level = "now" });
            foreach (var answer in new[] { sent, leveled })
            {
                Assert.Equal(
                    (id, true, reaches, now, why),
                    (id, answer.GetProperty("sent").GetBoolean(), answer.GetProperty("reaches").GetString(), answer.GetProperty("now").GetBoolean(),
                        answer.GetProperty("why").GetString()));
            }
        }

        Assert.Equal(0, stopped);
        Assert.Equal(["start with the tests", "and the readme"], steered);
        var plain = await AnswerAsync(module, "SESSION_INPUT", new { id = "d1", text = "and the changelog" });
        Assert.False(plain.GetProperty("now").GetBoolean());
        Assert.Equal(JsonValueKind.Null, plain.GetProperty("why").ValueKind);
        Assert.Equal(0, stopped);
    }

    /// <summary>
    /// COWORK1a (the message-urgency design §3.0, §9): <c>SESSION_INPUT</c>'s <c>level</c> is when the words should reach a
    /// working session. Ordinary on a door that takes words at its next step waits for the turn's end, and the queue lists it with
    /// its level; urgent goes at the next step; a level this build does not know is refused in a sentence, and nothing is kept.
    /// </summary>
    [Fact]
    public async Task A_level_to_a_driven_session_is_held_or_sent_by_its_door_and_listed_with_it()
    {
        var (loop, module) = await UpAsync(new Ledger());
        var sent = new List<string>();
        var inbox = loop.Processes.OpenInbox("d3");
        inbox.Attach(() => Task.CompletedTask, message => { sent.Add(message.Text); return new TaskCompletionSource<string>().Task; });
        inbox.Flow();

        var ordinary = await AnswerAsync(module, "SESSION_INPUT", new { id = "d3", text = "tidy the readme after", level = "ordinary" });
        var urgent = await AnswerAsync(module, "SESSION_INPUT", new { id = "d3", text = "the budget is 64 KiB", level = "urgent" });
        var refusal = await RefusalAsync(module, "SESSION_INPUT", new { id = "d3", text = "hello", level = "priority" });

        Assert.Equal(("turn-end", "next-step"), (ordinary.GetProperty("reaches").GetString(), urgent.GetProperty("reaches").GetString()));
        Assert.Equal(["the budget is 64 KiB"], sent);
        Assert.Contains("ordinary, urgent or now", refusal);
        var queued = Assert.Single((await AnswerAsync(module, "SESSION_QUEUE", new { id = "d3" })).GetProperty("queued").EnumerateArray());
        Assert.Equal(("tidy the readme after", "ordinary"), (queued.GetProperty("text").GetString(), queued.GetProperty("level").GetString()));
    }

    /// <summary>
    /// COWORK1a (the design §8.1, §8.2): the queue's reach says what the door can do while a turn runs, which the page offers the
    /// levels and the keeping stop by: whether its agent steers, and whether it can stop a turn keeping the session. The native
    /// door's run can do neither; a protocol door that takes words at its next step and steers can do both.
    /// </summary>
    [Fact]
    public async Task The_queue_says_whether_the_door_steers_and_has_a_keeping_stop()
    {
        var (loop, module) = await UpAsync(new Ledger());
        loop.Processes.OpenInbox("d2").Attach(interrupt: null);
        var steering = loop.Processes.OpenInbox("d3");
        steering.Attach(() => Task.CompletedTask, _ => new TaskCompletionSource<string>().Task, _ => Task.FromResult("injected"));
        steering.Flow();

        foreach (var (id, steers, keeps) in new[] { ("d2", false, false), ("d3", true, true) })
        {
            var reach = (await AnswerAsync(module, "SESSION_QUEUE", new { id })).GetProperty("reach");
            Assert.Equal((id, steers, keeps), (id, reach.GetProperty("steers").GetBoolean(), reach.GetProperty("keepingStop").GetBoolean()));
        }
    }

    /// <summary>
    /// COWORK1a (the design §8.2): <c>SESSION_QUEUE_EDIT</c> with <c>level</c> changes a held word's level under its id; raised on a
    /// door that takes words at its next step it goes there. A word nothing here holds answers taken, and its record is left alone:
    /// a level means nothing to words waiting on a record that is not running.
    /// </summary>
    [Fact]
    public async Task A_held_words_level_is_changed_by_its_id_and_a_records_word_is_left_alone()
    {
        var ledger = new Ledger { Records = Records(Waiting("s1", "completed", Word("w1", "also the changelog"))) };
        var (loop, module) = await UpAsync(ledger);
        var sent = new List<string>();
        var inbox = loop.Processes.OpenInbox("d3");
        inbox.Attach(() => Task.CompletedTask, message => { sent.Add(message.Text); return new TaskCompletionSource<string>().Task; });
        inbox.Flow();
        inbox.Hold(new ChatMessage("tidy the readme after", []) { Level = MessageLevel.Ordinary });
        var id = Assert.Single(inbox.State.Queued).Id!;

        var raised = await AnswerAsync(module, "SESSION_QUEUE_EDIT", new { session = "d3", id, level = "urgent" });
        var record = await AnswerAsync(module, "SESSION_QUEUE_EDIT", new { session = "s1", id = "w1", level = "urgent" });

        Assert.True(raised.GetProperty("done").GetBoolean());
        Assert.Equal(["tidy the readme after"], sent);
        Assert.Equal("taken", record.GetProperty("why").GetString());
        Assert.DoesNotContain(ledger.Posts, post => post.Path.EndsWith("/take-back", StringComparison.Ordinal));
    }

    /// <summary>
    /// COWORK1a (the design §8.6, RPC1d): <c>CANCEL_TURN</c> asked for its words by <c>sessions stop &lt;id&gt; --turn</c> is the
    /// keeping stop, answering the lines the verb prints: the turn stopped so what waits goes next; a door with no stop that keeps the
    /// session says so and stops nothing.
    /// </summary>
    [Fact]
    public async Task The_keeping_stop_asked_for_its_words_answers_the_verbs_lines()
    {
        var (loop, module) = await UpAsync(new Ledger { Records = Records(Record("d1", "working"), Record("d2", "working", quest: "q2")) });
        var stopped = 0;
        var keeping = loop.Processes.OpenInbox("d1");
        keeping.Attach(() => { stopped++; return Task.CompletedTask; });
        keeping.Hold(new ChatMessage("the tests are in /spec", []));
        loop.Processes.OpenInbox("d2").Attach(interrupt: null);

        var stop = (await AnswerAsync(module, "CANCEL_TURN", new { id = "d1", keep = true, said = true })).GetProperty("said");
        var none = (await AnswerAsync(module, "CANCEL_TURN", new { id = "d2", keep = true, said = true })).GetProperty("said");

        Assert.Equal(
            (0, "sessions: stopping d1's turn: what waits goes next, and what it has done stays."),
            (stop.GetProperty("code").GetInt32(), Assert.Single(stop.GetProperty("out").EnumerateArray()).GetString()));
        Assert.Equal(1, stopped);
        Assert.Equal(1, none.GetProperty("code").GetInt32());
        Assert.StartsWith("sessions: d2's door cannot stop a turn and keep the session", Assert.Single(none.GetProperty("out").EnumerateArray()).GetString());
    }

    /// <summary>
    /// STEER2a: <c>CANCEL_TURN</c> with <c>keep</c> stops the turn and withdraws nothing. A driven session's stop already sends what
    /// is held now; a turn nothing here holds stops nothing, keep or not.
    /// </summary>
    [Fact]
    public async Task A_stop_that_keeps_withdraws_nothing_and_sends_a_driven_sessions_held_words_now()
    {
        var (loop, module) = await UpAsync(new Ledger());
        var stopped = 0;
        var inbox = loop.Processes.OpenInbox("d1");
        inbox.Attach(() => { stopped++; return Task.CompletedTask; });
        inbox.Hold(new ChatMessage("the tests are in /spec", []));

        var stop = await AnswerAsync(module, "CANCEL_TURN", new { id = "d1", keep = true });
        var nothing = await AnswerAsync(module, "CANCEL_TURN", new { id = "nothing-here", keep = true });

        Assert.True(stop.GetProperty("cancelled").GetBoolean());
        Assert.Empty(stop.GetProperty("withdrawn").EnumerateArray());
        Assert.Equal(1, stopped);
        Assert.Equal("the tests are in /spec", inbox.TakeOrClose()?.Text);
        Assert.False(nothing.GetProperty("cancelled").GetBoolean());
        Assert.Empty(nothing.GetProperty("withdrawn").EnumerateArray());
    }

    /// <summary>
    /// CONV4a: what a conversation has waiting is asked for by a page that just opened it, and takes
    /// every change after that live — nothing waiting is an empty list.
    /// </summary>
    [Fact]
    public async Task A_conversation_with_nothing_waiting_answers_an_empty_queue()
    {
        var queue = await AnswerAsync(Module(), "SESSION_QUEUE", new { id = "nothing-here" });

        Assert.Equal("nothing-here", queue.GetProperty("session").GetString());
        Assert.Empty(queue.GetProperty("queued").EnumerateArray());
        Assert.False(queue.GetProperty("taking").GetBoolean());
        // HELP4: nothing is opening either, so nothing waits on a door.
        Assert.False(queue.GetProperty("opening").GetBoolean());
        // RAIL2: no turn ended here, so no last move is claimed — the page keeps the record's.
        Assert.Equal(JsonValueKind.Null, queue.GetProperty("lastTurn").ValueKind);
        // MSG1d: before the loop is up nothing is known of where a word would go, and nothing is claimed either way.
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null), (queue.GetProperty("reaches").ValueKind, queue.GetProperty("why").ValueKind));
    }

    /// <summary>
    /// D76 §2 (CONV1): a session's conversation is read back over the bridge a page at a time — the
    /// newest first, then earlier, then only what is newer — from the record under the home, so it
    /// answers after a restart when the console's window is long gone.
    /// </summary>
    [Fact]
    public async Task A_sessions_conversation_is_read_back_a_page_at_a_time()
    {
        var loop = Loop();
        var module = new DriverModule(Bus, loop);
        for (var i = 1; i <= 5; i++)
        {
            loop.Events.Append("s1", new SessionEvent { Kind = SessionEventKind.Message, Text = $"m{i}" });
        }

        var latest = await AnswerAsync(module, "SESSION_HISTORY", new { id = "s1", limit = 2 });
        Assert.Equal("s1", latest.GetProperty("session").GetString());
        Assert.Equal(["m4", "m5"], latest.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("text").GetString()));
        Assert.True(latest.GetProperty("earlier").GetBoolean());
        Assert.Equal(5, latest.GetProperty("latest").GetInt64());
        Assert.Equal("message", latest.GetProperty("events")[0].GetProperty("kind").GetString());

        var earlier = await AnswerAsync(module, "SESSION_HISTORY", new { id = "s1", before = 4, limit = 2 });
        Assert.Equal(["m2", "m3"], earlier.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("text").GetString()));

        var newer = await AnswerAsync(module, "SESSION_HISTORY", new { id = "s1", after = 3 });
        Assert.Equal(["m4", "m5"], newer.GetProperty("events").EnumerateArray().Select(e => e.GetProperty("text").GetString()));

        var none = await AnswerAsync(module, "SESSION_HISTORY", new { id = "nothing-here" });
        Assert.Empty(none.GetProperty("events").EnumerateArray());
    }

    /// <summary>An id arrives from the page, so one that is not an id is refused in a sentence, never read.</summary>
    [Fact]
    public async Task A_history_asked_for_by_a_path_is_refused()
    {
        var refusal = await RefusalAsync(Module(), "SESSION_HISTORY", new { id = "../escape" });

        Assert.Contains("is not a session id", refusal);
    }

    /// <summary>
    /// A cold start is the state a person meets most often, and it must be a SENTENCE: the loop's
    /// service is not answering yet, so there is nothing to put behind a conversation.
    /// </summary>
    [Fact]
    public async Task A_chat_asked_for_before_the_loop_is_up_says_so_rather_than_crashing()
    {
        var refusal = await RefusalAsync(Module(), "START_CHAT", new { repository = "engine" });

        Assert.Contains(Refusals.DriverNotReady, refusal);
    }

    /// <summary>A loop whose service is the stand-in, and the module over it.</summary>
    private async Task<(DriverLoop Loop, DriverModule Module)> UpAsync(Ledger ledger)
    {
        var loop = Loop();
        await loop.ComeUpAsync(new ServiceClient("http://stand-in", null, new HttpClient(ledger)));
        return (loop, new DriverModule(Bus, loop));
    }

    private static string Records(params string[] records) => $"[{string.Join(",", records)}]";

    private static string Record(string id, string state, string quest = "q1") =>
        $$"""{"id":"{{id}}","repository":"engine","state":"{{state}}","quest":"{{quest}}","kind":"driven","created":"2026-10-03T08:00:00Z"}""";

    /// <summary>A driven record of quest #q1, or another, with these words waiting on it (MSG1a's <c>said</c>) as the service answers them.</summary>
    private static string Waiting(string id, string state, params string[] said) => WaitingIn("q1", id, state, said);

    private static string WaitingIn(string quest, string id, string state, params string[] said) =>
        $$"""{"id":"{{id}}","repository":"engine","state":"{{state}}","quest":"{{quest}}","kind":"driven","created":"2026-10-03T08:00:00Z","said":[{{string.Join(",", said)}}]}""";

    /// <summary>One word as the service answers it on a record (MSG1a), with another agent's mark where one is named.</summary>
    private static string Word(string id, string text, string files = "", string mark = "") =>
        $$"""{"id":"{{id}}","text":"{{text}}","at":"2026-10-15T08:00:00+00:00","files":[{{files}}],"reopens":true{{mark}}}""";

    /// <summary>A conversation's record, continuing <paramref name="from"/> where one is named (MIXAGENT1a-iii's <c>from</c>).</summary>
    private static string Chat(string id, string state, string? from = null)
    {
        var continues = from is null ? "" : $",\"from\":\"{from}\"";
        return $$"""{"id":"{{id}}","repository":"engine","state":"{{state}}","kind":"chat","created":"2026-10-03T08:00:00Z"{{continues}}}""";
    }

    /// <summary>The say door's yes: the record, its sentence, and the word as kept, with the id the record gave it.</summary>
    private static string Kept(string id, string word = "w1", string text = "also the changelog") => $$"""
        {"session":{"id":"{{id}}","state":"completed"},"message":"Kept for session `{{id}}` to go on with.",
         "said":{"id":"{{word}}","text":"{{text}}","at":"2026-10-03T09:00:00+00:00","files":[],"reopens":true}
        }
        """;

    /// <summary>
    /// A local host standing in: the records as given, the say door as given, and the state door, which moves a record
    /// as asked. Each write is heard by its path and body.
    /// </summary>
    private sealed class Ledger : HttpMessageHandler
    {
        private readonly List<(string Method, string Path, string Body)> _posts = [];

        /// <summary>The records the service answers; a test moves them on as a take would (HANDWORDS1).</summary>
        public string Records { get; set; } = "[]";

        public Func<string, (HttpStatusCode Status, string Body)> Say { get; set; } = id => (HttpStatusCode.OK, Kept(id));

        /// <summary>
        /// The take-back door (SAIDBACK1a) by the session and the body posted: a word no longer waiting, as the service answers
        /// one, unless a test answers otherwise (and moves <see cref="Records"/> on as the service would).
        /// </summary>
        public Func<string, string, (HttpStatusCode Status, string Body)> TakeBack { get; set; } = (id, _) =>
            (HttpStatusCode.OK, $$"""{"done":false,"why":"taken","message":"No word of that id waits on session `{{id}}`."}""");

        /// <summary>Every write but a move: what the person's words were posted to.</summary>
        public IReadOnlyList<(string Method, string Path, string Body)> Posts
        {
            get { lock (_posts) return [.. _posts]; }
        }

        private readonly List<string> _moves = [];

        /// <summary>Every move asked of the state door, as <c>id → state</c> (ANSWER2).</summary>
        public IReadOnlyList<string> Moves
        {
            get { lock (_moves) return [.. _moves]; }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
            var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var (status, answer) = (request.Method.Method, parts) switch
            {
                ("GET", ["api", "sessions"]) => (HttpStatusCode.OK, Records),
                ("POST", ["api", "sessions", var id, "state"]) => Moved(Uri.UnescapeDataString(id), JsonDocument.Parse(body).RootElement.GetProperty("state").GetString()),
                ("POST", ["api", "sessions", var id, "say"]) => Heard(path, body, Say(Uri.UnescapeDataString(id))),
                ("POST", ["api", "sessions", var id, "take-back"]) => Heard(path, body, TakeBack(Uri.UnescapeDataString(id), body)),
                ("POST", _) => Heard(path, body, (HttpStatusCode.OK, """{"kept":true,"message":"Kept."}""")),
                _ => (HttpStatusCode.NotFound, ""),
            };
            return new HttpResponseMessage(status) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
        }

        private (HttpStatusCode, string) Heard(string path, string body, (HttpStatusCode, string) answer)
        {
            lock (_posts) _posts.Add(("POST", path, body));
            return answer;
        }

        /// <summary>A record moved as asked, and the move heard.</summary>
        private (HttpStatusCode, string) Moved(string id, string? state)
        {
            lock (_moves) _moves.Add($"{id} → {state}");
            return (HttpStatusCode.OK, $$"""{"session":{"id":"{{id}}","state":"{{state}}"},"message":"Moved."}""");
        }
    }
}
