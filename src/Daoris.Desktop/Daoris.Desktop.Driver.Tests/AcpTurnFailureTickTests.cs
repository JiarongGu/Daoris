using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ACPEND1, through a REAL tick on the protocol door. Measured on the first real run: the agent took
/// the quest, worked, and then answered its prompt with an error — the account's spend limit — and
/// exited 0 once the driver closed its stdin. The quest was still taken, so the record said
/// "stood-down — someone else has it". It was nobody else: the turn had been refused.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class AcpTurnFailureTickTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-acp-end-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public AcpTurnFailureTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "acp@example.com");
        Git("config", "user.name", "ACPEND1");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_turn_the_agent_refused_after_taking_the_quest_is_a_failure_not_a_stand_down()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var driver = Driver(service);

        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        var record = service.Session("s1");
        Assert.Equal("Taken", service.Status("q1"));
        Assert.Equal("failed", record["state"]!.GetValue<string>());
        // The table knows these words (TOOL4d): the failure is a limit, and so not a strike.
        Assert.True(record["limit"]!.GetValue<bool>());
        // A limit's note carries its facts, the cooling line; the agent's own words stay in its transcript (AGT3c).
        var note = record["note"]!.GetValue<string>();
        Assert.StartsWith("the agent's turn failed with the quest still taken: The account it ran on is cooling until", note);
        Assert.DoesNotContain("spend limit", note);
        Assert.Contains("spend limit", File.ReadAllText(Path.Combine(_home, "sessions", "s1.log")));
    }

    /// <summary>
    /// D80, the rest of the measured run: the quest a cut-off left taken is carried on at the next tick,
    /// in the tree the cut-off session worked in, by a session told it is carrying on — which closes it.
    /// </summary>
    /// <remarks>
    /// Cut off by a refusal no limit table knows, since a limit's carry-on waits for the reset the agent named
    /// (TOOL4d): that wait is <c>AccountLimitTickTests</c>'.
    /// </remarks>
    [Fact]
    public async Task The_next_tick_carries_the_quest_on_in_the_same_tree_and_the_quest_closes()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var driver = Driver(service, trees: true, refusal: "Internal error: the connection was reset");

        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));
        var cutOff = service.Session("s1");
        Assert.Equal("failed", cutOff["state"]!.GetValue<string>());

        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        var carried = service.Session("s2");
        Assert.Equal("completed", carried["state"]!.GetValue<string>());
        Assert.Equal("Done", service.Status("q1"));
        Assert.Equal(
            Path.GetFullPath(cutOff["tree"]!.GetValue<string>()),
            Path.GetFullPath(carried["tree"]!.GetValue<string>()), ignoreCase: true);
    }

    private const string SpendLimit = "Internal error: You've hit your individual spend limit · your session limit resets 7am";

    private Daoris.Driver.Driver Driver(AskAndWaitTickTests.StandIn service, bool trees = false, string refusal = SpendLimit)
    {
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = trees ? ["engine"] : [],
            Adapter = "acp-stub",
            TimeoutMinutes = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), refusal] },
        };
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
    }

    /// <summary>
    /// The protocol door's stand-in for the measured run: it takes its quest through the service, then
    /// answers the prompt with the error the real adapter sent (or the refusal it is handed as its last
    /// argument), and exits 0 when its stdin closes. Told it is carrying the quest on, it closes the quest
    /// done and ends its turn as a real one would.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const refusal = process.argv[2];
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const respond = (body) => fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            for await (const line of createInterface({ input: process.stdin })) {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-1' } });
              } else if (frame.method === 'session/prompt') {
                const said = (frame.params?.prompt ?? []).map((block) => block.text ?? '').join('\n');
                if (said.includes('carrying on quest')) {
                  await respond({ action: 'done', reason: 'finished what the cut-off session started' });
                  send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
                } else {
                  await respond({ action: 'take' });
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: refusal } });
                }
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
            process.exit(0);
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
}
