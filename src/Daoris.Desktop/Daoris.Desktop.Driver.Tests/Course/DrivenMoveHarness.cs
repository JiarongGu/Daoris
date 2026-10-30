using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The driven carry-on's move (FOUNDATION1f-iv part b, sitting 2; D212; the foundation design §13.5's downgrade table, §19.1, §19.3, §19.4)
/// across a killed loop, beside <see cref="LiveLimitHarness"/>, whose kill and release it mirrors: one home, one stand-in service
/// (<see cref="SessionMessagesTickTests.SaidStandIn"/>, which keeps a quest's records and says the keyed open), and two kinds of loop over them.
/// The <b>killed loop</b> is a real <see cref="Driver"/> look whose move holder is a run of its own (<see cref="Killed"/>), told each boundary of
/// <see cref="Cuts"/> and stopped at one by blocking there for good, as a killed process never leaves it; its run is then declared dead the way
/// the lease tells a dead process. The <b>coming-up loop</b> is the real <see cref="DriverWatch"/>, whose first look sweeps the dead loop's
/// records, takes its move up and plans the quest's starts, over the same protocol stub, which writes down each prompt it hears.
/// </summary>
/// <remarks>
/// <para>The quest <c>q1</c>'s first run failed on its turn and the person said <see cref="Word"/> to its record <c>s1</c> after (or said nothing),
/// so the carry-on that follows is a <c>retry</c>: the resume of <c>s1</c> is refused on the wire and the line goes on to a new part <c>s2</c>
/// through the keyed open, which moves the word. The killed loop's agent is a real process, and the coming-up loop's records its own.</para>
/// <para>An <b>older build's</b> come-up is the same watch while the killed run still reads as alive, so its take-up takes nothing up, as a build
/// before the store takes none; what it does is the sweep, the settlement and the look. Where an older build would have carried the line on
/// itself, with no move to hold it, the older build's statements are made at the service by hand (<c>DrivenMoveOlderBuildProcessTests</c>).</para>
/// <para>Every case has its own scratch home under <c>_fixtures/</c> and sets no variable of the process's environment.</para>
/// </remarks>
internal sealed class DrivenMoveHarness : IAsyncDisposable
{
    /// <summary>The person's word kept on the failed record, which the keyed open moves.</summary>
    public const string Word = "Also log the port.";

    /// <summary>The killed loop's run.</summary>
    public const string Killed = "run-killed";

    private readonly ManualResetEventSlim _gate = new(false);

    private readonly List<string> _passed = [];

    private Task? _looking;

    private TaskCompletionSource? _hit;

    private CancellationTokenSource? _closing;

    private Task? _watching;

    public DrivenMoveHarness(string name)
    {
        Home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", name, Guid.NewGuid().ToString("N")[..8]);
        Repository = Path.Combine(Home, "engine");
        Directory.CreateDirectory(Repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "driven@example.com");
        Git("config", "user.name", "FOUNDATION1f");
        File.WriteAllText(Path.Combine(Repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
        Service = SessionMessagesTickTests.SaidStandIn.Start(Repository);
        Service.Opens = 2;
        Service.Asks = true;
    }

    public string Home { get; }

    public string Repository { get; }

    public SessionMessagesTickTests.SaidStandIn Service { get; }

    /// <summary>The boundary the killed loop reached, or null while it runs on.</summary>
    public string? Reached { get; private set; }

    public string Log => Path.Combine(Home, "agent.log");

    private string ConfigPath => DaorisHome.Entry(Home, HomeEntry.DriverConfig);

    public SessionEvents Events => new(DaorisHome.Entry(Home, HomeEntry.Sessions));

    /// <summary>Every boundary the killed loop's move passed, in order.</summary>
    public IReadOnlyList<string> Passed
    {
        get
        {
            lock (_passed) return [.. _passed];
        }
    }

    private sealed class LoopKilled : Exception;

    private DriverConfig Config() =>
        DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "acp-stub",
            TestBound = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>>
            {
                ["acp-stub"] = ["node", Agent(), Log],
            },
        };

    private Daoris.Driver.Driver Driver(ConversationMoves? moves = null, DriverConfig? config = null)
    {
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(Service.Url, null), config ?? Config(), adapters, Home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(Home, HomeEntry.Harnesses)))
        {
            Moves = moves!,
        };
    }

    /// <summary>A move store of one run, alive by the lease's own test.</summary>
    public MoveStore Store(string run) => new(Home, () => StoreSettled.Yes, run: run, kind: StoreProcess.Host);

    /// <summary>The move of a record left, as a reader of the store sees it.</summary>
    public MoveRecord? MoveFrom(string left) => Store("run-probe").Of(left).LastOrDefault();

    /// <summary>The run declared dead as the lease declares one: alive under another start time.</summary>
    public void Die(string run)
    {
        using var store = DesktopStore.Open(DesktopStore.LocationIn(Home), new DesktopStoreOptions { Settled = () => StoreSettled.Yes });
        store.Write("probe", write => write.Execute("UPDATE store_runs SET started = 1 WHERE run = $run", ("$run", run)));
    }

    // ——— The failed first run, the killed loop and its kill.

    /// <summary>
    /// The quest's first run, which fails on its turn with its quest still taken, so its record <c>s1</c> ends <c>failed</c>; the person's word is
    /// then said to it, where the case keeps one.
    /// </summary>
    public async Task SeedAsync(bool word)
    {
        File.WriteAllText(Path.Combine(Home, "go.flag"), "");
        await Driver().RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.True(Service.State("s1") == "failed", Service.Record("s1"));
        if (word) Assert.Equal("w1", await Service.SayAsync("s1", Word));
    }

    /// <summary>
    /// The carry-on of <c>s1</c>, run by a loop of run <see cref="Killed"/> stopped for good at <paramref name="cut"/>; none runs the look to its
    /// end. Returns once the boundary is reached.
    /// </summary>
    /// <param name="cut">The boundary to stop at.</param>
    /// <param name="atTheCut">Told each boundary as it is passed, before the stop.</param>
    /// <param name="config">The loop's configuration, where a case runs a door of its own.</param>
    public async Task KillAtAsync(string? cut, Action<string>? atTheCut = null, DriverConfig? config = null)
    {
        var holder = new ConversationMoves(
            Home, new ServiceClient(Service.Url, null), Events, store: Store(Killed), scope: MoveTakeUpScope.LeaseHolder);
        _hit = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        holder.Cut = name =>
        {
            lock (_passed) _passed.Add(name);
            atTheCut?.Invoke(name);
            if (name != cut) return;
            Reached = name;
            _hit.TrySetResult();
            _gate.Wait();
            throw new LoopKilled();
        };
        var driver = Driver(holder, config);
        _looking = Task.Run(async () => { await driver.RunOnceAsync(); });
        if (cut is null)
        {
            await _looking.WaitAsync(Poll.Patience);
            return;
        }

        await Poll.Until(
            () => _hit.Task.IsCompleted || _looking.IsFaulted || _looking.IsCompleted,
            () => $"the loop reached {Reached ?? "nothing"}, its look {_looking.Status}, {Seen()}");
        if (!_hit.Task.IsCompleted) throw new InvalidOperationException($"the look ended before `{cut}`: {await Settled(_looking)}; {Seen()}");
    }

    private static async Task<string> Settled(Task task)
    {
        try
        {
            await task;
            return "ended";
        }
        catch (Exception error)
        {
            return error.GetType().Name + ": " + error.Message;
        }
    }

    /// <summary>
    /// The killed loop is dead by the lease's test: its run alive under another start time. The records it left live are what a restarted service
    /// leaves of a dead loop's, <c>stopped</c> and interrupted, as the orphan sweep ends them (the desktop hosts the service and the loop in one
    /// process, so one dies with the other; a <c>queued</c> record no opener marked, which the sweep leaves, is ended alike here). Nothing it
    /// blocked on is released.
    /// </summary>
    public async Task KillAsync()
    {
        Die(Killed);
        await EndLiveAsync();
    }

    /// <summary>
    /// The records the killed loop left live ended as <see cref="KillAsync"/> ends them, its run left reading as alive: the view an older build's
    /// come-up has of it, whose take-up takes no run's move up.
    /// </summary>
    public async Task EndLiveAsync()
    {
        using var http = new HttpClient();
        for (var at = 1; at <= Service.SessionCount; at++)
        {
            if (Service.State($"s{at}") is not ("queued" or "starting" or "working")) continue;
            using var response = await http.PostAsync(
                $"{Service.Url}/api/sessions/s{at}/state", new StringContent("""{"state":"stopped","interrupted":true}""", System.Text.Encoding.UTF8, "application/json"));
            Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        }
    }

    /// <summary>The killed loop is let go: whatever it blocked on throws, as a process that died never would; its look is awaited and its error said.</summary>
    public async Task<string> ReleaseAsync()
    {
        _gate.Set();
        if (_looking is null) return "no look";
        try
        {
            await _looking.WaitAsync(TimeSpan.FromSeconds(30));
            return "ended";
        }
        catch (Exception error)
        {
            return error.GetType().Name + ": " + error.Message;
        }
    }

    // ——— An older build's statements, made at the service as a build before the store made them.

    /// <summary>An older build's open of a successor for the quest, unkeyed and moving no word: the record's id.</summary>
    public async Task<string> OlderOpenAsync()
    {
        using var http = new HttpClient();
        var body = new JsonObject { ["quest"] = "q1", ["adapter"] = "acp-stub", ["tree"] = Repository };
        using var response = await http.PostAsync($"{Service.Url}/api/sessions", new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!["session"]!["id"]!.GetValue<string>();
    }

    /// <summary>A state move of a record as an older build asks it; the status the service answered.</summary>
    public async Task<int> AdvanceAsync(string session, string state, bool interrupted = false)
    {
        using var http = new HttpClient();
        var body = new JsonObject { ["state"] = state, ["interrupted"] = interrupted };
        using var response = await http.PostAsync(
            $"{Service.Url}/api/sessions/{session}/state", new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"));
        return (int)response.StatusCode;
    }

    /// <summary>The words a record holds taken off it, as an older build's go-on takes the words it carries.</summary>
    public async Task TakeAsync(string session, params string[] words)
    {
        using var http = new HttpClient();
        var body = new JsonObject { ["said"] = new JsonArray([.. words.Select(word => (JsonNode)JsonValue.Create(word)!)]) };
        using var response = await http.PostAsync(
            $"{Service.Url}/api/sessions/{session}/taken", new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"));
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    // ——— The coming-up loop.

    /// <summary>
    /// The real watch's first look and the looks after it, over the one home: the sweep of the dead loop's records, the take-up of its move, the
    /// look's starts. It runs until <see cref="StopAsync"/>.
    /// </summary>
    public void ComeUp()
    {
        Config().Save(ConfigPath);
        var adapters = AdapterSet.Built();
        _closing = new CancellationTokenSource();
        var watch = new DriverWatch(
            new ServiceClient(Service.Url, null), ConfigPath, Home, new SessionProcesses(), sync: null,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(Home, HomeEntry.Harnesses)));
        var closing = _closing.Token;
        _watching = Task.Run(async () =>
        {
            try
            {
                await watch.RunAsync((_, _) => Task.CompletedTask, onError: null, closing);
            }
            catch (OperationCanceledException)
            {
            }
        });
    }

    /// <summary>
    /// Waits for the coming-up loop to have nothing more to do: no record of the quest live, no move standing, and nothing seen to change for a
    /// while (a second delivery would show in that while), bounded by <see cref="Poll.Patience"/>.
    /// </summary>
    /// <param name="movesMayStand">The loop leaves a move standing by design (an older build's come-up takes none up), so one standing is no work left.</param>
    /// <param name="quiet">How long nothing may change.</param>
    public async Task SettleAsync(bool movesMayStand = false, TimeSpan? quiet = null)
    {
        var last = "";
        var since = Stopwatch.GetTimestamp();
        var still = quiet ?? TimeSpan.FromSeconds(8);
        await Poll.Until(
            () =>
            {
                var now = Seen();
                if (now != last)
                {
                    last = now;
                    since = Stopwatch.GetTimestamp();
                    return false;
                }

                return Stopwatch.GetElapsedTime(since) > still && !Live(movesMayStand);
            },
            Seen);
    }

    private bool Live(bool movesMayStand)
    {
        var store = Store("run-probe");
        for (var at = 1; at <= Service.SessionCount; at++)
        {
            if (Service.State($"s{at}") is "queued" or "starting" or "working") return true;
            if (!movesMayStand && store.Of($"s{at}").Any(move => move.Stands)) return true;
        }

        return false;
    }

    /// <summary>The coming-up loop is stopped: the watch's token cancelled and its sessions with it.</summary>
    public async Task StopAsync()
    {
        if (_closing is null) return;
        await _closing.CancelAsync();
        try
        {
            if (_watching is not null) await _watching.WaitAsync(TimeSpan.FromSeconds(30));
        }
        catch (Exception error) when (error is OperationCanceledException or TimeoutException)
        {
        }

        _closing.Dispose();
        _closing = null;
        _watching = null;
    }

    // ——— What the stub heard.

    /// <summary>Every line the stub wrote down: the method, its session, its blocks.</summary>
    public IReadOnlyList<JsonObject> Heard() => [.. StubFile.Lines(Log).Select(line => JsonNode.Parse(line)!.AsObject())];

    /// <summary>The prompts the stub was sent, each with the session whose run it was.</summary>
    public IReadOnlyList<(string Session, string Text)> Prompts() =>
        [.. Heard().Where(line => line["method"]!.GetValue<string>() == "session/prompt")
            .Select(line => (line["session"]!.GetValue<string>(), string.Join("\n", line["blocks"]!.AsArray().Select(block => block!.GetValue<string>()))))];

    /// <summary>How many prompts of <paramref name="session"/> carried <paramref name="text"/>, each counted once however often it names it.</summary>
    public int Carried(string session, string text) => Prompts().Count(prompt => prompt.Session == session && prompt.Text.Contains(text, StringComparison.Ordinal));

    /// <summary>How many times <paramref name="session"/>'s prompts name <paramref name="text"/> in all.</summary>
    public int Named(string session, string text) =>
        Prompts().Where(prompt => prompt.Session == session).Sum(prompt => Count(prompt.Text, text));

    public static int Count(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    /// <summary>The marks the stand-in's records carry now, for a failing case to say.</summary>
    public string Seen() =>
        $"heard [{string.Join(" | ", Heard().Select(line => $"{line["session"]}:{line["method"]}:{line["blocks"]?.ToJsonString().Length}"))}], "
        + $"sessions {Service.SessionCount} [{string.Join(", ", Enumerable.Range(1, Service.SessionCount).Select(at => $"s{at}={Service.State($"s{at}")} said {string.Join("+", Service.Said($"s{at}"))}"))}], "
        + $"taken [{string.Join(" | ", Service.Taken)}], ask [{string.Join(" | ", Service.AskWords)}], "
        + $"moves [{string.Join(" ; ", Enumerable.Range(1, Service.SessionCount).Select(at => $"s{at}: " + string.Join(" | ", Store("run-probe").Of($"s{at}").Select(move => $"{move.PhaseWord}/{move.HeldWhy}/{move.ClosedWhy}/{move.FellBackCode}"))))}], "
        + $"faults [{string.Join(" | ", Service.Faults)}]";

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        if (_looking is not null) await ReleaseAsync();
        await Service.DisposeAsync();
        _gate.Dispose();
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(Home);
    }

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = Repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }

    // ——— The stub agents.

    /// <summary>
    /// The protocol door's agent: its first run (the record <c>s1</c>) takes the quest and fails its turn once the test lets it; every other run
    /// refuses a resume of the first run's conversation on the wire, as a door whose conversation cannot be loaded does (so the line goes on to
    /// a new part), honours any other, takes nothing and ends its turn. Each prompt is written
    /// down with the session it ran for and its blocks.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(Home, "driven-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { appendFileSync, existsSync } from 'node:fs';
            const [log] = process.argv.slice(2);
            const url = process.env.DAORIS_SERVICE_URL;
            const session = process.env.DAORIS_SESSION_ID;
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const heard = (what) => appendFileSync(log, JSON.stringify({ ...what, session }) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            let conversation = null;
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
                if (frame.params?.sessionId === 'conv-s1') {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: 'Internal error: the thread could not be loaded' } });
                } else {
                  conversation = frame.params.sessionId;
                  send({ jsonrpc: '2.0', id: frame.id, result: {} });
                }
              } else if (frame.method === 'session/prompt') {
                const blocks = (frame.params?.prompt ?? []).filter((block) => block.type === 'text').map((block) => block.text);
                heard({ method: 'session/prompt', conversation: frame.params?.sessionId, blocks });
                if (session === 's1') {
                  await respond({ action: 'take' });
                  const go = log.replace(/agent\.log$/, 'go.flag');
                  for (let waited = 0; !existsSync(go) && waited < 30000; waited += 50) await new Promise((r) => setTimeout(r, 50));
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: 'API Error: Connection dropped (ECONNRESET)' } });
                  return;
                }
                await respond({ action: 'take' });
                await respond({ action: 'done', reason: 'served the report' });
                say('Served.');
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }

}
