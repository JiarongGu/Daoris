using System.Diagnostics;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MSG1f2 (D137 §2.2, §5.3's <c>SESSION_START_FROM</c>): *Start a conversation with these words* as one act. The words a
/// record could not go on with are the first message of a new conversation in its repository, handed with the files said
/// with them and a preface naming the session, which is the agent's to read; then they leave the record they were said to
/// by their ids, naming that conversation, and the record's conversation says where they went by the reason's code
/// (<c>started</c>). Refused before anything starts by a code: D137 §2.2's nevers, a record still running, one with no words
/// waiting, and one whose quest the driver carries on by itself. The real client over the in-process ledger; the start and
/// the first message stand in, so nothing is spawned.
/// </summary>
public sealed class StartFromTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "start-from", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public StartFromTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => GitSeed.Discard(_home);

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    /// <summary>What the stand-in start and first message were asked, in order.</summary>
    private sealed class Asked
    {
        public List<string> Started { get; } = [];

        /// <summary>What each start was asked to move (FOUNDATION1d-ii part 4).</summary>
        public List<MoveWords> Moves { get; } = [];

        public List<(string Session, string Text, IReadOnlyList<ChatUpload> Files, string Preface)> Said { get; } = [];

        /// <summary>What each first message was to show of the words it carries: each moved word's own event, or null for one joined.</summary>
        public List<IReadOnlyList<SessionEvent>?> Shown { get; } = [];
    }

    private Task<StartedFrom> RunAsync(
        ChatLedger ledger, string id, Asked asked, ChatStart? start = null, Func<IReadOnlyList<ChatUpload>, bool>? say = null,
        Action<string, LineCourse>? decided = null)
    {
        var client = ledger.Client();
        return StartFrom.RunAsync(
            client, _home, Events, output: null, id,
            (repository, moves, _) =>
            {
                asked.Started.Add(repository);
                asked.Moves.Add(moves);
                return Task.FromResult(start ?? new ChatStart("c9", "Conversation `c9` opened in `engine`."));
            },
            (session, text, files, preface, shown) =>
            {
                asked.Said.Add((session, text, files, preface));
                asked.Shown.Add(shown);
                return say?.Invoke(files) ?? true;
            },
            decided: decided);
    }

    /// <summary>
    /// The stand-in start through the client's keyed door (FOUNDATION1d-ii part 4), as the chat runner's own start opens: the moves sent
    /// where the ledger's mark is 2, and what moved answered back.
    /// </summary>
    private Task<StartedFrom> OpenedAsync(
        ChatLedger ledger, ServiceClient client, string id, Asked asked, Func<string, IReadOnlyList<ChatUpload>, bool>? say = null) =>
        StartFrom.RunAsync(
            client, _home, Events, output: null, id,
            async (repository, moves, token) =>
            {
                asked.Started.Add(repository);
                asked.Moves.Add(moves);
                var opened = await client.Opens.OpenAsync(
                    "/api/sessions/chat", $$"""{"repository":"{{repository}}","adapter":"talk"}""", $"start/{asked.Started.Count}", moves.From,
                    ct: token, moves: moves);
                return new ChatStart(opened.SessionId, opened.Message) { Moves = opened.Moves, Moved = opened.Moved };
            },
            (session, text, files, preface, shown) =>
            {
                asked.Said.Add((session, text, files, preface));
                asked.Shown.Add(shown);
                return say?.Invoke(session, files) ?? true;
            });

    /// <summary>
    /// 🔴 FOUNDATION1d-ii part 4 (the foundation design §3.4 rule 3): start-from's open moves the words, under its key, from the record they
    /// were said to, which its new record names no <c>from</c> link to, so it names that record as the moves' source. A word kept there between
    /// the read and the open moves with them. The first message is composed from the new conversation's moved words, never the old record's:
    /// each moved word's files are copied into the new conversation's folder first and read from there, each word is marked attempted there
    /// before it is handed and shown by its own event under its id, and once handed they leave the new conversation by their ids. The old
    /// record says where they went.
    /// </summary>
    [Fact]
    public async Task The_open_moves_the_words_and_a_late_one_and_the_first_message_is_the_new_conversations_moved_words()
    {
        var ledger = new ChatLedger { Opens = 2 }.Register("engine", _root);
        ledger.Chat("c1", "completed", "talk", _root).Say("w1", "See the trace.", "trace.txt");
        ChatFiles.Keep(_home, "c1", [new ChatUpload("trace.txt", Encoding.UTF8.GetBytes("exit 3"))]);
        ledger.BeforeRequest = (request, _) =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/sessions/chat") ledger.For("c1").Say("w2", "And the readme.");
            return Task.CompletedTask;
        };
        using var client = ledger.Client();
        var asked = new Asked();
        var attempted = new List<string>();

        var started = await OpenedAsync(ledger, client, "c1", asked, (session, _) =>
        {
            attempted.AddRange(ledger.Record(session)["said"]!.AsArray().Where(word => word!["attempt"] is not null).Select(word => word!["id"]!.GetValue<string>()));
            return true;
        });

        Assert.True(started.Sent, started.Message);
        Assert.Equal("c2", started.SessionId);
        var moves = Assert.Single(asked.Moves);
        Assert.Equal(("c1", "w1", "w1"), (moves.From, string.Join(',', moves.Words), string.Join(',', moves.Read)));
        Assert.Equal(("start/1", "c1"), (ledger.Opened[0].Key, ledger.Opened[0].Moves!["from"]!.GetValue<string>()));

        var (session, text, files, _) = Assert.Single(asked.Said);
        Assert.Equal(("c2", "See the trace.\n\nAnd the readme."), (session, text));
        var handed = Assert.Single(files);
        Assert.Equal(("trace.txt", "exit 3"), (handed.Name, Encoding.UTF8.GetString(handed.Content)));
        Assert.Equal(["w1", "w2"], attempted);

        var shown = Assert.Single(asked.Shown)!;
        Assert.Equal([("w1", "See the trace."), ("w2", "And the readme.")], shown.Select(e => (e.Id!, e.Text!)));
        Assert.All(shown, e => Assert.Equal((SessionEventKind.User, "person"), (e.Kind, e.Origin)));
        Assert.Equal(["trace.txt"], shown[0].Files!);
        Assert.Null(shown[1].Files);

        Assert.Equal([("c2", "w1,w2", (string?)null)], ledger.TakenBy);
        Assert.Empty(ledger.Said("c1"));
        Assert.Empty(ledger.Said("c2"));
        var went = Assert.Single(Events.After("c1", 0).Events, e => e.Kind == SessionEventKind.Note);
        Assert.Equal(["w1", "w2"], went.Words!);
        Assert.Equal(("c2", ContinueWhy.Started), (went.To, went.Why));

        // The file is the new conversation's own now: read from its folder alone, its bytes and name as said.
        GitSeed.Discard(ChatFiles.Folder(_home, "c1"));
        var kept = Assert.Single(ChatFiles.Kept(_home, "c2", ["trace.txt"]));
        Assert.Equal(("trace.txt", "exit 3"), (kept.Name, Encoding.UTF8.GetString(AtomicFile.ReadBytes(kept.Path))));
    }

    /// <summary>
    /// 🔴 A moved word's files are copied by their kept names, and copied again changes nothing: a name the destination keeps already is
    /// passed over, so a take-back or a go-on at the destination finds the one file as it was said.
    /// </summary>
    [Fact]
    public void A_moved_words_files_copied_twice_are_one_file_with_its_bytes_and_name()
    {
        ChatFiles.Keep(_home, "c1", [new ChatUpload("trace.txt", Encoding.UTF8.GetBytes("exit 3"))]);

        ChatFiles.Moved(_home, "c1", "c2", ["trace.txt", "gone.txt"]);
        ChatFiles.Moved(_home, "c1", "c2", ["trace.txt"]);

        var leaf = Path.GetFileName(ChatFiles.Find(_home, "c1", "trace.txt")!.Path);
        Assert.Equal([leaf], Directory.EnumerateFiles(ChatFiles.Folder(_home, "c2")).Select(Path.GetFileName));
        GitSeed.Discard(ChatFiles.Folder(_home, "c1"));
        var kept = ChatFiles.Find(_home, "c2", "trace.txt")!;
        Assert.Equal(("trace.txt", "exit 3"), (kept.Name, Encoding.UTF8.GetString(AtomicFile.ReadBytes(kept.Path))));
    }

    /// <summary>
    /// 🔴 The moved words handed nowhere (nothing took the first message): they wait on the new conversation the open moved them to, which
    /// the sentence says, and nothing is taken off anything.
    /// </summary>
    [Fact]
    public async Task Moved_words_the_new_conversation_did_not_take_wait_on_it_and_the_sentence_says_so()
    {
        var ledger = new ChatLedger { Opens = 2 }.Register("engine", _root);
        ledger.Chat("c1", "completed", "talk", _root).Say("w1", "Also log the port.");
        using var client = ledger.Client();
        var asked = new Asked();

        var started = await OpenedAsync(ledger, client, "c1", asked, (_, _) => false);

        Assert.Equal(("c2", false), (started.SessionId, started.Sent));
        Assert.Contains("wait on conversation `c2`", started.Message);
        Assert.DoesNotContain("stay on session `c1`", started.Message);
        Assert.Empty(ledger.TakenBy);
        Assert.Empty(ledger.Said("c1"));
        Assert.Equal(["w1"], ledger.Said("c2"));
    }

    /// <summary>
    /// 🔴 Against a host whose keyed open moves nothing (its mark 1) or that keeps no marks, start-from reads, hands and takes the old record's
    /// words as it did: the moves are never sent to a mark-1 host, the first message is one joined word, and the words leave the record they
    /// were said to, naming the new conversation.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public async Task Against_an_older_host_the_words_go_from_the_record_they_were_said_to_as_before(int opens)
    {
        var ledger = new ChatLedger { Opens = opens }.Register("engine", _root);
        ledger.Chat("c1", "completed", "talk", _root).Say("w1", "Also log the port.");
        using var client = ledger.Client();
        var asked = new Asked();

        var started = await OpenedAsync(ledger, client, "c1", asked);

        Assert.True(started.Sent, started.Message);
        Assert.Equal(("c2", "Also log the port."), (asked.Said.Single().Session, asked.Said.Single().Text));
        Assert.Null(Assert.Single(asked.Shown));
        List<(string Key, JsonObject? Moves)> keyed = opens == 1 ? [("start/1", null)] : [];
        Assert.Equal(keyed, ledger.Opened);
        Assert.Equal([("c1", "w1", (string?)"c2")], ledger.TakenBy);
        Assert.Empty(ledger.Said("c1"));
    }

    /// <summary>
    /// 🔴 The words a closed quest's session could not go on with start a conversation in its repository, their first message
    /// the words in the order said and a preface naming the session in the agent's own language; then they leave that record
    /// by their ids naming the conversation, its marks go, and its conversation says where they went, by <c>started</c>.
    /// </summary>
    [Fact]
    public async Task The_words_start_a_conversation_and_leave_their_record_naming_it()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Quest("q1", "Done");
        ledger.Chat("s1", "completed", "talk", _root, kind: "driven", quest: "q1").Say("w1", "Also log the port.").Say("w2", "And the readme.");
        new GoOnMarks(_home).Mark("s1", ["w1", "w2"], ContinueWhy.Of(ContinueWhy.Refused), DateTimeOffset.UtcNow);
        var asked = new Asked();

        var started = await RunAsync(ledger, "s1", asked);

        Assert.Equal(("c9", true, (string?)null), (started.SessionId, started.Sent, started.Why));
        Assert.Equal(["engine"], asked.Started);
        var (session, text, files, preface) = Assert.Single(asked.Said);
        Assert.Equal(("c9", "Also log the port.\n\nAnd the readme."), (session, text));
        Assert.Empty(files);
        Assert.Equal(StartFrom.Preface("s1"), preface);
        Assert.Contains("session `s1`", preface);
        Assert.Equal([("s1", "w1,w2", (string?)"c9")], ledger.TakenBy);
        Assert.Empty(ledger.Said("s1"));
        Assert.Null(new GoOnMarks(_home).Read("s1"));

        var went = Assert.Single(Events.After("s1", 0).Events, e => e.Kind == SessionEventKind.Note);
        Assert.Equal(["w1", "w2"], went.Words!);
        Assert.Equal(("c9", ContinueWhy.Started), (went.To, went.Why));
        Assert.Equal("— your words went to session `c9`, because you started a conversation with them.", went.Text);
    }

    /// <summary>An ended conversation's words, which nothing carries on by itself either, start one the same way.</summary>
    [Fact]
    public async Task An_ended_conversations_words_start_a_conversation_too()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c1", "stopped", "talk", _root).Say("w1", "Try it with the cache off.");
        var asked = new Asked();

        var started = await RunAsync(ledger, "c1", asked);

        Assert.True(started.Sent);
        Assert.Equal("Try it with the cache off.", Assert.Single(asked.Said).Text);
        Assert.Equal([("c1", "w1", (string?)"c9")], ledger.TakenBy);
    }

    /// <summary>
    /// MIXAGENT2a-iii: a helper's answer is another agent's claim, never the person's words, so a new conversation is never
    /// handed it as theirs: where only a helper's answer waits nothing starts, and where the person's words wait beside it only
    /// theirs go, the claim staying on the record for the conversation it was meant for.
    /// </summary>
    [Fact]
    public async Task A_helpers_answer_is_never_carried_into_a_new_conversation_as_the_persons_words()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c1", "completed", "talk", _root).Helped("h1w", "Another agent says five.");
        ledger.Chat("c2", "completed", "talk", _root).Say("w1", "Also log the port.").Helped("h2w", "Another agent says five.");
        var asked = new Asked();

        var only = await RunAsync(ledger, "c1", asked);
        Assert.Equal((null, false, StartFrom.NoWords), (only.SessionId, only.Sent, only.Why));
        Assert.Empty(asked.Started);

        var mixed = await RunAsync(ledger, "c2", asked);
        Assert.True(mixed.Sent);
        Assert.Equal("Also log the port.", Assert.Single(asked.Said).Text);
        Assert.Equal([("c2", "w1", (string?)"c9")], ledger.TakenBy);
        Assert.Equal(["h1w"], ledger.Said("c1"));
        Assert.Equal(["h2w"], ledger.Said("c2"));
    }

    /// <summary>
    /// What is refused is refused before anything starts, by its code, nothing taken and nothing said: what never goes on
    /// (a teammate's record, Ask Daoris's own, an intake, a stand-down), a session still running or parked (its words reach
    /// it there), one with no words waiting, a session whose quest the driver carries on by itself, and no record.
    /// </summary>
    [Theory]
    [InlineData("laptop/s1", "completed", "driven", "q1", "Done", "engine", true, WordsNever.Teammate)]
    [InlineData("s1", "completed", "chat", null, null, "daoris:help", true, WordsNever.Help)]
    [InlineData("s1", "stood-down", "driven", "q1", "Taken", "engine", true, WordsNever.StoodDown)]
    [InlineData("s1", "working", "driven", "q1", "Taken", "engine", true, StartFrom.Running)]
    [InlineData("s1", "awaiting-person", "driven", "q1", "Taken", "engine", true, StartFrom.Running)]
    [InlineData("c1", "working", "chat", null, null, "engine", true, StartFrom.Running)]
    [InlineData("s1", "completed", "driven", "q1", "Done", "engine", false, StartFrom.NoWords)]
    [InlineData("s1", "failed", "driven", "q1", "Taken", "engine", true, StartFrom.Carried)]
    [InlineData("s1", "completed", "driven", "q1", "Open", "engine", true, StartFrom.Carried)]
    public async Task What_is_refused_starts_nothing_and_takes_nothing(
        string id, string state, string kind, string? quest, string? status, string repository, bool words, string why)
    {
        var ledger = new ChatLedger().Register("engine", _root);
        if (quest is not null && status is not null) ledger.Quest(quest, status);
        var record = ledger.Chat(id, state, "talk", _root, kind, quest, repository);
        if (words) record.Say("w1", "One more thing.");
        var asked = new Asked();

        var started = await RunAsync(ledger, id, asked);

        Assert.Equal((null, false, why), (started.SessionId, started.Sent, started.Why));
        Assert.False(string.IsNullOrWhiteSpace(started.Message));
        Assert.Empty(asked.Started);
        Assert.Empty(ledger.TakenBy);
        Assert.Empty(Events.After(id.Replace('/', '-'), 0).Events);
    }

    /// <summary>
    /// 🔴 FOUNDATION1h branch 8 (the foundation design §3.1, §14.2): the press is the course's decision for <see cref="CourseTrigger.StartFrom"/>.
    /// Heard from the start, its answer is each refusal by the code it has always had, or the start of a fresh conversation with the person's
    /// words alone, keyed by the record they were said to.
    /// </summary>
    [Theory]
    [InlineData("laptop/s1", "completed", "driven", "q1", "Done", "engine", true, "never:Teammate")]
    [InlineData("s1", "completed", "chat", null, null, "daoris:help", true, "hold:Refused:help")]
    [InlineData("s1", "stood-down", "driven", "q1", "Taken", "engine", true, "never:StoodDown")]
    [InlineData("c1", "working", "chat", null, null, "engine", true, "hold:Refused:running")]
    [InlineData("s1", "completed", "driven", "q1", "Done", "engine", false, "hold:Refused:no-words")]
    [InlineData("s1", "completed", "driven", "q1", "Open", "engine", true, "hold:Refused:carried")]
    [InlineData("c1", "completed", "chat", null, null, "engine", true, "start:w1")]
    public async Task The_courses_answer_is_what_refuses_or_starts_the_press(
        string id, string state, string kind, string? quest, string? status, string repository, bool words, string course)
    {
        var ledger = new ChatLedger().Register("engine", _root);
        if (quest is not null && status is not null) ledger.Quest(quest, status);
        var record = ledger.Chat(id, state, "talk", _root, kind, quest, repository);
        if (words) record.Say("w1", "One more thing.").Helped("h1", "Another agent says five.");
        var decided = new List<(string Session, LineCourse Answer)>();

        await RunAsync(ledger, id, new Asked(), decided: (session, answer) => decided.Add((session, answer)));

        var (session, heard) = Assert.Single(decided);
        Assert.Equal(
            (id, course),
            (session, heard switch
            {
                LineCourse.Never never => $"never:{never.Why}",
                LineCourse.Hold hold => $"hold:{hold.Why}:{hold.Code}",
                LineCourse.Start start => $"start:{string.Join(",", start.Words.Ids)}",
                _ => heard.GetType().Name,
            }));
    }

    /// <summary>
    /// HANDWORDS1: a conversation handed over or moved went on in another, which takes its words: nothing starts from them, and the
    /// refusal names the one that continues it.
    /// </summary>
    [Fact]
    public async Task A_conversation_that_went_on_in_another_starts_nothing_and_names_it()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c1", "completed", "talk", _root).Say("w1", "One more thing.");
        ledger.Chat("c2", "working", "talk", _root, from: new ContinuedFrom("c1", HandOverWhy.Press, HandOverBy.Person));
        var asked = new Asked();

        var started = await RunAsync(ledger, "c1", asked);

        Assert.Equal((null, false, WordsNever.Continued), (started.SessionId, started.Sent, started.Why));
        Assert.Equal("c1 went on in conversation c2; say it to that one.", started.Message);
        Assert.Empty(asked.Started);
        Assert.Empty(ledger.TakenBy);
    }

    /// <summary>An intake takes no words, and an id no record has is said as such: both refused by their codes.</summary>
    [Theory]
    [InlineData("i1", WordsNever.Intake)]
    [InlineData("s9", WordsNever.NotFound)]
    public async Task An_intake_and_no_record_are_refused_by_their_codes(string id, string why)
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("i1", "completed", "talk", _root, kind: "driven", ask: "a1").Say("w1", "One more thing.");
        var asked = new Asked();

        var started = await RunAsync(ledger, id, asked);

        Assert.Equal((false, why), (started.Sent, started.Why));
        Assert.Empty(asked.Started);
    }

    /// <summary>
    /// Nothing started (no checkout here, a harness missing): the driver's sentence comes back whole, and the words stay on
    /// the record they were said to, unsaid and untaken.
    /// </summary>
    [Fact]
    public async Task A_conversation_that_does_not_start_leaves_the_words_where_they_were()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c1", "completed", "talk", _root).Say("w1", "Also log the port.");
        var asked = new Asked();

        var started = await RunAsync(ledger, "c1", asked, start: new ChatStart(null, "`engine` has no checkout on this machine."));

        Assert.Equal((null, false, (string?)null, "`engine` has no checkout on this machine."),
            (started.SessionId, started.Sent, started.Why, started.Message));
        Assert.Empty(asked.Said);
        Assert.Empty(ledger.TakenBy);
        Assert.Equal(["w1"], ledger.Said("c1"));
        Assert.Empty(Events.After("c1", 0).Events);
    }

    /// <summary>A conversation that opened but did not take the words: it is named, and the words stay where they were.</summary>
    [Fact]
    public async Task Words_the_new_conversation_did_not_take_stay_where_they_were()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c1", "completed", "talk", _root).Say("w1", "Also log the port.");
        var asked = new Asked();

        var started = await RunAsync(ledger, "c1", asked, say: _ => false);

        Assert.Equal(("c9", false, (string?)null), (started.SessionId, started.Sent, started.Why));
        Assert.Contains("session `c1`", started.Message);
        Assert.Empty(ledger.TakenBy);
        Assert.Equal(["w1"], ledger.Said("c1"));
        Assert.Empty(Events.After("c1", 0).Events);
    }

    /// <summary>
    /// The files said with the words go with them (MSG1d3), read back from where the record's session kept them, by name; and
    /// where they are more than one message carries, the words go alone rather than not at all.
    /// </summary>
    [Fact]
    public async Task The_files_said_with_the_words_go_with_them_and_the_words_alone_where_they_cannot()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c1", "completed", "talk", _root).Say("w1", "See the trace.", "trace.txt");
        ledger.Chat("c2", "completed", "talk", _root).Say("w2", "See the trace too.", "trace.txt");
        ChatFiles.Keep(_home, "c1", [new ChatUpload("trace.txt", Encoding.UTF8.GetBytes("exit 3"))]);
        ChatFiles.Keep(_home, "c2", [new ChatUpload("trace.txt", Encoding.UTF8.GetBytes("exit 4"))]);
        var asked = new Asked();

        var first = await RunAsync(ledger, "c1", asked);
        var second = await RunAsync(ledger, "c2", asked, say: files => files.Count == 0 ? true : throw new DriverException("too many files"));

        Assert.True(first.Sent);
        var handed = Assert.Single(asked.Said[0].Files);
        Assert.Equal(("trace.txt", "exit 3"), (handed.Name, Encoding.UTF8.GetString(handed.Content)));
        Assert.True(second.Sent);
        Assert.Equal([1, 0], asked.Said.Skip(1).Select(each => each.Files.Count));
    }

    /// <summary>
    /// The chat runner's door (MSG1f2): the start-from runs through its own start, so a conversation whose spawn fails ends
    /// as any start does, and the words it would have carried stay on the record they were said to.
    /// </summary>
    [Fact]
    public async Task The_runners_door_starts_through_its_own_start()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c1", "completed", "talk", _root).Say("w1", "Also log the port.");
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase) { ["talk"] = new Talk() });
        using var client = ledger.Client();
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: Events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
        var ended = new List<(string Session, string State)>();

        var started = await runner.StartFromAsync(
            "c1", "talk", DriverConfig.Empty, (session, state) => { lock (ended) ended.Add((session, state)); return Task.CompletedTask; });

        Assert.Equal((null, false, (string?)null), (started.SessionId, started.Sent, started.Why));
        Assert.Contains("this test spawns nothing", started.Message);
        Assert.Equal(["starting", "failed"], ledger.Moves("c2"));
        lock (ended) Assert.Equal([("c2", "failed")], ended);
        Assert.Equal(["w1"], ledger.Said("c1"));
        Assert.Empty(ledger.TakenBy);
    }

    /// <summary>A conversation's adapter on the protocol door with no toolchain to ask, so nothing is probed or spawned.</summary>
    private sealed class Talk : ISessionAdapter
    {
        public string Name => "talk";

        public SessionWire Wire => SessionWire.Acp;

        public bool Interactive => true;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("this test spawns nothing");

        public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("this test spawns nothing");
    }
}
