using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CODEXCREDITS1 (D210, D206) through REAL ticks on the protocol door. Three driven Codex sessions ended on the ACP error
/// <c>Internal error: Your workspace is out of credits. Ask your workspace owner to refill in order to continue.</c>, which the door
/// carries as <c>codex-acp</c> sends it: the message <c>Internal error</c> and the sentence in <c>data.message</c>. It is a limit whose
/// reset the sentence does not name: the account cools on the default, the quest is never struck or parked, and the work goes to the next
/// allowed account, or, with none, waits for one, said as it is.
/// </summary>
/// <remarks>
/// The stand-in speaks the wire and nothing else, as <c>codex-acp</c> under the adapter's own name: its first prompt takes the quest
/// and is refused with that error; a carry-on closes the quest. The clock and the zone are the roster's, the test's.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class CodexCreditsTickTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Seen = new(2026, 10, 26, 14, 0, 0, TimeSpan.FromMinutes(345));

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-codex-credits-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public CodexCreditsTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "credits@example.com");
        Git("config", "user.name", "CODEXCREDITS1");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Log => Path.Combine(_home, "agent.log");

    /// <summary>Codex's accounts, in the order the person set, each a home that exists (the adapter's <c>ProfileMustExist</c>).</summary>
    private void Accounts(params string[] names)
    {
        foreach (var account in names) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "codex", account));
        new HarnessSettings().WithDefault("codex", names[0]).WithRotation("codex", names).Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
    }

    private DriverConfig Config() => DriverConfig.Empty with
    {
        Drivable = ["engine"],
        Trees = ["engine"],
        Adapter = "codex-acp",
        TestBound = 1,
        PollSeconds = 1,
        // One failure would park it: the limit must hold none.
        Strikes = 1,
        // The account agent's own reads run the stand-in too, never PATH's codex.
        Commands = new Dictionary<string, IReadOnlyList<string>> { ["codex-acp"] = ["node", Agent(), Log], ["codex"] = ["node", Agent(), Log] },
    };

    /// <summary>
    /// 🔴 With another allowed account: the first session is refused with the sentence, its account cools on the default with the reset
    /// unstated, the quest holds no strike, and the carry-on opens on the next account in the same tree and closes the quest.
    /// </summary>
    [Fact]
    public async Task A_workspace_out_of_credits_cools_the_account_and_the_carry_on_goes_to_the_next_allowed_account()
    {
        Accounts("account-1", "account-2");
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => Seen, Zone = Zone };
        using var client = new ServiceClient(service.Url, null);
        var driver = new Daoris.Driver.Driver(client, Config(), adapters, _home, processes: new SessionProcesses(), harnesses: roster);

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        var cut = service.Session("s1");
        Assert.Equal(("failed", "account-1", true), (cut["state"]!.GetValue<string>(), cut["profile"]!.GetValue<string>(), cut["limit"]!.GetValue<bool>()));
        // The note travels, so it carries the facts and never the agent's sentence, which its transcript keeps (AGT3c).
        Assert.DoesNotContain("out of credits", cut["note"]!.GetValue<string>());
        Assert.Contains("out of credits", File.ReadAllText(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1.log")));

        var cooling = roster.CoolingOf("codex-acp", "account-1")!;
        Assert.Equal(("codex", "account-1", Seen + AccountLimits.DefaultCoolOff, false), (cooling.Agent, cooling.Account, cooling.Until, cooling.Stated));
        Assert.Null(roster.CoolingOf("codex-acp", "account-2"));

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        var carried = service.Session("s2");
        Assert.Equal(("completed", "account-2"), (carried["state"]!.GetValue<string>(), carried["profile"]!.GetValue<string>()));
        Assert.Equal("Done", service.Status("q1"));
        Assert.Equal(["account-1", "account-2"], File.ReadAllLines(Log).Select(line => JsonNode.Parse(line)!["account"]!.GetValue<string>()));
    }

    /// <summary>
    /// 🔴 With no other allowed account: the quest is not struck, parked or started again blind. It waits for an account, held blocked
    /// with D206's own words for the wait, nothing spawned at the looks that follow.
    /// </summary>
    [Fact]
    public async Task A_workspace_out_of_credits_with_no_other_account_waits_for_one_unparked_and_unspawned()
    {
        Accounts("account-1");
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => Seen, Zone = Zone };
        using var client = new ServiceClient(service.Url, null);
        var driver = new Daoris.Driver.Driver(client, Config(), adapters, _home, processes: new SessionProcesses(), harnesses: roster);

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.True(service.Session("s1")["limit"]!.GetValue<bool>());
        var cooling = roster.CoolingOf("codex-acp", "account-1")!;

        for (var look = 0; look < 2; look++)
        {
            var held = await driver.TickAsync().WaitAsync(Poll.Patience);

            Assert.Equal(1, service.SessionCount);
            var sitting = held.Considerations.Single(c => c.Quest.Id == "q1");
            Assert.Equal(StartVerdict.Blocked, sitting.Verdict);
            Assert.Equal(LimitResume.Waits(cooling, Zone).Note, sitting.Reason);
            Assert.Equal(["q1"], Assert.Single(held.Waits).Quests);
        }

        Assert.Equal("Taken", service.Status("q1"));
        Assert.Single(File.ReadAllLines(Log));
    }

    /// <summary>
    /// The protocol door's stand-in for <c>codex-acp</c>. It speaks the wire and nothing else on stdout; for each prompt it writes down
    /// which account it ran as (<c>CODEX_HOME</c>'s folder). A first prompt takes the quest and is refused as the install met it; a
    /// carry-on closes the quest.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "codex-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { appendFileSync } from 'node:fs';
            import { basename } from 'node:path';
            const [log] = process.argv.slice(2);
            if (process.argv.includes('login') || process.argv.includes('--version')) process.exit(0);
            const account = basename(process.env.CODEX_HOME ?? '(own)');
            const url = process.env.DAORIS_SERVICE_URL;
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            for await (const line of createInterface({ input: process.stdin })) {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-1' } });
              } else if (frame.method === 'session/prompt') {
                const prompt = (frame.params?.prompt ?? []).map((block) => block.text ?? '').join('\n');
                appendFileSync(log, JSON.stringify({ account, prompt }) + '\n');
                if (!prompt.includes('carrying on quest')) {
                  await respond({ action: 'take' });
                  send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: 'acp-1',
                    update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'I started the field.' } } } });
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: 'Internal error',
                    data: { message: 'Your workspace is out of credits. Ask your workspace owner to refill in order to continue.' } } });
                } else {
                  await respond({ action: 'done', reason: 'finished on the next account' });
                  send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
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
        using var process = Process.Start(TestGit.Past(info))!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
}
