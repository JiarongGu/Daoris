using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A session whose record says it runs must have something running it (2026-09-25). A chat open when
/// the shell closed was left `working` with no process behind it, and stop could not end it, because
/// stop only knew the processes this driver held.
/// </summary>
/// <remarks>
/// <para><b>"Not held here" is not "dead".</b> A terminal's driver or chat shares the home and holds
/// its own processes, so a record this driver cannot reach may still be running. Every tracked process
/// leaves a marker under the home, and only a record with no live marker is an orphan.</para>
///
/// <para>Real processes (node, as every gate already needs) and a stand-in service on a loopback port:
/// the spawn, the marker and the record's move are exactly what a fake would get wrong.</para>
/// </remarks>
public sealed class OrphanedSessionTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-orphans-" + Guid.NewGuid().ToString("N")[..8]);

    private string Markers => Path.Combine(_home, "sessions");

    private readonly List<Process> _spawned = [];

    public OrphanedSessionTests() => Directory.CreateDirectory(Path.Combine(_home, "engine"));

    public void Dispose()
    {
        foreach (var process in _spawned)
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            process.Dispose();
        }

        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private Process Holding()
    {
        var process = Process.Start(new ProcessStartInfo("node", ["-e", "setTimeout(() => {}, 60000)"])
        {
            UseShellExecute = false,
        })!;
        _spawned.Add(process);
        return process;
    }

    /// <summary>Another driver on this machine — a terminal's — sees the process this one holds, and no longer once it is released.</summary>
    [Fact]
    public void A_held_process_is_alive_to_every_driver_sharing_the_home_until_it_is_released()
    {
        var desktop = new SessionProcesses(Markers);
        var terminal = new SessionProcesses(Markers);

        var tracked = desktop.Track("a1b2c3d4", Holding());
        Assert.True(terminal.AliveOnThisMachine("a1b2c3d4"));

        tracked.Dispose();
        Assert.False(terminal.AliveOnThisMachine("a1b2c3d4"));
        Assert.Empty(Directory.GetFiles(Markers));
    }

    /// <summary>A crash leaves the marker and not the process: the marker alone is not life.</summary>
    [Fact]
    public void A_marker_whose_process_died_without_releasing_it_is_not_alive()
    {
        var process = Holding();
        _ = new SessionProcesses(Markers).Track("a1b2c3d4", process);
        process.Kill();
        process.WaitForExit();

        Assert.False(new SessionProcesses(Markers).AliveOnThisMachine("a1b2c3d4"));
    }

    /// <summary>
    /// The sweep ends this machine's `working` record with nothing running it, and nothing else: not a
    /// session another driver here holds, a teammate's, a parked one, or one still starting.
    /// </summary>
    [Fact]
    public async Task The_sweep_ends_only_a_working_record_nothing_on_this_machine_runs()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        service.Seed("a1", "working");
        service.Seed("a2", "working");
        service.Seed("person@machine-b/a3", "working");
        service.Seed("a4", "awaiting-person");
        service.Seed("a5", "starting");
        _ = new SessionProcesses(Markers).Track("a2", Holding());

        using var client = new ServiceClient(service.Url, null);
        var ended = await Orphans.EndAsync(client, new SessionProcesses(Markers));

        Assert.Equal(["a1"], ended.Select(e => e.Id));
        Assert.Equal("stopped", service.State("a1"));
        Assert.Contains("nothing on this machine was running it", service.Note("a1"));
        Assert.Equal(("working", "working", "awaiting-person", "starting"),
            (service.State("a2"), service.State("person@machine-b/a3"), service.State("a4"), service.State("a5")));
    }

    /// <summary>The person's stop on one record ends it even while it says it is starting — they asked, about that one.</summary>
    [Fact]
    public async Task A_stop_asked_of_one_orphan_ends_that_one_starting_or_working()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        service.Seed("a1", "working");
        service.Seed("a5", "starting");

        using var client = new ServiceClient(service.Url, null);
        var ended = await Orphans.EndAsync(client, new SessionProcesses(Markers), only: "a5");

        Assert.Equal(["a5"], ended.Select(e => e.Id));
        Assert.Equal(("working", "stopped"), (service.State("a1"), service.State("a5")));
    }

    /// <summary>
    /// 🔴 The shutdown half: a runner disposed inside the scope of the client it concludes through ends
    /// its chats and records each before the client goes — the loop's own `using` order. Stopping them
    /// after the client was disposed wrote nothing, and the record was left `working` (seen on the
    /// window, 2026-09-25: the loop's client is scoped to its run, and the shell stopped the chats after).
    /// </summary>
    [Fact]
    public async Task A_runner_disposed_before_its_client_ends_its_chats_and_records_each()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"), TimeSpan.FromMilliseconds(400));
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Conversing()] },
        };
        var adapters = AdapterSet.Built();
        var processes = new SessionProcesses(Markers);
        string? id;

        using (var client = new ServiceClient(service.Url, null))
        {
            using var runner = new ChatRunner(
                client, adapters, _home, processes,
                harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));

            id = (await runner.StartAsync("engine", "stub", config)).SessionId;
            Assert.NotNull(id);
            await Until(() => service.State(id!) == "working");
        }

        Assert.Equal("stopped", service.State(id!));
        Assert.Contains("closed", service.Note(id!));
        Assert.Empty(processes.Running);
    }

    /// <summary>
    /// A crash no shutdown order reaches: the loop's first look ends what the last run left `working`,
    /// before that tick counts it against the cap and its repository's lock — in both hosts, since
    /// both run this loop — and its report says so.
    /// </summary>
    [Fact]
    public async Task The_loop_ends_what_the_last_run_left_working_before_its_first_tick()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        service.Seed("a1", "working");
        File.WriteAllText(Path.Combine(_home, "driver.json"), "{}");

        using var client = new ServiceClient(service.Url, null);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var reports = new List<TickReport>();
        var errors = new List<Exception>();
        var watch = new DriverWatch(
            client, Path.Combine(_home, "driver.json"), _home, new SessionProcesses(Markers), sync: null,
            harnesses: new HarnessRoster(AdapterSet.Built(), Path.Combine(_home, "harnesses.json")));

        await watch.RunAsync(
            (report, _) => { reports.Add(report); stop.Cancel(); return Task.CompletedTask; },
            error => { errors.Add(error); stop.Cancel(); return Task.CompletedTask; },
            stop.Token);

        Assert.Empty(errors);
        Assert.Equal("stopped", service.State("a1"));
        Assert.Contains(Assert.Single(reports).Events, line => line.Contains("a1") && line.Contains("nothing on this machine"));
    }

    /// <summary>
    /// 🔴 REV3: a hand edit that left `driver.json` torn threw from the load, OUTSIDE the loop's catch —
    /// so the watch died on the first tick, and in the shell nothing said so. The loop says what is wrong
    /// with the file, keeps watching it, and ticks again the moment it reads.
    /// </summary>
    [Fact]
    public async Task A_torn_driver_json_is_said_and_watched_never_the_end_of_the_loop()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        var config = Path.Combine(_home, "driver.json");
        File.WriteAllText(config, """{ "drivable": [], "pollSeconds": 1, }""");

        using var client = new ServiceClient(service.Url, null);
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var reports = new List<TickReport>();
        var errors = new List<Exception>();
        var watch = new DriverWatch(
            client, config, _home, new SessionProcesses(Markers), sync: null,
            harnesses: new HarnessRoster(AdapterSet.Built(), Path.Combine(_home, "harnesses.json")));

        var running = watch.RunAsync(
            (report, _) => { reports.Add(report); stop.Cancel(); return Task.CompletedTask; },
            error =>
            {
                errors.Add(error);
                File.WriteAllText(config, """{ "drivable": [], "pollSeconds": 1 }""");
                watch.Nudge();
                return Task.CompletedTask;
            },
            stop.Token);
        await running;

        var said = Assert.Single(errors);
        Assert.IsType<DriverException>(said);
        Assert.Contains("driver.json", said.Message);
        Assert.Single(reports);
    }

    /// <summary>A conversation's stand-in: it answers the toolchain's two questions, then listens until its input closes.</summary>
    private string Conversing()
    {
        var script = Path.Combine(_home, "conversing.mjs");
        File.WriteAllText(script, """
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            process.stdin.resume();
            await new Promise((resolve) => setTimeout(resolve, 60000));
            """);
        return script;
    }

    private static Task Until(Func<bool> condition) => Poll.Until(condition, within: TimeSpan.FromSeconds(15));

    /// <summary>
    /// The service's session doors, on a loopback port: records seeded or opened as chats, listed as
    /// the ledger lists them (active only, unless asked for all), and moved.
    /// </summary>
    internal sealed class StandInService : IAsyncDisposable
    {
        private readonly HttpListener _listener;
        private readonly Task _serving;
        private readonly string _root;
        private readonly List<JsonObject> _sessions = [];

        public string Url { get; }

        /// <summary>How long a record's move takes to land — a real host is not instant, and a caller that did not wait for it must be seen not to.</summary>
        public TimeSpan WriteDelay { get; init; }

        private StandInService(HttpListener listener, string url, string root)
        {
            _listener = listener;
            Url = url.TrimEnd('/');
            _root = root;
            _serving = ServeAsync();
        }

        /// <summary>A state this ledger will not move a record to, as a terminal record refuses every move.</summary>
        public string? Refuses { get; init; }

        public static StandInService Start(string root, TimeSpan writeDelay = default, string? refuses = null)
        {
            var probe = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            probe.Start();
            var port = ((IPEndPoint)probe.LocalEndpoint).Port;
            probe.Stop();

            var url = $"http://127.0.0.1:{port}/";
            var listener = new HttpListener();
            listener.Prefixes.Add(url);
            listener.Start();
            return new StandInService(listener, url, root) { WriteDelay = writeDelay, Refuses = refuses };
        }

        public void Seed(string id, string state, string? tree = null, string? baseCommit = null)
        {
            lock (_sessions)
            {
                var session = new JsonObject { ["id"] = id, ["repository"] = "engine", ["state"] = state, ["kind"] = "chat" };
                if (tree is not null) session["tree"] = tree;
                if (baseCommit is not null) session["baseCommit"] = baseCommit;
                _sessions.Add(session);
            }
        }

        public string? State(string id) => Field(id, "state");

        public string? Note(string id) => Field(id, "note");

        private string? Field(string id, string name)
        {
            lock (_sessions) return _sessions.SingleOrDefault(s => s["id"]!.GetValue<string>() == id)?[name]?.GetValue<string>();
        }

        private async Task ServeAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try { context = await _listener.GetContextAsync(); }
                catch (Exception error) when (error is HttpListenerException or ObjectDisposedException or InvalidOperationException) { return; }

                if (context.Request.HttpMethod == "POST" && context.Request.Url!.AbsolutePath.EndsWith("/state", StringComparison.Ordinal))
                {
                    await Task.Delay(WriteDelay);
                }

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
                        return (200, "[]");

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

                    case ("POST", "/api/sessions/chat"):
                    {
                        var session = new JsonObject
                        {
                            ["id"] = $"c{_sessions.Count + 1}", ["repository"] = Body()["repository"]!.GetValue<string>(),
                            ["state"] = "starting", ["kind"] = "chat",
                        };
                        _sessions.Add(session);
                        return (200, new JsonObject { ["session"] = session.DeepClone(), ["message"] = "opened" }.ToJsonString());
                    }

                    case ("POST", _) when path.StartsWith("/api/sessions/", StringComparison.Ordinal) && path.EndsWith("/state", StringComparison.Ordinal):
                    {
                        var id = Uri.UnescapeDataString(path["/api/sessions/".Length..^"/state".Length]);
                        var body = Body();
                        if (body["state"]!.GetValue<string>() == Refuses)
                        {
                            return (409, $$"""{"error":"session {{id}} will not move to {{Refuses}}"}""");
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
