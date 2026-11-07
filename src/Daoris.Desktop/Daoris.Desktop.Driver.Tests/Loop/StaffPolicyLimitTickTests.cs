using System.Diagnostics;
using System.Globalization;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// STAFFPOLICY1e2 (the staff policy design §4, §9 row e) through REAL ticks, the limit condition's <c>wait</c> and <c>ask</c>: a driven
/// run on Build's first choice (the stub's agent, both its accounts at their limit) while the second choice (another agent) is ready. With
/// <c>wait</c> the line holds for the first choice's reset though the second is ready, and goes on in its own conversation at the reset; with
/// <c>upTo</c> it walks to the second choice once that many minutes have passed on the held clock; with <c>ask</c> it holds for the person's yes
/// and one yes releases one move. The held clock is the roster's (<c>.claude/knowledge/held-clocks.md</c>); nothing here waits on time.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class StaffPolicyLimitTickTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    // The held clock starts at the real moment, so the records' own stamps (the service's, real) and the policy's minutes agree.
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    private readonly DateTimeOffset _reset;

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "staff-limit-tick", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    private static readonly ActorChoice Sonnet = new("acp-stub", "sonnet", "low");

    private static readonly ActorChoice Other = new("acp-other", "codex-mini", "high");

    public StaffPolicyLimitTickTests()
    {
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "limit@example.com");
        Git("config", "user.name", "STAFFPOLICY1e2");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
        var local = TimeZoneInfo.ConvertTime(_now, Zone).Date.AddDays(3);
        _reset = new DateTimeOffset(local.AddHours(16).AddMinutes(2), Zone.GetUtcOffset(local));
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
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private string Log => Path.Combine(_home, "walk-agent.log");

    private JsonObject[] Heard() => File.Exists(Log) ? [.. StubFile.Lines(Log).Select(line => JsonNode.Parse(line)!.AsObject())] : [];

    private IReadOnlyList<string> Moves()
    {
        using var reader = DesktopStoreReader.Open(DesktopStore.LocationIn(_home));
        return reader is null ? [] : reader.Query("SELECT line || ':' || kind || ':' || COALESCE(condition,'') FROM role_moves ORDER BY id", row => row.GetString(0));
    }

    private Daoris.Driver.Driver Driver(SessionMessagesTickTests.SaidStandIn service, string policy)
    {
        var adapters = TestDoors.Registry([.. TestDoors.Shipped("stub", "acp-stub"), WalkAgentTickTests.AcpOtherAdapter()]);
        var config = ActorChoices.With(
            DriverConfig.Empty with
            {
                Drivable = ["engine"],
                Trees = ["engine"],
                Actors = StaffedRoles.On("acp-stub"),
                TestBound = 1,
                PollSeconds = 1,
                Commands = new Dictionary<string, IReadOnlyList<string>>
                {
                    ["acp-stub"] = ["node", Agent(), Log, "stub"],
                    ["acp-other"] = ["node", Agent(), Log, "other"],
                },
            },
            "build", [Sonnet, Other]);
        var file = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file,
            """{ "adapter": "acp-stub", "actors": { "build": [ { "agent": "acp-stub", "model": "sonnet", "effort": "low" }, { "agent": "acp-other", "model": "codex-mini", "effort": "high" } ] } }""");
        var staffing = new RoleStaffing(_home, () => StoreSettled.Yes);
        Assert.Equal(StaffingImported.Done, staffing.ImportFile().How);
        // The policy keeps its rule and choices; its moves are the machine's rules below.
        var parsed = PolicyRules.Parse(policy)!;
        RoleDoor.Apply(staffing, RoleChange.Set(RoleCatalog.Seed[0]), null, null, new RolePolicyPress(StaffPolicy.Parse(policy).Policy!));
        SeededRules.In(_home, strikes: 3, parsed);
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => _now, Zone = Zone });
    }

    private static string Policy(string limit) =>
        $$"""
        {"rule":"order","choices":[{"agent":"acp-stub","model":"sonnet","effort":"low"},{"agent":"acp-other","model":"codex-mini","effort":"high"}],
         "moves":{"limit":{{limit}}},"most":3}
        """;

    /// <summary>The first look: choice 1 takes the quest on stub account-1 and its first turn is refused by the limit; account-2 already cools.</summary>
    private async Task LimitedAsync(SessionMessagesTickTests.SaidStandIn service, Daoris.Driver.Driver driver)
    {
        AccountCooling.Cool(_home, new CoolingEntry("stub", "account-2", _reset.AddDays(5), true, "weekly", _now, "s0"), _now);
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal(("failed", "Taken"), (service.State("s1"), service.Status("q1")));
        Assert.Contains("\"limit\":true", service.Record("s1"), StringComparison.Ordinal);
    }

    private string Seeing(SessionMessagesTickTests.SaidStandIn service, TickReport look) =>
        $"look: {string.Join(" | ", look.Events)}\nsessions: {service.SessionCount}\nmoves: {string.Join(" | ", Moves())}\n"
        + $"heard: {string.Join(" | ", Heard().Select(line => $"{line["tag"]}/{line["account"]} {line["method"]}"))}";

    [Fact]
    public async Task A_limit_that_waits_holds_the_line_for_its_own_reset_though_the_second_choice_is_ready_and_goes_on_there_at_the_reset()
    {
        await using var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        var driver = Driver(service, Policy("""{"do":"wait"}"""));
        await LimitedAsync(service, driver);

        for (var held = 0; held < 2; held++)
        {
            var waiting = await driver.TickAsync().WaitAsync(Poll.Patience);
            var sitting = waiting.Considerations.Single(c => c.Quest.Id == "q1");
            Assert.True(sitting.Verdict == StartVerdict.Blocked, Seeing(service, waiting));
            Assert.Contains("Every account it may run on is at its limit", sitting.Reason);
        }

        Assert.Equal(1, service.SessionCount);
        Assert.DoesNotContain(Heard(), line => line["tag"]!.GetValue<string>() == "other");
        Assert.Empty(Moves());

        _now = _reset.AddMinutes(1);
        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.True(service.SessionCount == 1 && (service.State("s1"), service.Status("q1")) == ("completed", "Done"), Seeing(service, look));
        Assert.DoesNotContain(Heard(), line => line["tag"]!.GetValue<string>() == "other");
    }

    [Fact]
    public async Task A_limit_that_waits_up_to_thirty_minutes_walks_to_the_second_choice_once_they_have_passed_on_the_held_clock()
    {
        await using var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        var driver = Driver(service, Policy("""{"do":"wait","upTo":30}"""));
        await LimitedAsync(service, driver);

        _now = _now.AddMinutes(20);
        var held = await driver.TickAsync().WaitAsync(Poll.Patience);
        Assert.True(service.SessionCount == 1, Seeing(service, held));
        Assert.Equal(StartVerdict.Blocked, held.Considerations.Single(c => c.Quest.Id == "q1").Verdict);

        _now = _now.AddMinutes(15);
        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        var seen = Seeing(service, look);
        Assert.True(service.SessionCount == 2, seen);
        Assert.Equal("acp-other", JsonNode.Parse(service.Record("s2"))!["door"]!.GetValue<string>());
        Assert.Equal(["q1:moved:limit"], Moves());
    }

    [Fact]
    public async Task A_limit_that_asks_holds_the_quest_for_the_person_and_one_yes_releases_one_move()
    {
        await using var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        var driver = Driver(service, Policy("""{"do":"ask"}"""));
        await LimitedAsync(service, driver);

        IReadOnlyList<Consideration> verdicts = [];
        for (var held = 0; held < 2; held++)
        {
            var waiting = await driver.TickAsync().WaitAsync(Poll.Patience);
            Assert.True(service.SessionCount == 1, Seeing(service, waiting));
            verdicts = waiting.Considerations;
        }

        var verdict = Assert.Single(verdicts, each => each.Quest.Id == "q1");
        Assert.Equal(StartVerdict.PolicyHeld, verdict.Verdict);
        Assert.Equal(SittingFact.PolicyAsk, verdict.Said!.Code);
        Assert.Equal((RetryDecision.Approved, "build"), (QuestRetry.Decide("q1", verdicts).Did, QuestRetry.Decide("q1", verdicts).Approve));
        Assert.Empty(Moves());

        // The yes through the terminal's door, which writes the one approval; the window's route is held by DriverModuleRetryTests.
        var said = new StringWriter();
        var world = new DriverChoicesWorld
        {
            Verbs = DriverChoiceVerbs.With((_, _) => Task.FromResult(verdicts)),
            Instance = new InstanceWorld { Home = _home, Environment = _ => null, Out = said, Error = said, Verb = "daoris" },
            ConfigPath = DaorisHome.Entry(_home, HomeEntry.DriverConfig),
        };
        Assert.Equal(0, await DriverChoicesCommand.RunAsync(["retry", "q1"], world, CancellationToken.None));
        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        var seen = Seeing(service, look);
        Assert.True(service.SessionCount == 2, seen);
        Assert.Equal("acp-other", JsonNode.Parse(service.Record("s2"))!["door"]!.GetValue<string>());
        Assert.Equal(["q1:approved:", "q1:moved:limit"], Moves());
    }

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }

    /// <summary>The stand-in for both agents, as the walk-agent tick's: the stub's first prompt takes the quest and is refused by the limit's sentence; any other closes it.</summary>
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
            const LIMIT = "Internal error: You've hit your individual spend limit · run /usage-credits to ask your admin for a higher limit · your weekly limit resets RESET (ZONE)";
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
            """
            .Replace("RESET", $"{TimeZoneInfo.ConvertTime(_reset, Zone).ToString("MMM d", CultureInfo.InvariantCulture)}, 4pm", StringComparison.Ordinal)
            .Replace("(ZONE)", $"({Zone.Id})", StringComparison.Ordinal));
        return script;
    }
}
