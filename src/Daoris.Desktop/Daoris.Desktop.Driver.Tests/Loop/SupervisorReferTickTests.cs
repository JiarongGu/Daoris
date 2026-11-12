using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Daoris.Driver;
using SaidStandIn = Daoris.Desktop.Driver.Tests.SessionMessagesTickTests.SaidStandIn;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The supervisor's referred point, through REAL ticks: a driven quest whose stand-in agent's turns end failed twice, the failures
/// rule's bound, which the machine's rules <c>refer</c>. The quest parks as <c>hold</c> parks it, the rule's act is kept as <c>refer</c>
/// and read back as an open point, the supervisor's go-on is kept through the decision store and held for its window, and the look after
/// the plan releases it as the quest's own <i>Try again</i>, so the next look starts the third session, which closes the quest.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Needs, Needed.Harness)]
public sealed class SupervisorReferTickTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "supervisor-refer-tick", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public SupervisorReferTickTests()
    {
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "refer@example.com");
        Git("config", "user.name", "refer");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private string Log => Path.Combine(_home, "refer-agent.log");

    private static readonly ActorChoice Sonnet = new("acp-stub", "sonnet", "low");

    private readonly ProviderRegistry _adapters = TestDoors.Registry(TestDoors.Shipped("stub", "acp-stub"));

    private DriverConfig Prepared()
    {
        var config = ActorChoices.With(
            DriverConfig.Empty with
            {
                Drivable = ["engine"],                Actors = StaffedRoles.On("acp-stub"),
                TestBound = 1,
                PollSeconds = 1,
                Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Log] },
            },
            "build", [Sonnet]);
        var file = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, """{ "adapter": "acp-stub", "actors": { "build": [ { "agent": "acp-stub", "model": "sonnet", "effort": "low" } ] } }""");
        Assert.Equal(StaffingImported.Done, new RoleStaffing(_home, () => StoreSettled.Yes).ImportFile().How);
        SeededRules.In(_home, strikes: 3);
        var store = new SupervisorRuleStore(_home, () => StoreSettled.Yes);
        var refer = new SupervisorRule("r-failures", SupervisorRules.Failures, SupervisorRules.Windows[SupervisorRules.Failures], 2, SupervisorRules.Refer, null, SupervisorRules.Machine, null);
        IReadOnlyList<SupervisorRule> rules = [.. store.Of(SupervisorRules.Machine).Rules.Where(rule => rule.Count != SupervisorRules.Failures), refer];
        var written = store.Write(SupervisorRules.Machine, store.Of(SupervisorRules.Machine).Revision, rules, "test", SupervisorRuleDoor.World(_home));
        Assert.True(written.Stored, written.Refusal?.Code);
        return config;
    }

    /// <summary>One look's driver, as the watch builds it: the choices read at that look, the one decision store carried between looks.</summary>
    private Daoris.Driver.Driver Look(SaidStandIn service, DriverConfig config, QuestionSupervisorDecisions supervisor)
    {
        var driver = new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, _adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(_adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
        driver.Context.Supervisor = supervisor;
        return driver;
    }

    private sealed class Admits : IDecisionAdmission
    {
        public DecisionAdmitted Admit(string kind, RolePlace place, string? act) => new("g-1", null) { Window = TimeSpan.FromMinutes(1) };
    }

    private string Seeing(SaidStandIn service, TickReport look) =>
        $"look: {string.Join(" | ", look.Events)}\nsessions: {service.SessionCount}\nquest: {service.Status("q1")}\n"
        + $"considered: {string.Join(" | ", look.Considerations.Select(c => $"{c.Quest.Id} {c.Verdict} {c.Reason}"))}\n"
        + string.Join("\n", Enumerable.Range(1, service.SessionCount).Select(n => service.Record($"s{n}")));

    [Fact]
    public async Task A_quest_parked_by_a_failures_rule_that_refers_is_a_point_the_supervisors_go_on_releases_as_its_try_again()
    {
        await using var service = SaidStandIn.Start(_repository);
        var config = Prepared();
        // The window's admission stands in for a started terminal holding the supervisor, which the headless driver has none of.
        var supervisor = new QuestionSupervisorDecisions(_home, new Admits());
        var driver = Look(service, config, supervisor);

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.True(service.State("s1") == "failed" && service.State("s2") == "failed", $"{service.Record("s1")}\n{service.Record("s2")}");

        var parked = await driver.TickAsync().WaitAsync(Poll.Patience);
        var sitting = parked.Considerations.Single(c => c.Quest.Id == "q1");
        Assert.True(service.SessionCount == 2 && sitting.Verdict == StartVerdict.Exhausted, Seeing(service, parked));

        var acts = new SupervisorRuleActs(_home).Of("s2");
        var act = Assert.Single(acts);
        Assert.Equal((SupervisorRules.Failures, SupervisorRules.Refer, 2, "q1"), (act.Rule.Count, act.Rule.Do, act.N, act.Line));

        var record = new PriorSession("s2", "trees/default/engine/s-2", "failed") { Quest = "q1", Actor = "build", Adapter = "acp-stub", Said = [], Repository = "engine" };
        var points = ReferredPoints.For(_home, new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), new GoOnMarks(_home));
        Assert.Equal(act.Id, points.Open("s2", parked.Considerations)!.Id);

        var kept = await supervisor.CourseAsync(
            new SupervisorCourse("s2", act.Id, "g-1", SupervisorCourseActs.GoOn, [new SupervisorReason("Both failures were the account's.", "the quest's failed sessions in its records")], new SupervisorHolder(RoleHolders.Terminal, null)),
            points.PointOf(record, null, parked.Considerations), DateTimeOffset.UtcNow.AddMinutes(-2));
        Assert.True(kept.Kept, kept.Why);
        Assert.Equal(SupervisorState.Held, Assert.Single(supervisor.CoursesOf("s2")).State);

        var released = await driver.TickAsync().WaitAsync(Poll.Patience);
        Assert.Equal(SupervisorState.WentOn, Assert.Single(supervisor.CoursesOf("s2")).State);
        Assert.True(service.SessionCount == 2, Seeing(service, released));

        // The release wrote the quest's own mark to the person's choices, which the next look reads.
        var marked = DriverConfig.Load(DaorisHome.Entry(_home, HomeEntry.DriverConfig)).ForgivenAt("q1");
        Assert.Equal(2, marked);
        var look = await Look(service, config.WithForgiven("q1", marked), supervisor).RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.True(service.SessionCount == 3 && (service.State("s3"), service.Status("q1")) == ("completed", "Done"), Seeing(service, look));
        Assert.Contains(
            new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After("s2", 0).Events,
            e => e.Parts?.Any(part => part.Code == HeldNoteCodes.SupervisorRetried.Code) == true);
    }

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }

    /// <summary>The stand-in agent on the protocol door: each session takes the quest, the first two end their turn failed, the third closes it.</summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "refer-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            const url = process.env.DAORIS_SERVICE_URL;
            const session = process.env.DAORIS_SESSION_ID;
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            const current = { model: 'default', effort: 'default' };
            const options = () => [
              { id: 'model', name: 'Model', category: 'model', type: 'select', currentValue: current.model,
                options: ['default', 'sonnet'].map((value) => ({ value, name: value })) },
              { id: 'effort', name: 'Effort', category: 'thought_level', type: 'select', currentValue: current.effort,
                options: ['default', 'low'].map((value) => ({ value, name: value })) },
            ];
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1,
                  agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'conv-' + session, configOptions: options() } });
              } else if (frame.method === 'session/set_config_option') {
                current[frame.params.configId] = frame.params.value;
                send({ jsonrpc: '2.0', id: frame.id, result: { configOptions: options() } });
              } else if (frame.method === 'session/prompt') {
                await respond({ action: 'take' });
                if (session === 's1' || session === 's2') {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: 'Internal error: the build step crashed' } });
                } else {
                  await respond({ action: 'done', reason: 'finished' });
                  send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: 'conv-' + session,
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
