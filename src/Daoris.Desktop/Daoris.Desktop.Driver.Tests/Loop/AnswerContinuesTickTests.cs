using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ANSWER1a (D131) through REAL ticks on the protocol door. A driven session takes its quest and parks to ask which port;
/// the person answers; at the next look the same record goes on and the stub's own conversation is resumed with the
/// answer as its next prompt — one record, no second session. Where the agent no longer has the conversation, or the
/// start runs on another account, the park ends saying why and a new session carries the answer on in the same tree.
/// </summary>
/// <remarks>
/// The stand-in service models ANSWER1b (design §5): an answer keeps the record parked, and a move into
/// <c>awaiting-person</c> clears it. The real service still ends the record as it takes the answer.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class AnswerContinuesTickTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-answer-tick-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public AnswerContinuesTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "answer@example.com");
        Git("config", "user.name", "ANSWER1");
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

    private Daoris.Driver.Driver Driver(ParkStandIn service, string mode, HarnessRoster? roster = null)
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
            harnesses: roster ?? new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
    }

    /// <summary>The first look: a session takes the quest, asks which port, and parks with its conversation id kept.</summary>
    private async Task<JsonObject> ParkedAsync(ParkStandIn service, Daoris.Driver.Driver driver)
    {
        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));
        var parked = service.Session("s1");
        Assert.Equal("awaiting-person", parked["state"]!.GetValue<string>());
        Assert.Equal("Taken", service.Status("q1"));
        Assert.Equal(new HarnessConversation("acp-stub", "conv-s1"), new HarnessConversations(_home).Read("s1"));
        return parked;
    }

    /// <summary>
    /// 🔴 The owner's case: the answer goes on in the same session. The record that parked moves back to working, the stub
    /// is sent <c>session/resume</c> on the conversation it named, its next prompt is the answer exactly, and it closes the
    /// quest — with one record from start to end, its transcript and conversation going on.
    /// </summary>
    [Fact]
    public async Task An_answer_resumes_the_parked_sessions_own_conversation_and_the_same_record_goes_on()
    {
        await using var service = ParkStandIn.Start(_repository);
        var driver = Driver(service, "resumes");
        await ParkedAsync(service, driver);

        var (answered, message) = await new ServiceClient(service.Url, null).AnswerSessionAsync("s1", "Port 8080.");
        Assert.True(answered, message);
        // The precondition the next look plans from: the park, still parked, with the answer on it.
        var park = service.Session("s1");
        Assert.True(
            park["state"]!.GetValue<string>() == "awaiting-person" && park["answer"]?.GetValue<string>() == "Port 8080.",
            $"the answered park before the next look: {park.ToJsonString()}");

        var look = await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        // 🔴 Said whole on failure (the merge of 2026-10-03): the park before the look, the plan, what the look did, and the
        // record and its quest after it. A park whose resumed run closed its quest once ended still parked here.
        var record = service.Session("s1");
        var seen = $"before: {park.ToJsonString()}\nplan: {string.Join(" | ", look.Considerations.Select(c => $"{c.Quest.Id} {c.Verdict}: {c.Reason}"))}"
                   + $"\nlook: {string.Join(" | ", look.Events)}\nafter: {record.ToJsonString()}\nquest: {service.Status("q1")}"
                   + $"\nmoves since the answer: {string.Join(", ", service.MovesAfterAnswer("s1"))}";
        Assert.True(service.SessionCount == 1, seen);
        Assert.True(record["state"]!.GetValue<string>() == "completed", seen);
        Assert.True(service.Status("q1") == "Done", seen);
        Assert.True(service.MovesAfterAnswer("s1").SequenceEqual(["working", "completed"]), seen);

        var heard = Heard();
        Assert.Single(heard, line => line["method"]!.GetValue<string>() == "session/new");
        var resumed = Assert.Single(heard, line => line["method"]!.GetValue<string>() == "session/resume");
        Assert.Equal("conv-s1", resumed["conversation"]!.GetValue<string>());
        Assert.Equal(
            Path.GetFullPath(record["tree"]!.GetValue<string>()), Path.GetFullPath(resumed["cwd"]!.GetValue<string>()), ignoreCase: true);
        var prompt = heard.Last(line => line["method"]!.GetValue<string>() == "session/prompt");
        Assert.Equal(("conv-s1", "Port 8080.", true),
            (prompt["conversation"]!.GetValue<string>(), prompt["prompt"]!.GetValue<string>(), prompt["resumed"]!.GetValue<bool>()));

        // One conversation, one home: the transcript holds both runs, and the answer is the person's, once.
        var transcript = File.ReadAllText(Path.Combine(_home, "sessions", "s1.log"));
        Assert.Contains("Which port should it listen on?", transcript);
        Assert.Contains("resumed on `acp-stub`", transcript);
        Assert.Single(Events("s1"), e => e is { Kind: SessionEventKind.User, Origin: "person", Text: "Port 8080." });
    }

    /// <summary>
    /// The agent no longer has the conversation (<c>resource_not_found</c>): the park ends with its answer and the reason,
    /// and a new session carries the quest on in the same tree, handed the answer, its note saying why in one line.
    /// </summary>
    [Fact]
    public async Task A_conversation_the_agent_no_longer_has_is_carried_on_in_a_new_session_saying_why()
    {
        await using var service = ParkStandIn.Start(_repository);
        var driver = Driver(service, "gone");
        await ParkedAsync(service, driver);
        await new ServiceClient(service.Url, null).AnswerSessionAsync("s1", "Port 8080.");

        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        var park = service.Session("s1");
        Assert.Equal("completed", park["state"]!.GetValue<string>());
        Assert.EndsWith("Carried on in a new session, because the agent no longer has its conversation.", park["note"]!.GetValue<string>());
        var carried = service.Session("s2");
        Assert.Equal("completed", carried["state"]!.GetValue<string>());
        Assert.Equal("Done", service.Status("q1"));
        Assert.Equal(
            Path.GetFullPath(park["tree"]!.GetValue<string>()), Path.GetFullPath(carried["tree"]!.GetValue<string>()), ignoreCase: true);
        Assert.Contains("A new session, because the agent no longer has its conversation.", service.NoteOnStart("s2"));

        var heard = Heard();
        Assert.Contains(heard, line => line["method"]!.GetValue<string>() == "session/resume");
        var handed = heard.Last(line => line["method"]!.GetValue<string>() == "session/prompt")["prompt"]!.GetValue<string>();
        Assert.Contains("You are carrying on quest", handed);
        Assert.Contains("Port 8080.", handed);
        // The answer sits beneath its question in the park's own record once, though a resume was tried there first.
        Assert.Single(Events("s1"), e => e is { Kind: SessionEventKind.User, Origin: "person", Text: "Port 8080." });
    }

    /// <summary>
    /// 🔴 A different account never resumes (D131 §1): the park ran on stub account 1, and the person's default moved to stub
    /// account 2 with no list, so account 1 is no longer an account this work may use (MSG1g asks for the park's own account,
    /// and finds it off the scope) and the stub is never asked to resume. The park ends saying why, the reason's line then its
    /// own, naming no account, and the carry-on runs on account 2 in the same tree.
    /// </summary>
    [Fact]
    public async Task An_answer_on_another_account_never_resumes_and_is_carried_on_saying_why()
    {
        foreach (var account in new[] { "account-1", "account-2" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", account));
        }

        var settings = Path.Combine(_home, "harnesses.json");
        new HarnessSettings().WithDefault("stub", "account-1").Save(settings);
        await using var service = ParkStandIn.Start(_repository);
        var parked = await ParkedAsync(service, Driver(service, "resumes"));
        Assert.Equal("account-1", parked["profile"]!.GetValue<string>());

        // The person's default moves to account 2 before they answer: the next start runs there.
        new HarnessSettings().WithDefault("stub", "account-2").Save(settings);
        await new ServiceClient(service.Url, null).AnswerSessionAsync("s1", "Port 8080.");

        await Driver(service, "resumes").RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        var park = service.Session("s1");
        Assert.Equal("completed", park["state"]!.GetValue<string>());
        Assert.EndsWith(
            "Carried on in a new session, because its conversation stays with the account it ran on, and this start runs on another. "
            + "This work no longer uses that account.",
            park["note"]!.GetValue<string>());
        var carried = service.Session("s2");
        Assert.Equal(("completed", "account-2"), (carried["state"]!.GetValue<string>(), carried["profile"]!.GetValue<string>()));
        Assert.DoesNotContain(Heard(), line => line["method"]!.GetValue<string>() is "session/resume" or "session/load");
        var note = service.NoteOnStart("s2");
        Assert.DoesNotContain("account-1", note);
        Assert.DoesNotContain("account-2", note);
    }

    /// <summary>
    /// The protocol door's stand-in: it names its conversation <c>conv-&lt;session&gt;</c>, takes the quest and asks which
    /// port; told the answer on a resumed conversation it closes the quest; told a carry-on it closes it too. The mode is
    /// how it resumes: <c>resumes</c> advertises and honours <c>session/resume</c>, <c>gone</c> refuses it
    /// <c>resource_not_found</c>. Each frame it acts on goes to the log, with where it ran and on which account.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { appendFileSync } from 'node:fs';
            import { basename } from 'node:path';
            const [log, mode] = process.argv.slice(2);
            const home = process.env.DAORIS_STUB_CONFIG_DIR ?? '';
            const account = home ? basename(home) : '(own)';
            const url = process.env.DAORIS_SERVICE_URL;
            const session = process.env.DAORIS_SESSION_ID;
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const heard = (what) => appendFileSync(log, JSON.stringify({ ...what, cwd: process.cwd(), account, session }) + '\n');
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
                if (mode === 'gone') {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32002, message: 'Resource not found: ' + frame.params?.sessionId } });
                } else {
                  conversation = frame.params.sessionId;
                  resumed = true;
                  send({ jsonrpc: '2.0', id: frame.id, result: {} });
                }
              } else if (frame.method === 'session/prompt') {
                const prompt = (frame.params?.prompt ?? []).map((block) => block.text ?? '').join('\n');
                heard({ method: 'session/prompt', conversation: frame.params?.sessionId, prompt, resumed });
                if (resumed) {
                  say('Listening on the port you named.');
                  await respond({ action: 'done', reason: 'listening on the port the person named' });
                } else if (prompt.includes('You are carrying on quest')) {
                  say('Carried on from what I was handed.');
                  await respond({ action: 'done', reason: 'carried on in a new session' });
                } else {
                  await respond({ action: 'take' });
                  say('Which port should it listen on?');
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

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }

    /// <summary>
    /// The service as ANSWER1b would have it (design §5): sessions with their adapter, account, tree, base and take; the
    /// ledger's moves, a finished record refusing any; an answer kept on a park that stays parked; and a move into
    /// <c>awaiting-person</c> clearing it.
    /// </summary>
    internal sealed class ParkStandIn : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _serving;
        private readonly string _root;
        private readonly List<JsonObject> _sessions = [];
        private readonly Dictionary<string, List<string>> _moves = [];
        private readonly Dictionary<string, string> _startNotes = [];
        private readonly List<JsonObject> _quests;

        public string Url { get; }

        private ParkStandIn(HttpListener listener, string url, string root)
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

        public static ParkStandIn Start(string root)
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var url = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            return new ParkStandIn(listener, url, root);
        }

        public int SessionCount { get { lock (_sessions) return _sessions.Count; } }

        public JsonObject Session(string id)
        {
            lock (_sessions) return (JsonObject)_sessions.Single(s => s["id"]!.GetValue<string>() == id).DeepClone();
        }

        public string Status(string quest)
        {
            lock (_sessions) return _quests.Single(q => q["id"]!.GetValue<string>() == quest)["status"]!.GetValue<string>();
        }

        /// <summary>The states a record moved to since the person answered it.</summary>
        public IReadOnlyList<string> MovesAfterAnswer(string id)
        {
            lock (_sessions) return [.. _moves.GetValueOrDefault(id + "/answered") ?? []];
        }

        /// <summary>The note a session's record carried as it started: the sentence that says why it exists.</summary>
        public string NoteOnStart(string id)
        {
            lock (_sessions) return _startNotes.GetValueOrDefault(id) ?? "";
        }

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

        private (int, string) Answer(HttpListenerRequest request)
        {
            var path = request.Url!.AbsolutePath;
            var all = request.QueryString["includeClosed"] == "true";
            JsonObject Body()
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                return JsonNode.Parse(reader.ReadToEnd())!.AsObject();
            }

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
                            .Select(s => s.DeepClone())]).ToJsonString());

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
                        };
                        _sessions.Add(session);
                        return (200, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "opened" }.ToJsonString());
                    }

                    case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/state", StringComparison.Ordinal):
                    {
                        var id = path["/api/sessions/".Length..^"/state".Length];
                        var body = Body();
                        var session = _sessions.Single(s => s["id"]!.GetValue<string>() == id);
                        var from = session["state"]!.GetValue<string>();
                        var to = body["state"]!.GetValue<string>();
                        if (!Allowed.TryGetValue(from, out var moves) || !moves.Contains(to))
                        {
                            return (409, $$"""{"error":"Session `{{id}}` cannot move {{from}} → {{to}}."}""");
                        }

                        session["state"] = to;
                        if (body["note"] is { } note) session["note"] = note.GetValue<string>();
                        if (to == "starting") _startNotes[id] = session["note"]?.GetValue<string>() ?? "";
                        // ANSWER1b: a session that parks again asks anew.
                        if (to == "awaiting-person") session.Remove("answer");
                        if (session["answer"] is not null) _moves[id + "/answered"].Add(to);
                        return (200, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "moved" }.ToJsonString());
                    }

                    case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/answer", StringComparison.Ordinal):
                    {
                        var id = path["/api/sessions/".Length..^"/answer".Length];
                        var said = Body()["answer"]?.GetValue<string>() ?? "carry on.";
                        var session = _sessions.Single(s => s["id"]!.GetValue<string>() == id);
                        if (session["state"]!.GetValue<string>() != "awaiting-person")
                        {
                            return (409, $$"""{"error":"Session `{{id}}` is not waiting on you."}""");
                        }

                        // ANSWER1b: the answer is kept and the record stays parked, for the driver's next look.
                        session["answer"] = said;
                        session["note"] = $"{session["note"]?.GetValue<string>()}\n\nAnswered: {said}";
                        _moves[id + "/answered"] = [];
                        return (200, new JsonObject
                        {
                            ["session"] = session.DeepClone(),
                            ["message"] = $"Answered session `{id}`: it carries on at the driver's next look.",
                        }.ToJsonString());
                    }

                    case ("POST", "/api/quests/q1/respond"):
                    {
                        var body = Body();
                        var quest = _quests.Single(q => q["id"]!.GetValue<string>() == "q1");
                        switch (body["action"]!.GetValue<string>())
                        {
                            case "take" when quest["status"]!.GetValue<string>() == "Open":
                                quest["status"] = "Taken";
                                // The connector marks the take on the session it speaks for (STANDDOWN2).
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
