using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// 🔴 FOUNDATION1j part ii (the foundation design §2.4's first row and §7 row 5; D205 point 1) through REAL ticks on the protocol door: a part
/// that ran on Build's second choice and was cut off is carried on with that choice, its agent, model and effort, never the machine's
/// <c>adapter</c> key's agent with the part before's model. Build lists the stub first and another agent second; every account of the stub
/// is cooling, so the walk takes the second choice; its turn fails with the quest taken, and the next look's carry-on opens on the second
/// choice again, asking its own model and effort, and closes the quest. No frame reaches the stub.
/// </summary>
/// <remarks>
/// <c>acp-stub</c> is the first agent (its accounts <c>account-1</c> and <c>account-2</c>, both cooling) and <c>acp-other</c> the second, on
/// another tool's account (<c>account-x</c>): the one node stand-in, which offers a model and an effort and writes down each frame it hears.
/// Its first prompt takes the quest and fails the turn; a carry-on's prompt closes it. No model, no real account, no sign-in.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class ContinuedChoiceTickTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Now = new(DateTime.UtcNow.Date.AddHours(12), TimeSpan.Zero);

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "foundation1j-tick-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public ContinuedChoiceTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "continued@example.com");
        Git("config", "user.name", "FOUNDATION1j");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");

        foreach (var (agent, account) in new[] { ("stub", "account-1"), ("stub", "account-2"), ("other", "account-x") })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, agent, account));
        }

        new HarnessSettings()
            .WithDefault("stub", "account-1").WithRotation("stub", ["account-1", "account-2"])
            .WithDefault("other", "account-x").WithRotation("other", ["account-x"])
            .Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
    }

    public void Dispose() => GitSeed.Discard(_home);

    private string Log => Path.Combine(_home, "continued-choice.log");

    private static AdapterSet Adapters() => new(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
    {
        ["stub"] = new StubAdapter(),
        ["acp-stub"] = new AcpStubAdapter(),
        ["acp-other"] = new WalkAgentTickTests.AcpOtherAdapter(),
    });

    /// <summary>Build's order: the stub · sonnet · low, then the other agent · codex-mini · high; a failed turn leaves a strike to carry on past.</summary>
    private DriverConfig Config() => ActorChoices.With(
        DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "acp-stub",
            TestBound = 1,
            PollSeconds = 1,
            Strikes = 2,
            Commands = new Dictionary<string, IReadOnlyList<string>>
            {
                ["acp-stub"] = ["node", Agent(), Log, "stub"],
                ["acp-other"] = ["node", Agent(), Log, "other"],
            },
        },
        "build", [new ActorChoice("acp-stub", "sonnet", "low"), new ActorChoice("acp-other", "codex-mini", "high")]);

    private JsonObject[] Heard() => [.. StubFile.Lines(Log).Select(line => JsonNode.Parse(line)!.AsObject())];

    private string Seeing(AskAndWaitTickTests.StandIn service, TickReport look) =>
        $"look: {string.Join(" | ", look.Events)}\nsessions: {service.SessionCount}\n"
        + $"heard: {string.Join(" | ", Heard().Select(line => $"{line["tag"]}/{line["account"]}/{line["session"]} {line["method"]} {line["configId"]}={line["value"]}"))}";

    [Fact]
    public async Task A_part_on_Build_s_second_choice_is_carried_on_with_that_choice_s_agent_model_and_effort()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.KeepsAdapter = true;
        service.Claim = "held";
        foreach (var account in new[] { "account-1", "account-2" })
        {
            AccountCooling.Cool(_home, new CoolingEntry("stub", account, Now.AddDays(5), true, "weekly", Now, "s0"), Now);
        }

        var adapters = Adapters();
        var driver = new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), Config(), adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => Now, Zone = Zone });

        // The first look: the stub's accounts cool, so Build's second choice takes the quest, and its turn fails with the quest taken.
        var first = Seeing(service, await driver.RunOnceAsync().WaitAsync(Poll.Patience));
        Assert.True((service.Session("s1")["state"]!.GetValue<string>(), service.Status("q1")) == ("failed", "Taken"), first);
        Assert.Equal(("acp-other", "2"), (service.Session("s1")["adapter"]!.GetValue<string>(), service.Session("s1")["choice"]?.GetValue<string>()));

        var seen = first;
        for (var look = 0; look < 3 && service.Status("q1") != "Done"; look++)
        {
            seen = Seeing(service, await driver.RunOnceAsync().WaitAsync(Poll.Patience));
        }

        Assert.True(service.Status("q1") == "Done", seen);
        Assert.True(service.SessionCount == 2, seen);
        var carried = service.Session("s2");
        Assert.Equal(("completed", "acp-other", "account-x"), (carried["state"]!.GetValue<string>(), carried["adapter"]!.GetValue<string>(), carried["profile"]!.GetValue<string>()));
        Assert.Equal(("2", "acp-other", "codex-mini"), (carried["choice"]?.GetValue<string>(), carried["asked"]?["agent"]?.GetValue<string>(), carried["asked"]?["model"]?["value"]?.GetValue<string>()));
        // On the second choice's own wire, its model and effort set for the carry-on; nothing ever reached the stub.
        var set = Heard().Where(line => line["session"]?.GetValue<string>() == "s2" && line["method"]!.GetValue<string>() == "session/set_config_option")
            .Select(line => (line["configId"]!.GetValue<string>(), line["value"]!.GetValue<string>())).ToList();
        Assert.Contains(("model", "codex-mini"), set);
        Assert.Contains(("effort", "high"), set);
        Assert.DoesNotContain(Heard(), line => line["tag"]!.GetValue<string>() == "stub");
    }

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }

    /// <summary>
    /// The protocol door's stand-in for both agents, <c>stub</c> or <c>other</c> by its argument: it offers Claude's aliases and
    /// <c>codex-mini</c>, and writes down each frame with the agent it is, its session and the account it runs as. A first prompt takes the
    /// quest and fails its turn with a plain error, no limit and nothing to try again; a carry-on's prompt closes the quest.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "continued-choice.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { appendFileSync } from 'node:fs';
            import { basename } from 'node:path';
            const [log, tag] = process.argv.slice(2);
            const url = process.env.DAORIS_SERVICE_URL;
            const session = process.env.DAORIS_SESSION_ID;
            const home = process.env.DAORIS_STUB_CONFIG_DIR ?? '';
            const account = home ? basename(home) : '(own)';
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const heard = (what) => appendFileSync(log, JSON.stringify({ ...what, tag, session, account }) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            const current = { model: 'default', effort: 'default' };
            const options = () => [
              { id: 'model', name: 'Model', category: 'model', type: 'select', currentValue: current.model,
                options: ['default', 'sonnet', 'opus', 'codex-mini'].map((value) => ({ value, name: value })) },
              { id: 'effort', name: 'Effort', category: 'thought_level', type: 'select', currentValue: current.effort,
                options: ['default', 'low', 'high'].map((value) => ({ value, name: value })) },
            ];
            let conversation = null;
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1,
                  agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                conversation = 'conv-' + session;
                heard({ method: 'session/new' });
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation, configOptions: options() } });
              } else if (frame.method === 'session/set_config_option') {
                heard({ method: frame.method, configId: frame.params.configId, value: frame.params.value });
                current[frame.params.configId] = frame.params.value;
                send({ jsonrpc: '2.0', id: frame.id, result: { configOptions: options() } });
              } else if (frame.method === 'session/prompt') {
                const prompt = (frame.params?.prompt ?? []).filter((block) => block.type === 'text').map((block) => block.text).join('\n');
                heard({ method: 'session/prompt', prompt });
                if (!prompt.includes('carrying on quest')) {
                  await respond({ action: 'take' });
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: 'Internal error: the stand-in broke its turn.' } });
                } else {
                  await respond({ action: 'done', reason: 'finished' });
                  send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
                    update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'Done.' } } } });
                  send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
                }
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }
}
