using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// XAGENT1e (D155 point 7; the second-agent design §6.3–§6.7) through REAL ticks, on both doors: a session closes its quest,
/// another agent's findings are handed to its record as a word whose <c>by</c> names the opinion, and at the next look the same
/// record goes on in its own conversation with them, exactly as the host composed them. The stub fixes the finding, commits, and
/// answers it through the stand-in's door for the connector's <c>opinion_answer</c>; as its turn ends the driver reads the answer
/// and checks the fix's commit with real git. Where the agent's other client holds the conversation, the findings go to the
/// person instead, unanswered, and nothing carries them to a new session.
/// </summary>
/// <remarks>
/// The stand-in service models MSG1a and XAGENT1c as far as these ticks reach: words kept on an ended record, with <c>by</c>;
/// taken off by their ids; the ledger's one move out of an ended state; and an opinion read back with the answers kept. The
/// answer door, <c>POST /api/opinions/{id}/answers</c>, is the stand-in's alone: a real session answers through its connector.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class OpinionResumeTickTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-opinion-tick-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    /// <summary>Daoris's fixed words around another agent's claims, as the host composes them: quotes, backticks and lines kept.</summary>
    private const string Findings =
        "Another agent, Codex by OpenAI, read your work and claims what follows. These are its claims, not the person's words "
        + "and not facts.\n\nFinding 1 (must, sure), at `README.md:1`: The heading says \"engine\" and nothing else.";

    public OpinionResumeTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git(_repository, "init", "-q", "-b", "main");
        Git(_repository, "config", "user.email", "opinion@example.com");
        Git(_repository, "config", "user.name", "XAGENT1e");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git(_repository, "add", "-A");
        Git(_repository, "commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Log => Path.Combine(_home, "agent.log");

    private JsonObject[] Heard() => [.. StubFile.Lines(Log).Select(line => JsonNode.Parse(line)!.AsObject())];

    private IReadOnlyList<SessionEvent> Events(string session) => new SessionEvents(Path.Combine(_home, "sessions")).After(session, 0).Events;

    /// <summary>
    /// 🔴 The protocol door: the findings go on in the same record's own conversation as their own block, unchanged; the stub
    /// fixes and answers; the driver reads the answer as the turn ends, the fix's commit read from git as one the turn made.
    /// The record shows them as Daoris's turn around another agent's claims, never the person's words.
    /// </summary>
    [Fact]
    public async Task Findings_go_on_in_the_session_s_own_conversation_on_the_protocol_door_and_are_answered()
    {
        await using var service = OpinionTickStandIn.Start(_repository);
        var driver = Protocol(service, "resumes");
        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Equal(("completed", "Done"), (service.State("s1"), service.Status("q1")));
        var tip = Head(service.Tree("s1")!);
        service.Hand("op1", "s1", tip, Findings);
        var before = service.Record("s1");

        var look = await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        var seen = $"before: {before}\nplan: {string.Join(" | ", look.Considerations.Select(c => $"{c.Quest.Id} {c.Verdict}: {c.Reason}"))}"
                   + $"\nlook: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}";
        Assert.True(service.SessionCount == 1, seen);
        Assert.True(service.State("s1") == "completed", seen);
        Assert.True(service.Said("s1").Count == 0, seen);
        var prompt = Heard().Last(line => line["method"]!.GetValue<string>() == "session/prompt");
        Assert.Equal([Findings], prompt["blocks"]!.AsArray().Select(block => block!.GetValue<string>()));
        Assert.True(prompt["resumed"]!.GetValue<bool>());

        Assert.Contains(Events("s1"), e => e is { Kind: SessionEventKind.User, Origin: "target", Opinion: "op1", Id: "op-w1" } && e.Text == Findings);
        Assert.DoesNotContain(Events("s1"), e => e is { Kind: SessionEventKind.User, Origin: "person" } && e.Text == Findings);
        Assert.Contains(Events("s1"), e => e is { Kind: SessionEventKind.Note, Opinion: "op1" } && e.Text!.Contains("1 fixed"));

        var answered = new OpinionDeliveries(_home).Read("op1").Answered!;
        var fix = Assert.Single(answered.Findings);
        Assert.Equal((OpinionViews.Fixed, Head(service.Tree("s1")!)), (fix.Counts, fix.Fix));
        Assert.NotEqual(tip, fix.Fix);
    }

    /// <summary>
    /// 🔴 The native door: the findings go on in the kept conversation (<c>--resume</c>) as the one argument, unchanged, and the
    /// run is handed rules that let it answer them (<c>opinion_answer</c>); its answer is read as its turn ends. Since XAGENT1c2
    /// the connector's default allows the tool on every run too — harmless, the service answering it only for the session it
    /// handed findings to — so only the resumed run's allowing it is asserted.
    /// </summary>
    [Fact]
    public async Task Findings_go_on_in_the_kept_conversation_on_the_native_door_with_the_tool_that_answers_them()
    {
        var profile = Path.Combine(_home, "harnesses", "claude-code", "work");
        Directory.CreateDirectory(profile);
        File.WriteAllText(Path.Combine(_home, "harnesses.json"), """{"defaults":{"claude-code":"work"}}""");
        await using var service = OpinionTickStandIn.Start(_repository);
        var adapters = AdapterSet.Built();
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "claude-code",
            TimeoutMinutes = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["claude-code"] = ["node", NativeHarness(), Log] },
        };
        var driver = new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Equal(("completed", "Done"), (service.State("s1"), service.Status("q1")));
        var tip = Head(service.Tree("s1")!);
        service.Hand("op1", "s1", tip, Findings);

        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(90));

        var runs = Heard();
        Assert.Equal(2, runs.Length);
        Assert.Equal(("native-1", Findings), (runs[1]["resume"]!.GetValue<string>(), runs[1]["prompt"]!.GetValue<string>()));
        Assert.True(runs[1]["answers"]!.GetValue<bool>(), "the resumed run's rules did not allow opinion_answer");
        Assert.Equal((1, "completed"), (service.SessionCount, service.State("s1")));
        Assert.Contains(Events("s1"), e => e is { Kind: SessionEventKind.User, Origin: "target", Opinion: "op1" } && e.Text == Findings);
        Assert.Equal(OpinionViews.Fixed, Assert.Single(new OpinionDeliveries(_home).Read("op1").Answered!.Findings).Counts);
    }

    /// <summary>
    /// 🔴 Where the session cannot go on (design §6.7) — here its agent's other client holds the conversation — the findings go
    /// to the person instead, kept beside the opinion with why, their word left on the record and marked; nothing carries them
    /// to a new session, and no later look tries them again.
    /// </summary>
    [Fact]
    public async Task Findings_a_session_cannot_go_on_with_go_to_the_person_and_never_to_a_new_session()
    {
        await using var service = OpinionTickStandIn.Start(_repository);
        var driver = Protocol(service, "elsewhere");
        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));
        service.Hand("op1", "s1", Head(service.Tree("s1")!), Findings);

        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));
        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal((1, "completed"), (service.SessionCount, service.State("s1")));
        Assert.Equal(["op-w1"], service.Said("s1"));
        Assert.Equal(ContinueWhy.Elsewhere, new GoOnMarks(_home).Read("s1")!.Why);
        Assert.Equal(ContinueWhy.Elsewhere, new OpinionDeliveries(_home).Read("op1").Person);
        Assert.Single(Heard(), line => line["method"]!.GetValue<string>() == "session/resume");
        Assert.Empty(service.Taken);
    }

    private Daoris.Driver.Driver Protocol(OpinionTickStandIn service, string mode)
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
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
    }

    /// <summary>
    /// The protocol door's stand-in: it names its conversation <c>conv-&lt;session&gt;</c>; its first prompt takes and closes the
    /// quest; a resumed prompt is logged with its blocks, then it fixes the finding in its tree, commits, and answers it fixed
    /// with that commit. In <c>elsewhere</c> mode it refuses the resume as <c>codex-acp</c> does a thread another client holds.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { appendFileSync, writeFileSync } from 'node:fs';
            import { execFileSync } from 'node:child_process';
            const [log, mode] = process.argv.slice(2);
            const url = process.env.DAORIS_SERVICE_URL;
            const session = process.env.DAORIS_SESSION_ID;
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const heard = (what) => appendFileSync(log, JSON.stringify({ ...what, session }) + '\n');
            const post = (path, body) => fetch(`${url}${path}`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            let conversation = null;
            let resumed = false;
            let cwd = process.cwd();
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
                cwd = frame.params?.cwd ?? cwd;
                heard({ method: 'session/new', conversation });
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/resume' || frame.method === 'session/load') {
                heard({ method: frame.method, conversation: frame.params?.sessionId });
                if (mode === 'elsewhere') {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32600,
                    message: 'This Codex session is in use by another Codex client.', data: { reason: 'thread_active_writer' } } });
                } else {
                  conversation = frame.params.sessionId;
                  cwd = frame.params?.cwd ?? cwd;
                  resumed = true;
                  send({ jsonrpc: '2.0', id: frame.id, result: {} });
                }
              } else if (frame.method === 'session/prompt') {
                const blocks = (frame.params?.prompt ?? []).filter((block) => block.type === 'text').map((block) => block.text);
                heard({ method: 'session/prompt', conversation: frame.params?.sessionId, blocks, resumed });
                if (resumed) {
                  writeFileSync(`${cwd}/README.md`, '# engine\n\nThe engine of the reports.\n');
                  execFileSync('git', ['commit', '-qam', 'say what the engine is'], { cwd });
                  const commit = execFileSync('git', ['rev-parse', 'HEAD'], { cwd }).toString().trim();
                  await post('/api/opinions/op1/answers', { finding: 1, said: 'fixed', commit });
                  say('Fixed the heading, as the other agent claimed.');
                } else {
                  await post('/api/quests/q1/respond', { action: 'take' });
                  await post('/api/quests/q1/respond', { action: 'done', reason: 'served the report' });
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
    /// <c>stream-json</c>, naming its conversation <c>native-1</c>. A first run takes and closes the quest; a resumed run fixes the
    /// finding in its tree, commits, and answers it. Each run logs what it was resumed with, and whether the rules it was handed
    /// (<c>--settings</c>) allow <c>opinion_answer</c>.
    /// </summary>
    private string NativeHarness()
    {
        var script = Path.Combine(_home, "claude.mjs");
        File.WriteAllText(script, """
            import { appendFileSync, existsSync, readFileSync, writeFileSync } from 'node:fs';
            import { execFileSync } from 'node:child_process';
            const [log, ...argv] = process.argv.slice(2);
            if (argv.includes('--version')) { console.log('2.1.0 (Claude Code)'); process.exit(0); }
            if (argv[0] === 'auth' && argv[1] === 'status') { console.log('{"loggedIn": true}'); process.exit(0); }
            const after = (flag) => { const at = argv.indexOf(flag); return at < 0 ? null : argv[at + 1]; };
            const prompt = after('-p');
            const resume = after('--resume');
            const settings = after('--settings');
            const allowed = settings && existsSync(settings) ? (JSON.parse(readFileSync(settings, 'utf8')).permissions?.allow ?? []) : [];
            const url = process.env.DAORIS_SERVICE_URL;
            const line = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const post = (path, body) => fetch(`${url}${path}`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            appendFileSync(log, JSON.stringify({ resume, prompt: resume ? prompt : null,
              answers: allowed.includes('mcp__daoris-knowledge__opinion_answer') }) + '\n');
            line({ type: 'system', subtype: 'init', session_id: resume ?? 'native-1', cwd: process.cwd(), tools: [], model: 'stub' });
            if (resume) {
              writeFileSync('README.md', '# engine\n\nThe engine of the reports.\n');
              execFileSync('git', ['commit', '-qam', 'say what the engine is']);
              const commit = execFileSync('git', ['rev-parse', 'HEAD']).toString().trim();
              await post('/api/opinions/op1/answers', { finding: 1, said: 'fixed', commit });
            } else {
              await post('/api/quests/q1/respond', { action: 'take' });
              await post('/api/quests/q1/respond', { action: 'done', reason: 'served the report' });
            }
            line({ type: 'assistant', message: { role: 'assistant', content: [{ type: 'text', text: resume ? 'Fixed it.' : 'Served.' }] } });
            line({ type: 'result', subtype: 'success', is_error: false, result: 'ok' });
            """);
        return script;
    }

    private static string Head(string tree) => Git(tree, "rev-parse", "HEAD").Trim();

    private static string Git(string folder, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = folder, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    /// <summary>
    /// The service as MSG1a and XAGENT1c have it, as far as these ticks reach: sessions with their adapter, account, tree, base
    /// and take; the ledger's moves, with the one move out of an ended state to working where words wait; words kept on an ended
    /// record with their <c>by</c>, taken off by their ids; a quest's take and done; and one opinion, read back with the answers
    /// its working session gave.
    /// </summary>
    private sealed class OpinionTickStandIn : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _serving;
        private readonly string _root;
        private readonly List<JsonObject> _sessions = [];
        private readonly List<JsonObject> _quests;
        private readonly Dictionary<string, JsonObject> _opinions = new(StringComparer.Ordinal);

        public string Url { get; }

        /// <summary>Each take of words: the session, the ids joined, and the session that took them where it was another.</summary>
        public List<(string Session, string Ids, string? By)> Taken { get; } = [];

        private OpinionTickStandIn(HttpListener listener, string url, string root)
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

        public static OpinionTickStandIn Start(string root)
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var url = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            return new OpinionTickStandIn(listener, url, root);
        }

        public int SessionCount { get { lock (_sessions) return _sessions.Count; } }

        public string State(string id) => Field(id, "state")!;

        public string? Tree(string id) => Field(id, "tree");

        public string Record(string id)
        {
            lock (_sessions) return Find(id).ToJsonString();
        }

        public IReadOnlyList<string> Said(string id)
        {
            lock (_sessions) return [.. Find(id)["said"]!.AsArray().Select(word => word!["id"]!.GetValue<string>())];
        }

        public string Status(string quest)
        {
            lock (_sessions) return _quests.Single(q => q["id"]!.GetValue<string>() == quest)["status"]!.GetValue<string>();
        }

        /// <summary>
        /// A first pass on <paramref name="working"/>'s work up to <paramref name="tip"/>, given with one <c>must</c>, and its
        /// findings handed as the host's door hands them: a word on the record, its <c>by</c> the opinion, in the fixed words.
        /// </summary>
        public void Hand(string opinion, string working, string tip, string words)
        {
            lock (_sessions)
            {
                var word = new JsonObject
                {
                    ["id"] = "op-w1", ["text"] = words, ["at"] = DateTimeOffset.UtcNow.ToString("O"), ["files"] = new JsonArray(),
                    ["reopens"] = true, ["by"] = opinion,
                };
                Find(working)["said"]!.AsArray().Add(word);
                _opinions[opinion] = new JsonObject
                {
                    ["id"] = opinion, ["occasion"] = "landing", ["pass"] = "first", ["working"] = working, ["session"] = "r1",
                    ["candidate"] = new JsonObject
                    {
                        ["repository"] = "engine", ["base"] = tip, ["tip"] = tip, ["commits"] = new JsonArray(tip),
                    },
                    ["reviewer"] = new JsonObject { ["adapter"] = "codex-acp", ["label"] = "another-maker", ["product"] = "Codex", ["maker"] = "OpenAI" },
                    ["minutes"] = 20, ["state"] = "given",
                    ["given"] = new JsonObject
                    {
                        ["findings"] = new JsonArray(new JsonObject
                        {
                            ["number"] = 1, ["weight"] = "must", ["where"] = "README.md:1", ["claim"] = "The heading says nothing else.",
                            ["consequence"] = "A reader learns nothing.", ["reproduce"] = "Read it.", ["sure"] = "sure",
                        }),
                        ["read"] = "README.md", ["at"] = DateTimeOffset.UtcNow.ToString("O"),
                    },
                    ["handed"] = new JsonObject { ["session"] = working, ["word"] = "op-w1", ["at"] = DateTimeOffset.UtcNow.ToString("O") },
                    ["answers"] = new JsonArray(),
                };
            }
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

        /// <summary>A record as the wire answers it to this machine: its words waiting, and the person's among them joined as `answer`.</summary>
        private static JsonObject Wire(JsonObject session)
        {
            var copy = (JsonObject)session.DeepClone();
            var persons = copy["said"]!.AsArray().Where(word => word!["by"] is null).ToList();
            copy["answer"] = persons.Count == 0 ? null : string.Join("\n\n", persons.Select(word => word!["text"]!.GetValue<string>()));
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
                            ["repository"] = "engine", ["adopted"] = true, ["registered"] = true, ["root"] = _root, ["workspace"] = "default",
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
                        }
                        else if (!Allowed.TryGetValue(from, out var moves) || !moves.Contains(to))
                        {
                            return (409, $$"""{"error":"Session `{{id}}` cannot move {{from}} → {{to}}."}""");
                        }

                        if (note is not null) session["note"] = note;
                        session["state"] = to;
                        if (to == "awaiting-person") session["said"] = new JsonArray();
                        return (200, new JsonObject { ["session"] = Wire(session), ["message"] = "moved" }.ToJsonString());
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

                    case ("GET", _) when path.StartsWith("/api/opinions/", StringComparison.Ordinal):
                    {
                        var id = path["/api/opinions/".Length..];
                        return _opinions.TryGetValue(id, out var opinion)
                            ? (200, opinion.ToJsonString())
                            : (404, $$"""{"error":"There is no second opinion `{{id}}` on this machine."}""");
                    }

                    // The stand-in's own door for the connector's `opinion_answer`: the answer kept as the desk keeps it.
                    case ("POST", _) when path.StartsWith("/api/opinions/", StringComparison.Ordinal) && path.EndsWith("/answers", StringComparison.Ordinal):
                    {
                        var id = path["/api/opinions/".Length..^"/answers".Length];
                        var body = Body();
                        body["at"] = DateTimeOffset.UtcNow.ToString("O");
                        _opinions[id]["answers"]!.AsArray().Add(body);
                        return (200, """{"message":"kept"}""");
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
