using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// TOOL4d (D125 §4, §5.2), observation 4 replayed through REAL ticks on the protocol door. On 1 October a driven session
/// carrying a quest on was refused mid-turn by the account's weekly spend limit; the driver carried the quest on twice
/// more, each refused at once, and the third failure parked the quest on its strikes in seconds. Now the refusal cools
/// the account until the reset the agent named, the carry-on waits for it with nothing spawned, the quest is never
/// parked for it, the wait is said once, and at the reset the carry-on runs and closes the quest.
/// </summary>
/// <remarks>
/// The stub agent speaks the wire and nothing else (D46 §8): it takes its quest and is cut off by a failure no table
/// knows, so the quest holds one strike; carrying it on, it answers its prompt with observation 4's sentence, the zone
/// the test's; carrying it on again, it closes the quest. The protocol stub reads the stub's words as its owner's, so
/// what cools is the stub's own sign-in. The clock and the zone are the roster's, the test's.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class AccountLimitTickTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Seen = new(2026, 10, 1, 14, 0, 0, TimeSpan.FromMinutes(345));

    private static readonly DateTimeOffset Until = new(2026, 10, 3, 16, 2, 0, TimeSpan.FromMinutes(345));

    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-limit-tick-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public AccountLimitTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "limit@example.com");
        Git("config", "user.name", "TOOL4d");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_carry_on_refused_for_the_account_s_limit_waits_for_the_reset_unparked_and_is_said_once()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var now = Seen;
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")) { Clock = () => now, Zone = Zone };
        using var client = new ServiceClient(service.Url, null);
        var lines = new List<AccountLine>();
        client.AccountLined += line => { lock (lines) lines.Add(line); };
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Adapter = "acp-stub",
            TimeoutMinutes = 1,
            PollSeconds = 1,
            // Two failures park it, and the quest already holds one: were the limit a strike, the carry-on it refused
            // would park the quest, as observation 4's did.
            Strikes = 2,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent()] },
        };
        var driver = new Daoris.Driver.Driver(client, config, adapters, _home, processes: new SessionProcesses(), harnesses: roster);
        var attention = new AttentionWatch();
        var said = new List<AttentionEvent>();

        // The first session takes the quest and is cut off by a failure no table recognises: a strike, as today.
        said.AddRange(attention.Observe(await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60))));
        Assert.Equal("failed", service.Session("s1")["state"]!.GetValue<string>());
        Assert.Null(service.Session("s1")["limit"]);
        Assert.Null(roster.CoolingOf("acp-stub", null));

        // Carrying it on, the next is refused mid-turn by observation 4's sentence: a limit, which cools the account.
        said.AddRange(attention.Observe(await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60))));
        var refused = service.Session("s2");
        Assert.Equal("failed", refused["state"]!.GetValue<string>());
        Assert.True(refused["limit"]!.GetValue<bool>());
        Assert.Contains("You've hit your individual spend limit", refused["note"]!.GetValue<string>());
        Assert.Contains($"The account it ran on is cooling until Oct 3, 16:02 ({Zone.Id}), as the agent said", refused["note"]!.GetValue<string>());
        var cooling = roster.CoolingOf("acp-stub", null)!;
        Assert.Equal(("stub", (string?)null, Until, true, "s2"), (cooling.Agent, cooling.Account, cooling.Until, cooling.Stated, cooling.Session));
        Assert.Equal("weekly", cooling.Window);

        // The looks that follow start nothing, and the quest waits for an account, never parked.
        for (var look = 0; look < 2; look++)
        {
            var held = await driver.TickAsync().WaitAsync(TimeSpan.FromSeconds(30));
            said.AddRange(attention.Observe(held));

            Assert.Equal(2, service.SessionCount);
            var sitting = held.Considerations.Single(c => c.Quest.Id == "q1");
            Assert.Equal(StartVerdict.Blocked, sitting.Verdict);
            Assert.Equal(CoolingWords.Hold(cooling, Zone), sitting.Reason);
            Assert.Equal(["q1"], Assert.Single(held.Waits).Quests);
        }

        Assert.Equal("Taken", service.Status("q1"));
        var waiting = Assert.Single(said, item => item.Kind == AttentionKind.Waiting);
        Assert.Equal("engine — waits for an account", waiting.Headline);
        lock (lines)
        {
            Assert.Single(lines, line => line.Event == "account.limited");
            Assert.Single(lines, line => line.Event == "starts.waiting");
        }

        // At the reset the carry-on runs, on the account that became ready, and closes the quest.
        now = Until.AddMinutes(1);
        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(3, service.SessionCount);
        Assert.Equal("completed", service.Session("s3")["state"]!.GetValue<string>());
        Assert.Equal("Done", service.Status("q1"));
    }

    /// <summary>
    /// The protocol door's stand-in for observation 4: a first prompt takes the quest and is cut off by a failure no table
    /// knows; the first carry-on is refused with the recorded sentence, its zone the test's; the next closes the quest.
    /// Which carry-on it is, is counted in a file beside it, since each is its own process.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        var counted = Path.Combine(_home, "carried.txt").Replace('\\', '/');
        File.WriteAllText(script, $$"""
            import { createInterface } from 'node:readline';
            import { existsSync, readFileSync, writeFileSync } from 'node:fs';
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const respond = (body) => fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            const refuse = (id, message) => send({ jsonrpc: '2.0', id, error: { code: -32603, message } });
            for await (const line of createInterface({ input: process.stdin })) {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-1' } });
              } else if (frame.method === 'session/prompt') {
                const said = (frame.params?.prompt ?? []).map((block) => block.text ?? '').join('\n');
                if (!said.includes('carrying on quest')) {
                  await respond({ action: 'take' });
                  refuse(frame.id, 'Internal error: the connection was reset');
                } else {
                  const carried = existsSync('{{counted}}') ? Number(readFileSync('{{counted}}', 'utf8')) : 0;
                  writeFileSync('{{counted}}', String(carried + 1));
                  if (carried === 0) {
                    refuse(frame.id, "Internal error: You've hit your individual spend limit · run /usage-credits to ask your admin for a higher limit · your weekly limit resets Oct 3, 4pm ({{Zone.Id}})");
                  } else {
                    await respond({ action: 'done', reason: 'finished what the cut-off sessions started' });
                    send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
                  }
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
