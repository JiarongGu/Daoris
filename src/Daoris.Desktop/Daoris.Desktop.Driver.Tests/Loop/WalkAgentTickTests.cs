using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// WALKAGENT1 (D206 point 6, D205 §3.5) through REAL ticks on the protocol door: a driven line a limit cut off is carried on by walking its
/// Build actor's choices, choice-major, and never opens on an agent the role does not list. The install's case first: Build lists two
/// choices of one agent, every account of that agent is cooling and another agent has an account ready, which the role does not list, so
/// the line waits for the earliest reset and goes on in its own conversation at it. Then a role that does list another agent after the
/// first: the move opens there asking that choice's own model and effort, never the part before's.
/// </summary>
/// <remarks>
/// <c>acp-stub</c> is the first agent (the stub's accounts <c>account-1</c> and <c>account-2</c>, an agent whose first turn is refused by
/// Claude Code's limit sentence) and <c>acp-other</c> the second, a door on another tool's accounts (<c>account-x</c>), which answers every
/// prompt. Both are the one node stand-in, which offers a model and an effort and writes down each frame it hears. No model, no real
/// account, no sign-in.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class WalkAgentTickTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Seen = new(2026, 10, 1, 14, 0, 0, TimeSpan.FromMinutes(345));

    /// <summary>The reset the stand-in's limit sentence names: 3 October, 16:02 in the test's zone.</summary>
    private static readonly DateTimeOffset Until = new(2026, 10, 3, 16, 2, 0, TimeSpan.FromMinutes(345));

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-walk-agent-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    private DateTimeOffset _now = Seen;

    public WalkAgentTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "walk@example.com");
        Git("config", "user.name", "WALKAGENT1");
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

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Log => Path.Combine(_home, "walk-agent.log");

    private static readonly ActorChoice Sonnet = new("acp-stub", "sonnet", "low");

    private static readonly ActorChoice Opus = new("acp-stub", "opus", "high");

    private static readonly ActorChoice Other = new("acp-other", "codex-mini", "high");

    /// <summary>The stand-ins' adapters: the built stub's protocol door, and a second door onto another tool's accounts.</summary>
    private static AdapterSet Adapters() => new(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
    {
        ["stub"] = new StubAdapter(),
        ["acp-stub"] = new AcpStubAdapter(),
        ["acp-other"] = new AcpOtherAdapter(),
    });

    /// <summary>The machine's choices with Build's <paramref name="order"/>, and the legacy <c>adapter</c> key <paramref name="adapter"/>.</summary>
    private DriverConfig Config(IReadOnlyList<ActorChoice> order, string? adapter = null)
    {
        var agent = Agent();
        var config = ActorChoices.With(
            DriverConfig.Empty with
            {
                Drivable = ["engine"],
                Trees = ["engine"],
                Adapter = "acp-stub",
                TestBound = 1,
                PollSeconds = 1,
                Strikes = 1,
                Commands = new Dictionary<string, IReadOnlyList<string>>
                {
                    ["acp-stub"] = ["node", agent, Log, "stub"],
                    ["acp-other"] = ["node", agent, Log, "other"],
                },
            },
            "build", order);
        return adapter is null ? config : config with { Adapter = adapter };
    }

    private Daoris.Driver.Driver Driver(SessionMessagesTickTests.SaidStandIn service, DriverConfig config)
    {
        var adapters = Adapters();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => _now, Zone = Zone });
    }

    private JsonObject[] Heard() => [.. StubFile.Lines(Log).Select(line => JsonNode.Parse(line)!.AsObject())];

    private string Seeing(SessionMessagesTickTests.SaidStandIn service, TickReport look) =>
        $"look: {string.Join(" | ", look.Events)}\nsessions: {service.SessionCount}\n"
        + $"heard: {string.Join(" | ", Heard().Select(line => $"{line["tag"]}/{line["account"]} {line["method"]} {line["configId"]}={line["value"]}"))}";

    /// <summary>The first look: Build's first choice takes the quest on stub account-1, and its first turn is refused by the account's limit.</summary>
    private async Task LimitedAsync(SessionMessagesTickTests.SaidStandIn service, Daoris.Driver.Driver driver)
    {
        // account-2 cools for days, so every account of the stub is at its limit once account-1's turn is refused.
        AccountCooling.Cool(_home, new CoolingEntry("stub", "account-2", Until.AddDays(5), true, "weekly", Seen, "s0"), Seen);
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal(("failed", "Taken"), (service.State("s1"), service.Status("q1")));
        Assert.Contains("\"limit\":true", service.Record("s1"), StringComparison.Ordinal);
        Assert.Equal("acp-stub", JsonNode.Parse(service.Record("s1"))!["adapter"]!.GetValue<string>());
    }

    /// <summary>
    /// 🔴 The install's case: Build lists two choices of the stub's agent, each of its accounts is cooling and the other agent's account is
    /// ready, though Build does not list that agent. The carry-on waits for the earliest reset, said on the record once, and never opens
    /// there; the quest sits held and no frame reaches the other agent. At the reset the line resumes its own conversation, on its own
    /// account.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("acp-other")]
    public async Task A_line_at_a_limit_with_every_account_of_its_roles_agent_cooling_waits_for_the_earliest_reset_and_never_opens_on_another_agent(string? legacyAdapter)
    {
        await using var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        var driver = Driver(service, Config([Sonnet, Opus], legacyAdapter));
        await LimitedAsync(service, driver);

        for (var held = 0; held < 2; held++)
        {
            var waiting = await driver.TickAsync().WaitAsync(Poll.Patience);
            var sitting = waiting.Considerations.Single(c => c.Quest.Id == "q1");
            Assert.True(sitting.Verdict == StartVerdict.Blocked, Seeing(service, waiting));
            Assert.Contains("Every account it may run on is at its limit", sitting.Reason);
            Assert.Contains("Oct 3, 16:02", sitting.Reason);
        }

        Assert.Equal(1, service.SessionCount);
        Assert.DoesNotContain(Heard(), line => line["tag"]!.GetValue<string>() == "other");
        Assert.Empty(await new ServiceClient(service.Url, null).StrikesAsync());

        // At the earliest reset the line goes on in its own conversation on its own account, as D206's step 1 has it.
        _now = Until.AddMinutes(1);
        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.True(service.SessionCount == 1, Seeing(service, look));
        Assert.True((service.State("s1"), service.Status("q1")) == ("completed", "Done"), Seeing(service, look));
        Assert.Equal(["stub"], Heard().Select(line => line["tag"]!.GetValue<string>()).Distinct());
        Assert.Contains(Heard(), line => line["method"]!.GetValue<string>() == "session/resume");
    }

    /// <summary>
    /// 🔴 A role that lists another agent after the first: every account of the first cooling, the carry-on moves to the second choice,
    /// handed over by the limit as Daoris's move, asking that choice's own model and effort and never the part before's, on the other
    /// agent's own account.
    /// </summary>
    [Fact]
    public async Task A_role_listing_another_agent_after_the_first_moves_there_asking_that_choice_s_own_model_and_effort()
    {
        await using var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        var driver = Driver(service, Config([Sonnet, Other]));
        await LimitedAsync(service, driver);

        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = Seeing(service, look);
        Assert.True(service.SessionCount == 2, seen);
        var moved = JsonNode.Parse(service.Record("s2"))!;
        Assert.Equal(("acp-other", "account-x"), (moved["adapter"]!.GetValue<string>(), moved["profile"]!.GetValue<string>()));
        Assert.True((service.State("s2"), service.Status("q1")) == ("completed", "Done"), seen);

        // The part before says once that it went on with the other agent, by the limit.
        var went = Assert.Single(
            new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After("s1", 0).Events,
            e => e.Kind == SessionEventKind.Note && e.Parts?.Any(part => part.Code == "moved.on") == true)
            .Parts!.Single(part => part.Code == "moved.on");
        Assert.Equal(("acp-other", "limit"), (went.Value("agent")?.ToString(), went.Value("why")?.ToString()));

        // On the other agent's wire: its choice's model and effort set, never the first choice's.
        var others = Heard().Where(line => line["tag"]!.GetValue<string>() == "other").ToList();
        var set = others.Where(line => line["method"]!.GetValue<string>() == "session/set_config_option")
            .Select(line => (line["configId"]!.GetValue<string>(), line["value"]!.GetValue<string>())).ToList();
        Assert.Contains(("model", "codex-mini"), set);
        Assert.Contains(("effort", "high"), set);
        Assert.DoesNotContain(set, each => each.Item2 is "sonnet" or "low");
        Assert.Contains(others, line => line["prompt"]?.GetValue<string>().Contains("carrying on quest", StringComparison.Ordinal) == true);
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
    /// The protocol door's stand-in for both agents, <c>stub</c> or <c>other</c> by its argument. It offers a model and an effort and writes
    /// down each frame with the agent it is, the account it runs as and what it was set or prompted. A first prompt on the stub takes the
    /// quest and is refused by observation 4's sentence; any other prompt, a carry-on's, a resumed one or the other agent's, closes it.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "walk-agent.mjs");
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
            const LIMIT = "Internal error: You've hit your individual spend limit · run /usage-credits to ask your admin for a higher limit · your weekly limit resets Oct 3, 4pm (ZONE)";
            const current = { model: 'default', effort: 'default' };
            const options = () => [
              { id: 'model', name: 'Model', category: 'model', type: 'select', currentValue: current.model,
                options: ['default', 'sonnet', 'opus', 'codex-mini'].map((value) => ({ value, name: value })) },
              { id: 'effort', name: 'Effort', category: 'thought_level', type: 'select', currentValue: current.effort,
                options: ['default', 'low', 'high'].map((value) => ({ value, name: value })) },
            ];
            let conversation = null;
            let resumed = false;
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
              } else if (frame.method === 'session/resume' || frame.method === 'session/load') {
                heard({ method: frame.method, conversation: frame.params?.sessionId });
                conversation = frame.params.sessionId;
                resumed = true;
                send({ jsonrpc: '2.0', id: frame.id, result: { configOptions: options() } });
              } else if (frame.method === 'session/set_config_option') {
                heard({ method: frame.method, configId: frame.params.configId, value: frame.params.value });
                current[frame.params.configId] = frame.params.value;
                send({ jsonrpc: '2.0', id: frame.id, result: { configOptions: options() } });
              } else if (frame.method === 'session/prompt') {
                const prompt = (frame.params?.prompt ?? []).filter((block) => block.type === 'text').map((block) => block.text).join('\n');
                heard({ method: 'session/prompt', prompt, resumed });
                if (tag === 'stub' && !resumed && !prompt.includes('carrying on quest')) {
                  await respond({ action: 'take' });
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: LIMIT } });
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
            """.Replace("(ZONE)", $"({Zone.Id})", StringComparison.Ordinal));
        return script;
    }

    /// <summary>
    /// A door onto another tool's accounts (the way Codex's protocol door is onto Codex's): <see cref="AcpStubAdapter"/>'s own, as the
    /// agent <c>other</c>, so its accounts and its cool-offs are kept apart from the stub's.
    /// </summary>
    internal sealed class AcpOtherAdapter : ISessionAdapter
    {
        public string Name => "acp-other";

        public SessionWire Wire => SessionWire.Acp;

        public bool Interactive => true;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
            Spawning.InRoot(target, command![0], command.Skip(1), redirectInput: true);

        public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
            Spawning.ChatInRoot(target, command![0], command.Skip(1));

        public HarnessToolchain? Toolchain => new(
            Binary: [],
            VersionArguments: [],
            ProfileVariable: StubAdapter.ConfigVariable,
            AccountOf: "other",
            ProbeByPresence: true,
            RunsInOwnHome: true);
    }
}
