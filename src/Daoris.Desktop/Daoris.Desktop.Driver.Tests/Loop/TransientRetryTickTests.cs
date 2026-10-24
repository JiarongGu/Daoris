using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.SessionMessagesTickTests;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTERRORS1a (D210, the agent-errors design §5.1, §11's row) through REAL ticks: a driven session whose prompt fails with the 529 is
/// tried again in the SAME conversation inside its run, on the driver's retry clock, which the test holds
/// (<c>.claude/knowledge/held-clocks.md</c>); one that keeps failing ends <c>ended.transient</c>, no strike and no cool-off, and the planner
/// holds its quest across looks until a word of the person's said after the end resumes the same conversation with their words alone.
/// </summary>
/// <remarks>
/// The stand-in agent is a script this class writes under its own scratch home, never an agent's binary (<c>node tools/live-logins.mjs</c>),
/// and its 529 frame is replayed from <c>claude-agent-acp</c> 0.84.0's source (<c>acp-agent.js:4178-4180</c>, <c>:7249-7251</c>):
/// source-derived (§3.3). The service is MSG1b's stand-in, <see cref="SaidStandIn"/>.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class TransientRetryTickTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-transient-tick-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public TransientRetryTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "transient@example.com");
        Git("config", "user.name", "AGENTERRORS1a");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Log => Path.Combine(_home, "agent.log");

    private JsonObject[] Heard() => File.Exists(Log) ? [.. StubFile.Lines(Log).Select(line => JsonNode.Parse(line)!.AsObject())] : [];

    private JsonObject[] Prompts() => [.. Heard().Where(line => line["method"]!.GetValue<string>() == "session/prompt")];

    private IReadOnlyList<SessionEvent> Events(string session) => new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After(session, 0).Events;

    private Daoris.Driver.Driver Protocol(SaidStandIn service, string mode, MovedClock clock, Func<DriverConfig, DriverConfig>? change = null)
    {
        var adapters = AdapterSet.Built();
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "acp-stub",
            TestBound = 10,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Log, mode] },
        };
        config = change?.Invoke(config) ?? config;
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            RetryClock = clock,
        };
    }

    /// <summary>Passes each wait the run makes on the held clock, once it waits: <paramref name="minutes"/>, one wait at a time.</summary>
    private static async Task PassAsync(MovedClock clock, Task tick, params int[] minutes)
    {
        foreach (var each in minutes)
        {
            await Poll.Until(() => clock.Waiting || tick.IsCompleted, () => "the run never waited to try again", Poll.Patience);
            Assert.False(tick.IsCompleted, "the tick ended where its run should have waited");
            clock.Advance(TimeSpan.FromMinutes(each));
        }
    }

    private string Seen(SaidStandIn service, object? look = null) =>
        $"look: {look}\nrecord: {service.Record("s1")}\nheard: {string.Join(" | ", Heard().Select(h => h.ToJsonString()))}";

    /// <summary>
    /// 🔴 Shape 1 (the design §0): the prompt fails <c>-32603</c> with <c>data.errorKind: "overloaded"</c> twice, then the agent works.
    /// One record, one session id, no carry-on: the run goes on in the same conversation and ends as its work does, with one
    /// <c>agent.transient-retrying</c> line, no strike and no <c>cooling.json</c> entry. Red before the retry was handed, as today's
    /// <c>ended.turn-failed-taken</c>.
    /// </summary>
    [Fact]
    public async Task A_driven_session_failing_transient_twice_goes_on_in_the_same_conversation_and_ends_as_its_work_does()
    {
        await using var service = SaidStandIn.Start(_repository);
        var clock = new MovedClock();
        var driver = Protocol(service, "twice", clock);

        var tick = driver.RunOnceAsync();
        await PassAsync(clock, tick, 1, 2);
        var look = await tick.WaitAsync(Poll.Patience);

        var seen = Seen(service, string.Join(" | ", look.Events));
        Assert.True(service.State("s1") == "completed", seen);
        Assert.Equal("Done", service.Status("q1"));
        Assert.True(service.SessionCount == 1, seen);
        Assert.Single(Heard(), line => line["method"]!.GetValue<string>() == "session/new");
        var prompts = Prompts();
        Assert.Equal(3, prompts.Length);
        Assert.All(prompts, prompt => Assert.Equal("conv-s1", prompt["conversation"]!.GetValue<string>()));
        Assert.All(prompts.Skip(1), prompt => Assert.StartsWith(
            "The last request failed on the provider's side (Internal error: API Error: 529 Overloaded.",
            Assert.Single(prompt["blocks"]!.AsArray())!.GetValue<string>()));
        Assert.Single(Events("s1"), e => e.Parts?.Any(part => part.Code == NoteCodes.AgentTransientRetrying.Code) == true);
        Assert.False(File.Exists(AccountCooling.PathOf(_home)) && AccountCooling.Read(_home, DateTimeOffset.UtcNow).Count > 0, "an outage cooled an account");
        // AGENTERRORS1a2: the record's quiet events the head reads, each wait with when it ends and each try by its number, in order.
        Assert.Equal(
            ["wait", "try:1", "wait", "try:2"],
            Events("s1").Where(e => e.Kind == SessionEventKind.Transient)
                .Select(e => e.Try is { } number ? $"try:{number}" : e.Next is not null ? "wait" : "neither"));
    }

    /// <summary>
    /// 🔴 The same agent erring every try: past the bound the record ends <c>failed</c> with <c>ended.transient</c>, no strike and no
    /// cool-off, after five tries and 31 minutes of waits. Then the next looks start nothing for the Taken quest, a word of Daoris's kept
    /// on the record releasing nothing, until the person's word, said after the end, resumes the SAME conversation id by MSG1b's go-on
    /// with their words alone and no go-on after them.
    /// </summary>
    [Fact]
    public async Task Past_the_bound_the_quest_is_held_across_looks_until_the_person_s_word_resumes_the_same_conversation()
    {
        await using var service = SaidStandIn.Start(_repository);
        var clock = new MovedClock();
        var driver = Protocol(service, "always", clock);

        var tick = driver.RunOnceAsync();
        await PassAsync(clock, tick, 1, 2, 4, 8, 16);
        await tick.WaitAsync(Poll.Patience);

        var seen = Seen(service);
        Assert.True(service.State("s1") == "failed", seen);
        Assert.Contains("kept failing after 5 tries, with 31 minutes of waits", service.Note("s1"));
        Assert.Equal(6, Prompts().Length);
        Assert.True(service.Said("s1").Count == 0, seen);
        Assert.False(File.Exists(AccountCooling.PathOf(_home)) && AccountCooling.Read(_home, DateTimeOffset.UtcNow).Count > 0, "an outage cooled an account");

        // The next looks start nothing, and a word of Daoris's on the record is no release.
        await new ServiceClient(service.Url, null).SayAsync("s1", "Daoris's word.", from: DaorisWords.From, key: "test/daoris");
        for (var look = 0; look < 3; look++)
        {
            var report = await driver.RunOnceAsync().WaitAsync(Poll.Patience);
            Assert.True(service.SessionCount == 1, Seen(service, string.Join(" | ", report.Events)));
            Assert.True(service.State("s1") == "failed", Seen(service, string.Join(" | ", report.Events)));
        }

        Assert.Equal(6, Prompts().Length);
        Assert.Equal(0, ServiceClient.ReadStrikes(await SessionsJsonAsync(service)).GetValueOrDefault("q1"));

        // The person's word, said after the end, resumes the same conversation with their words alone.
        await service.SayAsync("s1", "Go on where the overload stopped you.");
        var resumed = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var after = Seen(service, string.Join(" | ", resumed.Events));
        Assert.True(service.SessionCount == 1, after);
        Assert.Contains(Heard(), line => line["method"]!.GetValue<string>() == "session/resume" && line["conversation"]!.GetValue<string>() == "conv-s1");
        var last = Prompts()[^1];
        Assert.True(last["resumed"]!.GetValue<bool>(), after);
        // Every word waiting goes with the person's release, once, and Daoris's word with it (the hold's word rule).
        Assert.Equal(
            new[] { "Daoris's word.", "Go on where the overload stopped you." },
            last["blocks"]!.AsArray().Select(block => block!.GetValue<string>()).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(last["blocks"]!.AsArray(), block => block!.GetValue<string>().StartsWith("The last request failed", StringComparison.Ordinal));
    }

    /// <summary>
    /// An Open quest held the same way (§5.1, round 3, R2-O4): its first run failed past the bound before it took the quest, and the
    /// next looks never start it afresh, the strikes never count it, until the person's word.
    /// </summary>
    [Fact]
    public async Task An_open_quest_past_the_bound_is_never_started_afresh()
    {
        await using var service = SaidStandIn.Start(_repository);
        var clock = new MovedClock();
        var driver = Protocol(service, "always-open", clock);

        var tick = driver.RunOnceAsync();
        await PassAsync(clock, tick, 1, 2, 4, 8, 16);
        await tick.WaitAsync(Poll.Patience);

        Assert.True(service.State("s1") == "failed", Seen(service));
        Assert.Equal("Open", service.Status("q1"));
        for (var look = 0; look < 3; look++)
        {
            var report = await driver.RunOnceAsync().WaitAsync(Poll.Patience);
            Assert.True(service.SessionCount == 1, Seen(service, string.Join(" | ", report.Events)));
        }

        Assert.Single(Heard(), line => line["method"]!.GetValue<string>() == "session/new");
    }

    /// <summary>
    /// 🔴 AGENTERRORS1a2, <b>the release</b> (the design §5.1, §11's row, moved from 1a by R3-O11): exhausted, then <i>Try again</i> once
    /// (<see cref="StandingChoices.Retry"/> over the look's verdicts) resumes the SAME conversation by D206's step 1 with Daoris's go-on
    /// alone; the resumed run, exhausted again, is held again by its new end, across several looks and a driver restart that reads the
    /// same release from its config: nothing starts. Red before the release, as <c>quest-not-held</c>.
    /// </summary>
    [Fact]
    public async Task Try_again_releases_one_end_alone_and_the_same_record_exhausted_again_is_held_across_looks_and_a_restart()
    {
        await using var service = SaidStandIn.Start(_repository);
        var clock = new MovedClock();
        var driver = Protocol(service, "relentless", clock);

        var tick = driver.RunOnceAsync();
        await PassAsync(clock, tick, 1, 2, 4, 8, 16);
        await tick.WaitAsync(Poll.Patience);
        Assert.True(service.State("s1") == "failed", Seen(service));
        Assert.Contains("your words, or Try again, go on in the same conversation", service.Note("s1"));

        var held = await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Contains(held.Considerations, c => c.Quest.Id == "q1" && c.Verdict == StartVerdict.TransientHeld);
        var pressed = StandingChoices.Retry("q1", held.Considerations);
        Assert.Equal((RetryDecision.Released, "s1"), (pressed.Did, pressed.Session));

        // Released: the same record resumes its own conversation, Daoris's go-on alone, and keeps failing past its bound again.
        var released = Protocol(service, "relentless", clock, pressed.Change);
        var resumed = released.RunOnceAsync();
        await PassAsync(clock, resumed, 1, 2, 4, 8, 16);
        await resumed.WaitAsync(Poll.Patience);

        var seen = Seen(service);
        Assert.True(service.SessionCount == 1, seen);
        Assert.True(service.State("s1") == "failed", seen);
        Assert.Single(Heard(), line => line["method"]!.GetValue<string>() == "session/new");
        Assert.Single(Heard(), line => line["method"]!.GetValue<string>() == "session/resume" && line["conversation"]!.GetValue<string>() == "conv-s1");
        var first = Prompts().First(prompt => prompt["resumed"]!.GetValue<bool>());
        Assert.StartsWith(
            "The last request failed on the provider's side (Internal error: API Error: 529 Overloaded.",
            Assert.Single(first["blocks"]!.AsArray())!.GetValue<string>());
        Assert.Equal(12, Prompts().Length);
        Assert.True(service.Said("s1").Count == 0, seen);

        // Held again by its new end: several looks, then a driver restart reading the same release, start nothing.
        for (var look = 0; look < 3; look++)
        {
            var report = await released.RunOnceAsync().WaitAsync(Poll.Patience);
            Assert.Contains(report.Considerations, c => c.Quest.Id == "q1" && c.Verdict == StartVerdict.TransientHeld);
        }

        var restarted = Protocol(service, "relentless", clock, pressed.Change);
        for (var look = 0; look < 3; look++)
        {
            var report = await restarted.RunOnceAsync().WaitAsync(Poll.Patience);
            Assert.True(service.SessionCount == 1, Seen(service, string.Join(" | ", report.Events)));
        }

        Assert.Equal(12, Prompts().Length);
        Assert.Equal(0, ServiceClient.ReadStrikes(await SessionsJsonAsync(service)).GetValueOrDefault("q1"));
    }

    /// <summary>
    /// 🔴 AGENTERRORS1a2: an <b>Open quest with no commits</b>, released by <i>Try again</i>, resumes its original conversation id in its
    /// own record (<c>TransientReleasedFrom</c> ahead of <c>RetriedFrom</c>'s commit rule): no fresh tree, no new session, and no replay
    /// of its target, only Daoris's go-on.
    /// </summary>
    [Fact]
    public async Task An_open_quest_with_no_commits_released_by_try_again_resumes_its_original_conversation()
    {
        await using var service = SaidStandIn.Start(_repository);
        var clock = new MovedClock();
        var driver = Protocol(service, "always-open", clock);

        var tick = driver.RunOnceAsync();
        await PassAsync(clock, tick, 1, 2, 4, 8, 16);
        await tick.WaitAsync(Poll.Patience);
        Assert.Equal("Open", service.Status("q1"));
        var target = Assert.Single(Prompts()[0]["blocks"]!.AsArray())!.GetValue<string>();

        var held = await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        var pressed = StandingChoices.Retry("q1", held.Considerations);
        var report = await Protocol(service, "always-open", clock, pressed.Change).RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = Seen(service, string.Join(" | ", report.Events));
        Assert.True(service.SessionCount == 1, seen);
        Assert.Single(Heard(), line => line["method"]!.GetValue<string>() == "session/new");
        Assert.Contains(Heard(), line => line["method"]!.GetValue<string>() == "session/resume" && line["conversation"]!.GetValue<string>() == "conv-s1");
        var last = Prompts()[^1];
        Assert.True(last["resumed"]!.GetValue<bool>(), seen);
        var blocks = last["blocks"]!.AsArray().Select(block => block!.GetValue<string>()).ToList();
        Assert.StartsWith("The last request failed on the provider's side", Assert.Single(blocks));
        Assert.DoesNotContain(target, blocks);
    }

    /// <summary>
    /// 🔴 AGENTERRORS1a2, the open's <b>nothing prompted</b> end (the design §5.1, §5.6; round 3, R2-O2; round 3's read, R3-O9): the person's
    /// word resumes the held record, and its <c>session/resume</c> meets the 529 past the bound, so none of their words went. The record
    /// ends <c>ended.transient</c> again, no word of Daoris's waits and no mark is written, and the next looks start nothing, since the
    /// carried word was said before this end; a new word of theirs, said after it, goes on with the carried one, alone.
    /// </summary>
    [Fact]
    public async Task A_resume_whose_open_fails_past_the_bound_holds_its_carried_words_until_a_new_word_of_theirs()
    {
        await using var service = SaidStandIn.Start(_repository);
        var clock = new MovedClock();
        var driver = Protocol(service, "open-fails", clock);

        var tick = driver.RunOnceAsync();
        await PassAsync(clock, tick, 1, 2, 4, 8, 16);
        await tick.WaitAsync(Poll.Patience);
        Assert.True(service.State("s1") == "failed", Seen(service));

        await service.SayAsync("s1", "Go on where the overload stopped you.");
        var resuming = driver.RunOnceAsync();
        await PassAsync(clock, resuming, 1, 2, 4, 8, 16);
        await resuming.WaitAsync(Poll.Patience);

        var seen = Seen(service);
        Assert.True(service.State("s1") == "failed", seen);
        Assert.Contains("kept failing after 5 tries", service.Note("s1"));
        Assert.Equal(6, Heard().Count(line => line["method"]!.GetValue<string>() == "session/resume"));
        Assert.Equal(6, Prompts().Length);
        // Only the person's carried word waits, unmarked: nothing of Daoris's.
        Assert.Single(service.Said("s1"));

        for (var look = 0; look < 3; look++)
        {
            var report = await driver.RunOnceAsync().WaitAsync(Poll.Patience);
            Assert.True(service.SessionCount == 1, Seen(service, string.Join(" | ", report.Events)));
            Assert.Equal(6, Heard().Count(line => line["method"]!.GetValue<string>() == "session/resume"));
        }

        await service.SayAsync("s1", "The provider is back; go on.");
        var resumed = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var after = Seen(service, string.Join(" | ", resumed.Events));
        Assert.True(service.SessionCount == 1, after);
        var last = Prompts()[^1];
        Assert.True(last["resumed"]!.GetValue<bool>(), after);
        Assert.Equal(
            ["Go on where the overload stopped you.", "The provider is back; go on."],
            last["blocks"]!.AsArray().Select(block => block!.GetValue<string>()));
    }

    private static async Task<string> SessionsJsonAsync(SaidStandIn service)
    {
        using var http = new HttpClient();
        return await http.GetStringAsync($"{service.Url}/api/sessions?includeClosed=true");
    }

    /// <summary>
    /// The protocol door's stand-in: it names its conversation <c>conv-&lt;session&gt;</c>. <c>twice</c> takes its quest and fails its
    /// first two prompts with the 529, streaming the error as its agent's words first as the adapter does, then closes the quest done;
    /// <c>always</c> fails every prompt; <c>always-open</c> too, never taking its quest. A resumed prompt is logged and closes the quest,
    /// but under <c>relentless</c>, which takes its quest and fails every prompt, a resumed one included. <c>open-fails</c> is
    /// <c>always</c> whose first six resumes meet the 529 at the open, before any prompt, and whose seventh works.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            import { appendFileSync, readFileSync } from 'node:fs';
            const [log, mode] = process.argv.slice(2);
            const url = process.env.DAORIS_SERVICE_URL;
            const session = process.env.DAORIS_SESSION_ID;
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const heard = (what) => appendFileSync(log, JSON.stringify({ ...what, session }) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            const overloaded = 'API Error: 529 Overloaded. This is a server-side issue, usually temporary — try again in a moment. If it persists, check https://status.claude.com.';
            let conversation = null;
            let resumed = false;
            let prompts = 0;
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
                // open-fails: the first six resumes heard meet the 529 at the open, before any prompt (source-derived).
                if (mode === 'open-fails' && readFileSync(log, 'utf8').split(/\r?\n/).filter((l) => l.includes('"session/resume"')).length <= 6) {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: 'Internal error: ' + overloaded, data: { errorKind: 'overloaded' } } });
                  return;
                }
                conversation = frame.params.sessionId;
                resumed = true;
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.method === 'session/prompt') {
                prompts++;
                const blocks = (frame.params?.prompt ?? []).filter((block) => block.type === 'text').map((block) => block.text);
                heard({ method: 'session/prompt', conversation: frame.params?.sessionId, blocks, resumed });
                if (resumed && mode !== 'relentless') {
                  say('Went on where it stopped.');
                  await respond({ action: 'done', reason: 'went on' });
                } else {
                  if (prompts === 1 && !resumed && mode !== 'always-open') await respond({ action: 'take' });
                  if (mode !== 'twice' || prompts <= 2) {
                    // claude-agent-acp streams the error as its agent's words, then answers the prompt with it (source-derived).
                    say(overloaded);
                    send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: 'Internal error: ' + overloaded, data: { errorKind: 'overloaded' } } });
                    return;
                  }
                  say('Served.');
                  await respond({ action: 'done', reason: 'served the report' });
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
