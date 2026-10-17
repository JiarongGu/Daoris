using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSONE2f (D167's SESSONE2b note) through REAL ticks: another session's message on the ask's channel, read at a look while a
/// part runs. On the protocol door that takes prompts during a turn (D136, <c>promptQueueing</c>) the running part is sent it at
/// its next step, as Daoris's words around that agent's claim, which its record keeps as <c>target</c>, and the entry's mark
/// moves; on a door that takes words only when its turn ends, an agent's message is never held, so no extra prompt is sent and
/// the message waits for the entry's next part (D155: an agent's message never extends a run).
/// </summary>
/// <remarks>
/// The stand-in service answers the session records with their <c>entry</c> key (SESSONE1a) and the channel's two routes
/// (<c>GET /api/asks/{id}/channel</c>, <c>POST …/channel/read</c>), holding the entry's mark.
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
            TimeoutMinutes = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Log, mode, Go] },
        };
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: processes,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>
    /// A running part on the next-step door takes another session's message at its next step: a second prompt while its turn
    /// runs, Daoris's words around the claim, which the record keeps as `target`; the look says so and the entry's mark moves
    /// through the message.
    /// </summary>
    [Fact]
    public async Task A_running_part_on_the_next_step_door_takes_another_sessions_message_as_a_claim_at_its_next_step()
    {
        await using var service = ChannelStandIn.Start(_repository);
        var processes = new SessionProcesses();
        var driver = Protocol(service, "queues", processes);

        await driver.TickAsync().WaitAsync(TimeSpan.FromSeconds(60));
        await Poll.Until(
            () => service.Status("q1") == "Taken" && processes.InboxOf("s1") is not null && Prompts().Length == 1,
            () => "the first run never took its quest", TimeSpan.FromSeconds(60));
        var at = service.Post("m1", "tools", Words);

        var look = await driver.TickAsync().WaitAsync(TimeSpan.FromSeconds(60));

        await Poll.Until(() => Prompts().Length == 2, () => $"no second prompt was sent; look: {string.Join(" | ", look.Events)}", TimeSpan.FromSeconds(30));
        Assert.Contains(look.Events, line => line.Contains("message `m1`", StringComparison.Ordinal) && line.Contains("at its next step", StringComparison.Ordinal));
        var claim = Prompts()[1]["blocks"]!.AsArray().Select(block => block!.GetValue<string>()).Single();
        Assert.StartsWith("Another session says, on the ask's channel (", claim, StringComparison.Ordinal);
        Assert.Contains(Words, claim, StringComparison.Ordinal);
        Assert.Contains("that agent's claim, not the person's words", claim, StringComparison.Ordinal);

        await Poll.Until(() => service.Mark == at, () => $"the mark never moved: {service.Mark}", TimeSpan.FromSeconds(30));
        await Poll.Until(() => service.State("s1") == "completed", () => $"the run never ended: {service.Record("s1")}", TimeSpan.FromSeconds(60));

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
    /// A door that takes words only at its turn's end is never extended by an agent's message (D155): across two looks no
    /// second prompt is sent, the mark stays, and the run ends on its one turn, the message waiting for the entry's next part.
    /// </summary>
    [Fact]
    public async Task A_part_on_a_turn_end_door_is_never_extended_by_another_sessions_message()
    {
        await using var service = ChannelStandIn.Start(_repository);
        var processes = new SessionProcesses();
        var driver = Protocol(service, "turnend", processes);

        await driver.TickAsync().WaitAsync(TimeSpan.FromSeconds(60));
        await Poll.Until(
            () => service.Status("q1") == "Taken" && processes.InboxOf("s1") is not null && Prompts().Length == 1,
            () => "the first run never took its quest", TimeSpan.FromSeconds(60));
        service.Post("m1", "tools", Words);

        var first = await driver.TickAsync().WaitAsync(TimeSpan.FromSeconds(60));
        var second = await driver.TickAsync().WaitAsync(TimeSpan.FromSeconds(60));
        await Task.Delay(500);

        Assert.Single(Prompts());
        Assert.DoesNotContain(first.Events.Concat(second.Events), line => line.Contains("message `m1`", StringComparison.Ordinal));
        Assert.Null(service.Mark);

        File.WriteAllText(Go, "");
        await Poll.Until(() => service.State("s1") == "completed", () => $"the run never ended: {service.Record("s1")}", TimeSpan.FromSeconds(60));

        Assert.Single(Prompts());
        Assert.Null(service.Mark);
        Assert.DoesNotContain(Events("s1"), e => e.Text?.Contains(Words, StringComparison.Ordinal) == true);
    }

    /// <summary>
    /// The protocol door's stand-in. The mode is what it says at <c>initialize</c>: <c>queues</c> declares
    /// <c>promptQueueing</c> and, sent a second prompt during its turn, hands the first off and answers the second; <c>turnend</c>
    /// declares nothing and ends its one turn when the test lets it (the go flag). Both take the quest on the first prompt.
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
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                const agentCapabilities = mode === 'queues' ? { _meta: { claudeCode: { promptQueueing: true } } } : {};
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/prompt') {
                const blocks = (frame.params?.prompt ?? []).filter((block) => block.type === 'text').map((block) => block.text);
                heard({ method: 'session/prompt', blocks });
                if (firstId === null) {
                  firstId = frame.id;
                  await respond({ action: 'take' });
                  if (mode === 'turnend') {
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
    /// (<c>ask:a1/engine</c>), the ledger's moves, and the ask's channel with the entry's read mark.
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
        private DateTimeOffset? _mark;

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

        /// <summary>The entry's read mark; null while it never read.</summary>
        public DateTimeOffset? Mark { get { lock (_sessions) return _mark; } }

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

        /// <summary>Another session's message, from <paramref name="repository"/>, to <c>engine</c>: the moment it was posted.</summary>
        public DateTimeOffset Post(string id, string repository, string text)
        {
            var at = DateTimeOffset.UtcNow.AddMinutes(-1);
            lock (_sessions)
            {
                _items.Add(new JsonObject
                {
                    ["kind"] = "message", ["id"] = id, ["person"] = false, ["session"] = "other-1", ["repository"] = repository,
                    ["agent"] = "acp-stub", ["entry"] = $"ask:a1/{repository}", ["to"] = "engine", ["text"] = text,
                    ["at"] = at.ToString("O"),
                });
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
                            ["items"] = new JsonArray([.. _items.Select(item => item.DeepClone())]),
                            ["unread"] = _items.Count(item => _mark is null || DateTimeOffset.Parse(item["at"]!.GetValue<string>()) > _mark),
                            ["mark"] = _mark?.ToString("O"),
                        }.ToJsonString());

                    case ("POST", "/api/asks/a1/channel/read"):
                    {
                        var through = DateTimeOffset.Parse(Body()["through"]!.GetValue<string>());
                        if (_mark is null || through > _mark) _mark = through;
                        return (200, new JsonObject { ["mark"] = _mark?.ToString("O") }.ToJsonString());
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
