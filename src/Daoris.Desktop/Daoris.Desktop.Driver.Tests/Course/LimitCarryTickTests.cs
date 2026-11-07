using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Through real ticks on the protocol door: a driven line an account's limit cut off goes on in a new part on the next account of the
/// same agent in the same conversation, its files carried to that account's home, Daoris's word its prompt and no brief; compacted
/// first past its provider's bound; and handed the brief, said, where the carry's copy fails.
/// </summary>
/// <remarks>
/// The stand-in keeps each conversation as a file under its account's home and resumes only one it finds there, so a resume on the next
/// account proves the copy. Its first turn takes the quest, says its context and is refused by the account's limit; a resumed turn, or a
/// briefed one, closes the quest. Two of the stand-in's accounts are in the set; the roster's clock and zone are the test's.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class LimitCarryTickTests : IDisposable
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kathmandu");

    private static readonly DateTimeOffset Seen = new(2026, 10, 1, 14, 0, 0, TimeSpan.FromMinutes(345));

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-limit-carry-tick-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public LimitCarryTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "carry@example.com");
        Git("config", "user.name", "carry");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
        foreach (var account in new[] { "account-1", "account-2" }) Directory.CreateDirectory(Home(account));
        new HarnessSettings().WithRotation("stub", ["account-1", "account-2"]).Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
    }

    public void Dispose()
    {
        GitSeed.Discard(_home);
    }

    private string Log => Path.Combine(_home, "agent.log");

    private string Home(string account) => HarnessSettings.ProfileHome(_home, "stub", account);

    /// <summary>
    /// 🔴 The install's case: the limit on account-1 goes on on account-2 in the same conversation, its file copied there, resumed by its
    /// id with Daoris's word and no brief, said on both parts and in the machine log as not briefed.
    /// </summary>
    [Fact]
    public async Task A_limit_on_a_door_that_carries_goes_on_in_the_same_conversation_on_the_other_account()
    {
        var (service, lines, driver) = await LimitedAsync("small");
        await using var _ = service;

        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = Said(service, look);
        Assert.True((service.SessionCount, service.State("s2"), service.Status("q1")) == (2, "completed", "Done"), seen);
        var heard = Heard();
        var resumed = Assert.Single(heard, line => line["method"]!.GetValue<string>() is "session/resume" or "session/load");
        Assert.Equal(("conv-s1", "s2", Account("account-2")), (Text(resumed, "conversation"), Text(resumed, "session"), Text(resumed, "account")));
        Assert.Single(heard, line => line["method"]!.GetValue<string>() == "session/new");
        var prompt = heard.Last(line => line["method"]!.GetValue<string>() == "session/prompt");
        var blocks = Blocks(prompt);
        Assert.Contains(LimitCarried.Words, blocks);
        Assert.DoesNotContain("carrying on quest", blocks);
        Assert.Null(prompt["compactAt"]?.GetValue<string>());
        Assert.True(File.Exists(Path.Combine(Home("account-2"), StubConversationHome.Folder, "conv-s1")), seen);

        Assert.Contains(Codes("s1"), part => part.Code == "moved.carried-on" && Equals(part.Value("account"), "account-2"));
        Assert.Contains(Codes("s2"), part => part.Code == "moved.carried-from" && Equals(part.Value("session"), "s1"));
        Assert.DoesNotContain(Codes("s2"), part => part.Code is "moved.from" or "moved.not-carried");
        lock (lines)
        {
            var moved = Assert.Single(lines, line => line.Event == "session.moved");
            Assert.Contains(moved.Data, field => field is ("briefed", false));
            Assert.Contains(moved.Data, field => field is ("carry", "whole"));
        }
    }

    /// <summary>🔴 Past the stand-in's bound the carried conversation goes on compacted first: its process is asked to compact, said so.</summary>
    [Fact]
    public async Task Past_the_bound_the_carried_conversation_is_compacted_first()
    {
        var (service, lines, driver) = await LimitedAsync("large");
        await using var _ = service;

        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = Said(service, look);
        Assert.True((service.State("s2"), service.Status("q1")) == ("completed", "Done"), seen);
        var prompt = Heard().Last(line => line["method"]!.GetValue<string>() == "session/prompt");
        Assert.True(prompt["resumed"]!.GetValue<bool>(), seen);
        Assert.Equal(StubConversationHome.Bound.ToString(System.Globalization.CultureInfo.InvariantCulture), Text(prompt, "compactAt"));
        var compacted = Assert.Single(Codes("s2"), part => part.Code == "moved.compacted-from");
        Assert.Equal((150_000L, StubConversationHome.Bound), (Convert.ToInt64(compacted.Value("tokens")), Convert.ToInt64(compacted.Value("bound"))));
        lock (lines) Assert.Contains(Assert.Single(lines, line => line.Event == "session.moved").Data, field => field is ("carry", "compacted"));
    }

    /// <summary>🔴 A carry whose copy fails, the conversation's file absent from the home it ran in, is briefed, saying why.</summary>
    [Fact]
    public async Task A_carry_that_fails_falls_back_to_the_brief_and_says_why()
    {
        var (service, lines, driver) = await LimitedAsync("unfiled");
        await using var _ = service;

        var look = await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var seen = Said(service, look);
        Assert.True((service.State("s2"), service.Status("q1")) == ("completed", "Done"), seen);
        var heard = Heard();
        Assert.DoesNotContain(heard, line => line["method"]!.GetValue<string>() is "session/resume" or "session/load");
        Assert.Contains("carrying on quest", Blocks(heard.Last(line => line["method"]!.GetValue<string>() == "session/prompt")));
        Assert.Contains(Codes("s2"), part => part.Code == "moved.not-carried" && Equals(part.Value("reason"), LimitCarry.Missing));
        Assert.Contains(Codes("s2"), part => part.Code == "moved.from");
        lock (lines)
        {
            var moved = Assert.Single(lines, line => line.Event == "session.moved");
            Assert.Contains(moved.Data, field => field is ("briefed", true));
            Assert.Contains(moved.Data, field => field is ("carry", "missing"));
        }
    }

    /// <summary>The first look: the session takes its quest on account-1, says its context and is refused by that account's limit.</summary>
    private async Task<(SessionMessagesTickTests.SaidStandIn Service, List<AccountLine> Lines, Daoris.Driver.Driver Driver)> LimitedAsync(string mode)
    {
        var service = SessionMessagesTickTests.SaidStandIn.Start(_repository);
        var lines = new List<AccountLine>();
        var client = new ServiceClient(service.Url, null);
        client.AccountLined += line => { lock (lines) lines.Add(line); };
        var adapters = ProviderRegistry.Built();
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Actors = StaffedRoles.On("acp-stub"),
            TestBound = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Log, mode], ["stub"] = ["node", TextAgent()] },
        };
        var driver = new Daoris.Driver.Driver(
            client, config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => Seen, Zone = Zone });

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);
        Assert.Equal(("failed", "Taken"), (service.State("s1"), service.Status("q1")));
        Assert.Contains("\"limit\":true", service.Record("s1"), StringComparison.Ordinal);
        Assert.Equal(Account("account-1"), Text(Heard().First(line => line["method"]!.GetValue<string>() == "session/new"), "account"));
        return (service, lines, driver);
    }

    private string Account(string account) => Path.GetFileName(Home(account));

    private JsonObject[] Heard() => [.. StubFile.Lines(Log).Select(line => JsonNode.Parse(line)!.AsObject())];

    private static string? Text(JsonObject line, string key) => line[key]?.GetValue<string>();

    private static string Blocks(JsonObject prompt) => string.Join("\n", prompt["blocks"]!.AsArray().Select(block => block!.GetValue<string>()));

    private IEnumerable<NotePart> Codes(string session) =>
        new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After(session, 0).Events.SelectMany(e => e.Parts ?? []);

    private string Said(SessionMessagesTickTests.SaidStandIn service, TickReport look) =>
        $"look: {string.Join(" | ", look.Events)}\ns1: {service.Record("s1")}\nheard: {string.Join("\n", Heard().Select(h => h.ToJsonString()))}";

    /// <summary>
    /// The stand-in on the protocol door: it keeps each conversation as a file under its account's home (none in mode <c>unfiled</c>), says
    /// its context (150,000 tokens in mode <c>large</c>, else 1,000) and the compaction its process is asked for.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-carry-agent.mjs");
        File.WriteAllText(script, $$"""
            import { createInterface } from 'node:readline';
            import { appendFileSync, existsSync, mkdirSync, writeFileSync } from 'node:fs';
            import { basename, join } from 'node:path';
            const [log, mode] = process.argv.slice(2);
            const url = process.env.DAORIS_SERVICE_URL;
            const session = process.env.DAORIS_SESSION_ID;
            const home = process.env.{{StubProvider.ConfigVariable}} ?? '';
            const account = basename(home);
            const compactAt = process.env.{{StubConversationHome.CompactVariable}} ?? null;
            const kept = (id) => join(home, '{{StubConversationHome.Folder}}', id);
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const heard = (what) => appendFileSync(log, JSON.stringify({ ...what, session, account, compactAt }) + '\n');
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
                if (mode !== 'unfiled') {
                  mkdirSync(join(home, '{{StubConversationHome.Folder}}'), { recursive: true });
                  writeFileSync(kept(conversation), 'its turns');
                }
                heard({ method: 'session/new', conversation });
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/resume' || frame.method === 'session/load') {
                const id = frame.params?.sessionId;
                heard({ method: frame.method, conversation: id });
                if (existsSync(kept(id))) {
                  conversation = id;
                  resumed = true;
                  send({ jsonrpc: '2.0', id: frame.id, result: {} });
                } else {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32002, message: 'Resource not found: ' + id } });
                }
              } else if (frame.method === 'session/prompt') {
                const blocks = (frame.params?.prompt ?? []).filter((block) => block.type === 'text').map((block) => block.text);
                heard({ method: 'session/prompt', conversation: frame.params?.sessionId, blocks, resumed });
                if (resumed || blocks.join('\n').includes('carrying on quest')) {
                  await respond({ action: 'done', reason: 'finished what the limit cut off' });
                  send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
                } else {
                  await respond({ action: 'take' });
                  send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
                    update: { sessionUpdate: 'usage_update', used: mode === 'large' ? 150000 : 1000, size: 1000000 } } });
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

    private string TextAgent()
    {
        var script = Path.Combine(_home, "text-agent.mjs");
        File.WriteAllText(script, """
            if (process.argv.includes('--version')) { console.log('stub 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
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
