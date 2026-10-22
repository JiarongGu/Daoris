using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-CUTOVER1g (D190's binding moment, the cutover design §8; amends D157 point 11): a run is bound inside its start, once the start
/// is past the holds before its tree and before its take, so the take names the graph its run is bound to. A start held after the bind
/// keeps its run bound to what it chose, and a choice changed since applies to the next run; a bind not made holds the start, said, and
/// the next look binds and takes it; a start held before its tree opens binds nothing.
/// </summary>
/// <remarks>
/// Driven through a real look, on a stand-in for Claude Code that answers only its version and its login (as <c>TrustHoldTickTests</c>),
/// under a named profile whose home is this test's, and a stand-in service whose ledger refuses every session it is asked to open, so a
/// start past its holds reaches the open, is refused, and runs nothing. No model anywhere, and no account.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class WorkflowBindStartTickTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-bind-start-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public WorkflowBindStartTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "bind@example.com");
        Git("config", "user.name", "CUTOVER1g");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");

        var profile = DaorisHome.Entry(_home, HomeEntry.AgentHomes, "claude-code", "work");
        Directory.CreateDirectory(profile);
        File.WriteAllText(Path.Combine(profile, ClaudeTrust.FileName), """{"projects":{}}""");
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Harnesses), """{"defaults":{"claude-code":"work"}}""");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static JsonNode Steps() => JsonNode.Parse("""[{"id":"work","kind":"work"},{"id":"landing","kind":"landing","form":"merge","accept":"you"}]""")!;

    private void Save() =>
        WorkflowStore.Add(_home, new WorkflowVersionAdded("merge-after", "Merge after you accept", "terminal", "2026-10-09T09:00:00Z", Steps()), kept: []);

    /// <summary>
    /// The start binds its run before its open, so the ledger's refusal leaves the run bound, said once in the look; a version saved
    /// since leaves it on the version it chose, and the next run takes the newest.
    /// </summary>
    [Fact]
    public async Task A_start_refused_after_its_bind_keeps_its_run_bound_and_a_version_saved_since_applies_to_the_next_run()
    {
        Save();
        await using var service = StandInService.Start(_repository);
        service.Publish("q1");
        var driver = Driver(service, """{"workflows":{"engine":{"default":"merge-after"}}}""");

        var first = await driver.RunOnceAsync();

        Assert.Equal(1, service.Opens);
        Assert.Contains(first.Events, line => line.StartsWith("refused  #q1", StringComparison.Ordinal));
        Assert.Equal(("merge-after", (int?)1), (WorkflowRunBindings.Read(_home, "q1")!.Workflow, WorkflowRunBindings.Read(_home, "q1")!.Version));
        Assert.Single(first.Events, line => line == "workflow  #q1 → engine: its run follows `merge-after` v1, `engine`'s default.");

        // A version saved since: q1's next start finds its run bound to the version it chose, and says nothing again.
        Save();
        var second = await driver.RunOnceAsync();

        Assert.Equal(2, service.Opens);
        Assert.Contains(second.Events, line => line.StartsWith("refused  #q1", StringComparison.Ordinal));
        Assert.Equal((int?)1, WorkflowRunBindings.Read(_home, "q1")!.Version);
        Assert.DoesNotContain(second.Events, line => line.StartsWith("workflow  #q1", StringComparison.Ordinal));

        // The next run takes the newest. (The repository's sessions run in its root, which holds one start a look: q1 goes elsewhere.)
        service.Withdraw("q1");
        service.Publish("q2");
        var third = await driver.RunOnceAsync();

        Assert.Equal(3, service.Opens);
        Assert.Equal((int?)2, WorkflowRunBindings.Read(_home, "q2")!.Version);
        Assert.Single(third.Events, line => line == "workflow  #q2 → engine: its run follows `merge-after` v2, `engine`'s default.");
    }

    /// <summary>
    /// A bind that is not made (here its file cannot be written) holds the start before its open, saying why, so no take goes on under a
    /// graph its run is not bound to; the next look binds the run and goes on to the open.
    /// </summary>
    [Fact]
    public async Task A_bind_not_made_holds_the_start_said_and_the_next_look_binds_and_takes_it()
    {
        // A file where the runs' folder goes: the binding cannot be written beneath it.
        var runs = WorkflowRunBindings.FolderOf(_home);
        Directory.CreateDirectory(Path.GetDirectoryName(runs)!);
        File.WriteAllText(runs, "in the way");
        await using var service = StandInService.Start(_repository);
        service.Publish("q1");
        var driver = Driver(service, "{}");

        var held = await driver.RunOnceAsync();

        Assert.Equal(0, service.Opens);
        Assert.Contains(held.Events, line => line.StartsWith(
            "held  #q1 → engine: its run was not bound to a workflow, and its quest is taken only once it is: ", StringComparison.Ordinal));
        Assert.False(WorkflowRunBindings.Bound(_home, "q1"));

        File.Delete(runs);
        var taken = await driver.RunOnceAsync();

        Assert.Equal(1, service.Opens);
        Assert.True(WorkflowRunBindings.Read(_home, "q1")!.IsCurrent);
        Assert.DoesNotContain(taken.Events, line => line.StartsWith("held  #q1", StringComparison.Ordinal));
    }

    /// <summary>A start held before its tree opens (the checkout holds work in flight) binds nothing, as before.</summary>
    [Fact]
    public async Task A_start_held_before_its_tree_opens_binds_nothing()
    {
        Save();
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine, in flight\n");
        await using var service = StandInService.Start(_repository);
        service.Publish("q1");
        var driver = Driver(service, """{"workflows":{"engine":{"default":"merge-after"}}}""");

        var held = await driver.RunOnceAsync();

        Assert.Equal(0, service.Opens);
        Assert.Contains(held.Events, line => line.StartsWith("held  #q1", StringComparison.Ordinal));
        Assert.False(WorkflowRunBindings.Bound(_home, "q1"));
    }

    private Daoris.Driver.Driver Driver(StandInService service, string choices)
    {
        var config = DriverConfig.Parse(choices) with
        {
            Drivable = ["engine"],
            Adapter = "claude-code",
            Cap = 4,
            TestBound = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["claude-code"] = ["node", Harness()] },
        };
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>A stand-in for Claude Code: it answers its version and its login question, and nothing else.</summary>
    private string Harness()
    {
        var script = Path.Combine(_home, "claude.mjs");
        File.WriteAllText(script, """
            const argv = process.argv.slice(2);
            if (argv.includes('--version')) { console.log('2.1.0 (Claude Code)'); process.exit(0); }
            if (argv[0] === 'auth' && argv[1] === 'status') { console.log('{"loggedIn": true}'); process.exit(0); }
            process.exit(3);
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
    /// The service's reads a look makes: the open quests published to `engine`, adopted with a root here; and a ledger that refuses every
    /// session it is asked to open, counting each ask, so a start past its holds starts nothing.
    /// </summary>
    private sealed class StandInService : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _serving;
        private readonly string _root;
        private readonly List<string> _quests = [];
        private int _opens;

        public string Url { get; }

        public int Opens => Volatile.Read(ref _opens);

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

        /// <summary>An open quest to `engine`, its chain's first.</summary>
        public void Publish(string id)
        {
            lock (_quests) _quests.Add(id);
        }

        /// <summary>A quest no longer listed open here, as one another machine took.</summary>
        public void Withdraw(string id)
        {
            lock (_quests) _quests.Remove(id);
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
            switch (request.HttpMethod, request.Url!.AbsolutePath)
            {
                case ("GET", "/api/quests"):
                    var quests = new JsonArray();
                    lock (_quests)
                    {
                        foreach (var id in _quests)
                        {
                            quests.Add(new JsonObject
                            {
                                ["id"] = id, ["from"] = "game", ["to"] = "engine", ["title"] = $"Expose budget {id}",
                                ["body"] = "The game needs one.", ["status"] = "Open",
                            });
                        }
                    }

                    return (200, quests.ToJsonString());
                case ("GET", "/api/registry"):
                    return (200, new JsonArray(new JsonObject
                    {
                        ["repository"] = "engine", ["adopted"] = true, ["registered"] = true, ["root"] = _root, ["workspace"] = "default",
                    }).ToJsonString());
                case ("POST", "/api/sessions"):
                    Interlocked.Increment(ref _opens);
                    return (409, """{"error":"refused by the stand-in"}""");
                case ("GET", _):
                    return (200, "[]");
                default:
                    return (404, """{"error":"not here"}""");
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
