using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT1a-ii (D163 point 1; the mixed-agents design §2) through REAL ticks: a quest's session working on the machine's agent
/// (the protocol stub, which took its quest and holds its turn) is handed to <c>stub</c>. The look takes the box, stops the session
/// as the person's, and the next look carries the quest on in the same tree on the named agent, told it was handed over, with the
/// note that says so; the quest closes done. A real process (node) and the stand-in service <see cref="AskAndWaitTickTests.StandIn"/>,
/// as <see cref="StopHoldTickTests"/> uses them; no model and no account.
/// </summary>
/// <remarks>
/// Written by MIXAGENT1a-ii and run by the parent at the merge (MOD8): a Process case. The stand-in service keeps each record's
/// adapter and answers a claim as this machine's, which the hand-over's judge reads.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class HandOverQuestTickTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-hand-quest-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    private readonly SessionProcesses _processes = new();

    public HandOverQuestTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "hand@example.com");
        Git("config", "user.name", "MIXAGENT1a-ii");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_running_quest_session_handed_to_stub_is_stopped_and_the_quest_goes_on_in_the_same_tree_on_stub()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.KeepsAdapter = true;
        service.Claim = "held";
        var log = Path.Combine(_home, "agent.log");
        var driver = Driver(service, Config(log));

        // 1. The machine's agent, the protocol stub, takes the quest and works on it.
        await driver.TickAsync().WaitAsync(TimeSpan.FromSeconds(60));
        await UntilAsync(() => service.SessionCount == 1 && service.Status("q1") == "Taken");
        var first = service.Session("s1");
        Assert.Equal("acp-stub", first["adapter"]!.GetValue<string>());
        var tree = first["tree"]!.GetValue<string>();

        // 2. The person hands it to `stub`: the look takes the box and stops the session as theirs, with the hand-over's own line.
        new HandOverBox(_home).Write("s1", new HandOverRequest("stub", null, HandOverWhy.Press, HandOverBy.Person, DateTimeOffset.UtcNow));
        var taking = await driver.TickAsync().WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Contains(taking.Events, line => line == "hand-over  session `s1` (#q1): stopped, and `#q1` goes on with stub.");
        await UntilAsync(() => service.Session("s1")["state"]!.GetValue<string>() == "stopped");
        await driver.Running.SettledAsync().WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Contains("Handed over to stub", service.Session("s1")["note"]!.GetValue<string>());
        Assert.Equal("Taken", service.Status("q1"));
        Assert.Equal("stub", new HandedQuests(_home).Read("q1")!.Agent);
        Assert.Null(new HandOverBox(_home).Read("s1"));

        // 3. The next look reads that stop as released: the quest is carried on in the same tree, on `stub`, and closes.
        await driver.TickAsync().WaitAsync(TimeSpan.FromSeconds(60));
        await driver.Running.SettledAsync().WaitAsync(TimeSpan.FromSeconds(60));

        var carried = service.Session("s2");
        Assert.Equal("stub", carried["adapter"]!.GetValue<string>());
        Assert.Equal("completed", carried["state"]!.GetValue<string>());
        // Its record names the session it was handed over from, with the hand-over's why and asker (MIXAGENT1a-iii).
        Assert.Equal(
            ("s1", "press", "person"),
            (carried["from"]?.GetValue<string>(), carried["fromWhy"]?.GetValue<string>(), carried["fromBy"]?.GetValue<string>()));
        Assert.Equal(Path.GetFullPath(tree), Path.GetFullPath(carried["tree"]!.GetValue<string>()), ignoreCase: true);
        Assert.Equal("Done", service.Status("q1"));
        var run = Runs(log).Single();
        Assert.Equal(Path.GetFullPath(tree), Path.GetFullPath(run["cwd"]!.GetValue<string>()), ignoreCase: true);
        Assert.Contains("run by acp-stub, was stopped so that the quest could go on with you", run["target"]!.GetValue<string>());
        Assert.Contains("started.handed-over", run["note"]!.GetValue<string>());
        Assert.DoesNotContain("released", run["target"]!.GetValue<string>());

        // Held until the quest leaves this machine: it closed, so the next look sends it back to the machine's agent.
        await driver.TickAsync().WaitAsync(TimeSpan.FromSeconds(60));
        Assert.Empty(new HandedQuests(_home).All());
    }

    private DriverConfig Config(string log) => DriverConfig.Empty with
    {
        Drivable = ["engine"],
        Trees = ["engine"],
        Adapter = "acp-stub",
        TimeoutMinutes = 1,
        PollSeconds = 1,
        Commands = new Dictionary<string, IReadOnlyList<string>>
        {
            ["acp-stub"] = ["node", AcpAgent()],
            ["stub"] = ["node", Agent(), log],
        },
    };

    // One driver for every look, as the goal's tick test builds one, sharing the processes the stop reaches.
    private Daoris.Driver.Driver Driver(AskAndWaitTickTests.StandIn service, DriverConfig config)
    {
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: _processes,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>
    /// The machine's agent, on the protocol door: it speaks the wire, takes its quest through the service's own door when its turn
    /// comes, and then holds the turn until it is stopped.
    /// </summary>
    private string AcpAgent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            for await (const line of createInterface({ input: process.stdin })) {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-1' } });
              } else if (frame.method === 'session/prompt') {
                await fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
                  method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ action: 'take' }) });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
            process.exit(0);
            """);
        return script;
    }

    /// <summary>
    /// The agent the quest is handed to: it writes down where it runs, what it was handed, and the starting note its own record
    /// carries (the stand-in keeps the note's parts until the conclusion replaces them), then closes the quest done.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const [log] = process.argv.slice(2);
            const url = process.env.DAORIS_SERVICE_URL;
            const records = await (await fetch(`${url}/api/sessions?includeClosed=true`)).json();
            const mine = records.filter((record) => record.adapter === 'stub').at(-1);
            appendFileSync(log, JSON.stringify({
              cwd: process.cwd(), target: process.env.DAORIS_TARGET ?? '', note: JSON.stringify(mine?.noteParts ?? []) }) + '\n');
            await fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ action: 'done', reason: 'landed' }) });
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
