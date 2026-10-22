using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSONE2f (D167's SESSONE2b note) and COWORK1d (D188; the message-urgency design §2.3, §3.1, §4.2) through REAL ticks: another
/// session's message on the ask's channel, read at a look while a part runs. On the protocol door that takes prompts during a turn
/// (D136, <c>promptQueueing</c>) an urgent claim, and one from before, is sent at the part's next step, as Daoris's words around that
/// agent's claim, which its record keeps as <c>target</c>, and the entry acknowledges it by its id; an ordinary claim never enters the
/// turn. On a door that takes words only when its turn ends, even an urgent claim earns no earlier delivery: no extra prompt is sent,
/// the run ends on its own turn, and the claim is let go for the entry's next part (D155: an agent's message never extends a run).
/// </summary>
/// <remarks>
/// The stand-in service answers the session records with their <c>entry</c> key (SESSONE1a) and the channel's two routes
/// (<c>GET /api/asks/{id}/channel</c>, <c>POST …/channel/ack</c>), holding the entry's acknowledgments.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class ChannelNextStepTickTests : IDisposable
{
    private const string Words = "The budget lives in level.json, not in the manifest.";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-chan-tick-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public ChannelNextStepTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "chan@example.com");
        Git("config", "user.name", "SESSONE2f");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Log => Path.Combine(_home, "agent.log");

    private string Go => Path.Combine(_home, "go.flag");

    private JsonObject[] Heard() => [.. StubFile.Lines(Log).Select(line => JsonNode.Parse(line)!.AsObject())];

    private JsonObject[] Prompts() => [.. Heard().Where(line => line["method"]!.GetValue<string>() == "session/prompt")];

    private IReadOnlyList<SessionEvent> Events(string session) => new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After(session, 0).Events;

    private Daoris.Driver.Driver Protocol(ChannelStandIn service, string mode, SessionProcesses processes)
    {
        var adapters = AdapterSet.Built();
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "acp-stub",
            TestBound = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Log, mode, Go] },
        };
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: processes,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>
    /// A running part on the next-step door takes another session's urgent message at its next step (one from before keeps that
    /// reach): a second prompt while its turn runs, Daoris's words around the claim, which the record keeps as `target`; the look says
    /// so and the entry acknowledges it by its id, claimed, sent at the write's return and taken on the door's word.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(null)]
    public async Task A_running_part_on_the_next_step_door_takes_an_urgent_claim_at_its_next_step(bool? urgent)
    {
        await using var service = ChannelStandIn.Start(_repository);
        var processes = new SessionProcesses();
        var driver = Protocol(service, "queues", processes);

        await driver.TickAsync().WaitAsync(Poll.Patience);
        await Poll.Until(
            () => service.Status("q1") == "Taken" && processes.InboxOf("s1") is not null && Prompts().Length == 1,
            () => "the first run never took its quest");
        service.Post("m1", "tools", Words, urgent);

        var look = await driver.TickAsync().WaitAsync(Poll.Patience);

        await Poll.Until(() => Prompts().Length == 2, () => $"no second prompt was sent; look: {string.Join(" | ", look.Events)}", Poll.Patience);
        Assert.Contains(look.Events, line => line.Contains("message `m1`", StringComparison.Ordinal) && line.Contains("at its next step", StringComparison.Ordinal));
        var claim = Prompts()[1]["blocks"]!.AsArray().Select(block => block!.GetValue<string>()).Single();
        Assert.StartsWith("Another session says, on the ask's channel (", claim, StringComparison.Ordinal);
        Assert.Contains(Words, claim, StringComparison.Ordinal);
        Assert.Contains("that agent's claim, not the person's words", claim, StringComparison.Ordinal);

        await Poll.Until(
            () => service.Acks("m1").SequenceEqual(["taking", "sent", "taken"]), () => $"the entry acknowledged: {string.Join(", ", service.Acks("m1"))}",
            Poll.Patience);
        await Poll.Until(() => service.State("s1") == "completed", () => $"the run never ended: {service.Record("s1")}");

        var turns = Events("s1").Where(e => e is { Kind: SessionEventKind.User } && e.Text?.Contains(Words, StringComparison.Ordinal) == true).ToList();
        // Shown as said, reaching the next step, then again under the same id once the agent took it.
        Assert.Equal(["next-step", null], turns.Select(turn => turn.Reaches));
        Assert.Single(turns.Select(turn => turn.Id).Distinct());
        Assert.All(turns, turn =>
        {
            Assert.Equal("target", turn.Origin);
            Assert.StartsWith("Another session says", turn.Text, StringComparison.Ordinal);
        });
        Assert.DoesNotContain(Events("s1"), e => e is { Kind: SessionEventKind.User, Origin: "person" });
    }

    /// <summary>
    /// 🔴 Trap 3 (the design §3.1): a door that takes words only at its turn's end is never extended by an agent's message (D155), an
    /// urgent one included, which earns no earlier delivery there: across two looks no second prompt is sent, and the run ends on its
    /// one turn, the claim let go for the entry's next part and never recorded as said to this one.
    /// </summary>
    [Fact]
    public async Task A_part_on_a_turn_end_door_is_never_extended_by_an_urgent_claim()
    {
        await using var service = ChannelStandIn.Start(_repository);
        var processes = new SessionProcesses();
        var driver = Protocol(service, "turnend", processes);

        await driver.TickAsync().WaitAsync(Poll.Patience);
        await Poll.Until(
            () => service.Status("q1") == "Taken" && processes.InboxOf("s1") is not null && Prompts().Length == 1,
            () => "the first run never took its quest");
        service.Post("m1", "tools", Words, urgent: true);

        var first = await driver.TickAsync().WaitAsync(Poll.Patience);
        var second = await driver.TickAsync().WaitAsync(Poll.Patience);
        await Task.Delay(500);

        Assert.Single(Prompts());
        Assert.Equal(["taking"], service.Acks("m1"));

        File.WriteAllText(Go, "");
        await Poll.Until(() => service.State("s1") == "completed", () => $"the run never ended: {service.Record("s1")}");

        Assert.Single(Prompts());
        await Poll.Until(() => service.Acks("m1").SequenceEqual(["taking", "released"]), () => string.Join(", ", service.Acks("m1")), Poll.Patience);
        Assert.DoesNotContain(Events("s1"), e => e.Text?.Contains(Words, StringComparison.Ordinal) == true);
        Assert.DoesNotContain(first.Events.Concat(second.Events), line => line.Contains("at its next step", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 Trap 4: on the next-step door an ordinary claim never enters the running turn: no second prompt is sent, the run ends on its
    /// own turn, and the claim is let go for the entry's next part.
    /// </summary>
    [Fact]
    public async Task An_ordinary_claim_never_enters_a_running_turn_on_the_next_step_door()
    {
        await using var service = ChannelStandIn.Start(_repository);
        var processes = new SessionProcesses();
        var driver = Protocol(service, "turnend-queues", processes);

        await driver.TickAsync().WaitAsync(Poll.Patience);
        await Poll.Until(
            () => service.Status("q1") == "Taken" && processes.InboxOf("s1") is not null && Prompts().Length == 1,
            () => "the first run never took its quest");
        service.Post("m1", "tools", Words, urgent: false);

        await driver.TickAsync().WaitAsync(Poll.Patience);
        await Task.Delay(500);
        Assert.Single(Prompts());

        File.WriteAllText(Go, "");
        await Poll.Until(() => service.State("s1") == "completed", () => $"the run never ended: {service.Record("s1")}");

        Assert.Single(Prompts());
        await Poll.Until(() => service.Acks("m1").SequenceEqual(["taking", "released"]), () => string.Join(", ", service.Acks("m1")), Poll.Patience);
    }

    /// <summary>
    /// The protocol door's stand-in. The mode is what it says at <c>initialize</c>: <c>queues</c> declares
    /// <c>promptQueueing</c> and, sent a second prompt during its turn, hands the first off and answers the second; <c>turnend</c>
    /// declares nothing and ends its one turn when the test lets it (the go flag); <c>turnend-queues</c> declares
    /// <c>promptQueueing</c> and ends its one turn when the test lets it. Each takes the quest on the first prompt.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { appendFileSync, existsSync } from 'node:fs';
            const [log, mode, go] = process.argv.slice(2);
            const url = process.env.DAORIS_SERVICE_URL;
            const session = process.env.DAORIS_SESSION_ID;
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const heard = (what) => appendFileSync(log, JSON.stringify({ ...what, session }) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            const conversation = 'conv-' + session;
            const say = (text) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
              update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text } } } });
            let firstId = null;
            const queues = mode === 'queues' || mode === 'turnend-queues';
            const waits = mode === 'turnend' || mode === 'turnend-queues';
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                const agentCapabilities = queues ? { _meta: { claudeCode: { promptQueueing: true } } } : {};
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/prompt') {
                const blocks = (frame.params?.prompt ?? []).filter((block) => block.type === 'text').map((block) => block.text);
                heard({ method: 'session/prompt', blocks });
                if (firstId === null) {
                  firstId = frame.id;
                  await respond({ action: 'take' });
                  if (waits) {
                    for (let waited = 0; !existsSync(go) && waited < 30000; waited += 50) await new Promise((r) => setTimeout(r, 50));
                    await respond({ action: 'done', reason: 'served the report' });
                    say('Served.');
                    send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
                  }
                } else {
                  // The next step folded the words in: the first prompt handed off, the work going on as the second's.
                  send({ jsonrpc: '2.0', id: firstId, result: { stopReason: 'end_turn' } });
                  say('Noted, and served.');
                  await respond({ action: 'done', reason: 'served the report' });
                  send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
                }
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
        using var process = Process.Start(TestGit.Past(info))!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }

    /// <summary>
    /// The service as far as these ticks reach: one quest from ask #a1 to <c>engine</c>, sessions carrying their entry key
    /// (<c>ask:a1/engine</c>), the ledger's moves, and the ask's channel with the entry's acknowledgments by message id.
    /// </summary>
    private sealed class ChannelStandIn : IAsyncDisposable
    {
        private const string Key = "ask:a1/engine";

        private readonly HttpListener _listener;
        private readonly Task _serving;
        private readonly string _root;
        private readonly List<JsonObject> _sessions = [];
        private readonly JsonObject _quest = new()
        {
            ["id"] = "q1", ["from"] = "ask #a1", ["to"] = "engine", ["title"] = "Serve the report",
            ["body"] = "The report needs a port.", ["status"] = "Open",
        };
        private readonly List<JsonObject> _items = [];
        // COWORK1d: the entry's acknowledgment of each message, and every state it moved through, in order.
        private readonly Dictionary<string, (string State, string? Session)> _acks = new(StringComparer.Ordinal);
        private readonly List<(string Message, string State)> _acked = [];

        public string Url { get; }

        private ChannelStandIn(HttpListener listener, string url, string root)
        {
            _listener = listener;
            Url = url.TrimEnd('/');
            _root = root;
            _serving = ServeAsync();
        }

        public static ChannelStandIn Start(string root)
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var url = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            return new ChannelStandIn(listener, url, root);
        }

        /// <summary>Each state the entry's acknowledgment of a message moved through, in order.</summary>
        public IReadOnlyList<string> Acks(string message)
        {
            lock (_sessions) return [.. _acked.Where(ack => ack.Message == message).Select(ack => ack.State)];
        }

        public string State(string id)
        {
            lock (_sessions) return _sessions.Single(s => s["id"]!.GetValue<string>() == id)["state"]!.GetValue<string>();
        }

        public string Record(string id)
        {
            lock (_sessions) return _sessions.Single(s => s["id"]!.GetValue<string>() == id).ToJsonString();
        }

        public string Status(string quest)
        {
            lock (_sessions) return _quest["status"]!.GetValue<string>();
        }

        /// <summary>
        /// Another session's message, from <paramref name="repository"/>, to <c>engine</c>, at its level (none for one from before):
        /// the moment it was posted.
        /// </summary>
        public DateTimeOffset Post(string id, string repository, string text, bool? urgent = null)
        {
            var at = DateTimeOffset.UtcNow.AddMinutes(-1);
            lock (_sessions)
            {
                var item = new JsonObject
                {
                    ["kind"] = "message", ["id"] = id, ["person"] = false, ["session"] = "other-1", ["repository"] = repository,
                    ["agent"] = "acp-stub", ["entry"] = $"ask:a1/{repository}", ["to"] = "engine", ["text"] = text,
                    ["at"] = at.ToString("O"),
                };
                if (urgent is { } level) item["urgent"] = level;
                _items.Add(item);
            }

            return at;
        }

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

        private static readonly HashSet<string> Live = ["queued", "starting", "working", "awaiting-person"];

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
                        return (200, new JsonArray([.. new[] { _quest }
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
                            ["repository"] = "engine", ["state"] = "queued", ["kind"] = "drive", ["entry"] = Key,
                            ["adapter"] = body["adapter"]?.GetValue<string>(),
                            ["harnessVersion"] = body["harnessVersion"]?.GetValue<string>(),
                            ["tree"] = body["tree"]?.GetValue<string>(),
                            ["baseCommit"] = body["baseCommit"]?.GetValue<string>(),
                            ["profile"] = body["profile"]?.GetValue<string>(),
                            ["created"] = DateTimeOffset.UtcNow.AddMinutes(-10).ToString("O"),
                            ["said"] = new JsonArray(), ["answer"] = null,
                        };
                        _sessions.Add(session);
                        return (200, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "opened" }.ToJsonString());
                    }

                    case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/state", StringComparison.Ordinal):
                    {
                        var id = path["/api/sessions/".Length..^"/state".Length];
                        var body = Body();
                        var session = _sessions.Single(s => s["id"]!.GetValue<string>() == id);
                        session["state"] = body["state"]!.GetValue<string>();
                        if (body["note"]?.GetValue<string>() is { } note) session["note"] = note;
                        return (200, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "moved" }.ToJsonString());
                    }

                    case ("POST", "/api/quests/q1/respond"):
                    {
                        switch (Body()["action"]!.GetValue<string>())
                        {
                            case "take" when _quest["status"]!.GetValue<string>() == "Open":
                                _quest["status"] = "Taken";
                                break;
                            case "done" when _quest["status"]!.GetValue<string>() == "Taken":
                                _quest["status"] = "Done";
                                break;
                            default:
                                return (409, """{"error":"refused"}""");
                        }

                        return (200, new JsonObject { ["message"] = "moved" }.ToJsonString());
                    }

                    case ("GET", "/api/asks/a1/channel"):
                        return (200, new JsonObject
                        {
                            ["ask"] = "a1", ["entry"] = request.QueryString["entry"],
                            ["items"] = new JsonArray([.. _items.Select(item =>
                            {
                                var answered = item.DeepClone().AsObject();
                                if (_acks.TryGetValue(item["id"]!.GetValue<string>(), out var ack))
                                {
                                    answered["ack"] = ack.State;
                                    answered["ackSession"] = ack.Session;
                                }

                                return answered;
                            })]),
                            ["unread"] = _items.Count,
                        }.ToJsonString());

                    case ("POST", "/api/asks/a1/channel/ack"):
                    {
                        // As the service's one statement: a claim only where none is held, a let-go only of one's own claim.
                        var body = Body();
                        var (state, session) = (body["state"]!.GetValue<string>(), body["session"]?.GetValue<string>());
                        var changed = new JsonArray();
                        foreach (var id in body["messages"]!.AsArray().Select(m => m!.GetValue<string>()))
                        {
                            var held = _acks.TryGetValue(id, out var ack) ? ack : default;
                            var moves = state switch
                            {
                                "taking" => held.State is null,
                                "released" => held.State == "taking" && held.Session == session,
                                _ => true,
                            };
                            if (!moves) continue;
                            if (state == "released") _acks.Remove(id);
                            else _acks[id] = (state, session);
                            _acked.Add((id, state));
                            changed.Add(id);
                        }

                        return (200, new JsonObject { ["acknowledged"] = changed }.ToJsonString());
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
