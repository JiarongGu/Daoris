using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PAUSE1b (D132 points 2–4, design §2) through REAL ticks: the person pauses an ask while its session works, the session is
/// stopped as their stop with the pause's words on its record, the looks that follow hold its quest with nothing spawned, and
/// *Resume* carries it on in its tree. A real process (node) and the stand-in service <see cref="AskAndWaitTickTests.StandIn"/>,
/// as the stop's ticks use them; no model and no account.
/// </summary>
/// <remarks>
/// <para>The pause and the resume are <see cref="WorkPausing"/>'s, with the loop's own registry, as <c>WORK_PAUSE</c> and
/// <c>WORK_RESUME</c> call them; each look reads <c>driver.json</c> again, as the watch builds a driver per look.</para>
///
/// <para>🔴 The stand-in opens a session on any quest it is asked about; the service's ledger carries on a released stop of a
/// take this machine made (SESSUX1b2), which is the case here.</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class PauseTickTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-pause-tick-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    private readonly SessionProcesses _processes = new();

    public PauseTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "pause@example.com");
        Git("config", "user.name", "PAUSE1b");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string ConfigPath => Path.Combine(_home, "driver.json");

    [Fact]
    public async Task An_ask_paused_while_its_session_works_holds_its_quest_with_nothing_spawned_and_Resume_carries_it_on_in_its_tree()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        // The ask's own route answers once its words are set: the pause reads the ask it names (WORK_UNKNOWN otherwise).
        service.Words = [];
        var log = Path.Combine(_home, "agent.log");
        Config(log, "take").Save(ConfigPath);
        var world = World(service);

        // 1. The session takes its quest and works; the person pauses its ask. The session is stopped as their stop, with the
        // pause's words on its record, the quest still taken here, and the pause keeps the stop it made.
        var first = Driver(service).RunOnceAsync();
        await UntilAsync(() => service.SessionCount == 1 && service.Status("q1") == "Taken");
        var paused = await WorkPausing.PauseAsync(world, WorkScope.Ask, "a1", PluginEvents.Screen).WaitAsync(TimeSpan.FromSeconds(30));
        await first.WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(new WorkStop("s1", "q1"), Assert.Single(paused.Stopped));
        var stopped = service.Session("s1");
        Assert.Equal("stopped", stopped["state"]!.GetValue<string>());
        Assert.Equal("paused with ask `#a1`.", stopped["note"]!.GetValue<string>());
        Assert.Equal("Taken", service.Status("q1"));
        Assert.Equal("s1", DriverConfig.Load(ConfigPath).PausedAsk("a1")!.Stopped["q1"]);
        var tree = stopped["tree"]!.GetValue<string>();

        // 2. The looks that follow hold the quest, naming the pause and its door, and start nothing.
        for (var look = 0; look < 2; look++)
        {
            var held = await Driver(service).TickAsync().WaitAsync(TimeSpan.FromSeconds(30));

            var sitting = Assert.Single(held.Considerations, c => c.Quest.Id == "q1");
            Assert.Equal(StartVerdict.Paused, sitting.Verdict);
            Assert.Equal("paused with ask `#a1`; Resume carries it on — `daoris-driver ask --resume a1`.", sitting.Reason);
            Assert.Equal(1, service.SessionCount);
        }

        // 3. Resume: the pause's stop released, nothing still holds the quest, and the next look carries it on in the same tree.
        var resumed = await WorkPausing.ResumeAsync(world, WorkScope.Ask, "a1", PluginEvents.Screen).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(new WorkStop("s1", "q1"), Assert.Single(resumed.Released));
        Assert.Empty(resumed.Holds!);

        await Driver(service).RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        var carried = service.Session("s2");
        Assert.Equal("completed", carried["state"]!.GetValue<string>());
        Assert.Equal(Path.GetFullPath(tree), Path.GetFullPath(carried["tree"]!.GetValue<string>()), ignoreCase: true);
        Assert.Equal("Done", service.Status("q1"));
        Assert.Contains("carrying on quest `#q1`", Runs(log)[1]["target"]!.GetValue<string>());
    }

    [Fact]
    public async Task An_open_quest_paused_before_any_session_starts_nothing_until_Resume_starts_it()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.Words = [];
        var log = Path.Combine(_home, "agent.log");
        Config(log, "close").Save(ConfigPath);
        var world = World(service);

        // 1. Paused before the driver ever looked: nothing runs, so the pause stops nothing and is written.
        var paused = await WorkPausing.PauseAsync(world, WorkScope.Quest, "q1", PluginEvents.Terminal).WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal((PauseVerdict.Paused, 0), (paused.Verdict, paused.Stopped.Count));

        // 2. Each look holds it, and nothing is spawned.
        for (var look = 0; look < 2; look++)
        {
            var held = await Driver(service).TickAsync().WaitAsync(TimeSpan.FromSeconds(30));

            var sitting = Assert.Single(held.Considerations, c => c.Quest.Id == "q1");
            Assert.Equal(StartVerdict.Paused, sitting.Verdict);
            Assert.Equal("you paused `#q1`; Resume starts it — `daoris-driver quest resume q1`.", sitting.Reason);
            Assert.Equal(0, service.SessionCount);
            Assert.False(File.Exists(log));
        }

        // 3. Resume: the next look starts it where it stood, oldest first, and it closes.
        await WorkPausing.ResumeAsync(world, WorkScope.Quest, "q1", PluginEvents.Terminal).WaitAsync(TimeSpan.FromSeconds(30));
        await Driver(service).RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal("completed", service.Session("s1")["state"]!.GetValue<string>());
        Assert.Equal("Done", service.Status("q1"));
    }

    private DriverConfig Config(string log, string first) => DriverConfig.Empty with
    {
        Drivable = ["engine"],
        Trees = ["engine"],
        Adapter = "stub",
        TimeoutMinutes = 1,
        PollSeconds = 1,
        Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Agent(), log, first] },
    };

    /// <summary>The loop's world: its service, the home, the file each look reads, and the loop's own registry, which stops what it runs.</summary>
    private WorkWorld World(AskAndWaitTickTests.StandIn service) =>
        new(new ServiceClient(service.Url, null), _home, ConfigPath, _processes) { Poll = TimeSpan.FromMilliseconds(50) };

    // A driver per look over the file as it stands, sharing the processes, as the watch builds one per look (DEV3).
    private Daoris.Driver.Driver Driver(AskAndWaitTickTests.StandIn service)
    {
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), DriverConfig.Load(ConfigPath), adapters, _home, processes: _processes,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
    }

    /// <summary>
    /// The stand-in harness: it writes down what it was handed; on its first run it takes the quest and works until it is
    /// stopped (<c>take</c>), or takes it and closes it (<c>close</c>); on any later run it takes the quest unless it is
    /// carrying it on, and closes it done.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync, existsSync, readFileSync } from 'node:fs';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const [log, first] = process.argv.slice(2);
            const runs = existsSync(log) ? readFileSync(log, 'utf8').split('\n').filter(Boolean).length : 0;
            const target = process.env.DAORIS_TARGET ?? '';
            appendFileSync(log, JSON.stringify({ cwd: process.cwd(), target }) + '\n');
            const respond = (body) => fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            if (runs === 0 && first === 'take') {
              await respond({ action: 'take' });
              setInterval(() => {}, 1 << 30);
            } else {
              if (!target.includes('carrying on quest')) await respond({ action: 'take' });
              await respond({ action: 'done', reason: 'landed' });
            }
            """);
        return script;
    }

    private static List<JsonNode> Runs(string log) => [.. File.ReadAllLines(log).Select(line => JsonNode.Parse(line)!)];

    private static async Task UntilAsync(Func<bool> condition)
    {
        var patience = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < patience, "the condition never held");
            await Task.Delay(50);
        }
    }

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
}
