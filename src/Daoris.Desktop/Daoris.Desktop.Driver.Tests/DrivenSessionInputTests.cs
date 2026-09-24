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

    private Daoris.Driver.Driver Driver(string adapter, string heard, StandInService service, SessionProcesses processes)
    {
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Adapter = adapter,
            TimeoutMinutes = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>>
            {
                ["stub"] = ["node", PipeAgent()],
                ["acp-stub"] = ["node", ProtocolAgent(), heard],
            },
        };
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: processes,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
    }

    /// <summary>The pipe door's stand-in: it answers the toolchain's two questions, then holds its turn.</summary>
    private string PipeAgent()
    {
        var script = Path.Combine(_home, "pipe-agent.mjs");
        File.WriteAllText(script, """
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            console.log('stub: driven for quest ' + process.env.DAORIS_QUEST_ID);
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

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }

    private static async Task Until(Func<bool> condition)
    {
        for (var waited = 0; !condition(); waited += 50)
        {
            if (waited > 15_000) throw new TimeoutException("the driven session never started");
            await Task.Delay(50);
        }
    }

    /// <summary>
    /// A stand-in for the service's doors one driven quest crosses — a real loopback listener, because
    /// the executor's client can only reach a real address. One open quest to `engine`, which is
    /// adopted with a root here; the session records it opens and moves.
    /// </summary>
    private sealed class StandInService : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _serving;
        private readonly string _root;
        private readonly List<JsonObject> _sessions = [];

        public string Url { get; }

        private StandInService(HttpListener listener, string url, string root)
        {
            _listener = listener;
            Url = url.TrimEnd('/');
            _root = root;
            _serving = ServeAsync();
        }

        public static StandInService Start(string root)
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var url = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            return new StandInService(listener, url, root);
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
