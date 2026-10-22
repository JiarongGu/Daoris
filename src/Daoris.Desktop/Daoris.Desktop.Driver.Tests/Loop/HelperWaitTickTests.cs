using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// HELPERWAIT1 (D163's and D188's notes; the owner's install, 2026-10-23) through REAL ticks on the protocol door. A driven session that
/// asks a helper and ends its turn so the helper can read is waiting on that helper, not on the person: its record ends <c>completed</c>
/// by <c>ended.awaits-helper</c>, the helper's answer is handed to it, and at the next look its own conversation goes on with the answer
/// as its next turn, with no press of the person's. A session the install parked so before the rule is handed its answer the same way.
/// </summary>
/// <remarks>
/// The stand-in service is <see cref="SessionMessagesTickTests.SaidStandIn"/>, which models the ledger's one move out of an ended state
/// and the helper's hand door; the helper's pass is the driver's seam, a stand-in that answers through the answer file, so the only real
/// process is the asker's stand-in agent.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class HelperWaitTickTests : IDisposable
{
    private const string AskId = "a1b2c3d4";

    private const string HelperId = "h1";

    private const string Answer = "The window is exclusive at src/report.ts:42.";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-helper-wait-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public HelperWaitTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "wait@example.com");
        Git("config", "user.name", "HELPERWAIT1");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Log => Path.Combine(_home, "agent.log");

    private HelperFiles Files => new(_home);

    private JsonObject[] Heard() => [.. StubFile.Lines(Log).Select(line => JsonNode.Parse(line)!.AsObject())];

    /// <summary>The look's driver: the rule for helpers on, and the helper's tree, agent, capture and pass handed in as stand-ins.</summary>
    private Daoris.Driver.Driver Driver(SessionMessagesTickTests.SaidStandIn service, string mode)
    {
        var adapters = AdapterSet.Built();
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "acp-stub",
            TimeoutMinutes = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>>
            {
                ["acp-stub"] = ["node", Agent(), Log, mode, DaorisHome.Entry(_home, HomeEntry.Helpers)],
            },
            Helpers = new Dictionary<string, HelperRule> { ["engine"] = new(HelperRules.On, []) },
        };
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            HelperTrees = (_, _) => Task.FromResult<(string?, string?)>(("1111111111111111111111111111111111111111", _repository)),
            HelperCaptures = (_, _) => Task.FromResult(new HelperCaptureRead(new HelperCaptured(
                "c0ffee123456", "1111111111111111111111111111111111111111", DateTimeOffset.UtcNow, [], [], []))),
            HelperChoices = (_, _, _, _) => Task.FromResult(new ReviewerChoice("codex-acp", "another-maker", null, "codex-acp reads it.")),
            HelperPasses = (ask, _, _) =>
            {
                // The helper reads and answers through `helper_answer`'s file, named BEFORE its turn as the real pass names it.
                ask.Opened?.Invoke(HelperId);
                File.WriteAllText(Files.PathOf(ask.Ask.Id, "answer"),
                    new JsonObject { ["id"] = ask.Ask.Id, ["helper"] = HelperId, ["answer"] = Answer, ["at"] = DateTimeOffset.UtcNow.ToString("O") }.ToJsonString());
                return Task.FromResult(new HelperPassRun(HelperId, $"completed  helper {HelperId}") { State = "completed" });
            },
        };
    }

    /// <summary>
    /// 🔴 The owner's case: the session asks a helper and ends its turn. It reads as waiting on the helper, never on the person; the
    /// helper answers; the same record goes on in its own conversation with the helper's answer as its next prompt and closes its quest,
    /// and nothing of the person's was asked or pressed.
    /// </summary>
    [Fact]
    public async Task A_session_that_asked_a_helper_and_ended_its_turn_waits_on_it_and_goes_on_with_its_answer()
    {
        await using var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        var driver = Driver(service, "asks");

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        // The helper's record, as the host opens it for the pass the stand-in makes (after the asker's, which the stand-in numbers first).
        service.Helper(HelperId, "s1");
        var waiting = service.Record("s1");
        Assert.True(service.State("s1") == "completed", waiting);
        Assert.Contains("\"code\":\"ended.awaits-helper\"", waiting);
        Assert.Equal("Taken", service.Status("q1"));

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.True(Files.ReadState(AskId) is { State: HelperStates.Answered, Handed: not null }, service.Record("s1"));
        var (helper, text) = Assert.Single(service.HelperHands);
        Assert.Equal(HelperId, helper);
        Assert.Contains(Answer, text);

        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}\nstates: {string.Join(", ", service.States("s1"))}";
        Assert.True(service.Status("q1") == "Done", seen);
        Assert.True(service.States("s1").SequenceEqual(["starting", "working", "completed", "working", "completed"]), seen);
        Assert.DoesNotContain("awaiting-person", service.States("s1"));
        var resumed = Assert.Single(Heard(), line => line["method"]!.GetValue<string>() == "session/resume");
        Assert.Equal("conv-s1", resumed["conversation"]!.GetValue<string>());
        var prompt = Heard().Last(line => line["method"]!.GetValue<string>() == "session/prompt");
        Assert.True(prompt["resumed"]!.GetValue<bool>(), seen);
        Assert.Contains(prompt["blocks"]!.AsArray(), block => block!.GetValue<string>().Contains(Answer, StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 A session the install parked before the rule, on the clean-exit park, while its own helper read: once the ask is answered the
    /// next look completes the park as the wait it was and hands the answer, and the look after it goes on in its own conversation.
    /// </summary>
    [Fact]
    public async Task A_session_parked_on_its_own_helper_before_the_rule_is_handed_the_answer_and_goes_on()
    {
        await using var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        var driver = Driver(service, "parks");

        var asked = DateTimeOffset.UtcNow;
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.True(service.State("s1") == "awaiting-person", service.Record("s1"));
        service.Helper(HelperId, "s1");

        // What the install held: the agent's ask, made before its turn ended, answered by the helper that read it.
        WriteAsk(asked);
        Files.WriteState(new HelperState(AskId, HelperStates.Answered, null, HelperId, DateTimeOffset.UtcNow) { Agent = "codex-acp" });
        File.WriteAllText(Files.PathOf(AskId, "answer"),
            new JsonObject { ["id"] = AskId, ["helper"] = HelperId, ["answer"] = Answer, ["at"] = DateTimeOffset.UtcNow.ToString("O") }.ToJsonString());

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.True(service.State("s1") == "completed", service.Record("s1"));
        Assert.Contains("\"code\":\"ended.awaits-helper\"", service.Record("s1"));
        Assert.Single(service.HelperHands);

        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}\nstates: {string.Join(", ", service.States("s1"))}";
        Assert.True(service.Status("q1") == "Done", seen);
        Assert.True(service.States("s1").SequenceEqual(["starting", "working", "awaiting-person", "completed", "working", "completed"]), seen);
        var prompt = Heard().Last(line => line["method"]!.GetValue<string>() == "session/prompt");
        Assert.Contains(prompt["blocks"]!.AsArray(), block => block!.GetValue<string>().Contains(Answer, StringComparison.Ordinal));
    }

    /// <summary>The ask as <c>helper_ask</c> writes it, the agent's, made at <paramref name="at"/>.</summary>
    private void WriteAsk(DateTimeOffset at)
    {
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Helpers));
        File.WriteAllText(Files.PathOf(AskId, "ask"), new JsonObject
        {
            ["id"] = AskId, ["asker"] = "s1", ["question"] = "Why does the report drop its last day?", ["agent"] = null, ["askedBy"] = "agent",
            ["at"] = at.ToString("O"),
        }.ToJsonString());
    }

    /// <summary>
    /// The protocol door's stand-in: it names its conversation <c>conv-&lt;session&gt;</c> and honours <c>session/resume</c>. On its first
    /// prompt it takes its quest; <c>asks</c> then asks a helper, writing the ask as <c>helper_ask</c> does, and <c>parks</c> only says
    /// it waits; either ends its turn. A resumed prompt is logged with its blocks, and closes the quest.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, $$"""
            import { createInterface } from 'node:readline';
            import { appendFileSync, mkdirSync, writeFileSync } from 'node:fs';
            import { join } from 'node:path';
            const [log, mode, helpers] = process.argv.slice(2);
            const url = process.env.DAORIS_SERVICE_URL;
            const session = process.env.DAORIS_SESSION_ID;
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const heard = (what) => appendFileSync(log, JSON.stringify({ ...what, session }) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            let conversation = null;
            let resumed = false;
            const say = (text) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
              update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text } } } });
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1,
                  agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                conversation = 'conv-' + session;
                heard({ method: 'session/new', conversation });
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/resume' || frame.method === 'session/load') {
                heard({ method: frame.method, conversation: frame.params?.sessionId });
                conversation = frame.params.sessionId;
                resumed = true;
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.method === 'session/prompt') {
                const blocks = (frame.params?.prompt ?? []).filter((block) => block.type === 'text').map((block) => block.text);
                heard({ method: 'session/prompt', conversation: frame.params?.sessionId, blocks, resumed });
                if (resumed) {
                  await respond({ action: 'done', reason: 'fixed the window the helper found' });
                  say('Fixed the window, as the helper found.');
                } else {
                  await respond({ action: 'take' });
                  if (mode === 'asks') {
                    mkdirSync(helpers, { recursive: true });
                    writeFileSync(join(helpers, '{{AskId}}.ask.json'), JSON.stringify({
                      id: '{{AskId}}', asker: session, question: 'Why does the report drop its last day?', agent: null, askedBy: 'agent',
                      at: new Date().toISOString(),
                    }));
                  }
                  say('I asked a helper why the report drops its last day; I will go on when it answers.');
                }
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            // stdin closed: the ending. Never process.exit (STUB1): the process ends when the loop drains.
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
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
