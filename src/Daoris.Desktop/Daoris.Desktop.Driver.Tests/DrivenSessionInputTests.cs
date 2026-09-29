using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A driven quest session takes no person's line, on either door (INT4i) — what INT4h settled for an
/// intake, for the session the driver starts on a quest.
/// </summary>
/// <remarks>
/// <para>A driven session is handed its whole quest at once and works it in one turn. On the pipe door
/// it has no stdin; on the protocol door its stdin is open, because the driver writes the protocol's
/// frames into it (D53) — so a person's line sent there would land in the middle of the JSON-RPC
/// stream. The page offers such a session no box, but `SESSION_INPUT` names a session by id, and the
/// registry used to track a driven one as taking input.</para>
///
/// <para>Driven through a REAL tick: a real process — node, the stand-in harness every gate already
/// needs — spawned by the executor against a stand-in service on a loopback port. The spawn, the door
/// and the registry entry are exactly what a fake would get wrong. No model anywhere, and no account.</para>
/// </remarks>
public sealed class DrivenSessionInputTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-driven-input-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public DrivenSessionInputTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "driven@example.com");
        Git("config", "user.name", "INT4i");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// 🔴 While a driven session works, the registry refuses a person's line and a finish for it, in a
    /// sentence that names its quest — never "it ended" — and on the protocol door nothing reaches the
    /// agent's stream. The stop still reaches it, and the record says the person ended it.
    /// </summary>
    [Theory]
    [InlineData("stub")]
    [InlineData("acp-stub")]
    public async Task A_driven_session_takes_no_messages_on_either_door(string adapter)
    {
        await using var service = StandInService.Start(_repository);
        var processes = new SessionProcesses();
        var heard = Path.Combine(_home, "heard.txt");
        var driver = Driver(adapter, heard, service, processes);

        var tick = driver.TickAsync();
        await Until(() => processes.Running.Contains("s1"));

        var why = processes.RefusesInput("s1");
        Assert.NotNull(why);
        Assert.Contains("quest #q1", why);
        Assert.False(processes.Send("s1", "are you there?"));
        Assert.False(processes.CloseInput("s1"));

        // Given the agent a moment to have heard anything it was sent: on the protocol door a written
        // line would arrive between its frames, and it records every line that is not one.
        await Task.Delay(300);
        Assert.False(File.Exists(heard), File.Exists(heard) ? File.ReadAllText(heard) : "");

        Assert.True(processes.Stop("s1"));
        await tick;
        Assert.Equal("stopped", service.Session("s1")["state"]!.GetValue<string>());
    }

    /// <summary>
    /// SESS3 (the owner: "there is no way to send additional info in middle of the session"): on the
    /// protocol door a driven session's inbox holds what the person says, and the words are the next
    /// prompt of the SAME session — when its turn ends, or at once by stopping the turn — kept in the
    /// record as the person's. Still nothing is written into the stream (INT4i's line holds).
    /// </summary>
    [Theory]
    [InlineData("after-the-turn")]
    [InlineData("send-now")]
    public async Task A_driven_protocol_session_hears_what_the_person_adds_as_its_next_prompt(string when)
    {
        await using var service = StandInService.Start(_repository);
        var processes = new SessionProcesses();
        var prompts = Path.Combine(_home, "prompts.txt");
        var driver = Driver("acp-turns", prompts, service, processes, turns: when == "send-now" ? "hold" : "quick");

        var tick = driver.TickAsync();
        await Poll.Until(() => processes.InboxOf("s1") is not null && File.Exists(prompts), () => "the first turn never began", TimeSpan.FromSeconds(60));
        var inbox = processes.InboxOf("s1")!;
        Assert.True(inbox.Hold(new ChatMessage("the budget is in level.json, not config.json", [])));
        // Still refused as a line written into the stream: the frames are the driver's (INT4i).
        Assert.False(processes.Send("s1", "are you there?"));

        if (when == "send-now")
        {
            var stop = await inbox.SendNowAsync();
            Assert.True(stop.Cancelled);
            Assert.Empty(stop.Withdrawn);
        }

        await tick.WaitAsync(TimeSpan.FromSeconds(60));

        var heard = File.ReadAllLines(prompts).Select(line => System.Text.Json.JsonSerializer.Deserialize<string>(line)).ToList();
        Assert.Equal(2, heard.Count);
        Assert.Contains("#q1", heard[0]);
        Assert.Equal("the budget is in level.json, not config.json", heard[1]);
        // The same session, not a second one: the agent was asked for one session only.
        Assert.Single(File.ReadAllLines(prompts + ".sessions"));
        // And the record keeps the words as the person's, where the conversation is read.
        var record = Directory.GetFiles(_home, "s1.events.jsonl", SearchOption.AllDirectories).Single();
        Assert.Contains(File.ReadAllLines(record), line =>
            line.Contains("\"origin\":\"person\"") && line.Contains("the budget is in level.json, not config.json"));
        Assert.Null(processes.InboxOf("s1"));
    }

    /// <summary>
    /// 🔴 REV3: a failure between the spawn and the wait — here the record refusing to move to
    /// `working`, which is what a stop pressed during `starting` makes it do — concluded the record and
    /// left the harness running: untracked, unmarked, and holding a tree whose lock had just been freed.
    /// However the tick's session ends, its process ends with it.
    /// </summary>
    [Fact]
    public async Task A_session_the_record_will_not_let_work_does_not_outlive_its_tick()
    {
        await using var service = StandInService.Start(_repository, refuses: "working");
        var processes = new SessionProcesses();
        var driver = Driver("stub", Path.Combine(_home, "heard.txt"), service, processes);

        await driver.TickAsync().WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Empty(processes.Running);
        var heartbeat = Path.Combine(_repository, "heartbeat.txt");
        var before = File.Exists(heartbeat) ? File.ReadAllText(heartbeat) : null;
        await Task.Delay(700);
        var after = File.Exists(heartbeat) ? File.ReadAllText(heartbeat) : null;
        Assert.True(before == after, "the harness is still beating after its tick ended");
    }

    /// <summary>
    /// REV3: an adapter name nobody knows — a typo in `driver.json` — threw from the harness selection
    /// out of the whole tick, every tick. The quest is held on the driver's sentence instead, as the
    /// intake's twin already did, and the tick reports.
    /// </summary>
    [Fact]
    public async Task An_adapter_nobody_knows_holds_the_quest_and_the_tick_still_reports()
    {
        await using var service = StandInService.Start(_repository);
        var processes = new SessionProcesses();
        var driver = Driver("claude_code", Path.Combine(_home, "heard.txt"), service, processes);

        var report = await driver.TickAsync().WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Contains(report.Events, line => line.StartsWith("held", StringComparison.Ordinal) && line.Contains("claude_code"));
        Assert.Empty(processes.Running);
    }

    /// <summary>
    /// 🔴 REV3 driver F9: when the application closes, the tick's own token is cancelled, and the sync
    /// that runs beside the sessions then threw on it — so the tick left before its sessions had written
    /// how they ended, and the process exited with their records still saying `working`. A tick
    /// cancelled mid-session returns only after every session it started is recorded.
    /// </summary>
    [Fact]
    public async Task A_cancelled_tick_returns_only_after_its_sessions_are_recorded()
    {
        await using var service = StandInService.Start(_repository);
        var processes = new SessionProcesses();
        using var sync = new RemoteSyncSet([]);
        var driver = Driver("stub", Path.Combine(_home, "heard.txt"), service, processes, sync);
        using var closing = new CancellationTokenSource();

        var tick = driver.TickAsync(closing.Token);
        await Until(() => processes.Running.Contains("s1"));
        await Task.Delay(1500);   // past one beside-sync, so the loop is where the close finds it
        closing.Cancel();

        // About 50 ms on a quiet machine (measured). Bounded generously because the full suite runs
        // classes in parallel, each spawning node, and one loaded run went past 30 s (2026-09-25).
        try { await tick.WaitAsync(TimeSpan.FromSeconds(90)); }
        catch (OperationCanceledException) { }

        Assert.NotEqual("working", service.Session("s1")["state"]!.GetValue<string>());
    }

    private Daoris.Driver.Driver Driver(
        string adapter, string heard, StandInService service, SessionProcesses processes, RemoteSyncSet? sync = null,
        string turns = "quick")
    {
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Adapter = adapter == "acp-turns" ? "acp-stub" : adapter,
            TimeoutMinutes = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>>
            {
                ["stub"] = ["node", PipeAgent()],
                ["acp-stub"] = adapter == "acp-turns" ? ["node", TurnsAgent(), heard, turns] : ["node", ProtocolAgent(), heard],
            },
        };
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: processes, sync: sync,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
    }

    /// <summary>The pipe door's stand-in: it answers the toolchain's two questions, then holds its turn.</summary>
    private string PipeAgent()
    {
        var script = Path.Combine(_home, "pipe-agent.mjs");
        File.WriteAllText(script, """
            import { writeFileSync } from 'node:fs';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            console.log('stub: driven for quest ' + process.env.DAORIS_QUEST_ID);
            // Proof of life a test can read after the tick: a beat every 100 ms while this runs. Unref'd,
            // so it never keeps a leaked stand-in alive past its own minute.
            setInterval(() => writeFileSync('heartbeat.txt', String(Date.now())), 100).unref();
            await new Promise((resolve) => setTimeout(resolve, 60000));
            """);
        return script;
    }

    /// <summary>
    /// The protocol door's stand-in: it speaks the handshake, takes the prompt and holds its turn — and
    /// writes down any line that arrives which is not a frame, which is what a person's message sent
    /// into its stream would be.
    /// </summary>
    private string ProtocolAgent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            const heard = process.argv[2];
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            for await (const line of createInterface({ input: process.stdin })) {
              let frame;
              try { frame = JSON.parse(line); } catch { appendFileSync(heard, line + '\n'); continue; }
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-1' } });
              } else if (frame.method === 'session/prompt') {
                // The turn is held: answered only by the process ending.
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
            """);
        return script;
    }

    /// <summary>
    /// SESS3's stand-in: it writes down every prompt it is given (as a JSON string a line) and every
    /// session it is asked for. Its first turn ends by itself after a beat (<c>quick</c>), or only when
    /// the turn is stopped (<c>hold</c>); every later turn ends at once. It exits when its input ends.
    /// </summary>
    private string TurnsAgent()
    {
        var script = Path.Combine(_home, "acp-turns-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            const [prompts, mode] = process.argv.slice(2);
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            let turns = 0;
            let held = null;
            for await (const line of createInterface({ input: process.stdin })) {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                appendFileSync(prompts + '.sessions', 'acp-1\n');
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-1' } });
              } else if (frame.method === 'session/prompt') {
                const text = (frame.params?.prompt ?? []).map((block) => block.text ?? '').join('');
                appendFileSync(prompts, JSON.stringify(text) + '\n');
                turns += 1;
                if (turns === 1 && mode === 'hold') held = frame.id;
                else if (turns === 1) setTimeout(() => send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } }), 1500);
                else send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.method === 'session/cancel') {
                if (held !== null) send({ jsonrpc: '2.0', id: held, result: { stopReason: 'cancelled' } });
                held = null;
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
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

    // Sixty seconds, not fifteen: the suite now runs several classes that spawn node at once, and this
    // wait failed twice in one day under that load while passing alone (TASKS, FLAKE1). It only waits for
    // a START, so a longer bound hides no defect in what the test asserts.
    private static Task Until(Func<bool> condition) =>
        Poll.Until(condition, () => "the driven session never started", TimeSpan.FromSeconds(60));

    /// <summary>
    /// A stand-in for the service's doors one driven quest crosses — a real loopback listener, because
    /// the executor's client can only reach a real address. One open quest to `engine`, which is
    /// adopted with a root here; the session records it opens and moves.
    /// </summary>
    internal sealed class StandInService : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _serving;
        private readonly string _root;
        private readonly List<JsonObject> _sessions = [];
        private readonly string? _kept;
        private readonly string? _refuses;

        public string Url { get; }

        private StandInService(HttpListener listener, string url, string root, string? kept, string? refuses)
        {
            _listener = listener;
            Url = url.TrimEnd('/');
            _root = root;
            _kept = kept;
            _refuses = refuses;
            _serving = ServeAsync();
        }

        /// <param name="kept">Where this machine keeps a file the quest carries, or null for a quest with none (INT4j).</param>
        /// <param name="refuses">A state this ledger will not move a record to, as a terminal record refuses every move.</param>
        public static StandInService Start(string root, string? kept = null, string? refuses = null)
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var url = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            return new StandInService(listener, url, root, kept, refuses);
        }

        public JsonObject Session(string id)
        {
            lock (_sessions) return _sessions.Single(s => s["id"]!.GetValue<string>() == id);
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
                        return (200, new JsonArray(new JsonObject
                        {
                            ["id"] = "q1", ["from"] = "game", ["to"] = "engine", ["title"] = "Expose a budget",
                            ["body"] = "The game needs one.", ["status"] = "Open",
                            ["attachments"] = _kept is null
                                ? new JsonArray()
                                : new JsonArray(new JsonObject
                                {
                                    ["name"] = Path.GetFileName(_kept), ["sha256"] = "ab12", ["bytes"] = 2048, ["path"] = _kept,
                                }),
                        }).ToJsonString());

                    case ("GET", "/api/registry"):
                        return (200, new JsonArray(new JsonObject
                        {
                            ["repository"] = "engine", ["adopted"] = true, ["registered"] = true,
                            ["root"] = _root, ["workspace"] = "default",
                        }).ToJsonString());

                    case ("GET", "/api/sessions"):
                        return (200, new JsonArray([.. _sessions
                            .Where(s => all || s["state"]!.GetValue<string>() is "queued" or "starting" or "working" or "awaiting-person")
                            .Select(s => s.DeepClone())]).ToJsonString());

                    case ("POST", "/api/sessions"):
                    {
                        var body = Body();
                        var session = new JsonObject
                        {
                            ["id"] = $"s{_sessions.Count + 1}", ["quest"] = body["quest"]!.GetValue<string>(),
                            ["repository"] = "engine", ["state"] = "queued", ["kind"] = "drive",
                        };
                        _sessions.Add(session);
                        return (200, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "opened" }.ToJsonString());
                    }

                    case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/state", StringComparison.Ordinal):
                    {
                        var id = path["/api/sessions/".Length..^"/state".Length];
                        var body = Body();
                        if (body["state"]!.GetValue<string>() == _refuses)
                        {
                            return (409, $$"""{"error":"session {{id}} will not move to {{_refuses}}"}""");
                        }

                        var session = _sessions.Single(s => s["id"]!.GetValue<string>() == id);
                        session["state"] = body["state"]!.GetValue<string>();
                        if (body["note"] is { } note) session["note"] = note.GetValue<string>();
                        return (200, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "moved" }.ToJsonString());
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
