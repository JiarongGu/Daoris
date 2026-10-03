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
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-start-from-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public StartFromTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private SessionEvents Events => new(Path.Combine(_home, "sessions"));

    /// <summary>What the stand-in start and first message were asked, in order.</summary>
    private sealed class Asked
    {
        public List<string> Started { get; } = [];

        public List<(string Session, string Text, IReadOnlyList<ChatUpload> Files, string Preface)> Said { get; } = [];
    }

    private Task<StartedFrom> RunAsync(
        ChatLedger ledger, string id, Asked asked, ChatStart? start = null, Func<IReadOnlyList<ChatUpload>, bool>? say = null)
    {
        var client = ledger.Client();
        return StartFrom.RunAsync(
            client, _home, Events, output: null, id,
            (repository, _) =>
            {
                asked.Started.Add(repository);
                return Task.FromResult(start ?? new ChatStart("c9", "Conversation `c9` opened in `engine`."));
            },
            (session, text, files, preface) =>
            {
                asked.Said.Add((session, text, files, preface));
                return say?.Invoke(files) ?? true;
            });
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
            client, adapters, _home, new SessionProcesses(Path.Combine(_home, "sessions")), events: Events,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
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
