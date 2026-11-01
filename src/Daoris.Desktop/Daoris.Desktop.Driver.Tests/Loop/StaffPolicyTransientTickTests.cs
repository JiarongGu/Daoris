using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.SessionMessagesTickTests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// STAFFPOLICY1e (D226; the staff policy design §4, §9 row e) through REAL ticks, the transient condition: a driven run on Build's first
/// choice whose service never answers makes the role's <c>after</c> tries (two, three minutes of waits on the held clock,
/// <c>.claude/knowledge/held-clocks.md</c>) and ends <c>ended.transient</c>; the next look's course moves the line, once, to the second
/// choice in a new part that continues the first with the reason <c>transient</c>, the first choice's agent passed on the walk. A role with
/// no policy keeps its five tries and its hold for the person.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class StaffPolicyTransientTickTests : IAsyncLifetime
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "staff-transient-tick", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    private static readonly ActorChoice First = new("acp-stub", "sonnet", "low");

    private static readonly ActorChoice Second = new("acp-other", "codex-mini", "high");

    public StaffPolicyTransientTickTests() => _repository = Path.Combine(_home, "engine");

    public async Task InitializeAsync()
    {
        await GitSeed.PlainAsync(_repository, "engine");
        foreach (var (agent, account) in new[] { ("stub", "account-1"), ("other", "account-x") })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, agent, account));
        }

        new HarnessSettings()
            .WithDefault("stub", "account-1").WithRotation("stub", ["account-1"])
            .WithDefault("other", "account-x").WithRotation("other", ["account-x"])
            .Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
    }

    public Task DisposeAsync()
    {
        var failed = File.Exists(Log) ? Heard().Where(line => line["method"]!.GetValue<string>() == "stand-in-failed").Select(line => line.ToJsonString()).ToList() : [];
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
        Assert.True(failed.Count == 0, $"the stand-in agent failed on its own: {string.Join(" | ", failed)}");
        return Task.CompletedTask;
    }

    private string Log => Path.Combine(_home, "agent.log");

    private JsonObject[] Heard() => File.Exists(Log) ? [.. StubFile.Lines(Log).Select(line => JsonNode.Parse(line)!.AsObject())] : [];

    private JsonObject[] Prompts() => [.. Heard().Where(line => line["method"]!.GetValue<string>() == "session/prompt")];

    private IReadOnlyList<DateTimeOffset> Waits(string session) =>
        [.. new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After(session, 0).Events
            .Where(e => e.Kind == SessionEventKind.Transient && e.Next is not null).Select(e => e.Next!.Value)];

    private Daoris.Driver.Driver Protocol(SaidStandIn service, MovedClock clock, string? policy)
    {
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["stub"] = new StubAdapter(),
            ["acp-stub"] = new AcpStubAdapter(),
            ["acp-other"] = new WalkAgentTickTests.AcpOtherAdapter(),
        });
        var config = ActorChoices.With(
            DriverConfig.Empty with
            {
                Drivable = ["engine"],
                Trees = ["engine"],
                Adapter = "acp-stub",
                TestBound = 10,
                PollSeconds = 1,
                Commands = new Dictionary<string, IReadOnlyList<string>>
                {
                    ["acp-stub"] = ["node", Agent(), Log, "overloaded"],
                    ["acp-other"] = ["node", Agent(), Log, "serves"],
                },
            },
            "build", [First, Second]);

        // The staffing in the home's store, with the policy the case sets through the role door, as the person's press would.
        var file = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file,
            """{ "adapter": "acp-stub", "actors": { "build": [ { "agent": "acp-stub", "model": "sonnet", "effort": "low" }, { "agent": "acp-other", "model": "codex-mini", "effort": "high" } ] } }""");
        var staffing = new RoleStaffing(_home, () => StoreSettled.Yes);
        Assert.Equal(StaffingImported.Done, staffing.ImportFile().How);
        if (policy is not null)
        {
            RoleDoor.Apply(staffing, RoleChange.Set(RoleCatalog.Seed[0]), null, null, new RolePolicyPress(StaffPolicy.Parse(policy).Policy!));
        }

        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            RetryClock = clock,
        };
    }

    private const string Moving =
        """
        {"rule":"order","choices":[{"agent":"acp-stub","model":"sonnet","effort":"low"},{"agent":"acp-other","model":"codex-mini","effort":"high"}],
         "moves":{"transient":{"do":"next","after":2}},"most":3}
        """;

    /// <summary>Passes each wait <c>s1</c>'s run makes on the held clock, one at a time, by the record's own state and never by time.</summary>
    private async Task PassAsync(MovedClock clock, Task tick, params int[] minutes)
    {
        var wait = 0;
        foreach (var each in minutes)
        {
            wait++;
            DateTimeOffset? until = null;
            await Poll.Until(
                () =>
                {
                    until = Waits("s1").Skip(wait - 1).FirstOrDefault();
                    return (until is { } at && clock.Pending.Contains(at)) || tick.IsCompleted;
                },
                () => $"s1's wait {wait} never began on the held clock", Poll.Patience);
            Assert.False(tick.IsCompleted, $"the tick ended where s1's run should have waited ({wait})");
            Assert.Equal(TimeSpan.FromMinutes(each), until!.Value - clock.GetUtcNow());
            clock.Advance(until.Value - clock.GetUtcNow());
        }
    }

    private string Seen(SaidStandIn service) =>
        $"s1: {service.Record("s1")}\ns2: {(service.SessionCount > 1 ? service.Record("s2") : "none")}\nmoves: {string.Join(" | ", Moves())}\nheard: {string.Join(" | ", Heard().Select(h => h.ToJsonString()))}";

    private IReadOnlyList<string> Moves()
    {
        using var reader = DesktopStoreReader.Open(DesktopStore.LocationIn(_home));
        return reader is null ? [] : reader.Query("SELECT line || ':' || kind || ':' || COALESCE(session,'') || ':' || COALESCE(condition,'') FROM role_moves ORDER BY id", row => row.GetString(0));
    }

    [Fact]
    public async Task Two_tries_that_fail_end_the_run_and_the_next_look_moves_the_quest_to_the_second_choice_in_a_briefed_part()
    {
        await using var service = SaidStandIn.Start(_repository);
        var clock = new MovedClock();
        var driver = Protocol(service, clock, Moving);

        var tick = driver.RunOnceAsync();
        // The role's `after` is two tries: waits of one and two minutes, three in all, where D210's five make thirty-one.
        await PassAsync(clock, tick, 1, 2);
        await tick.WaitAsync(Poll.Patience);

        Assert.True(service.State("s1") == "failed", Seen(service));
        Assert.Contains("kept failing after 2 tries, with 3 minutes of waits", service.Note("s1"));
        Assert.Equal(3, Prompts().Length);
        Assert.Empty(Moves());

        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = Seen(service) + "\nlook: " + string.Join(" | ", look.Events);
        Assert.True(service.SessionCount == 2, seen);
        Assert.Equal(["q1:moved:s1:transient"], Moves());
        var second = service.Record("s2");
        Assert.Contains("\"adapter\":\"acp-other\"", second, StringComparison.Ordinal);
        Assert.Equal("Done", service.Status("q1"));
        // The part after was handed D189's brief by the second choice's agent, and the first choice's agent was not asked again.
        var prompts = Prompts();
        Assert.Equal(4, prompts.Length);
        Assert.True(
            prompts[^1]["blocks"]!.ToJsonString().Contains("the service it ran on did not answer", StringComparison.Ordinal),
            prompts[^1]["blocks"]!.ToJsonString()[..Math.Min(1500, prompts[^1]["blocks"]!.ToJsonString().Length)]);
    }

    [Fact]
    public async Task An_ask_holds_the_quest_for_the_person_typed_as_the_policys_and_one_yes_releases_one_move()
    {
        await using var service = SaidStandIn.Start(_repository);
        var clock = new MovedClock();
        var driver = Protocol(service, clock, Moving.Replace("\"do\":\"next\"", "\"do\":\"ask\"", StringComparison.Ordinal));

        var tick = driver.RunOnceAsync();
        await PassAsync(clock, tick, 1, 2);
        await tick.WaitAsync(Poll.Patience);

        // Looks start nothing, and the quest's verdict is the policy's hold, answered by Try again's door.
        IReadOnlyList<Consideration> verdicts = [];
        for (var look = 0; look < 2; look++)
        {
            var held = await driver.RunOnceAsync().WaitAsync(Poll.Patience);
            Assert.True(service.SessionCount == 1, Seen(service) + string.Join(" | ", held.Events));
            verdicts = held.Considerations;
        }

        var verdict = Assert.Single(verdicts, each => each.Quest.Id == "q1");
        Assert.Equal(StartVerdict.PolicyHeld, verdict.Verdict);
        Assert.Equal(SittingFact.PolicyAsk, verdict.Said!.Code);
        var decision = QuestRetry.Decide("q1", verdicts);
        Assert.Equal((RetryDecision.Approved, "build"), (decision.Did, decision.Approve));
        Assert.Empty(Moves());

        // The yes is one approval: the next look moves the line once.
        Assert.True(new StaffMoves(_home).Approve("q1", "build"));
        var released = driver.RunOnceAsync();
        await Poll.Until(() => released.IsCompleted, () => $"the look after the yes never ended\n{Seen(service)}", TimeSpan.FromSeconds(30));
        Assert.True(service.SessionCount == 2, Seen(service));
        Assert.Equal(["q1:approved::", "q1:moved:s1:transient"], Moves());
    }

    [Fact]
    public async Task A_role_with_no_policy_keeps_its_five_tries_and_its_hold_for_the_person()
    {
        await using var service = SaidStandIn.Start(_repository);
        var clock = new MovedClock();
        var driver = Protocol(service, clock, policy: null);

        var tick = driver.RunOnceAsync();
        await PassAsync(clock, tick, 1, 2, 4, 8, 16);
        await tick.WaitAsync(Poll.Patience);

        Assert.Equal(6, Prompts().Length);
        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.True(service.SessionCount == 1, Seen(service) + string.Join(" | ", look.Events));
        Assert.Empty(Moves());
    }

    /// <summary>The stand-in: <c>overloaded</c> takes its quest and fails every prompt with the 529; <c>serves</c> takes it and finishes it.</summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { appendFileSync } from 'node:fs';
            const [log, mode] = process.argv.slice(2);
            const url = process.env.DAORIS_SERVICE_URL;
            const session = process.env.DAORIS_SESSION_ID;
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const heard = (what) => appendFileSync(log, JSON.stringify({ ...what, session, mode }) + '\n');
            const respond = async (body) => {
              for (let attempt = 1; ; attempt++) {
                try {
                  return await fetch(`${url}/api/quests/q1/respond`, {
                    method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
                } catch (error) {
                  if (attempt >= 100) { heard({ method: 'stand-in-failed', what: body.action, error: String(error) }); throw error; }
                  await new Promise((done) => setTimeout(done, 50));
                }
              }
            };
            const overloaded = 'API Error: 529 Overloaded. This is a server-side issue, usually temporary — try again in a moment. If it persists, check https://status.claude.com.';
            let conversation = null;
            let prompts = 0;
            const say = (text) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
              update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text } } } });
            const lines = createInterface({ input: process.stdin });
            lines.on('line', (line) => handle(line).catch((error) => heard({ method: 'stand-in-failed', what: 'frame', error: String(error) })));
            const handle = async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1,
                  agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                conversation = 'conv-' + session;
                heard({ method: 'session/new', conversation });
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/prompt') {
                prompts++;
                const blocks = (frame.params?.prompt ?? []).filter((block) => block.type === 'text').map((block) => block.text);
                heard({ method: 'session/prompt', conversation: frame.params?.sessionId, blocks });
                if (prompts === 1) await respond({ action: 'take' });
                if (mode === 'overloaded') {
                  say(overloaded);
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: 'Internal error: ' + overloaded, data: { errorKind: 'overloaded' } } });
                  return;
                }
                say('Served.');
                await respond({ action: 'done', reason: 'served the report' });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            };
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }
}
