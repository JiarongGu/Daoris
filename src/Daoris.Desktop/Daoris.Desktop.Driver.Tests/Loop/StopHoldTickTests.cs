using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSUX1b (D126 §3.3, §3.4) through REAL ticks: the person stops a driven session, its quest is held across the looks
/// that follow with nothing spawned, and *Try again* releases it, after which it runs. A real process (node) and the
/// stand-in service <see cref="AskAndWaitTickTests.StandIn"/>, as the ask-and-wait ticks use them; no model and no account.
/// </summary>
/// <remarks>
/// <para>The stop is <see cref="SessionProcesses.Stop"/>, the call <c>STOP_SESSION</c> makes, so the record ends as the
/// person's stop does. *Try again* is the release <c>RETRY_QUEST</c> and <c>daoris driver retry --session</c> write, read
/// by a driver built for the next look, as the watch builds one per look with the same processes.</para>
///
/// <para>🔴 The stand-in opens a session on any quest it is asked about. The service's ledger does not: it opens one on a
/// taken quest only to resume or carry it on, and its carry-on does not yet include a person's stop
/// (<c>SessionLedger.OpenAsync</c>'s <c>carriesOn</c>). So the taken case here proves the driver's half; against a real
/// host the carry-on is refused until the service's half lands (D126's SESSUX1b note).</para>
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class StopHoldTickTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-stop-hold-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    private readonly SessionProcesses _processes = new();

    public StopHoldTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "stop@example.com");
        Git("config", "user.name", "SESSUX1b");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_take_the_person_stopped_is_held_across_looks_and_Try_again_carries_it_on_in_its_tree()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var log = Path.Combine(_home, "agent.log");
        var config = Config(log, "take");

        // 1. The session takes its quest and works; the person stops it. The record is their stop, the quest still taken.
        var first = Driver(service, config).RunOnceAsync();
        await UntilAsync(() => service.SessionCount == 1 && service.Status("q1") == "Taken");
        Assert.True(_processes.Stop("s1"));
        await first.WaitAsync(TimeSpan.FromSeconds(60));
        var stopped = service.Session("s1");
        Assert.Equal("stopped", stopped["state"]!.GetValue<string>());
        Assert.Equal("the person stopped it.", stopped["note"]!.GetValue<string>());
        Assert.Equal("Taken", service.Status("q1"));
        var tree = stopped["tree"]!.GetValue<string>();

        // 2. The looks that follow hold the quest, saying why and how to release it, and start nothing.
        for (var look = 0; look < 2; look++)
        {
            var held = await Driver(service, config).TickAsync().WaitAsync(TimeSpan.FromSeconds(30));

            var sitting = Assert.Single(held.Considerations, c => c.Quest.Id == "q1");
            Assert.Equal(StartVerdict.Stopped, sitting.Verdict);
            Assert.Equal("you stopped session `s1`; Try again carries it on — `daoris driver retry q1 --session s1`.", sitting.Reason);
            Assert.Equal(1, service.SessionCount);
        }

        // 3. Try again: released from that stop, the next look carries the quest on in the same tree, and it closes.
        await Driver(service, config.WithReleased("q1", "s1")).RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        var carried = service.Session("s2");
        Assert.Equal("completed", carried["state"]!.GetValue<string>());
        Assert.Equal(Path.GetFullPath(tree), Path.GetFullPath(carried["tree"]!.GetValue<string>()), ignoreCase: true);
        Assert.Equal("Done", service.Status("q1"));
        var handed = Runs(log)[1]["target"]!.GetValue<string>();
        Assert.Contains("carrying on quest `#q1`", handed);
        Assert.Contains("stopped by the person before it closed it, and they have since released the quest", handed);
    }

    [Fact]
    public async Task An_open_quest_whose_session_the_person_stopped_is_held_rather_than_started_again_until_Try_again()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var log = Path.Combine(_home, "agent.log");
        var config = Config(log, "wait");

        // 1. The session starts and has not taken its quest when the person stops it: the quest is still open.
        var first = Driver(service, config).RunOnceAsync();
        await UntilAsync(() => File.Exists(log));
        await UntilAsync(() => _processes.Stop("s1"));
        await first.WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Equal("stopped", service.Session("s1")["state"]!.GetValue<string>());
        Assert.Equal("Open", service.Status("q1"));

        // 2. It was planned again at the next look before SESSUX1b; now each look holds it, and nothing starts.
        for (var look = 0; look < 2; look++)
        {
            var held = await Driver(service, config).TickAsync().WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Equal(StartVerdict.Stopped, Assert.Single(held.Considerations, c => c.Quest.Id == "q1").Verdict);
            Assert.Equal(1, service.SessionCount);
        }

        // 3. Try again: the next look starts it as a first start, which takes it and closes it.
        await Driver(service, config.WithReleased("q1", "s1")).RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal("completed", service.Session("s2")["state"]!.GetValue<string>());
        Assert.Equal("Done", service.Status("q1"));
        Assert.Contains("First take the quest", Runs(log)[1]["target"]!.GetValue<string>());
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

    // A driver per look, sharing the processes, as the watch builds one per look (DEV3).
    private Daoris.Driver.Driver Driver(AskAndWaitTickTests.StandIn service, DriverConfig config)
    {
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: _processes,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
    }

    /// <summary>
    /// The stand-in harness: it writes down what it was handed, then on its first run takes the quest (<c>take</c>) or not
    /// (<c>wait</c>) and works until it is stopped; on any later run it takes the quest unless it is carrying it on, and
    /// closes it done.
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
            if (runs === 0) {
              if (first === 'take') await respond({ action: 'take' });
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
