using System.Diagnostics;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A quest's hand-over across a killed driver (FOUNDATION1f-v part b, D212; the foundation design §3.3, §13.4, §13.5, §19.4): one scratch home, the
/// stand-in service <see cref="AskAndWaitTickTests.StandIn"/> of <c>HandOverQuestTickTests</c>, and the real driver's ticks over real processes. The
/// protocol stub is both doors: its first run takes the quest and holds its turn; every run after it, the carry-on's, hears its first prompt, closes
/// the quest done and ends, so the door tells that it wrote the prompt and the move closes <c>delivered</c>. A <b>killed driver</b> is one whose
/// move holder is a run of its own (the store's, declared dead the way the lease tells a dead process), stopped at a boundary by blocking there for
/// good; the driver that comes after it shares the sessions' processes, as the one process does.
/// </summary>
/// <remarks>Every case has its own scratch home under <c>_fixtures/</c> and sets no variable of the process's environment.</remarks>
internal sealed class HandOverQuestHarness : IAsyncDisposable
{
    /// <summary>The killed driver's, or verb's, run.</summary>
    public const string Killed = "run-killed";

    private readonly ManualResetEventSlim _gate = new(false);

    private readonly List<Task> _held = [];

    private readonly RunningSessions _running = new();

    public HandOverQuestHarness(string name)
    {
        Home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", name, Guid.NewGuid().ToString("N")[..8]);
        Repository = Path.Combine(Home, "engine");
        Directory.CreateDirectory(Repository);
        foreach (var account in new[] { "account-1", "account-2" }) Directory.CreateDirectory(HarnessSettings.ProfileHome(Home, "stub", account));
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "hand@example.com");
        Git("config", "user.name", "FOUNDATION1f-v");
        File.WriteAllText(Path.Combine(Repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
        Service = AskAndWaitTickTests.StandIn.Start(Repository);
        Service.KeepsAdapter = true;
        Service.Claim = "held";
        Client = new ServiceClient(Service.Url, null);
    }

    public string Home { get; }

    public string Repository { get; }

    public AskAndWaitTickTests.StandIn Service { get; }

    public ServiceClient Client { get; }

    public SessionProcesses Processes { get; } = new();

    public string Log => Path.Combine(Home, "agent.log");

    /// <summary>The prompts the carry-on's agent heard, each once it is the target's, not the quest's first run.</summary>
    public int Carried => StubFile.Lines(Log).Count(line => line.Contains("\"first\":false", StringComparison.Ordinal));

    /// <summary>The move record of one run of this home: alive by the lease's own test unless <see cref="Die"/> says otherwise.</summary>
    public MoveStore Store(string run, string kind = StoreProcess.Host) => new(Home, () => StoreSettled.Yes, run: run, kind: kind);

    /// <summary>The driven holder of a run: the lease holder's scope over the home's one store, as <see cref="Driver.Moves"/> builds it.</summary>
    public ConversationMoves Moves(string run, string kind = StoreProcess.Host) =>
        new(Home, Client, new SessionEvents(DaorisHome.Entry(Home, HomeEntry.Sessions)), store: Store(run, kind), scope: MoveTakeUpScope.LeaseHolder);

    /// <summary>The moves of a session left, as a reader of the store sees them.</summary>
    public IReadOnlyList<MoveRecord> MovesOf(string session) => Store("run-probe").Of(session);

    /// <summary>The run declared dead as the lease declares one: alive under another start time.</summary>
    public void Die(string run)
    {
        using var store = DesktopStore.Open(DesktopStore.LocationIn(Home), new DesktopStoreOptions { Settled = () => StoreSettled.Yes });
        store.Write("probe", write => write.Execute("UPDATE store_runs SET started = 1 WHERE run = $run", ("$run", run)));
    }

    /// <summary>A driver for a look: the home's one running-sessions record and processes, its move holder the run's own where it is handed one.</summary>
    public Daoris.Driver.Driver Driver(ConversationMoves? moves = null, Func<string, Noted, bool>? stops = null)
    {
        var adapters = AdapterSet.Built();
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "acp-stub",
            TestBound = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Log, Path.Combine(Home, "first.flag")] },
        };
        return new Daoris.Driver.Driver(
            Client, config, adapters, Home, processes: Processes, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(Home, HomeEntry.Harnesses)), running: _running)
        {
            Stops = stops, Moves = moves!,
        };
    }

    /// <summary>The quest's first run: the first look starts <c>s1</c> on the stub, which takes <c>q1</c> and holds its turn.</summary>
    public async Task<string> StartQuestAsync()
    {
        var first = Driver();
        await first.TickAsync().WaitAsync(Poll.Patience);
        await Poll.Until(() => Service.SessionCount == 1 && Service.Status("q1") == "Taken", () => $"sessions {Service.SessionCount}, q1 {Service.Status("q1")}");
        return Service.Session("s1")["profile"]?.GetValue<string>() is "account-2" ? "account-1" : "account-2";
    }

    /// <summary>Ask the quest's hand-over of <c>s1</c> to the stub, on an account the first run is not on, decided in <paramref name="moves"/>'s store.</summary>
    public Task<HandOverAnswer> AskAsync(ConversationMoves moves, string account) =>
        QuestHandOver.AskAsync(Client, moves, QuestHandOver.Roster(AdapterSet.Built(), Home), "s1", "acp-stub", account, HandOverWhy.Press, HandOverBy.Person);

    /// <summary>A killed driver's look, run on a task that blocks at <paramref name="cut"/> or in the stop and is never let go until the case is over.</summary>
    public async Task KilledLookAsync(Daoris.Driver.Driver driver, Func<bool> reached)
    {
        _held.Add(Task.Run(async () =>
        {
            try { await driver.TickAsync(); }
            catch (Exception) { /* the killed look is let go at the end, and its writes then fail */ }
        }));
        await Poll.Until(reached, () => $"the killed look reached nothing, moves [{string.Join(" | ", MovesOf("s1").Select(move => move.PhaseWord))}]");
    }

    /// <summary>Block for good, as a killed process never returns; the case's end lets it go.</summary>
    public void Block() => _gate.Wait();

    public string Seen() =>
        $"quest {Service.Status("q1")}, sessions {Service.SessionCount}, carried {Carried}, moves [{string.Join(" | ", MovesOf("s1").Select(move => $"{move.PhaseWord}/{move.HeldWhy}/{move.ClosedWhy}/{move.FellBackCode}/{move.Destination}"))}], handed [{string.Join(",", new HandedQuests(Home).All().Select(each => each.Key))}]";

    /// <summary>The next looks of a driver until the carry-on has closed the move or <see cref="Poll.Patience"/> passed.</summary>
    public async Task CarryOnAsync(Daoris.Driver.Driver driver)
    {
        var patience = DateTime.UtcNow + Poll.Patience;
        while (!(MovesOf("s1").Count > 0 && !MovesOf("s1")[^1].Stands && Service.Status("q1") == "Done"))
        {
            if (DateTime.UtcNow > patience) throw new TimeoutException($"the carry-on never closed the move: {Seen()}");
            await driver.TickAsync().WaitAsync(Poll.Patience);
            await driver.Running.SettledAsync().WaitAsync(Poll.Patience);
            await Task.Delay(100);
        }
    }

    /// <summary>The killed driver is let go: whatever it blocked on returns, as a process that died never would, and its writes under its old claim fail.</summary>
    public async Task ReleaseAsync()
    {
        _gate.Set();
        try { await Task.WhenAll(_held).WaitAsync(Poll.Patience); } catch (Exception) { /* a killed look's writes fail once it is let go */ }
    }

    public async ValueTask DisposeAsync()
    {
        await ReleaseAsync();
        await Service.DisposeAsync();
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(Home);
        _gate.Dispose();
    }

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = Repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }

    /// <summary>The stub on the protocol door: the first run takes the quest and holds its turn; each run after hears its prompt, closes the quest and ends its turn.</summary>
    private string Agent()
    {
        var script = Path.Combine(Home, "acp-quest-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { appendFileSync, existsSync, writeFileSync } from 'node:fs';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [log, flag] = process.argv.slice(2);
            const first = !existsSync(flag);
            if (first) writeFileSync(flag, '1');
            const url = process.env.DAORIS_SERVICE_URL;
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            let conversation = null;
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                conversation = 'acp-' + (first ? '1' : '2');
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/prompt') {
                appendFileSync(log, JSON.stringify({ first }) + '\n');
                if (first) { await respond({ action: 'take' }); return; }
                await respond({ action: 'done', reason: 'landed' });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            lines.on('close', () => process.exit(0));
            """);
        return script;
    }
}
