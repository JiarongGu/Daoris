using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The install's session of 2026-11-07 replayed through REAL ticks on the protocol door: Claude Code's adapter ended a driven turn
/// <c>end_turn</c> with its account's limit said as the agent's whole last message, which Daoris read as the agent's own words, sent on
/// again and then parked on the person with the limit as the question, cooling nothing. Now that turn concludes limited, stub account 1
/// cools until the next 3am in the zone the sentence names, it is never prompted again, and the carry-on goes on on stub account 2.
/// </summary>
/// <remarks>
/// The stand-in agent speaks the wire and nothing else on stdout, and writes down each prompt with the stub account it ran as. A first
/// prompt takes the quest, says the sentence as its message, reports its usage and ends its turn; a carry-on closes the quest.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class LimitMessageTickTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Australia/Sydney");

    /// <summary>The frame's time on the install.</summary>
    private static readonly DateTimeOffset Seen = new(2026, 11, 7, 14, 8, 34, TimeSpan.Zero);

    /// <summary>The next 3am in Sydney after it, daylight time then, plus the margin.</summary>
    private static readonly DateTimeOffset Until = new(2026, 11, 8, 3, 2, 0, TimeSpan.FromHours(11));

    private const string Sentence = "You've hit your session limit · resets 3am (Australia/Sydney)";

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-limit-message-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public LimitMessageTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "limit@example.com");
        Git("config", "user.name", "LIMITRESUME");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");

        foreach (var account in new[] { "account-1", "account-2" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", account));
        }

        new HarnessSettings().WithDefault("stub", "account-1").WithRotation("stub", ["account-1", "account-2"])
            .Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
    }

    public void Dispose()
    {
        GitSeed.Discard(_home);
    }

    private string Log => Path.Combine(_home, "agent.log");

    [Fact]
    public async Task A_turn_ended_on_the_limit_s_sentence_cools_its_account_to_the_stated_reset_and_goes_on_on_another()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var adapters = ProviderRegistry.Built();
        var roster = new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => Seen, Zone = Zone };
        using var client = new ServiceClient(service.Url, null);
        var config = StaffedRoles.Config("acp-stub") with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            TestBound = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", AcpAgent(), Log] },
        };
        var driver = new Daoris.Driver.Driver(client, config, adapters, _home, processes: new SessionProcesses(), harnesses: roster);

        // The first session runs on stub account 1, takes the quest, and its turn ends on the sentence: limited, never parked.
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        var cut = service.Session("s1");
        Assert.Equal(("failed", "account-1"), (cut["state"]!.GetValue<string>(), cut["profile"]!.GetValue<string>()));
        Assert.True(cut["limit"]!.GetValue<bool>());
        var note = cut["note"]!.GetValue<string>();
        Assert.Contains($"The `stub` account it ran on is cooling until Nov 8, 03:02 ({Zone.Id}), as the agent said", note);
        Assert.DoesNotContain("to ask you", note);

        // Stub account 1 cools until the next 3am in the zone the sentence named, stated; stub account 2 does not.
        var cooling = roster.CoolingOf("acp-stub", "account-1")!;
        Assert.Equal(("stub", "account-1", Until, true, "s1"), (cooling.Agent, cooling.Account, cooling.Until, cooling.Stated, cooling.Session));
        Assert.Null(roster.CoolingOf("acp-stub", "account-2"));

        // The carry-on opens on stub account 2 and closes the quest; stub account 1 is prompted once and never sent on.
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        var carried = service.Session("s2");
        Assert.Equal(("completed", "account-2"), (carried["state"]!.GetValue<string>(), carried["profile"]!.GetValue<string>()));
        Assert.Equal("Done", service.Status("q1"));

        var runs = File.ReadAllLines(Log).Select(line => JsonNode.Parse(line)!).ToList();
        Assert.Equal(["account-1", "account-2"], runs.Select(run => run["account"]!.GetValue<string>()));
        Assert.Contains("carrying on quest `#q1`", runs[1]["prompt"]!.GetValue<string>());
    }

    /// <summary>
    /// The protocol door's stand-in for the install's frames: the sentence as the agent's message, its usage, and <c>end_turn</c>, on a
    /// first prompt after taking the quest; a carry-on closes the quest.
    /// </summary>
    private string AcpAgent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, $$"""
            import { createInterface } from 'node:readline';
            import { appendFileSync } from 'node:fs';
            import { basename } from 'node:path';
            const [log] = process.argv.slice(2);
            const home = process.env.DAORIS_STUB_CONFIG_DIR ?? '';
            const account = home ? basename(home) : '(own)';
            const url = process.env.DAORIS_SERVICE_URL;
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const update = (body) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: 'acp-1', update: body } });
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
                  update({ sessionUpdate: 'agent_message_chunk', messageId: 'm-1', content: { type: 'text', text: {{System.Text.Json.JsonSerializer.Serialize(Sentence)}} } });
                  update({ sessionUpdate: 'usage_update', used: 155233, size: 1000000 });
                } else {
                  await respond({ action: 'done', reason: 'finished on the next stub account' });
                }
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
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
