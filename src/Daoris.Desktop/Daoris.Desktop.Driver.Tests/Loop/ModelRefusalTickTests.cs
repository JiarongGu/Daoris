using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MODELROLE1c2 (D205 §3.3, §3.5(c), §4.6) through REAL ticks on the protocol door: a stand-in Build whose first choice names a model
/// one stub account refuses at the open runs that choice on the other account; with neither offering it, its second choice runs and
/// says so. A record its agent's refusal ended before any prompt ends <c>choice-not-offered</c>, holds no strike (one failure would
/// park the quest here), and the next look starts what the walk then answers, judging the reading the refusal kept.
/// </summary>
/// <remarks>
/// The stand-in agent offers <c>default</c>, <c>opus</c> and <c>sonnet</c>, and refuses the models its argument names for each stub
/// account at <c>session/set_config_option</c>, as Claude's door was measured to (MODELROLE1a); it writes down each open's account and
/// each model set, and closes its quest on its first prompt.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class ModelRefusalTickTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-model-refusal-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public ModelRefusalTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "refusal@example.com");
        Git("config", "user.name", "MODELROLE1c2");
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
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Log => Path.Combine(_home, "agent.log");

    private (Daoris.Driver.Driver Driver, HarnessRoster Roster) Driver(string url, string refuses)
    {
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses));
        var config = ActorChoices.With(
            DriverConfig.Empty with
            {
                Drivable = ["engine"],
                Trees = ["engine"],
                Adapter = "acp-stub",
                TestBound = 1,
                PollSeconds = 1,
                // One failure would park it: a choice its agent did not offer must hold none.
                Strikes = 1,
                Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Log, refuses] },
            },
            "build",
            [new ActorChoice("acp-stub", "opus"), new ActorChoice("acp-stub", "sonnet")]);
        return (new Daoris.Driver.Driver(new ServiceClient(url, null), config, adapters, _home, processes: new SessionProcesses(), harnesses: roster),
            roster);
    }

    private List<JsonNode> Runs() => [.. StubFile.Lines(Log).Select(line => JsonNode.Parse(line)!)];

    private static string? Code(JsonObject record, int at = 0) => record["noteParts"]?.AsArray().ElementAtOrDefault(at)?["code"]?.GetValue<string>();

    /// <summary>
    /// 🔴 The row's proof, first half: account-1 refuses `opus` at the open, so its record ends `choice-not-offered` with no strike and no
    /// prompt; the reading is kept, and the next look runs choice 1 on account-2, which takes and closes the quest.
    /// </summary>
    [Fact]
    public async Task A_first_choice_one_account_does_not_offer_runs_on_the_other_account()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var (driver, _) = Driver(service.Url, "account-1:opus");

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        var refused = service.Session("s1");
        Assert.Equal(("failed", "account-1"), (refused["state"]!.GetValue<string>(), refused["profile"]!.GetValue<string>()));
        Assert.Equal("ended.choice-not-offered", Code(refused));
        Assert.Equal("Open", service.Status("q1"));
        // The list named `opus` and the set refused it: that account's `refused` reading, kept before the record ended.
        Assert.Equal(AgentOffers.RefusedCode, AgentOffers.Judge(AgentOffers.Read(_home), "acp-stub", null, "account-1", "opus", DateTimeOffset.UtcNow));

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        var ran = service.Session("s2");
        Assert.Equal(("completed", "account-2"), (ran["state"]!.GetValue<string>(), ran["profile"]!.GetValue<string>()));
        Assert.Equal("1", ran["choice"]?.GetValue<string>());
        Assert.Equal("Done", service.Status("q1"));

        var runs = Runs();
        Assert.Equal([("account-1", "opus", false), ("account-2", "opus", true)],
            runs.Select(run => (run["account"]!.GetValue<string>(), run["model"]?.GetValue<string>(), run["prompted"]!.GetValue<bool>())));
    }

    /// <summary>
    /// 🔴 The row's proof, second half: with neither account offering `opus`, Build's second choice runs, its record's choice `2`, and its
    /// conversation's opening note says it ran as Build's choice 2, naming the choice passed; no strike was read on the way.
    /// </summary>
    [Fact]
    public async Task With_neither_account_offering_the_first_choice_the_second_runs_and_says_so()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var (driver, _) = Driver(service.Url, "account-1:opus,account-2:opus");

        for (var look = 0; look < 4 && service.Status("q1") != "Done"; look++)
        {
            await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        }

        Assert.Equal("Done", service.Status("q1"));
        var records = Enumerable.Range(1, service.SessionCount).Select(n => service.Session($"s{n}")).ToList();
        var done = Assert.Single(records, record => record["state"]!.GetValue<string>() == "completed");
        Assert.Equal("2", done["choice"]?.GetValue<string>());
        Assert.Equal("sonnet", done["asked"]?["model"]?["value"]?.GetValue<string>());
        Assert.All(records.Where(record => record != done), record => Assert.Equal("ended.choice-not-offered", Code(record)));

        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After(done["id"]!.GetValue<string>(), 0).Events;
        var actor = Assert.Single(events, e => e.Parts?.Any(part => part.Code == "opening.actor") == true);
        Assert.StartsWith("Ran as Build's choice 2, acp-stub · sonnet: choice 1, acp-stub · opus", actor.Text, StringComparison.Ordinal);
        // The actor line rides the local conversation alone, never the record's note, which travels (§4.6 item 1).
        Assert.DoesNotContain("Ran as", done["note"]?.GetValue<string>() ?? "", StringComparison.Ordinal);
    }

    /// <summary>
    /// The protocol door's stand-in on the stub's accounts: it lists Claude's three model values, refuses the models its argument names for
    /// its account at the set, writes down each open, and closes its quest on its first prompt.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { appendFileSync } from 'node:fs';
            import { basename } from 'node:path';
            const [log, refuses] = process.argv.slice(2);
            const home = process.env.DAORIS_STUB_CONFIG_DIR ?? '';
            const account = home ? basename(home) : '(own)';
            const refused = new Set(refuses.split(',').filter((pair) => pair.startsWith(account + ':')).map((pair) => pair.split(':')[1]));
            const url = process.env.DAORIS_SERVICE_URL;
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            let model = 'default';
            let asked = null;
            let prompted = false;
            const options = () => [{ id: 'model', name: 'Model', category: 'model', type: 'select', currentValue: model,
              options: [{ value: 'default', name: 'Default' }, { value: 'opus', name: 'Opus' }, { value: 'sonnet', name: 'Sonnet' }] }];
            for await (const line of createInterface({ input: process.stdin })) {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-1', configOptions: options() } });
              } else if (frame.method === 'session/set_config_option') {
                const value = frame.params.value;
                asked = value;
                if (refused.has(value)) {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: 'Internal error',
                    data: { details: `Invalid value for config option model: ${value}` } } });
                } else {
                  model = value;
                  send({ jsonrpc: '2.0', id: frame.id, result: { configOptions: options() } });
                }
              } else if (frame.method === 'session/prompt') {
                prompted = true;
                await respond({ action: 'take' });
                await respond({ action: 'done', reason: `ran ${model} on ${account}` });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
            appendFileSync(log, JSON.stringify({ account, model: asked, prompted }) + '\n');
            process.exit(0);
            """);
        return script;
    }

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        process.WaitForExit();
    }
}
