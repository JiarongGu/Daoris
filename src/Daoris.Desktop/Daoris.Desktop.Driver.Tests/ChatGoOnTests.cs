using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MSG1c (D137 §2.2, §4.2): an ended chat the person wrote to is taken up by the chat runner the moment its words are shown
/// in its conversation, and judged as a driven record is before anything is spawned. Where it cannot go on its words stay
/// waiting, marked, and its conversation says why by the reason's code; what holds it leaves them waiting and says so; a
/// driven record, a live one and a record with no words start nothing here. The real client over an in-process ledger, and
/// nothing spawned, so the fast half: the runs that resume a conversation are <c>ChatGoesOnProcessTests</c>'.
/// </summary>
public sealed class ChatGoOnTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-chat-goon-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public ChatGoOnTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private SessionEvents Events => new(Path.Combine(_home, "sessions"));

    /// <summary>A conversation's adapter on the protocol door with no toolchain to ask, so nothing is probed.</summary>
    private sealed class Talk : ISessionAdapter
    {
        public string Name => "talk";

        public SessionWire Wire => SessionWire.Acp;

        public bool Interactive => true;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("a judgement test spawns nothing");

        public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("a judgement test spawns nothing");
    }

    private ChatRunner Runner(ChatLedger ledger, SessionEvents events, List<AccountLine>? lines = null)
    {
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["talk"] = new Talk(),
            ["acp-stub"] = new AcpStubAdapter(),
        });
        var client = ledger.Client();
        if (lines is not null) client.AccountLined += line => { lock (lines) lines.Add(line); };
        return new ChatRunner(
            client, adapters, _home, new SessionProcesses(Path.Combine(_home, "sessions")), events: events,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
    }

    /// <summary>The person's words as the say door's door shows them (MSG1d): theirs, under the record's id, reaching it on resume.</summary>
    private static SessionEvent Shown(string id, string text) => new()
    {
        Kind = SessionEventKind.User, Origin = "person", Id = id, Text = text, Reaches = "resume", Door = "screen",
    };

    /// <summary>
    /// 🔴 A chat whose agent named no conversation (a text door, or one from before MSG1c) cannot go on: the record stays as
    /// it ended, its words wait marked by their ids, its conversation says why with the code the page words (`unkept`), and
    /// the machine log says it did not resume — all from the words being shown, with nothing else asked.
    /// </summary>
    [Fact]
    public async Task A_chat_whose_conversation_was_not_kept_cannot_go_on_and_says_why()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c1", "completed", "talk", _root).Say("w1", "Also log the port.");
        var events = Events;
        var lines = new List<AccountLine>();
        using var runner = Runner(ledger, events, lines);

        events.Append("c1", Shown("w1", "Also log the port."));

        var cannot = await NoteAsync(events, "c1", e => e.Why is not null);
        Assert.Equal(ContinueWhy.Unkept, cannot.Why);
        Assert.Equal(["w1"], cannot.Words!);
        Assert.Equal("completed", ledger.State("c1"));
        Assert.Empty(ledger.Moves("c1"));
        Assert.Equal(["w1"], ledger.Said("c1"));
        Assert.Equal(ContinueWhy.Unkept, new GoOnMarks(_home).Read("c1")!.Why);
        await Poll.Until(() => { lock (lines) return lines.Any(line => line.Event == "session.reopened"); }, () => "no reopen line");
        lock (lines)
        {
            var line = lines.Single(each => each.Event == "session.reopened");
            Assert.Contains(("kind", (object?)"chat"), line.Data);
            Assert.Contains(("resumed", (object?)false), line.Data);
            Assert.Contains(("why", (object?)ContinueWhy.Unkept), line.Data);
            Assert.Contains(("door", (object?)"screen"), line.Data);
        }
    }

    /// <summary>A chat whose tree is gone cannot go on in it either, said by its code, before anything is asked of the toolchain.</summary>
    [Fact]
    public async Task A_chat_whose_tree_is_gone_cannot_go_on()
    {
        var gone = Path.Combine(_home, "trees", "chat-1");
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c1", "stopped", "talk", gone).Say("w1", "Also log the port.");
        new HarnessConversations(_home).Keep("c1", "talk", "conv-c1");
        var events = Events;
        using var runner = Runner(ledger, events);

        events.Append("c1", Shown("w1", "Also log the port."));

        Assert.Equal(ContinueWhy.Tree, (await NoteAsync(events, "c1", e => e.Why is not null)).Why);
        Assert.Equal("stopped", ledger.State("c1"));
    }

    /// <summary>
    /// What holds it rather than refusing it (D137 §2.2): the ledger refusing the move, here because another session holds
    /// its tree (D51). The words wait unmarked, so the next word tries again, and its conversation says what held it.
    /// </summary>
    [Fact]
    public async Task The_ledger_refusing_the_move_holds_the_words_and_says_what_held_them()
    {
        var ledger = new ChatLedger { Busy = "Repository `engine` is busy: session `s9` holds its tree." }.Register("engine", _root);
        ledger.Chat("c1", "completed", "talk", _root).Say("w1", "Also log the port.");
        new HarnessConversations(_home).Keep("c1", "talk", "conv-c1");
        var events = Events;
        using var runner = Runner(ledger, events);

        events.Append("c1", Shown("w1", "Also log the port."));

        var held = await NoteAsync(events, "c1", e => e.Text?.Contains("does not go on yet") == true);
        Assert.Contains("session `s9` holds its tree", held.Text);
        Assert.Null(held.Words);
        Assert.Equal("completed", ledger.State("c1"));
        Assert.Equal(["w1"], ledger.Said("c1"));
        Assert.Null(new GoOnMarks(_home).Read("c1"));
    }

    /// <summary>
    /// Only an ended chat of this machine's with words waiting goes on here: a driven record is the planner's (MSG1b), a live
    /// chat hears words at its door, and a record with none waiting has nothing to go on with. Each starts nothing.
    /// </summary>
    [Theory]
    [InlineData("driven", "completed", true)]
    [InlineData("chat", "working", true)]
    [InlineData("chat", "completed", false)]
    public async Task What_is_no_ended_chat_with_words_waiting_starts_nothing_here(string kind, string state, bool words)
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var record = ledger.Chat("c1", state, "talk", _root, kind);
        if (words) record.Say("w1", "Also log the port.");
        new HarnessConversations(_home).Keep("c1", "talk", "conv-c1");
        var events = Events;
        using var runner = Runner(ledger, events);

        var answer = await runner.GoOnAsync("c1", DriverConfig.Empty);

        Assert.Null(answer.SessionId);
        Assert.Empty(ledger.Moves("c1"));
        Assert.DoesNotContain(events.After("c1", 0).Events, e => e.Kind == SessionEventKind.Note);
        Assert.Null(new GoOnMarks(_home).Read("c1"));
    }

    /// <summary>
    /// The runner hears only the words a record keeps waiting (the reach <c>resume</c>): words said into a running turn, and
    /// the same words where a session took them, start nothing.
    /// </summary>
    [Fact]
    public async Task Only_words_kept_waiting_on_a_record_are_taken_up()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c1", "completed", "talk", _root).Say("w1", "Also log the port.");
        var events = Events;
        using var runner = Runner(ledger, events);

        events.Append("c1", Shown("w1", "Also log the port.") with { Reaches = "turn-end" });
        events.Append("c1", Shown("w1", "Also log the port.") with { Reaches = null });
        await Task.Delay(300);

        Assert.Equal(0, ledger.Reads);
        Assert.DoesNotContain(events.After("c1", 0).Events, e => e.Kind == SessionEventKind.Note);
    }

    /// <summary>
    /// A record is read as a run that goes on needs it (MSG1c): its kind, state, words, adapter, account, tree, version,
    /// note and flags; a teammate's record with the same id is none of this machine's.
    /// </summary>
    [Fact]
    public void A_record_is_read_with_what_going_on_needs()
    {
        const string json = """
            [{"id":"laptop/c1","repository":"engine","state":"completed","kind":"chat"},
             {"id":"c1","repository":"engine","state":"stopped","kind":"chat","adapter":"claude-code","profile":"account-2",
              "tree":"D:/trees/chat-1","harnessVersion":"2.1.287","note":"the person ended the conversation.","interrupted":true,
              "said":[{"id":"w1","text":"Also log the port.","at":"2026-10-03T09:00:00Z","files":["trace.txt"],"reopens":true}]}]
            """;

        var record = ServiceClient.ReadRecord(json, "c1")!;

        Assert.Equal(("chat", "stopped", "claude-code", "account-2"), (record.Kind, record.State, record.Adapter, record.Profile));
        Assert.Equal(("D:/trees/chat-1", "2.1.287", "the person ended the conversation."), (record.Tree, record.HarnessVersion, record.Note));
        Assert.True(record.Interrupted);
        Assert.Equal(("w1", "Also log the port."), (record.Waiting.Single().Id, record.Waiting.Single().Text));
        Assert.Null(ServiceClient.ReadRecord(json, "laptop/c1"));
        Assert.Null(ServiceClient.ReadRecord(json, "c2"));
    }

    /// <summary>The first note the condition matches, waited for: the runner takes the words up on its own thread.</summary>
    private static async Task<SessionEvent> NoteAsync(SessionEvents events, string session, Func<SessionEvent, bool> matches)
    {
        SessionEvent? found = null;
        await Poll.Until(
            () => (found = events.After(session, 0).Events.FirstOrDefault(e => e.Kind == SessionEventKind.Note && matches(e))) is not null,
            () => string.Join(" | ", events.After(session, 0).Events.Select(e => $"{e.Kind}:{e.Text}")));
        return found!;
    }
}

/// <summary>
/// The service's doors an ended chat going on crosses, standing in (MSG1a's model): records with their kind, adapter,
/// account, tree and words waiting; the registry; the ledger's moves, with the one move out of an ended state to working
/// where words wait; and words taken off by their ids. In-process, through the real client over a handler.
/// </summary>
internal sealed class ChatLedger : HttpMessageHandler
{
    public const string Url = "http://chat-ledger.test";

    private readonly object _gate = new();
    private readonly List<JsonObject> _registry = [];
    private readonly List<JsonObject> _sessions = [];
    private readonly Dictionary<string, List<string>> _moves = new(StringComparer.Ordinal);

    /// <summary>Where set, the ledger refuses the move out of an ended state with this sentence (D51's tree, held by another).</summary>
    public string? Busy { get; set; }

    /// <summary>How many times the records were read.</summary>
    public int Reads { get; private set; }

    /// <summary>Each take of words: the session and the ids joined.</summary>
    public List<(string Session, string Ids)> Taken { get; } = [];

    /// <summary>A client over this ledger.</summary>
    public ServiceClient Client() => new(Url, null, new HttpClient(this, disposeHandler: false));

    public ChatLedger Register(string repository, string root)
    {
        lock (_gate)
        {
            _registry.Add(new JsonObject
            {
                ["repository"] = repository, ["adopted"] = true, ["registered"] = true, ["root"] = root, ["workspace"] = "default",
            });
        }

        return this;
    }

    /// <summary>A record of this machine's: a chat unless said otherwise, in the state given.</summary>
    public Words Chat(string id, string state, string adapter, string tree, string kind = "chat")
    {
        lock (_gate)
        {
            _sessions.Add(new JsonObject
            {
                ["id"] = id, ["repository"] = "engine", ["state"] = state, ["kind"] = kind, ["adapter"] = adapter,
                ["harnessVersion"] = "1.0.0", ["tree"] = tree, ["note"] = "the conversation ended; its commits are its record.",
                ["created"] = "2026-10-03T08:00:00Z", ["said"] = new JsonArray(),
            });
        }

        return new Words(this, id);
    }

    /// <summary>The words of a record already here, as the say door keeps them.</summary>
    public Words For(string id) => new(this, id);

    /// <summary>How many records the ledger holds.</summary>
    public int Count
    {
        get { lock (_gate) return _sessions.Count; }
    }

    /// <summary>A record's words, as the say door keeps them.</summary>
    public sealed class Words(ChatLedger ledger, string id)
    {
        public Words Say(string word, string text)
        {
            lock (ledger._gate)
            {
                ledger.Find(id)["said"]!.AsArray().Add(new JsonObject
                {
                    ["id"] = word, ["text"] = text, ["at"] = "2026-10-03T09:00:00Z", ["files"] = new JsonArray(), ["reopens"] = true,
                });
            }

            return this;
        }
    }

    public string State(string id)
    {
        lock (_gate) return Find(id)["state"]!.GetValue<string>();
    }

    public string? Note(string id)
    {
        lock (_gate) return Find(id)["note"]?.GetValue<string>();
    }

    public IReadOnlyList<string> Said(string id)
    {
        lock (_gate) return [.. Find(id)["said"]!.AsArray().Select(word => word!["id"]!.GetValue<string>())];
    }

    public IReadOnlyList<string> Moves(string id)
    {
        lock (_gate) return [.. _moves.GetValueOrDefault(id) ?? []];
    }

    private JsonObject Find(string id) => _sessions.Single(s => s["id"]!.GetValue<string>() == id);

    private static readonly HashSet<string> Live = ["queued", "starting", "working", "awaiting-person"];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var (status, answer) = Answer(request.Method.Method, request.RequestUri!, body);
        return new HttpResponseMessage(status) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
    }

    private (HttpStatusCode, string) Answer(string method, Uri uri, string? body)
    {
        var path = uri.AbsolutePath;
        var all = uri.Query.Contains("includeClosed=true", StringComparison.Ordinal);
        static string IdOf(string path, string suffix) => path["/api/sessions/".Length..^suffix.Length];

        lock (_gate)
        {
            switch (method, path)
            {
                case ("GET", "/api/registry"):
                    return (HttpStatusCode.OK, new JsonArray([.. _registry.Select(row => row.DeepClone())]).ToJsonString());

                case ("GET", "/api/quests"):
                    return (HttpStatusCode.OK, "[]");

                case ("POST", "/api/sessions/chat"):
                {
                    var asked = JsonNode.Parse(body!)!;
                    var session = new JsonObject
                    {
                        ["id"] = $"c{_sessions.Count + 1}", ["repository"] = asked["repository"]!.GetValue<string>(), ["state"] = "queued",
                        ["kind"] = "chat", ["adapter"] = asked["adapter"]!.GetValue<string>(),
                        ["harnessVersion"] = asked["harnessVersion"]?.GetValue<string>(), ["profile"] = asked["profile"]?.GetValue<string>(),
                        ["tree"] = asked["tree"]?.GetValue<string>(), ["created"] = "2026-10-03T08:00:00Z", ["said"] = new JsonArray(),
                    };
                    _sessions.Add(session);
                    return (HttpStatusCode.OK, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "opened" }.ToJsonString());
                }

                case ("GET", "/api/sessions"):
                    if (all) Reads++;
                    return (HttpStatusCode.OK, new JsonArray([.. _sessions
                        .Where(s => all || Live.Contains(s["state"]!.GetValue<string>()))
                        .Select(s => s.DeepClone())]).ToJsonString());

                case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/state", StringComparison.Ordinal):
                {
                    var id = IdOf(path, "/state");
                    var session = Find(id);
                    var to = JsonNode.Parse(body!)!["state"]!.GetValue<string>();
                    var from = session["state"]!.GetValue<string>();
                    if (!Live.Contains(from) && (to != "working" || session["said"]!.AsArray().Count == 0))
                    {
                        return (HttpStatusCode.Conflict, $$"""{"error":"Session `{{id}}` is {{from}} — a finished session does not move."}""");
                    }

                    if (!Live.Contains(from) && Busy is { } busy)
                    {
                        return (HttpStatusCode.Conflict, new JsonObject { ["error"] = busy }.ToJsonString());
                    }

                    session["state"] = to;
                    if (JsonNode.Parse(body!)!["note"]?.GetValue<string>() is { } note) session["note"] = note;
                    if (!_moves.TryGetValue(id, out var moves)) _moves[id] = moves = [];
                    moves.Add(to);
                    return (HttpStatusCode.OK, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "moved" }.ToJsonString());
                }

                case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/taken", StringComparison.Ordinal):
                {
                    var id = IdOf(path, "/taken");
                    var ids = JsonNode.Parse(body!)!["said"]!.AsArray().Select(word => word!.GetValue<string>()).ToList();
                    var session = Find(id);
                    session["said"] = new JsonArray([.. session["said"]!.AsArray()
                        .Where(word => !ids.Contains(word!["id"]!.GetValue<string>())).Select(word => word!.DeepClone())]);
                    Taken.Add((id, string.Join(',', ids)));
                    return (HttpStatusCode.OK, """{"message":"taken"}""");
                }

                default:
                    return (HttpStatusCode.NotFound, $$"""{"error":"the stand-in has no {{method}} {{path}}"}""");
            }
        }
    }
}
