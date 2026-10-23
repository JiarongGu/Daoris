using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LIMITRESUME1b (D206; the limit-resume design §3.3) through REAL ticks on the protocol door: a driven line an account's limit cut off
/// resumes its own conversation once that account is ready again, decided inside the carry-on's own start, with Daoris's word kept on
/// the ended record and taken off at its conclusion; where its own conversation is refused, it goes on in the same start, briefed; and
/// a limited end is never a strike.
/// </summary>
/// <remarks>
/// The stand-in agent takes its quest and is refused at its first turn by an account's limit (observation 4's sentence, the zone the
/// test's); what a resumed or carried-on prompt meets is its mode's. The stand-in service is MSG1a's (<c>SaidStandIn</c>), which keeps
/// the ledger's limit flag and Daoris's words under their key. The clock and the zone are the roster's, the test's. The stub's own
/// sign-in is the account that cools.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class LimitResumeTickTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Seen = new(2026, 10, 1, 14, 0, 0, TimeSpan.FromMinutes(345));

    private static readonly DateTimeOffset Until = new(2026, 10, 3, 16, 2, 0, TimeSpan.FromMinutes(345));

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-limit-resume-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    private DateTimeOffset _now = Seen;

    public LimitResumeTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "limit@example.com");
        Git("config", "user.name", "LIMITRESUME1");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Log => Path.Combine(_home, "agent.log");

    private JsonObject[] Heard() => [.. StubFile.Lines(Log).Select(line => JsonNode.Parse(line)!.AsObject())];

    private IReadOnlyList<SessionEvent> Events(string session) =>
        new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After(session, 0).Events;

    private Daoris.Driver.Driver Driver(SessionMessagesTickTests.SaidStandIn service, string mode, int strikes = 0)
    {
        var adapters = AdapterSet.Built();
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "acp-stub",
            TestBound = 1,
            PollSeconds = 1,
            Strikes = strikes,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Log, mode] },
        };
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => _now, Zone = Zone });
    }

    /// <summary>The first look: the session takes its quest and its first turn is refused by the account's limit.</summary>
    private static async Task LimitedAsync(SessionMessagesTickTests.SaidStandIn service, Daoris.Driver.Driver driver)
    {
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal(("failed", "Taken"), (service.State("s1"), service.Status("q1")));
        Assert.Contains("\"limit\":true", service.Record("s1"), StringComparison.Ordinal);
    }

    /// <summary>
    /// 🔴 B1's case, the install's (the design §1, 9967271d): the own account frees in the same look as the carry-on is planned, and the
    /// line resumes its own conversation on it: the same record and conversation id, Daoris's word as its prompt and no brief, never a
    /// briefed part on it. The word comes off as the run concludes, the line says it resumed, and no strike is counted.
    /// </summary>
    [Fact]
    public async Task A_line_whose_own_account_freed_resumes_its_own_conversation_with_Daoris_s_word_and_no_brief()
    {
        await using var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        var driver = Driver(service, "resumes", strikes: 1);
        await LimitedAsync(service, driver);

        // Held while the account cools, with the line's own sentence, said on its record once: nothing spawned, no word kept.
        for (var held = 0; held < 2; held++)
        {
            var waiting = await driver.TickAsync().WaitAsync(Poll.Patience);
            var sitting = waiting.Considerations.Single(c => c.Quest.Id == "q1");
            Assert.Equal(StartVerdict.Blocked, sitting.Verdict);
            Assert.StartsWith("Every account it may run on is at its limit: it goes on by itself once `stub`'s own sign-in is ready again at Oct 3, 16:02", sitting.Reason);
        }

        Assert.Single(Events("s1"), e => e.Parts?.Any(part => part.Code == "held.limit-waits-own") == true);
        Assert.Equal(1, service.SessionCount);
        Assert.Empty(service.DaorisSaid);

        _now = Until.AddMinutes(1);
        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\nafter: {service.Record("s1")}\nheard: {string.Join("\n", Heard().Select(h => h.ToJsonString()))}";
        Assert.True(service.SessionCount == 1, seen);
        Assert.True((service.State("s1"), service.Status("q1")) == ("completed", "Done"), seen);
        var heard = Heard();
        Assert.Single(heard, line => line["method"]!.GetValue<string>() == "session/new");
        var resumed = Assert.Single(heard, line => line["method"]!.GetValue<string>() == "session/resume");
        Assert.Equal("conv-s1", resumed["conversation"]!.GetValue<string>());
        var prompt = heard.Last(line => line["method"]!.GetValue<string>() == "session/prompt");
        Assert.True(prompt["resumed"]!.GetValue<bool>(), seen);
        var blocks = string.Join("\n", prompt["blocks"]!.AsArray().Select(block => block!.GetValue<string>()));
        Assert.Contains(LimitResume.Words, blocks);
        Assert.DoesNotContain("carrying on quest", blocks);

        // One word, under the cool-off's key, and off the record once the run took it.
        var (wordOn, key) = Assert.Single(service.DaorisSaid);
        Assert.Equal("s1", wordOn);
        Assert.StartsWith("limit-reset/s1/", key);
        Assert.Empty(service.Said("s1"));
        Assert.Empty(await new ServiceClient(service.Url, null).StrikesAsync());
        Assert.Contains(look.Events, line => line.Contains(Daoris.Driver.Driver.LimitResumedLine, StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 Round 3's B8: a full resume refused <c>gone</c> takes Daoris's word off and goes on in the same start as the carry-on planned,
    /// a briefed part on the walk's account, said with the refusal's code; nothing is left on the record, so the next look spawns nothing
    /// for it, and no strike is counted.
    /// </summary>
    [Fact]
    public async Task A_full_resume_refused_gone_goes_on_briefed_in_the_same_start_and_leaves_no_word()
    {
        await using var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        var driver = Driver(service, "gone", strikes: 1);
        await LimitedAsync(service, driver);

        _now = Until.AddMinutes(1);
        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = $"look: {string.Join(" | ", look.Events)}\ns1: {service.Record("s1")}";
        Assert.True(service.SessionCount == 2, seen);
        Assert.True((service.State("s2"), service.Status("q1")) == ("completed", "Done"), seen);
        Assert.True(service.State("s1") == "failed", seen);
        var carried = Heard().Last(line => line["method"]!.GetValue<string>() == "session/prompt");
        Assert.Equal("conv-s2", carried["conversation"]!.GetValue<string>());
        Assert.Contains("carrying on quest", string.Join("\n", carried["blocks"]!.AsArray().Select(block => block!.GetValue<string>())));
        Assert.Empty(service.Said("s1"));
        Assert.Contains(Events("s1"), e => e.Parts?.Any(part => part.Code == "limit.resume-refused" && Equals(part.Value("why"), ContinueWhy.Gone)) == true);
        Assert.Empty(await new ServiceClient(service.Url, null).StrikesAsync());

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal(2, service.SessionCount);
    }

    /// <summary>
    /// Round 3's B8 for <c>elsewhere</c>: another client holds the conversation, so Daoris's word is kept and marked, the line held for the
    /// person, and no process spawned at the next look.
    /// </summary>
    [Fact]
    public async Task A_full_resume_refused_elsewhere_keeps_the_word_marked_and_holds_the_line_for_the_person()
    {
        await using var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        var driver = Driver(service, "elsewhere");
        await LimitedAsync(service, driver);

        _now = Until.AddMinutes(1);
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        Assert.Equal(1, service.SessionCount);
        var word = Assert.Single(service.Said("s1"));
        Assert.Equal(ContinueWhy.Elsewhere, new GoOnMarks(_home).Read("s1")!.Why);
        Assert.Contains(word, new GoOnMarks(_home).Read("s1")!.Said);

        var spawned = Heard().Length;
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal(1, service.SessionCount);
        Assert.Equal(spawned, Heard().Length);
    }

    /// <summary>
    /// TURNFAIL1 amended: two limited full resumes running leave the person's words waiting, unmarked, never <i>failed twice</i>; at most
    /// one Daoris word is ever on the record, and none once each run concluded; no strike is counted.
    /// </summary>
    [Fact]
    public async Task Two_limited_full_resumes_running_leave_the_words_waiting_and_no_word_of_Daoris_s()
    {
        await using var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        var driver = Driver(service, "limited", strikes: 1);
        await LimitedAsync(service, driver);
        var asked = await service.SayAsync("s1", "Also log the port.");

        foreach (var reset in new[] { Until.AddMinutes(1), Until.AddDays(2).AddMinutes(2) })
        {
            _now = reset;
            await driver.RunOnceAsync().WaitAsync(Poll.Patience);
            Assert.Equal(1, service.SessionCount);
            Assert.Equal("failed", service.State("s1"));
            Assert.Equal([asked], service.Said("s1"));
            Assert.Null(new GoOnMarks(_home).Read("s1"));
        }

        Assert.Equal(2, service.DaorisSaid.Count);
        Assert.Equal(2, service.DaorisSaid.Select(said => said.Key).Distinct().Count());
        Assert.Contains("ended.limit-words-wait", service.Record("s1"));
        Assert.DoesNotContain("ended.words-wait-you", service.Record("s1"));
        Assert.Empty(await new ServiceClient(service.Url, null).StrikesAsync());
    }

    /// <summary>
    /// The design §3.3's bound: an attempt refused with the same kind of limit just after its account's cool-off ended says its reset did
    /// not lift the limit, once, on its end; it waits for the next cool-off, never parks, and counts no strike.
    /// </summary>
    [Fact]
    public async Task A_refusal_with_the_same_limit_just_after_the_reset_says_it_was_not_lifted_once_and_never_parks()
    {
        await using var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        var driver = Driver(service, "limited", strikes: 1);
        await LimitedAsync(service, driver);
        Assert.DoesNotContain("limit.not-lifted", service.Record("s1"));

        _now = Until.AddMinutes(1);
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        Assert.Equal(("failed", "Taken"), (service.State("s1"), service.Status("q1")));
        Assert.Contains("\"limit\":true", service.Record("s1"), StringComparison.Ordinal);
        Assert.Single(JsonNode.Parse(service.Record("s1"))!["noteParts"]!.AsArray(), part => part?["code"]?.GetValue<string>() == "limit.not-lifted");
        Assert.Contains("Its last reset did not lift the limit: weekly.", service.Note("s1"));
        Assert.Empty(await new ServiceClient(service.Url, null).StrikesAsync());

        // The next attempt, long after that cool-off, says nothing of it again.
        _now = Until.AddDays(2);
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.DoesNotContain("limit.not-lifted", service.Record("s1"));
        Assert.Equal(1, service.SessionCount);
    }

    /// <summary>
    /// Round 3's B8 item 4: a start held between the judge and its going on (here a replay holding the repository's trees) keeps no word.
    /// </summary>
    [Fact]
    public async Task A_start_held_before_it_goes_on_keeps_no_word()
    {
        await using var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        var driver = Driver(service, "resumes");
        await LimitedAsync(service, driver);

        _now = Until.AddMinutes(1);
        using (TreeLock.TryReplaying(_home, null, "engine"))
        {
            await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        }

        Assert.Equal(1, service.SessionCount);
        Assert.Equal("failed", service.State("s1"));
        Assert.Empty(service.DaorisSaid);
        Assert.Empty(service.Said("s1"));
    }

    /// <summary>
    /// The design §3.3's order: a GoOn on a limited record resumed on its own account hands the person's words first, then Daoris's
    /// word last.
    /// </summary>
    [Fact]
    public async Task Words_on_a_limited_record_resumed_on_its_own_account_go_first_and_Daoris_s_word_last()
    {
        await using var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        var driver = Driver(service, "resumes");
        await LimitedAsync(service, driver);
        await service.SayAsync("s1", "Also log the port.");

        _now = Until.AddMinutes(1);
        await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        Assert.Equal(1, service.SessionCount);
        Assert.Equal("completed", service.State("s1"));
        var blocks = Heard().Last(line => line["method"]!.GetValue<string>() == "session/prompt")["blocks"]!.AsArray()
            .Select(block => block!.GetValue<string>()).ToList();
        var person = blocks.FindIndex(block => block.Contains("Also log the port.", StringComparison.Ordinal));
        var daoris = blocks.FindIndex(block => block.Contains(LimitResume.Words, StringComparison.Ordinal));
        Assert.True(person >= 0 && daoris > person, string.Join(" | ", blocks));
        Assert.Empty(service.Said("s1"));
    }

    /// <summary>
    /// The protocol door's stand-in: a first prompt takes the quest and is refused by observation 4's sentence. A resume meets its mode:
    /// <c>gone</c> no longer has the conversation, <c>elsewhere</c> is held by another client; otherwise it resumes and its prompt closes
    /// the quest, or, <c>limited</c>, is refused by the limit again. A carry-on's prompt closes the quest.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-agent.mjs");
        File.WriteAllText(script, $$"""
            import { createInterface } from 'node:readline';
            import { appendFileSync } from 'node:fs';
            const [log, mode] = process.argv.slice(2);
            const url = process.env.DAORIS_SERVICE_URL;
            const session = process.env.DAORIS_SESSION_ID;
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const heard = (what) => appendFileSync(log, JSON.stringify({ ...what, session }) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            const limit = (id) => send({ jsonrpc: '2.0', id, error: { code: -32603, message:
              "Internal error: You've hit your individual spend limit · run /usage-credits to ask your admin for a higher limit · your weekly limit resets Oct 3, 4pm ({{Zone.Id}})" } });
            let conversation = null;
            let resumed = false;
            for await (const line of createInterface({ input: process.stdin })) {
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
                if (mode === 'elsewhere') {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32600,
                    message: 'This Codex session is in use by another Codex client.', data: { reason: 'thread_active_writer' } } });
                } else if (mode === 'gone') {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32002, message: 'Resource not found: ' + frame.params?.sessionId } });
                } else {
                  conversation = frame.params.sessionId;
                  resumed = true;
                  send({ jsonrpc: '2.0', id: frame.id, result: {} });
                }
              } else if (frame.method === 'session/prompt') {
                const blocks = (frame.params?.prompt ?? []).filter((block) => block.type === 'text').map((block) => block.text);
                heard({ method: 'session/prompt', conversation: frame.params?.sessionId, blocks, resumed });
                const said = blocks.join('\n');
                if (resumed && mode === 'limited') {
                  limit(frame.id);
                } else if (resumed || said.includes('carrying on quest')) {
                  await respond({ action: 'done', reason: 'finished what the limit cut off' });
                  send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
                } else {
                  await respond({ action: 'take' });
                  limit(frame.id);
                }
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
            process.exit(0);
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
