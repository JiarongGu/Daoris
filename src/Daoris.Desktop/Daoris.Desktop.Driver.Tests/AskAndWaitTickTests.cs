using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Ask and wait (D79), through REAL ticks: a quest's session asks another repository and waits, the
/// driver leaves the quest sitting while the question is open, and once it closes the driver resumes
/// the quest — in the same tree, handed the answer. A real process (node) and a stand-in service on a
/// loopback port, the shape <see cref="DrivenSessionInputTests"/> uses; no model and no account.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class AskAndWaitTickTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-ask-wait-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public AskAndWaitTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "ask@example.com");
        Git("config", "user.name", "ASK2");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_session_that_asks_and_waits_is_resumed_in_its_tree_with_the_answer()
    {
        await using var service = StandIn.Start(_repository);
        var log = Path.Combine(_home, "agent.log");
        var driver = Driver(service, log);

        // 1. The quest's first session takes it, asks `backend`, waits, and ends. A good ending.
        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));
        var first = service.Session("s1");
        Assert.Equal("completed", first["state"]!.GetValue<string>());
        Assert.Contains("#q2", first["note"]!.GetValue<string>());
        var tree = first["tree"]!.GetValue<string>();
        Assert.NotEqual(Path.GetFullPath(_repository), Path.GetFullPath(tree));

        // 2. While the question is open the quest sits, saying what it waits on, and nothing starts.
        var waiting = await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));
        var sitting = Assert.Single(waiting.Considerations, c => c.Quest.Id == "q1");
        Assert.Equal(StartVerdict.Waiting, sitting.Verdict);
        Assert.Contains("#q2", sitting.Reason);
        Assert.Equal(1, service.SessionCount);

        // 3. `backend` answers. The quest resumes in the tree it asked from, handed what came back.
        service.Close("q2", "Done", "POST /notes takes { text }.");
        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        var resumed = service.Session("s2");
        Assert.Equal("completed", resumed["state"]!.GetValue<string>());
        Assert.Equal(Path.GetFullPath(tree), Path.GetFullPath(resumed["tree"]!.GetValue<string>()), ignoreCase: true);
        Assert.Equal("Done", service.Status("q1"));

        var runs = File.ReadAllLines(log).Select(line => JsonNode.Parse(line)!).ToList();
        Assert.Equal(2, runs.Count);
        Assert.Contains("First take the quest", runs[0]["target"]!.GetValue<string>());
        var handed = runs[1]["target"]!.GetValue<string>();
        Assert.Contains("resuming quest `#q1`", handed);
        Assert.Contains("POST /notes takes { text }.", handed);
        Assert.Equal(Path.GetFullPath(tree), Path.GetFullPath(runs[1]["cwd"]!.GetValue<string>()), ignoreCase: true);
    }

    /// <summary>
    /// 🔴 A resumed session that stops short — the answer in hand, the quest still taken and still
    /// waiting on the old question — is waiting on the person (STANDDOWN2), never "someone else has
    /// it", and a park is not resumed again by itself, so nothing loops.
    /// </summary>
    [Fact]
    public async Task A_resumed_session_that_stops_short_waits_on_the_person_and_is_not_resumed_again()
    {
        await using var service = StandIn.Start(_repository);
        var driver = Driver(service, Path.Combine(_home, "agent.log"), resumedDoesNothing: true);

        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));
        service.Close("q2", "Declined", "Not ours.");
        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        var resumed = service.Session("s2");
        Assert.Equal("awaiting-person", resumed["state"]!.GetValue<string>());
        Assert.DoesNotContain("someone else", resumed["note"]!.GetValue<string>());

        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Equal(2, service.SessionCount);
    }

    private Daoris.Driver.Driver Driver(StandIn service, string log, bool resumedDoesNothing = false)
    {
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "stub",
            TimeoutMinutes = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>>
            {
                ["stub"] = ["node", Agent(), log, resumedDoesNothing ? "idle" : "work"],
            },
        };
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
    }

    /// <summary>
    /// The stand-in harness: it writes down where it ran and what it was handed, then does what an
    /// agent following that target would — take, ask and wait on a first start; close done on a resume.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const [log, mode] = process.argv.slice(2);
            const target = process.env.DAORIS_TARGET ?? '';
            appendFileSync(log, JSON.stringify({ cwd: process.cwd(), target }) + '\n');
            const respond = (body) => fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            if (!target.includes('resuming quest')) {
              await respond({ action: 'take' });
              await respond({ action: 'wait', on: 'q2' });
            } else if (mode === 'work') {
              await respond({ action: 'done', reason: 'landed, on the contract backend gave' });
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

    /// <summary>
    /// A stand-in for the doors a waiting quest crosses: two quests — `q1` to `engine`, adopted with a
    /// root here, and the question `q2` to `backend`, which this machine does not hold — the session
    /// records, and the respond door's take, wait and done as the exchange judges them.
    /// </summary>
    internal sealed class StandIn : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _serving;
        private readonly string _root;
        private readonly List<JsonObject> _sessions = [];
        private readonly List<JsonObject> _quests;

        public string Url { get; }

        private StandIn(HttpListener listener, string url, string root)
        {
            _listener = listener;
            Url = url.TrimEnd('/');
            _root = root;
            _quests =
            [
                new JsonObject
                {
                    ["id"] = "q1", ["from"] = "ask #a1", ["to"] = "engine", ["title"] = "Add the note field",
                    ["body"] = "The report needs one.", ["status"] = "Open",
                },
                new JsonObject
                {
                    ["id"] = "q2", ["from"] = "engine", ["to"] = "backend", ["title"] = "What does the notes endpoint take?",
                    ["body"] = "We need the contract.", ["status"] = "Open",
                },
            ];
            _serving = ServeAsync();
        }

        public static StandIn Start(string root)
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var url = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            return new StandIn(listener, url, root);
        }

        public int SessionCount { get { lock (_sessions) return _sessions.Count; } }

        public JsonObject Session(string id)
        {
            lock (_sessions) return (JsonObject)_sessions.Single(s => s["id"]!.GetValue<string>() == id).DeepClone();
        }

        public string Status(string quest)
        {
            lock (_sessions) return Quest(quest)["status"]!.GetValue<string>();
        }

        public void Close(string quest, string status, string note)
        {
            lock (_sessions)
            {
                Quest(quest)["status"] = status;
                Quest(quest)["note"] = note;
            }
        }

        private JsonObject Quest(string id) => _quests.Single(q => q["id"]!.GetValue<string>() == id);

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
                        // Open and taken only, unless asked for the closed too — as the service answers.
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
                            .Where(s => all || s["state"]!.GetValue<string>() is "queued" or "starting" or "working" or "awaiting-person")
                            .Select(s => s.DeepClone())]).ToJsonString());

                    case ("POST", "/api/sessions"):
                    {
                        var body = Body();
                        var session = new JsonObject
                        {
                            ["id"] = $"s{_sessions.Count + 1}", ["quest"] = body["quest"]!.GetValue<string>(),
                            ["repository"] = "engine", ["state"] = "queued", ["kind"] = "drive",
                            ["tree"] = body["tree"]?.GetValue<string>(),
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
                        session["state"] = body["state"]!.GetValue<string>();
                        if (body["note"] is { } note) session["note"] = note.GetValue<string>();
                        // Kept once said, as the service keeps it (TOOL4c).
                        if (body["limit"] is { } limit && limit.GetValue<bool>()) session["limit"] = true;
                        return (200, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "moved" }.ToJsonString());
                    }

                    case ("POST", "/api/quests/q1/respond"):
                    {
                        var body = Body();
                        var quest = Quest("q1");
                        switch (body["action"]!.GetValue<string>())
                        {
                            case "take":
                                quest["status"] = "Taken";
                                break;
                            case "wait" when quest["status"]!.GetValue<string>() == "Taken":
                                quest["awaits"] = body["on"]!.GetValue<string>();
                                break;
                            case "done":
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
