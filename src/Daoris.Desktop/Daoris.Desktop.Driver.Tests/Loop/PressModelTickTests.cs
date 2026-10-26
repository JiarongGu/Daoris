using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PRESSMODEL1 (D163 point 1, D205 §3.5(a), §4.0, D206 point 7) through REAL ticks on the protocol door: the person's press to go on now
/// with another agent, taken while a driven line waits for an account's limit to reset, carries on with THAT agent's own choice, never the
/// part before's model. The install's case first: Build lists the first agent alone, the press names a second agent whose door refuses the
/// first one's model, so the part opens on the second agent's default and runs. Then a role that lists the second agent: its choice's
/// model and effort. Then a model the second agent refuses at the open, re-asked on its next choice and its default; and a line the
/// refusal already held, which goes on at the next look.
/// </summary>
/// <remarks>
/// <c>acp-stub</c> is the first agent (the stub's accounts <c>account-1</c> and <c>account-2</c>, whose first turn is refused by Claude
/// Code's limit sentence) and <c>acp-other</c> the second, on another tool's account (<c>account-x</c>), which offers <c>codex-mini</c> and
/// <c>default</c> alone and refuses any other model at the set with <c>Invalid params</c>, as Codex's door did on the install. Both are the
/// one node stand-in, which writes down each frame it hears. No model, no real account, no sign-in.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class PressModelTickTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Seen = new(2026, 10, 1, 14, 0, 0, TimeSpan.FromMinutes(345));

    /// <summary>The reset the stand-in's limit sentence names: 3 October, 16:02 in the test's zone.</summary>
    private static readonly DateTimeOffset Until = new(2026, 10, 3, 16, 2, 0, TimeSpan.FromMinutes(345));

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-press-model-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    private readonly DateTimeOffset _now = Seen;

    public PressModelTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "press@example.com");
        Git("config", "user.name", "PRESSMODEL1");
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

    private string Log => Path.Combine(_home, "press-model.log");

    private static readonly ActorChoice Sonnet = new("acp-stub", "sonnet", "low");

    /// <summary>The stand-ins' adapters: the built stub's protocol door, and a second door onto another tool's accounts.</summary>
    private static AdapterSet Adapters() => new(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
    {
        ["stub"] = new StubAdapter(),
        ["acp-stub"] = new AcpStubAdapter(),
        ["acp-other"] = new WalkAgentTickTests.AcpOtherAdapter(),
    });

    private DriverConfig Config(IReadOnlyList<ActorChoice> order) => ActorChoices.With(
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
                ["acp-stub"] = ["node", Agent(), Log, "stub"],
                ["acp-other"] = ["node", Agent(), Log, "other"],
            },
        },
        "build", order);

    private Daoris.Driver.Driver Driver(AskAndWaitTickTests.StandIn service, DriverConfig config)
    {
        var adapters = Adapters();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => _now, Zone = Zone });
    }

    private JsonObject[] Heard() => [.. StubFile.Lines(Log).Select(line => JsonNode.Parse(line)!.AsObject())];

    private string Seeing(AskAndWaitTickTests.StandIn service, TickReport look) =>
        $"look: {string.Join(" | ", look.Events)}\nsessions: {service.SessionCount}\n"
        + $"heard: {string.Join(" | ", Heard().Select(line => $"{line["tag"]}/{line["account"]} {line["method"]} {line["configId"]}={line["value"]}"))}";

    /// <summary>The first look: Build's first choice takes the quest on stub account-1, and its first turn is refused by the account's limit.</summary>
    private async Task<Daoris.Driver.Driver> LimitedAsync(AskAndWaitTickTests.StandIn service, DriverConfig config)
    {
        service.KeepsAdapter = true;
        // The take this machine holds, which the hand-over's judge reads for a taken quest.
        service.Claim = "held";
        var driver = Driver(service, config);
        // account-2 cools for days, so every account of the stub is at its limit once account-1's turn is refused.
        AccountCooling.Cool(_home, new CoolingEntry("stub", "account-2", Until.AddDays(5), true, "weekly", Seen, "s0"), Seen);
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal(("failed", "Taken"), (service.Session("s1")["state"]!.GetValue<string>(), service.Status("q1")));
        Assert.True(service.Session("s1")["limit"]?.GetValue<bool>());
        Assert.Equal("sonnet", service.Session("s1")["asked"]?["model"]?["value"]?.GetValue<string>());
        return driver;
    }

    /// <summary>The person's press in the wait: go on now with the second agent, which the next look takes as a hand-over.</summary>
    private void Press(string agent = "acp-other") =>
        new HandOverBox(_home).Write("s1", new HandOverRequest(agent, null, HandOverWhy.Press, HandOverBy.Person, _now));

    private static JsonObject Record(AskAndWaitTickTests.StandIn service, string id) => service.Session(id);

    /// <summary>The model and effort the second agent was asked to set, in the order it heard them.</summary>
    private List<(string Id, string Value)> OtherSet() =>
        [.. Heard().Where(line => line["tag"]!.GetValue<string>() == "other" && line["method"]!.GetValue<string>() == "session/set_config_option")
            .Select(line => (line["configId"]!.GetValue<string>(), line["value"]!.GetValue<string>()))];

    /// <summary>
    /// 🔴 The install's case: Build lists the first agent alone, every account of it is at its limit, and the person's press goes on now with
    /// the second agent. The part opens there on the agent's own default, no model asked, and runs; it is never asked `sonnet`, which the
    /// second agent refuses, so it never ends before its prompt.
    /// </summary>
    [Fact]
    public async Task A_press_to_another_agent_the_role_does_not_list_opens_on_that_agent_s_default_and_runs()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var driver = await LimitedAsync(service, Config([Sonnet]));

        Press();
        var seen = "";
        for (var look = 0; look < 3 && service.Status("q1") != "Done"; look++)
        {
            seen = Seeing(service, await driver.RunOnceAsync().WaitAsync(Poll.Patience));
        }

        Assert.True(service.Status("q1") == "Done", seen);
        Assert.True(service.SessionCount == 2, seen);
        var ran = Record(service, "s2");
        Assert.Equal(("completed", "acp-other"), (ran["state"]!.GetValue<string>(), ran["adapter"]!.GetValue<string>()));
        Assert.Equal("account-default", ran["asked"]?["model"]?["source"]?.GetValue<string>());
        Assert.Equal("acp-other", ran["asked"]?["agent"]?.GetValue<string>());
        Assert.Empty(OtherSet());
        Assert.Contains(Heard(), line => line["tag"]!.GetValue<string>() == "other" && line["method"]!.GetValue<string>() == "session/prompt");
        Assert.Empty(await new ServiceClient(service.Url, null).StrikesAsync());
    }

    /// <summary>
    /// 🔴 A role that lists the second agent after the first: the press asks that choice's own model and effort, never the part before's
    /// (`sonnet`, `low`), and the record says it ran as the person's hand-over named it.
    /// </summary>
    [Fact]
    public async Task A_press_to_an_agent_the_role_lists_later_asks_that_choice_s_model_and_effort()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var driver = await LimitedAsync(service, Config([Sonnet, new ActorChoice("acp-other", "codex-mini", "high")]));

        Press();
        var seen = "";
        for (var look = 0; look < 3 && service.Status("q1") != "Done"; look++)
        {
            seen = Seeing(service, await driver.RunOnceAsync().WaitAsync(Poll.Patience));
        }

        Assert.True(service.Status("q1") == "Done", seen);
        var ran = Record(service, "s2");
        Assert.Equal(("completed", "acp-other", "handed"), (ran["state"]!.GetValue<string>(), ran["adapter"]!.GetValue<string>(), ran["choice"]?.GetValue<string>()));
        Assert.Equal("codex-mini", ran["asked"]?["model"]?["value"]?.GetValue<string>());
        var set = OtherSet();
        Assert.Contains(("model", "codex-mini"), set);
        Assert.Contains(("effort", "high"), set);
        Assert.DoesNotContain(set, each => each.Value is "sonnet" or "low");
    }

    /// <summary>
    /// 🔴 A model the second agent refuses at the open is re-asked as a new start's is: the role's next choice of that agent, then the agent's
    /// own default, on the same session, never the record ending before its prompt while another could run.
    /// </summary>
    [Fact]
    public async Task A_model_the_pressed_agent_refuses_at_the_open_is_re_asked_on_its_next_choice()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var driver = await LimitedAsync(service, Config(
            [Sonnet, new ActorChoice("acp-other", "gone", "high"), new ActorChoice("acp-other", "codex-mini", "low")]));

        Press();
        var seen = "";
        for (var look = 0; look < 3 && service.Status("q1") != "Done"; look++)
        {
            seen = Seeing(service, await driver.RunOnceAsync().WaitAsync(Poll.Patience));
        }

        Assert.True(service.Status("q1") == "Done", seen);
        Assert.True(service.SessionCount == 2, seen);
        var ran = Record(service, "s2");
        Assert.Equal(("completed", "3"), (ran["state"]!.GetValue<string>(), ran["choice"]?.GetValue<string>()));
        Assert.Equal("codex-mini", ran["asked"]?["model"]?["value"]?.GetValue<string>());
        Assert.Equal([("model", "gone"), ("model", "codex-mini")], OtherSet().Where(each => each.Id == "model"));
    }

    /// <summary>
    /// 🔴 A role whose only choice of the second agent is refused: its own default runs on the same session, no model asked after the
    /// refusal, and the record is not left `choice-not-offered`.
    /// </summary>
    [Fact]
    public async Task A_model_the_pressed_agent_refuses_with_no_choice_after_it_runs_on_that_agent_s_default()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var driver = await LimitedAsync(service, Config([Sonnet, new ActorChoice("acp-other", "gone", "high")]));

        Press();
        var seen = "";
        for (var look = 0; look < 3 && service.Status("q1") != "Done"; look++)
        {
            seen = Seeing(service, await driver.RunOnceAsync().WaitAsync(Poll.Patience));
        }

        Assert.True(service.Status("q1") == "Done", seen);
        Assert.True(service.SessionCount == 2, seen);
        var ran = Record(service, "s2");
        Assert.Equal("completed", ran["state"]!.GetValue<string>());
        Assert.Equal("account-default", ran["asked"]?["model"]?["source"]?.GetValue<string>());
        Assert.Equal([("model", "gone")], OtherSet().Where(each => each.Id == "model"));
    }

    /// <summary>
    /// 🔴 The line the install holds: moved by a press that asked `sonnet`, whose part the second agent refused before any prompt
    /// (`choice-not-offered`), the refusal kept against its account for a day. At the next look it goes on with the agent's own default,
    /// no press, and runs.
    /// </summary>
    [Fact]
    public async Task A_line_a_press_moved_and_the_refusal_holds_goes_on_at_the_next_look_on_the_agent_s_default()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var driver = await LimitedAsync(service, Config([Sonnet]));

        // What the old build left: the press's hand-over of the line, a part on the second agent that carried the first's `sonnet` and
        // ended before any prompt, and the account's refusal of that model kept.
        new HandedQuests(_home).Write("q1", new HandedQuest("acp-other", null, "s1", HandOverWhy.Press, HandOverBy.Person, _now));
        service.Keep(new JsonObject
        {
            ["id"] = "s2", ["quest"] = "q1", ["repository"] = "engine", ["state"] = "failed", ["kind"] = "drive",
            ["profile"] = "account-x", ["adapter"] = "acp-other", ["created"] = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O"),
            ["choice"] = "handed", ["asked"] = service.Session("s1")["asked"]!.DeepClone(),
            ["noteParts"] = new JsonArray(new JsonObject
            {
                ["code"] = "ended.choice-not-offered",
                ["values"] = new JsonObject { ["agent"] = "acp-other", ["model"] = "sonnet" },
            }),
        });
        Assert.Null(AgentOffers.Refused(_home, "acp-other", null, "account-x", "sonnet", DateTimeOffset.UtcNow));

        var seen = "";
        for (var look = 0; look < 3 && service.Status("q1") != "Done"; look++)
        {
            seen = Seeing(service, await driver.RunOnceAsync().WaitAsync(Poll.Patience));
        }

        Assert.True(service.Status("q1") == "Done", seen);
        var ran = Record(service, "s3");
        Assert.Equal(("completed", "acp-other"), (ran["state"]!.GetValue<string>(), ran["adapter"]!.GetValue<string>()));
        Assert.Equal("account-default", ran["asked"]?["model"]?["source"]?.GetValue<string>());
        Assert.Empty(OtherSet());
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
    /// The protocol door's stand-in for both agents, <c>stub</c> or <c>other</c> by its argument. The stub offers Claude's aliases; the other
    /// offers <c>default</c> and <c>codex-mini</c> and answers the set of any other model with <c>Invalid params</c>. It writes down each
    /// frame with the agent it is. A first prompt on the stub takes the quest and is refused by observation 4's sentence; any other prompt
    /// closes it.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "press-model.mjs");
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
            const models = tag === 'stub' ? ['default', 'sonnet', 'opus'] : ['default', 'codex-mini'];
            const current = { model: 'default', effort: 'default' };
            const options = () => [
              { id: 'model', name: 'Model', category: 'model', type: 'select', currentValue: current.model,
                options: models.map((value) => ({ value, name: value })) },
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
                if (frame.params.configId === 'model' && !models.includes(frame.params.value)) {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32602, message: 'Invalid params' } });
                } else {
                  current[frame.params.configId] = frame.params.value;
                  send({ jsonrpc: '2.0', id: frame.id, result: { configOptions: options() } });
                }
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
}
