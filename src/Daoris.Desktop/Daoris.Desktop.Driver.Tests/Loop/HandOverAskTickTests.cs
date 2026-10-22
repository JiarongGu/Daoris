using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT1c (D163 point 3; D191: "a quest session's ask under `on` is taken at its turn's end, never mid-turn, its note written
/// before the session stops") through REAL ticks: the machine's agent (the protocol stub) takes its quest, asks to hand its session
/// over with a note, as `handover_propose` writes it, and ends its turn holding the quest. Under the repository's `on` rule its run
/// takes the ask at that turn's end: the record stops as handed over, the quest is held on the next agent in the person's order with
/// the note, and the next look carries it on in the same tree on that agent, told the note as the outgoing agent's claim.
/// </summary>
/// <remarks>Written by MIXAGENT1c: a Process case (node and git). The roster's doors are the test's, so no binary is asked its version.</remarks>
[Trait(Category.Name, Category.Process)]
public sealed class HandOverAskTickTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-handask-tick-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    private readonly SessionProcesses _processes = new();

    public HandOverAskTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "hand@example.com");
        Git("config", "user.name", "MIXAGENT1c");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task An_agents_ask_under_on_is_taken_at_its_turns_end_and_the_next_agent_is_told_its_note()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.KeepsAdapter = true;
        service.Claim = "held";
        var log = Path.Combine(_home, "agent.log");
        var driver = Driver(service, Config(log));

        // 1. The machine's agent takes the quest, asks to hand over with its note, and ends its turn holding the quest.
        await driver.TickAsync().WaitAsync(Poll.Patience);
        await UntilAsync(() => service.SessionCount == 1 && service.Session("s1")["state"]!.GetValue<string>() == "stopped");
        await driver.Running.SettledAsync().WaitAsync(Poll.Patience);

        // 2. Taken at its turn's end, never mid-turn: the record says it was handed over, and the quest is held on the next agent with the note.
        Assert.Contains("Handed over to stub", service.Session("s1")["note"]!.GetValue<string>());
        Assert.Equal("Taken", service.Status("q1"));
        var handed = new HandedQuests(_home).Read("q1")!;
        Assert.Equal(("stub", "s1", HandOverWhy.Agent, HandOverBy.Agent), (handed.Agent, handed.From, handed.Why, handed.AskedBy));
        Assert.Equal("ParserTests fail on empty input; start there.", handed.Note);
        Assert.Equal(HandOverAskStates.Taken, new HandOverAskFiles(_home).ReadState("s1")!.State);

        // 3. The next look carries it on in the same tree on `stub`, told the note as the outgoing agent's claim; the quest closes.
        await driver.TickAsync().WaitAsync(Poll.Patience);
        await driver.Running.SettledAsync().WaitAsync(Poll.Patience);

        var carried = service.Session("s2");
        Assert.Equal("stub", carried["adapter"]!.GetValue<string>());
        Assert.Equal(("s1", "agent", "agent"),
            (carried["from"]?.GetValue<string>(), carried["fromWhy"]?.GetValue<string>(), carried["fromBy"]?.GetValue<string>()));
        var run = Runs(log).Single();
        Assert.Contains("ParserTests fail on empty input; start there.", run["target"]!.GetValue<string>());
        Assert.Contains("that agent's own claim", run["target"]!.GetValue<string>());
        Assert.Equal("Done", service.Status("q1"));
    }

    private DriverConfig Config(string log) =>
        HandOverRules.Apply(
            DriverConfig.Empty with
            {
                Drivable = ["engine"],
                Trees = ["engine"],
                Adapter = "acp-stub",
                TestBound = 1,
                PollSeconds = 1,
                Commands = new Dictionary<string, IReadOnlyList<string>>
                {
                    ["acp-stub"] = ["node", AcpAgent(), DaorisHome.Entry(_home, HomeEntry.Sessions)],
                    ["stub"] = ["node", Agent(), log],
                },
            },
            new HandOverEdit { Repository = "engine", Set = new HandOverSet { Mode = HandOverRules.On, ModeGiven = true } });

    private Daoris.Driver.Driver Driver(AskAndWaitTickTests.StandIn service, DriverConfig config)
    {
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: _processes,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            // The person's order: the machine's agent, then `stub`, another tool.
            HandOverDoors = _ => Task.FromResult<IReadOnlyList<HandOverDoor>>([new("acp-stub", true, null), new("stub", true, null)]),
        };
    }

    /// <summary>
    /// The machine's agent, on the protocol door: it takes its quest, writes its ask to hand over as <c>handover_propose</c> does for
    /// the session its connector names, and ends its turn.
    /// </summary>
    private string AcpAgent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { mkdirSync, writeFileSync } from 'node:fs';
            import { join } from 'node:path';
            const [folder] = process.argv.slice(2);
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
                const session = process.env.DAORIS_SESSION_ID;
                mkdirSync(folder, { recursive: true });
                writeFileSync(join(folder, `${session}.hand-over-ask.json`), JSON.stringify({
                  id: 'a1b2c3d4', session, note: 'ParserTests fail on empty input; start there.', askedBy: 'agent', at: new Date().toISOString() }));
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
            process.exit(0);
            """);
        return script;
    }

    /// <summary>The agent the quest is handed to: it writes down what it was handed, then closes the quest done.</summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const [log] = process.argv.slice(2);
            appendFileSync(log, JSON.stringify({ cwd: process.cwd(), target: process.env.DAORIS_TARGET ?? '' }) + '\n');
            await fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ action: 'done', reason: 'landed' }) });
            """);
        return script;
    }

    private static List<JsonNode> Runs(string log) => [.. File.ReadAllLines(log).Select(line => JsonNode.Parse(line)!)];

    private static async Task UntilAsync(Func<bool> condition)
    {
        var patience = DateTime.UtcNow + Poll.Patience;
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
        using var process = Process.Start(TestGit.Past(info))!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
}
