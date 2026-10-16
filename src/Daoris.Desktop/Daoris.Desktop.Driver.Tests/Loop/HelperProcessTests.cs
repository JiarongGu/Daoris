using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT2a-ii (D163 point 2; the mixed agents design §3, §5): a helper's whole pass on the real path, with no model. A working
/// session's ask on file is taken at a look, judged by the person's rule, started in a clone of its own made by real git, run as
/// a real process (node, the stand-in harness every gate needs) that answers through the answer file its connector's
/// <c>helper_answer</c> writes, or says words and ends; the helper's state names it BEFORE its turn; and the answer reaches the
/// asker as a word marked as the helper's, through a stand-in for the local host's two doors.
/// </summary>
/// <remarks>
/// Real git and a real process against a stand-in service on a loopback port, so the class is in the real-process half (MOD8): a
/// branch's worktree cannot run it, the parent runs it on the merge. It mirrors <see cref="ReviewTreeTests"/>' pass, the
/// reviewer's, which this reuses machine for machine.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class HelperProcessTests : IDisposable
{
    private const string AskId = "a1b2c3d4";

    private readonly GitTree _repository = new("helper-pass");
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-helper-pass-" + Guid.NewGuid().ToString("N")[..8]);
    private readonly string _tip;

    public HelperProcessTests()
    {
        Directory.CreateDirectory(_home);
        _repository.Commit("report.md");
        _tip = _repository.Output("rev-parse HEAD");
    }

    public void Dispose()
    {
        _repository.Dispose();
        try
        {
            foreach (var file in Directory.EnumerateFiles(_home, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
            Directory.Delete(_home, recursive: true);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private HelperFiles Files => new(_home);

    private void Ask(string question = "Why does the daily report drop its last day?")
    {
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Helpers));
        File.WriteAllText(Files.PathOf(AskId, "ask"),
            new JsonObject
            {
                ["id"] = AskId, ["asker"] = "s1", ["question"] = question, ["agent"] = null, ["askedBy"] = "agent", ["at"] = DateTimeOffset.UtcNow.ToString("O"),
            }.ToJsonString());
    }

    /// <summary>The look's driver: the rule on with the stub the only agent, the asker's tree read as its repository's, the real pass.</summary>
    private async Task<(Daoris.Driver.Driver Driver, HelperStandIn Service)> StartAsync(string agent)
    {
        var service = HelperStandIn.Start(_repository.Root, _tip);
        var config = DriverConfig.Empty with
        {
            Adapter = "claude-code",
            Cap = 3,
            TimeoutMinutes = 2,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", agent] },
            Helpers = new Dictionary<string, HelperRule> { ["reports"] = new(HelperRules.On, ["stub"]) },
        };
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            HelperTrees = (_, _) => Task.FromResult<(string?, string?)>((_tip, _repository.Root)),
        };
        await Task.CompletedTask;
        return (driver, service);
    }

    private static async Task SettleAsync(Daoris.Driver.Driver driver, HelperFiles files, Func<HelperState?, bool> done)
    {
        await driver.Running.SettledAsync().WaitAsync(TimeSpan.FromSeconds(90));
        var until = DateTime.UtcNow.AddSeconds(30);
        while (!done(files.ReadState(AskId)))
        {
            Assert.True(DateTime.UtcNow < until, "the helper's ask did not settle");
            await Task.Delay(50);
        }
    }

    /// <summary>
    /// 🔴 The whole pass: asked, started with `serves` (the asker's id on the record the host opened), the state naming the helper
    /// before its turn, answered by `helper_answer`'s file, and the answer reaching the asker as a `Helper`-marked word, prefaced as
    /// the helper's claim; the clone gone after, and the asker's repository untouched.
    /// </summary>
    [Fact]
    public async Task A_helper_started_on_a_stub_agent_answers_through_its_tool_and_the_answer_reaches_the_asker_marked()
    {
        var refs = _repository.Output("for-each-ref --format=\"%(refname) %(objectname)\"");
        Ask();
        var (driver, service) = await StartAsync(Agent(answers: true));
        await using var _ = service;

        await driver.HelpersAsync(new Snapshot([], [], []), 0, [], CancellationToken.None);
        await SettleAsync(driver, Files, state => state is { State: HelperStates.Answered, Handed: not null });

        // The record the host opened: serves the asker, on the stub, in a clone under the home's trees.
        var opened = Assert.Single(service.Opened);
        Assert.Equal(("s1", "stub"), (opened["asker"]!.GetValue<string>(), opened["adapter"]!.GetValue<string>()));
        Assert.StartsWith(DaorisHome.Entry(_home, HomeEntry.Trees), opened["tree"]!.GetValue<string>());
        Assert.Equal(["starting", "working", "completed"], service.States("h1"));

        // 🔴 What the helper saw at its turn: its state already named it (helper_answer finds its ask by that), its own session
        // and no quest, in its copy, handed the question as the asker wrote it.
        var saw = JsonNode.Parse(File.ReadAllText(Path.Combine(_home, "saw.json")))!;
        Assert.Equal("h1", saw["session"]!.GetValue<string>());
        Assert.Null(saw["quest"]);
        Assert.Contains("Why does the daily report drop its last day?", saw["target"]!.GetValue<string>());
        Assert.Contains(_tip, saw["target"]!.GetValue<string>());
        var seen = JsonNode.Parse(saw["state"]!.GetValue<string>())!;
        Assert.Equal(("started", "h1"), (seen["state"]!.GetValue<string>(), seen["helper"]!.GetValue<string>()));

        var hand = Assert.Single(service.Hands);
        Assert.Equal("h1", hand.Helper);
        Assert.StartsWith("Another agent, `stub` (helper session `h1`), answered the question you asked it", hand.Text);
        Assert.Contains("> The window is exclusive at report.md:1.", hand.Text);
        Assert.Equal(("h1", null), (service.Words.Single()["helper"]!.GetValue<string>(), service.Words.Single()["by"]?.GetValue<string>()));

        Assert.Equal(refs, _repository.Output("for-each-ref --format=\"%(refname) %(objectname)\""));
        Assert.Empty(Directory.EnumerateDirectories(DaorisHome.Entry(_home, HomeEntry.Trees,"work", "reports")));
    }

    /// <summary>The helper never used its tool and said words: its last words, bounded, stand as the answer, marked.</summary>
    [Fact]
    public async Task A_helper_that_used_no_tool_has_its_last_words_stand_as_the_answer()
    {
        Ask();
        var (driver, service) = await StartAsync(Agent(answers: false));
        await using var _ = service;

        await driver.HelpersAsync(new Snapshot([], [], []), 0, [], CancellationToken.None);
        await SettleAsync(driver, Files, state => state is { State: HelperStates.Answered, Handed: not null });

        Assert.True(Files.ReadState(AskId)!.LastWords);
        Assert.False(File.Exists(Files.PathOf(AskId, "answer")));
        var hand = Assert.Single(service.Hands);
        Assert.Contains("did not use its answer tool", hand.Text);
        Assert.Contains("> It is the window being exclusive.", hand.Text);
    }

    /// <summary>
    /// The stub helper: a harness with real mechanics and no model. It says what it saw (including the state file it was started
    /// under), then either answers through the file <c>helper_answer</c> writes or ends having said words only.
    /// </summary>
    private string Agent(bool answers)
    {
        var script = Path.Combine(_home, "helper-agent.mjs");
        var home = _home.Replace("\\", "/");
        File.WriteAllText(script, $$"""
            import { writeFileSync, readFileSync, mkdirSync } from 'node:fs';
            import { dirname, join } from 'node:path';
            import { fileURLToPath } from 'node:url';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }

            const home = '{{home}}';
            let state = null;
            try { state = readFileSync(join(home, 'helpers', '{{AskId}}.state.json'), 'utf8'); } catch {}
            writeFileSync(join(dirname(fileURLToPath(import.meta.url)), 'saw.json'), JSON.stringify({
              cwd: process.cwd(),
              session: process.env.DAORIS_SESSION_ID ?? null,
              quest: process.env.DAORIS_QUEST_ID ?? null,
              target: process.env.DAORIS_TARGET ?? '',
              state,
            }));

            if ({{(answers ? "true" : "false")}}) {
              mkdirSync(join(home, 'helpers'), { recursive: true });
              writeFileSync(join(home, 'helpers', '{{AskId}}.answer.json'), JSON.stringify({
                id: '{{AskId}}', helper: process.env.DAORIS_SESSION_ID, answer: 'The window is exclusive at report.md:1.', at: new Date().toISOString(),
              }));
              console.log('stub: answered through its tool');
            } else {
              console.log('It is the window being exclusive.');
            }
            """);
        return script;
    }

    /// <summary>
    /// The service's doors a helper crosses, on a real loopback listener, because the stub helper is a real process: the asker's
    /// record (an ended session in its tree), the host's creation door (<c>POST /api/helpers</c>), the ledger's state door and the
    /// hand door that keeps the answer on the asker as a word marked `helper`.
    /// </summary>
    private sealed class HelperStandIn : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _serving;
        private readonly string _root;
        private readonly string _tip;
        private readonly Dictionary<string, List<string>> _states = [];

        public string Url { get; }

        public List<JsonObject> Opened { get; } = [];

        public List<(string Helper, string Text)> Hands { get; } = [];

        public List<JsonObject> Words { get; } = [];

        private HelperStandIn(HttpListener listener, string url, string root, string tip)
        {
            _listener = listener;
            Url = url.TrimEnd('/');
            (_root, _tip) = (root, tip);
            _serving = ServeAsync();
        }

        public static HelperStandIn Start(string root, string tip)
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var url = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            return new HelperStandIn(listener, url, root, tip);
        }

        public IReadOnlyList<string> States(string session)
        {
            lock (_states) return _states.TryGetValue(session, out var moved) ? [.. moved] : [];
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
            JsonObject Body()
            {
                using var reader = new StreamReader(request.InputStream, Encoding.UTF8);
                return JsonNode.Parse(reader.ReadToEnd())!.AsObject();
            }

            lock (_states)
            {
                switch (request.HttpMethod, path)
                {
                    case ("GET", "/api/sessions"):
                    {
                        var asker = new JsonObject
                        {
                            ["id"] = "s1", ["quest"] = "q1", ["repository"] = "reports", ["state"] = "completed", ["kind"] = "driven",
                            ["adapter"] = "claude-code", ["workspace"] = "work", ["tree"] = _root, ["said"] = new JsonArray([.. Words]),
                        };
                        return (200, new JsonArray(asker).ToJsonString());
                    }

                    case ("POST", "/api/helpers"):
                    {
                        var body = Body();
                        Opened.Add(body);
                        _states["h1"] = [];
                        var session = new JsonObject
                        {
                            ["id"] = "h1", ["repository"] = "reports", ["state"] = "queued", ["kind"] = "chat", ["adapter"] = body["adapter"]!.GetValue<string>(),
                            ["workspace"] = "work", ["serves"] = body["asker"]!.GetValue<string>(), ["tree"] = body["tree"]!.GetValue<string>(),
                        };
                        return (200, new JsonObject { ["message"] = "Session `h1` reads `reports` in a clone of its own.", ["session"] = session }.ToJsonString());
                    }

                    case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/state", StringComparison.Ordinal):
                    {
                        var id = path["/api/sessions/".Length..^"/state".Length];
                        var state = Body()["state"]!.GetValue<string>();
                        if (!_states.TryGetValue(id, out var moved)) return (404, """{"error":"no such session"}""");
                        moved.Add(state);
                        return (200, new JsonObject { ["session"] = new JsonObject { ["id"] = id, ["state"] = state }, ["message"] = "moved" }.ToJsonString());
                    }

                    case ("POST", "/api/helpers/h1/hand"):
                    {
                        var text = Body()["text"]!.GetValue<string>();
                        Hands.Add(("h1", text));
                        var word = new JsonObject
                        {
                            ["id"] = "hw1", ["text"] = text, ["at"] = DateTimeOffset.UtcNow.ToString("O"), ["files"] = new JsonArray(), ["reopens"] = true, ["helper"] = "h1",
                        };
                        Words.Add(word);
                        return (200, new JsonObject { ["message"] = "Handed.", ["said"] = word.DeepClone() }.ToJsonString());
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
