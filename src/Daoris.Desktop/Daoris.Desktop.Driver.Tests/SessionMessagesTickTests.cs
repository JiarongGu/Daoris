using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MSG1b (D137 §2.1–§2.3) through REAL ticks. On the protocol door: a session closes its quest, the person writes to it,
/// and at the next look the same record goes on, its own conversation resumed with every word as its own block, its quest
/// staying as it closed; where the agent's other client holds the conversation, the record goes back to how it ended, its
/// words waiting and marked, and no later look tries them again until more are said. On the native door: words said while
/// the run works go on in its own conversation (<c>--resume</c>) before the record concludes.
/// </summary>
/// <remarks>
/// The stand-in service models MSG1a: words kept on a parked or ended record (<c>say</c>), taken off by their ids
/// (<c>taken</c>), and the ledger's one move out of an ended state, to working with words waiting.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class SessionMessagesTickTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-msg-tick-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public SessionMessagesTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "msg@example.com");
        Git("config", "user.name", "MSG1");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Log => Path.Combine(_home, "agent.log");

    private JsonObject[] Heard() => [.. StubFile.Lines(Log).Select(line => JsonNode.Parse(line)!.AsObject())];

    private IReadOnlyList<SessionEvent> Events(string session) => new SessionEvents(Path.Combine(_home, "sessions")).After(session, 0).Events;

    private Daoris.Driver.Driver Protocol(SaidStandIn service, string mode, SessionProcesses? processes = null)
    {
        var adapters = AdapterSet.Built();
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "acp-stub",
            TimeoutMinutes = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Log, mode] },
        };
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: processes ?? new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
    }

    /// <summary>
    /// 🔴 The owner's case: a session in *To review* the person writes to goes on in its own row. Its record leaves
    /// `completed` for working, the stub is sent <c>session/resume</c> on the conversation it named with both words as their
    /// own blocks, the words leave the record by their ids, the archive mark goes, and the record ends `completed` again with
    /// its quest still done — one record from start to end.
    /// </summary>
    [Fact]
    public async Task Words_to_a_session_whose_quest_closed_go_on_in_the_same_session()
    {
        await using var service = SaidStandIn.Start(_repository);
        var driver = Protocol(service, "resumes");

        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Equal(("completed", "Done"), (service.State("s1"), service.Status("q1")));
        Assert.Equal(new HarnessConversation("acp-stub", "conv-s1"), new HarnessConversations(_home).Read("s1"));

        Directory.CreateDirectory(Path.Combine(_home, "sessions"));
        File.WriteAllText(Path.Combine(_home, "sessions", SessionArchive.FileName), """{"archived":[{"session":"s1","at":"2026-10-03T09:00:00Z"}]}""");
        Assert.Equal("w1", await service.SayAsync("s1", "Also log the port."));
        Assert.Equal("w2", await service.SayAsync("s1", "And use 9090."));

        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(1, service.SessionCount);
        Assert.Equal(("completed", "Done"), (service.State("s1"), service.Status("q1")));
        Assert.Equal(["working", "completed"], service.MovesAfterWords("s1"));
        Assert.Empty(service.Said("s1"));
        Assert.Equal([("s1", "w1,w2", (string?)null)], service.Taken);

        var heard = Heard();
        Assert.Single(heard, line => line["method"]!.GetValue<string>() == "session/new");
        var resumed = Assert.Single(heard, line => line["method"]!.GetValue<string>() == "session/resume");
        Assert.Equal("conv-s1", resumed["conversation"]!.GetValue<string>());
        var prompt = heard.Last(line => line["method"]!.GetValue<string>() == "session/prompt");
        Assert.Equal(["Also log the port.", "And use 9090."], prompt["blocks"]!.AsArray().Select(block => block!.GetValue<string>()));
        Assert.True(prompt["resumed"]!.GetValue<bool>());

        Assert.False(new SessionArchive(_home).Marks().ContainsKey("s1"));
        Assert.Contains(Events("s1"), e => e is { Kind: SessionEventKind.User, Origin: "person", Id: "w1", Text: "Also log the port." });
        Assert.Contains(Events("s1"), e => e is { Kind: SessionEventKind.User, Origin: "person", Id: "w2", Text: "And use 9090." });

        // Nothing waits now, so the next look plans nothing for it.
        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Single(Heard(), line => line["method"]!.GetValue<string>() == "session/resume");
    }

    /// <summary>
    /// The agent's other client holds the conversation (<c>elsewhere</c>): the record goes back to `completed`, saying it
    /// cannot go on and why, its words still waiting as said and marked by their ids, and nothing else carries them on, its
    /// quest having closed. The next look does not try them again; a word said after does.
    /// </summary>
    [Fact]
    public async Task Words_a_closed_quests_session_cannot_go_on_with_wait_and_are_not_tried_again_until_more_are_said()
    {
        await using var service = SaidStandIn.Start(_repository);
        var driver = Protocol(service, "elsewhere");
        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));
        await service.SayAsync("s1", "Also log the port.");

        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(1, service.SessionCount);
        Assert.Equal("completed", service.State("s1"));
        Assert.EndsWith(
            "It cannot go on in this session, because its conversation is open in another client of its agent.", service.Note("s1"));
        Assert.Equal(["w1"], service.Said("s1"));
        Assert.Equal(new GoOnMark(["w1"], ContinueWhy.Elsewhere, default).Said, new GoOnMarks(_home).Read("s1")!.Said);
        Assert.Equal(ContinueWhy.Elsewhere, new GoOnMarks(_home).Read("s1")!.Why);
        Assert.Single(Heard(), line => line["method"]!.GetValue<string>() == "session/resume");

        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Single(Heard(), line => line["method"]!.GetValue<string>() == "session/resume");

        await service.SayAsync("s1", "Try again now.");
        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Equal(2, Heard().Count(line => line["method"]!.GetValue<string>() == "session/resume"));
    }

    /// <summary>
    /// 🔴 A native session never concludes while words are held (D137 §2.1): the person writes while it works, its process
    /// exits, and the driver resumes its own conversation (<c>claude -p &lt;words&gt; --resume &lt;kept id&gt;</c>) under the same
    /// record, which concludes once that run ends with nothing held. The words show at once with their reach, and again under
    /// the same id where the resumed run took them.
    /// </summary>
    [Fact]
    public async Task Words_said_while_a_native_session_works_go_on_in_its_own_conversation_before_it_concludes()
    {
        var profile = Path.Combine(_home, "harnesses", "claude-code", "work");
        Directory.CreateDirectory(profile);
        File.WriteAllText(Path.Combine(_home, "harnesses.json"), """{"defaults":{"claude-code":"work"}}""");
        var taken = Path.Combine(_home, "taken.flag");
        var go = Path.Combine(_home, "go.flag");
        await using var service = SaidStandIn.Start(_repository);
        var processes = new SessionProcesses();
        var adapters = AdapterSet.Built();
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "claude-code",
            TimeoutMinutes = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["claude-code"] = ["node", NativeHarness(), Log, taken, go] },
        };
        var driver = new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: processes,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));

        var tick = driver.RunOnceAsync();
        await Poll.Until(() => File.Exists(taken) && processes.InboxOf("s1") is not null, () => "the first run never took its quest", TimeSpan.FromSeconds(60));
        Assert.True(processes.InboxOf("s1")!.Hold(new ChatMessage("Also log the port.", [])));
        File.WriteAllText(go, "");
        await tick.WaitAsync(TimeSpan.FromSeconds(90));

        var runs = Heard();
        Assert.Equal(2, runs.Length);
        Assert.Null(runs[0]["resume"]);
        Assert.Equal(("native-1", "Also log the port."), (runs[1]["resume"]!.GetValue<string>(), runs[1]["prompt"]!.GetValue<string>()));
        Assert.Equal(1, service.SessionCount);
        Assert.Equal(("completed", "Done"), (service.State("s1"), service.Status("q1")));

        var events = Events("s1");
        Assert.Contains(events, e => e is { Kind: SessionEventKind.User, Origin: "person", Id: "said-1", Reaches: "turn-end" });
        Assert.Contains(events, e => e is { Kind: SessionEventKind.User, Origin: "person", Id: "said-1", Reaches: null, Text: "Also log the port." });
        Assert.Null(processes.InboxOf("s1"));
    }

    /// <summary>
    /// The protocol door's stand-in: it names its conversation <c>conv-&lt;session&gt;</c>, and on its first prompt takes and
    /// closes the quest. The mode is how it resumes: <c>resumes</c> honours <c>session/resume</c>, and a resumed prompt is
    /// logged with its blocks; <c>elsewhere</c> refuses it as <c>codex-acp</c> does a thread another client holds.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { appendFileSync } from 'node:fs';
            const [log, mode] = process.argv.slice(2);
            const url = process.env.DAORIS_SERVICE_URL;
            const session = process.env.DAORIS_SESSION_ID;
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const heard = (what) => appendFileSync(log, JSON.stringify({ ...what, session }) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            let conversation = null;
            let resumed = false;
            const say = (text) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
              update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text } } } });
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1,
                  agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                conversation = 'conv-' + session;
                heard({ method: 'session/new', conversation });
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/resume' || frame.method === 'session/load') {
                heard({ method: frame.method, conversation: frame.params?.sessionId });
                if (mode === 'elsewhere') {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32600,
                    message: 'This Codex session is in use by another Codex client.', data: { reason: 'thread_active_writer' } } });
                } else {
                  conversation = frame.params.sessionId;
                  resumed = true;
                  send({ jsonrpc: '2.0', id: frame.id, result: {} });
                }
              } else if (frame.method === 'session/prompt') {
                const blocks = (frame.params?.prompt ?? []).filter((block) => block.type === 'text').map((block) => block.text);
                heard({ method: 'session/prompt', conversation: frame.params?.sessionId, blocks, resumed });
                if (resumed) {
                  say('Logged the port, as you said.');
                } else {
                  await respond({ action: 'take' });
                  await respond({ action: 'done', reason: 'served the report' });
                  say('Served.');
                }
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            // stdin closed: the ending. Never process.exit (STUB1): the process ends when the loop drains.
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }

    /// <summary>
    /// A stand-in for Claude Code's native door: it answers its version and its login question; run with a prompt it speaks
    /// <c>stream-json</c>, naming its conversation <c>native-1</c> on its <c>init</c> line. A first run takes the quest, says so
    /// in a file, waits for the test's word to go on, and closes it; a resumed run logs what it was resumed with.
    /// </summary>
    private string NativeHarness()
    {
        var script = Path.Combine(_home, "claude.mjs");
        File.WriteAllText(script, """
            import { appendFileSync, existsSync, writeFileSync } from 'node:fs';
            const [log, taken, go, ...argv] = process.argv.slice(2);
            if (argv.includes('--version')) { console.log('2.1.0 (Claude Code)'); process.exit(0); }
            if (argv[0] === 'auth' && argv[1] === 'status') { console.log('{"loggedIn": true}'); process.exit(0); }
            const after = (flag) => { const at = argv.indexOf(flag); return at < 0 ? null : argv[at + 1]; };
            const prompt = after('-p');
            const resume = after('--resume');
            const url = process.env.DAORIS_SERVICE_URL;
            const line = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            appendFileSync(log, JSON.stringify({ resume, prompt: resume ? prompt : null }) + '\n');
            line({ type: 'system', subtype: 'init', session_id: resume ?? 'native-1', cwd: process.cwd(), tools: [], model: 'stub' });
            if (!resume) {
              await respond({ action: 'take' });
              writeFileSync(taken, '');
              for (let waited = 0; !existsSync(go) && waited < 30000; waited += 50) await new Promise((r) => setTimeout(r, 50));
              await respond({ action: 'done', reason: 'served the report' });
            }
            line({ type: 'assistant', message: { role: 'assistant', content: [{ type: 'text', text: resume ? 'Logged the port.' : 'Served.' }] } });
            line({ type: 'result', subtype: 'success', is_error: false, result: 'ok' });
            """);
        return script;
    }

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }

    /// <summary>
    /// The service as MSG1a has it, as far as these ticks reach: sessions with their adapter, account, tree, base and take; the
    /// ledger's moves, with the one move out of an ended state to working where words wait; words kept on a parked or ended
    /// record, each with its id, joined as <c>answer</c>; words taken off by their ids; a move into <c>awaiting-person</c>
    /// clearing them.
    /// </summary>
    internal sealed class SaidStandIn : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _serving;
        private readonly string _root;
        private readonly List<JsonObject> _sessions = [];
        private readonly Dictionary<string, List<string>> _moves = [];
        private readonly List<JsonObject> _quests;
        private int _words;

        public string Url { get; }

        /// <summary>Each take of words: the session, the ids joined, and the session that took them where it was another.</summary>
        public List<(string Session, string Ids, string? By)> Taken { get; } = [];

        private SaidStandIn(HttpListener listener, string url, string root)
        {
            _listener = listener;
            Url = url.TrimEnd('/');
            _root = root;
            _quests =
            [
                new JsonObject
                {
                    ["id"] = "q1", ["from"] = "ask #a1", ["to"] = "engine", ["title"] = "Serve the report",
                    ["body"] = "The report needs a port.", ["status"] = "Open",
                },
            ];
            _serving = ServeAsync();
        }

        public static SaidStandIn Start(string root)
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var url = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            return new SaidStandIn(listener, url, root);
        }

        public int SessionCount { get { lock (_sessions) return _sessions.Count; } }

        public string State(string id) => Field(id, "state")!;

        public string? Note(string id) => Field(id, "note");

        public IReadOnlyList<string> Said(string id)
        {
            lock (_sessions) return [.. Find(id)["said"]!.AsArray().Select(word => word!["id"]!.GetValue<string>())];
        }

        public string Status(string quest)
        {
            lock (_sessions) return _quests.Single(q => q["id"]!.GetValue<string>() == quest)["status"]!.GetValue<string>();
        }

        /// <summary>The states a record moved to since the person first wrote to it.</summary>
        public IReadOnlyList<string> MovesAfterWords(string id)
        {
            lock (_sessions) return [.. _moves.GetValueOrDefault(id) ?? []];
        }

        /// <summary>What the person says to a session, through the say door as the page's box will (MSG1d): the word's id.</summary>
        public async Task<string> SayAsync(string id, string text)
        {
            using var http = new HttpClient();
            using var response = await http.PostAsync(
                $"{Url}/api/sessions/{id}/say", new StringContent(new JsonObject { ["text"] = text }.ToJsonString(), Encoding.UTF8, "application/json"));
            var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            Assert.True(response.IsSuccessStatusCode, body.ToJsonString());
            return body["said"]!["id"]!.GetValue<string>();
        }

        private string? Field(string id, string name)
        {
            lock (_sessions) return Find(id)[name]?.GetValue<string>();
        }

        private JsonObject Find(string id) => _sessions.Single(s => s["id"]!.GetValue<string>() == id);

        private static readonly HashSet<string> Live = ["queued", "starting", "working", "awaiting-person"];

        private static readonly Dictionary<string, string[]> Allowed = new()
        {
            ["queued"] = ["starting", "stood-down", "failed", "stopped"],
            ["starting"] = ["working", "stood-down", "failed", "stopped"],
            ["working"] = ["awaiting-person", "completed", "declined", "stood-down", "failed", "stopped"],
            ["awaiting-person"] = ["working", "completed", "declined", "stopped"],
        };

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync(); }
                catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException) { return; }

                var (status, body) = Answer(context.Request);
                var bytes = Encoding.UTF8.GetBytes(body);
                context.Response.StatusCode = status;
                context.Response.ContentType = "application/json";
                await context.Response.OutputStream.WriteAsync(bytes);
                context.Response.Close();
            }
        }

        /// <summary>A record as the wire answers it to this machine: its words waiting, and them joined as `answer`.</summary>
        private static JsonObject Wire(JsonObject session)
        {
            var copy = (JsonObject)session.DeepClone();
            var said = copy["said"]!.AsArray();
            copy["answer"] = said.Count == 0 ? null : string.Join("\n\n", said.Select(word => word!["text"]!.GetValue<string>()));
            return copy;
        }

        private (int, string) Answer(HttpListenerRequest request)
        {
            var path = request.Url!.AbsolutePath;
            var all = request.QueryString["includeClosed"] == "true";
            JsonObject Body()
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                return JsonNode.Parse(reader.ReadToEnd())!.AsObject();
            }

            static string IdOf(string path, string suffix) => path["/api/sessions/".Length..^suffix.Length];

            lock (_sessions)
            {
                switch (request.HttpMethod, path)
                {
                    case ("GET", "/api/quests"):
                        return (200, new JsonArray([.. _quests
                            .Where(q => all || q["status"]!.GetValue<string>() is "Open" or "Taken")
                            .Select(q => q.DeepClone())]).ToJsonString());

                    case ("GET", "/api/registry"):
                        return (200, new JsonArray(new JsonObject
                        {
                            ["repository"] = "engine", ["adopted"] = true, ["registered"] = true,
                            ["root"] = _root, ["workspace"] = "default",
                        }).ToJsonString());

                    case ("GET", "/api/sessions"):
                        return (200, new JsonArray([.. _sessions
                            .Where(s => all || Live.Contains(s["state"]!.GetValue<string>()))
                            .Select(Wire)]).ToJsonString());

                    case ("POST", "/api/sessions"):
                    {
                        var body = Body();
                        var session = new JsonObject
                        {
                            ["id"] = $"s{_sessions.Count + 1}", ["quest"] = body["quest"]!.GetValue<string>(),
                            ["repository"] = "engine", ["state"] = "queued", ["kind"] = "drive",
                            ["adapter"] = body["adapter"]?.GetValue<string>(),
                            ["harnessVersion"] = body["harnessVersion"]?.GetValue<string>(),
                            ["tree"] = body["tree"]?.GetValue<string>(),
                            ["baseCommit"] = body["baseCommit"]?.GetValue<string>(),
                            ["profile"] = body["profile"]?.GetValue<string>(),
                            ["created"] = DateTimeOffset.UtcNow.AddMinutes(_sessions.Count).ToString("O"),
                            ["said"] = new JsonArray(),
                        };
                        _sessions.Add(session);
                        return (200, new JsonObject { ["session"] = Wire(session), ["message"] = "opened" }.ToJsonString());
                    }

                    case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/state", StringComparison.Ordinal):
                    {
                        var id = IdOf(path, "/state");
                        var body = Body();
                        var session = Find(id);
                        var from = session["state"]!.GetValue<string>();
                        var to = body["state"]!.GetValue<string>();
                        var note = body["note"]?.GetValue<string>();
                        if (!Live.Contains(from))
                        {
                            // MSG1a's one move out of an ended state: to working, with words waiting.
                            if (to != "working" || session["said"]!.AsArray().Count == 0)
                            {
                                return (409, $$"""{"error":"Session `{{id}}` is {{from}} — a finished session does not move."}""");
                            }

                            session["note"] = string.Join("\n\n", new[] { session["note"]?.GetValue<string>(), "Went on with your words at 2026-10-03 09:00 UTC.", note }
                                .Where(part => !string.IsNullOrWhiteSpace(part)));
                        }
                        else if (!Allowed.TryGetValue(from, out var moves) || !moves.Contains(to))
                        {
                            return (409, $$"""{"error":"Session `{{id}}` cannot move {{from}} → {{to}}."}""");
                        }
                        else if (note is not null)
                        {
                            session["note"] = note;
                        }

                        session["state"] = to;
                        if (to == "awaiting-person") session["said"] = new JsonArray();
                        if (_moves.TryGetValue(id, out var since)) since.Add(to);
                        return (200, new JsonObject { ["session"] = Wire(session), ["message"] = "moved" }.ToJsonString());
                    }

                    case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/say", StringComparison.Ordinal):
                    {
                        var id = IdOf(path, "/say");
                        var session = Find(id);
                        var state = session["state"]!.GetValue<string>();
                        if (Live.Contains(state) && state != "awaiting-person")
                        {
                            return (409, $$"""{"error":"Session `{{id}}` is {{state}}.","refusal":"running"}""");
                        }

                        var word = new JsonObject
                        {
                            ["id"] = $"w{++_words}", ["text"] = Body()["text"]!.GetValue<string>(),
                            ["at"] = DateTimeOffset.UtcNow.ToString("O"), ["files"] = new JsonArray(), ["reopens"] = state != "awaiting-person",
                        };
                        session["said"]!.AsArray().Add(word);
                        _moves.TryAdd(id, []);
                        return (200, new JsonObject { ["session"] = Wire(session), ["message"] = "kept", ["said"] = word.DeepClone() }.ToJsonString());
                    }

                    case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/taken", StringComparison.Ordinal):
                    {
                        var id = IdOf(path, "/taken");
                        var body = Body();
                        var ids = body["said"]!.AsArray().Select(word => word!.GetValue<string>()).ToList();
                        var session = Find(id);
                        var left = session["said"]!.AsArray().Where(word => !ids.Contains(word!["id"]!.GetValue<string>())).Select(word => word!.DeepClone());
                        session["said"] = new JsonArray([.. left]);
                        Taken.Add((id, string.Join(',', ids), body["by"]?.GetValue<string>()));
                        return (200, new JsonObject { ["session"] = Wire(session), ["message"] = "taken" }.ToJsonString());
                    }

                    case ("POST", "/api/quests/q1/respond"):
                    {
                        var body = Body();
                        var quest = _quests.Single(q => q["id"]!.GetValue<string>() == "q1");
                        switch (body["action"]!.GetValue<string>())
                        {
                            case "take" when quest["status"]!.GetValue<string>() == "Open":
                                quest["status"] = "Taken";
                                foreach (var session in _sessions.Where(s => Live.Contains(s["state"]!.GetValue<string>())))
                                {
                                    session["took"] = true;
                                }

                                break;
                            case "done" when quest["status"]!.GetValue<string>() == "Taken":
                                quest["status"] = "Done";
                                quest["note"] = body["reason"]?.GetValue<string>();
                                break;
                            default:
                                return (409, """{"error":"refused"}""");
                        }

                        return (200, new JsonObject { ["message"] = "moved" }.ToJsonString());
                    }

                    default:
                        return (404, $$"""{"error":"the stand-in has no {{request.HttpMethod}} {{path}}"}""");
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            _listener.Close();
            try { await _serving; } catch (ObjectDisposedException) { }
        }
    }
}
