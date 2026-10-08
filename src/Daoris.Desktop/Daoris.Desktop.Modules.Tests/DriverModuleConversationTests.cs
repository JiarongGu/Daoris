using System.Net;
using System.Text;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A conversation over the bridge (`DriverModule.Conversation.cs`, MOD5): its record, its start, the
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

        var files = DriverModule.FilesOf(payload.RootElement);

        Assert.Equal(["run.log", "shot.png"], files.Select(file => file.Name));
        Assert.Equal("exit 3", System.Text.Encoding.UTF8.GetString(files[0].Content));
        Assert.Empty(files[1].Content);
        Assert.Empty(DriverModule.FilesOf(bare.RootElement));
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

        public string Records { get; init; } = "[]";

        public Func<string, (HttpStatusCode Status, string Body)> Say { get; init; } = id => (HttpStatusCode.OK, Kept(id));

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
