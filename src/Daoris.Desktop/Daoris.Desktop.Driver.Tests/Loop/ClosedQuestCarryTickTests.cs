using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;
using SaidStandIn = Daoris.Desktop.Driver.Tests.SessionMessagesTickTests.SaidStandIn;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LANDBACK1 part 3 (b), through REAL ticks on the protocol door. A session closed its quest done and was left clean; the person then
/// said a word to it, and the resumed run (a went-on run, D83, whose quest stays as it closed) left a file uncommitted. It is carried on
/// once in Daoris's own words, as a start's turn end is carried on twice, and no Confirm card is made. Daoris commits nothing: any commit in
/// the tree is the stand-in's.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class ClosedQuestCarryTickTests : IDisposable
{
    private const string Words = "Your quest is done, but your tree has 1 uncommitted path(s).";

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "closed-carry-tick", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public ClosedQuestCarryTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git(_repository, "init", "-q", "-b", "main");
        Git(_repository, "config", "user.email", "closed-carry@example.com");
        Git(_repository, "config", "user.name", "LANDBACK1");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git(_repository, "add", "-A");
        Git(_repository, "commit", "-qm", "the starting point");
    }

    public void Dispose() => GitSeed.Discard(_home);

    private string Log => Path.Combine(_home, "agent.log");

    private JsonObject[] Heard() => [.. StubFile.Lines(Log).Select(line => JsonNode.Parse(line)!.AsObject())];

    private IReadOnlyList<SessionEvent> Events(string session) => new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After(session, 0).Events;

    /// <summary>The prompts the resumed conversation was given, in order: the person's word, then each carry-on.</summary>
    private IReadOnlyList<string> ResumedPrompts() => [.. Heard()
        .Where(line => line["method"]!.GetValue<string>() == "session/prompt" && line["resumed"]!.GetValue<bool>())
        .Select(line => string.Join("\n", line["blocks"]!.AsArray().Select(block => block!.GetValue<string>())))];

    private int Notes(NoteCode code) => Events("s1").Count(e => e.Parts?.Any(part => part.Code == code.Code) == true);

    /// <summary>A went-on run that leaves a file uncommitted is carried on once, commits, and the session reads completed with a clean tree.</summary>
    [Fact]
    public async Task A_closed_quests_resumed_run_that_left_its_tree_dirty_is_carried_on_once_and_commits()
    {
        await using var service = SaidStandIn.Start(_repository);
        var driver = Driver(service, "commits");
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal(("completed", "Done"), (service.State("s1"), service.Status("q1")));
        Assert.Empty(ResumedPrompts());

        Assert.Equal("w1", await service.SayAsync("s1", "Also add the extra file."));
        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}\nheard: {string.Join(" | ", Heard().Select(h => h.ToJsonString()))}";
        Assert.True((service.State("s1"), service.Status("q1")) == ("completed", "Done"), seen);
        var prompts = ResumedPrompts();
        Assert.True(prompts.Count == 2, seen);
        Assert.Equal("Also add the extra file.", prompts[0]);
        Assert.StartsWith(Words, prompts[1], StringComparison.Ordinal);
        Assert.Contains("Daoris never runs `git add` or `git commit` for you", prompts[1], StringComparison.Ordinal);
        Assert.Equal(1, Notes(NoteCodes.BackgroundCarriedOn));
        Assert.Equal(0, Notes(NoteCodes.BackgroundCarryBound));

        // The tree is clean, and the commit is the stand-in's: Daoris committed nothing, and no card was made.
        var tree = JsonNode.Parse(service.Record("s1"))!["tree"]!.GetValue<string>();
        Assert.Equal("", Git(tree, "status", "--porcelain").Trim());
        Assert.Equal("stand-in agent", Git(tree, "log", "-1", "--format=%an").Trim());
        Assert.DoesNotContain(Events("s1"), e => e.Text?.Contains("Confirm", StringComparison.Ordinal) == true);
        Assert.Null(JsonNode.Parse(service.Record("s1"))!["awaiting"]);
    }

    /// <summary>A went-on run that never commits is carried on once, not twice: the bound is said and the record reads completed, dirty.</summary>
    [Fact]
    public async Task A_closed_quests_resumed_run_that_never_commits_is_carried_on_once_then_ends_with_the_bound_said()
    {
        await using var service = SaidStandIn.Start(_repository);
        var driver = Driver(service, "never");
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        Assert.Equal("w1", await service.SayAsync("s1", "Also add the extra file."));
        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}\nheard: {string.Join(" | ", Heard().Select(h => h.ToJsonString()))}";
        Assert.True((service.State("s1"), service.Status("q1")) == ("completed", "Done"), seen);
        var prompts = ResumedPrompts();
        Assert.True(prompts.Count == 2, seen);
        Assert.StartsWith(Words, prompts[1], StringComparison.Ordinal);
        Assert.Equal(1, Notes(NoteCodes.BackgroundCarriedOn));
        Assert.Equal(1, Notes(NoteCodes.BackgroundCarryBound));
        var tree = JsonNode.Parse(service.Record("s1"))!["tree"]!.GetValue<string>();
        Assert.Equal("?? extra.txt", Git(tree, "status", "--porcelain").Trim());
    }

    /// <summary>A went-on run that left its tree clean is carried on for nothing: its one prompt is the person's.</summary>
    [Fact]
    public async Task A_closed_quests_resumed_run_that_left_its_tree_clean_is_not_carried_on()
    {
        await using var service = SaidStandIn.Start(_repository);
        var driver = Driver(service, "clean");
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        Assert.Equal("w1", await service.SayAsync("s1", "Just tell me the port."));
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        Assert.Equal(["Just tell me the port."], ResumedPrompts());
        Assert.Equal(0, Notes(NoteCodes.BackgroundCarriedOn));
        Assert.Equal(("completed", "Done"), (service.State("s1"), service.Status("q1")));
    }

    private Daoris.Driver.Driver Driver(SaidStandIn service, string mode)
    {
        var adapters = AdapterSet.Built();
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "acp-stub",
            TestBound = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Log, mode] },
        };
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>
    /// The protocol door's stand-in: its first prompt takes the quest and closes it done, leaving its tree clean. In a resumed conversation
    /// its first prompt (the person's word) leaves <c>extra.txt</c> uncommitted, except in mode <c>clean</c>; each later prompt is a
    /// carry-on, which commits as the person's session would by its repository's conventions, except in mode <c>never</c>.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { appendFileSync, writeFileSync } from 'node:fs';
            import { execFileSync } from 'node:child_process';
            const [log, mode] = process.argv.slice(2);
            const url = process.env.DAORIS_SERVICE_URL;
            const session = process.env.DAORIS_SESSION_ID;
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const heard = (what) => appendFileSync(log, JSON.stringify({ ...what, session, cwd: process.cwd() }) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            const commit = () => {
              const env = { ...process.env, GIT_AUTHOR_NAME: 'stand-in agent', GIT_AUTHOR_EMAIL: 'agent@example.com' };
              execFileSync('git', ['add', '-A'], { cwd: process.cwd(), env });
              execFileSync('git', ['commit', '-qm', 'the extra file'], { cwd: process.cwd(), env });
            };
            let conversation = null;
            let resumed = false;
            let resumedPrompts = 0;
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
                  resumedPrompts += 1;
                  if (resumedPrompts === 1) {
                    if (mode !== 'clean') writeFileSync('extra.txt', 'the extra file\n');
                    say('Added it.');
                  } else {
                    if (mode !== 'never') commit();
                    say('Committed.');
                  }
                } else {
                  await respond({ action: 'take' });
                  await respond({ action: 'done', reason: 'served the report' });
                  say('Served.');
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

    private static string Git(string folder, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = folder, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}
