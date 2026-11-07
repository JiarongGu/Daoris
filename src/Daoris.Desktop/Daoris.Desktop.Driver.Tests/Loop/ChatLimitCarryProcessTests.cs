using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// With real conversations on the protocol door: a chat whose account answers a limit goes on, at that turn's end, in its own
/// conversation on the other account of the same agent, its files carried to that account's home and resumed by its id with the held
/// words and Daoris's word, no brief; compacted first past its provider's bound; and handed the brief, said, where the door refuses
/// the carried resume, the copy taken out of that home.
/// </summary>
/// <remarks>
/// The stand-in keeps each conversation as a file under its account's home and resumes only one it finds there, so a resume on the
/// other account proves the copy. On <c>account-1</c> it says its context and answers every prompt but the first with Claude Code's
/// limit sentence. No model, no account, no sign-in.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class ChatLimitCarryProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-chat-carry-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public ChatLimitCarryProcessTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
        foreach (var account in new[] { "account-1", "account-2" }) Directory.CreateDirectory(Home(account));
        new HarnessSettings().WithRotation("stub", ["account-1", "account-2"]).Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Log => Path.Combine(_home, "agent.log");

    private string Home(string account) => HarnessSettings.ProfileHome(_home, "stub", account);

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    /// <summary>
    /// 🔴 The limit on account-1 goes on on account-2 in the same conversation: its file copied there, resumed by its id with the held
    /// words and Daoris's word and no brief, said on both parts and in the machine log as not briefed.
    /// </summary>
    [Fact]
    public async Task A_chat_at_its_limit_goes_on_in_its_own_conversation_on_the_other_account()
    {
        var (ledger, runner, lines, old) = await LimitedAsync("small");
        using var _ = runner;

        var next = await MovedAsync(ledger, old);
        await Poll.Until(() => Prompts("account-2").Any(), () => Seen(ledger, next));
        var resumed = Assert.Single(Heard(), line => line["method"]!.GetValue<string>() is "session/resume" or "session/load");
        Assert.Equal(("conv-account-1", Account("account-2")), (Text(resumed, "conversation"), Text(resumed, "account")));
        Assert.DoesNotContain(Heard(), line => line["method"]!.GetValue<string>() == "session/new" && Text(line, "account") == Account("account-2"));
        var blocks = Blocks(Prompts("account-2").First());
        Assert.Contains("Make the port configurable.", blocks);
        Assert.Contains(LimitCarried.Words, blocks);
        Assert.DoesNotContain("You continue session", blocks);
        Assert.Null(Text(Prompts("account-2").First(), "compactAt"));
        Assert.True(File.Exists(Path.Combine(Home("account-2"), StubConversationHome.Folder, "conv-account-1")), Seen(ledger, next));

        Assert.Contains(Codes(old), part => part.Code == "moved.carried-on" && Equals(part.Value("account"), "account-2"));
        Assert.Contains(Codes(next), part => part.Code == "moved.carried-from" && Equals(part.Value("session"), old));
        Assert.DoesNotContain(Codes(next), part => part.Code is "moved.from" or "moved.not-carried");
        lock (lines)
        {
            var moved = Assert.Single(lines, line => line.Event == "session.moved");
            Assert.Contains(moved.Data, field => field is ("briefed", false));
            Assert.Contains(moved.Data, field => field is ("carry", "whole"));
        }

        // The words it went on with left the new part, and its next words go on there, nothing moving again.
        await Poll.Until(() => Events.After(next, 0).Events.Any(e => e.Kind == SessionEventKind.Turn), () => Seen(ledger, next));
        Assert.True(runner.Say(next, "Now the tests."));
        await Poll.Until(() => Prompts("account-2").Any(prompt => Blocks(prompt).Contains("Now the tests.")), () => Seen(ledger, next));
        Assert.Equal(2, ledger.Count);
    }

    /// <summary>🔴 Past the stand-in's bound the carried conversation goes on compacted first: its process is asked to compact, said so.</summary>
    [Fact]
    public async Task Past_the_bound_the_carried_conversation_is_compacted_first()
    {
        var (ledger, runner, lines, old) = await LimitedAsync("large");
        using var _ = runner;

        var next = await MovedAsync(ledger, old);
        await Poll.Until(() => Prompts("account-2").Any(), () => Seen(ledger, next));
        var prompt = Prompts("account-2").First();
        Assert.True(prompt["resumed"]!.GetValue<bool>(), Seen(ledger, next));
        Assert.Equal(StubConversationHome.Bound.ToString(System.Globalization.CultureInfo.InvariantCulture), Text(prompt, "compactAt"));
        var compacted = Assert.Single(Codes(next), part => part.Code == "moved.compacted-from");
        Assert.Equal((150_000L, StubConversationHome.Bound), (Convert.ToInt64(compacted.Value("tokens")), Convert.ToInt64(compacted.Value("bound"))));
        lock (lines) Assert.Contains(Assert.Single(lines, line => line.Event == "session.moved").Data, field => field is ("carry", "compacted"));
    }

    /// <summary>🔴 A carried resume the door refuses goes on in the brief on the same part, said with why, and the copy is removed.</summary>
    [Fact]
    public async Task A_carried_resume_the_door_refuses_falls_back_to_the_brief_and_removes_its_copy()
    {
        var (ledger, runner, lines, old) = await LimitedAsync("refused");
        using var _ = runner;

        var next = await MovedAsync(ledger, old);
        await Poll.Until(() => Prompts("account-2").Any(), () => Seen(ledger, next));
        var blocks = Blocks(Prompts("account-2").First());
        Assert.Contains($"You continue session `{old}`", blocks);
        Assert.Contains("Make the port configurable.", blocks);
        Assert.False(File.Exists(Path.Combine(Home("account-2"), StubConversationHome.Folder, "conv-account-1")), Seen(ledger, next));
        Assert.True(File.Exists(Path.Combine(Home("account-1"), StubConversationHome.Folder, "conv-account-1")), Seen(ledger, next));
        Assert.Contains(Codes(next), part => part.Code == "moved.not-carried" && Equals(part.Value("reason"), LimitCarry.Refused));
        Assert.Contains(Codes(next), part => part.Code == "moved.from");
        Assert.Equal(2, ledger.Count);
        lock (lines)
        {
            var moved = lines.Where(line => line.Event == "session.moved").ToList();
            Assert.Contains(moved[0].Data, field => field is ("carry", "whole"));
            Assert.Contains(moved[^1].Data, field => field is ("briefed", true));
            Assert.Contains(moved[^1].Data, field => field is ("carry", LimitCarry.Refused));
        }
    }

    /// <summary>The conversation on account-1, one exchange answered and the next refused by that account's limit.</summary>
    private async Task<(ChatLedger Ledger, ChatRunner Runner, List<AccountLine> Lines, string Old)> LimitedAsync(string mode)
    {
        var ledger = new ChatLedger { Opens = 2 }.Register("engine", _root);
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>>
            {
                ["acp-stub"] = ["node", Agent(), Log, mode],
                ["stub"] = ["node", TextAgent()],
            },
        };
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        var client = ledger.Client();
        var lines = new List<AccountLine>();
        client.AccountLined += line => { lock (lines) lines.Add(line); };
        var adapters = ProviderRegistry.Built();
        var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: Events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));

        var old = (await runner.StartAsync("engine", "acp-stub", config, profile: "account-1")).SessionId!;
        await Poll.Until(() => ledger.State(old) == "working", () => Seen(ledger, old));
        Assert.True(runner.Say(old, "Earlier answered exchange."));
        await Poll.Until(() => Events.After(old, 0).Events.Any(e => e.Kind == SessionEventKind.Turn), () => Seen(ledger, old));
        Assert.True(runner.Say(old, "Make the port configurable."));
        return (ledger, runner, lines, old);
    }

    /// <summary>The new part the limit's move opened on account-2, continuing the old by the limit.</summary>
    private async Task<string> MovedAsync(ChatLedger ledger, string old)
    {
        await Poll.Until(() => ledger.Count == 2 && ledger.State(old) is "completed" or "stopped" or "failed", () => Seen(ledger, old));
        var next = (await ledger.Client().RecordAsync("c2"))!;
        Assert.Equal(("acp-stub", "account-2", _root), (next.Adapter, next.Profile, next.Tree));
        Assert.Equal((old, HandOverWhy.Limit, HandOverBy.Daoris), (next.From, next.FromWhy, next.FromBy));
        return next.Session;
    }

    private string Account(string account) => Path.GetFileName(Home(account));

    private JsonObject[] Heard() => [.. StubFile.Lines(Log).Select(line => JsonNode.Parse(line)!.AsObject())];

    private IEnumerable<JsonObject> Prompts(string account) =>
        Heard().Where(line => line["method"]!.GetValue<string>() == "session/prompt" && Text(line, "account") == Account(account));

    private static string? Text(JsonObject line, string key) => line[key]?.GetValue<string>();

    private static string Blocks(JsonObject prompt) => string.Join("\n", prompt["blocks"]!.AsArray().Select(block => block!.GetValue<string>()));

    private IEnumerable<NotePart> Codes(string session) => Events.After(session, 0).Events.SelectMany(e => e.Parts ?? []);

    private string Seen(ChatLedger ledger, string id) =>
        $"records {ledger.Count}, state {ledger.State(id)}, record [{string.Join(" | ", Events.After(id, 0).Events.Select(e => $"{e.Kind}:{e.Origin}:{e.Text}"))}]"
        + $"\nheard: {string.Join("\n", StubFile.Lines(Log))}";

    /// <summary>
    /// The stand-in on the protocol door: it keeps each conversation as a file under its account's home, says its context (150,000 tokens
    /// in mode <c>large</c>, else 1,000) and the compaction its process is asked for, and refuses every resume in mode <c>refused</c>.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-chat-carry-agent.mjs");
        File.WriteAllText(script, $$"""
            import { createInterface } from 'node:readline';
            import { appendFileSync, existsSync, mkdirSync, writeFileSync } from 'node:fs';
            import { basename, join } from 'node:path';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [log, mode] = process.argv.slice(2);
            const home = process.env.{{StubProvider.ConfigVariable}} ?? '';
            const account = basename(home);
            const compactAt = process.env.{{StubConversationHome.CompactVariable}} ?? null;
            const kept = (id) => join(home, '{{StubConversationHome.Folder}}', id);
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const heard = (what) => appendFileSync(log, JSON.stringify({ ...what, account, compactAt }) + '\n');
            let conversation = null;
            let resumed = false;
            for await (const line of createInterface({ input: process.stdin })) {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1,
                  agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                conversation = 'conv-' + account;
                mkdirSync(join(home, '{{StubConversationHome.Folder}}'), { recursive: true });
                writeFileSync(kept(conversation), 'its turns');
                heard({ method: 'session/new', conversation });
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/resume' || frame.method === 'session/load') {
                const id = frame.params?.sessionId;
                heard({ method: frame.method, conversation: id });
                if (mode !== 'refused' && existsSync(kept(id))) {
                  conversation = id;
                  resumed = true;
                  send({ jsonrpc: '2.0', id: frame.id, result: {} });
                } else {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32002, message: 'Resource not found: ' + id } });
                }
              } else if (frame.method === 'session/prompt') {
                const blocks = (frame.params?.prompt ?? []).filter((block) => block.type === 'text').map((block) => block.text);
                heard({ method: 'session/prompt', conversation: frame.params?.sessionId, blocks, resumed });
                if (account === 'account-1') {
                  send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
                    update: { sessionUpdate: 'usage_update', used: mode === 'large' ? 150000 : 1000, size: 1000000 } } });
                  if (!blocks.join('\n').startsWith('Earlier answered exchange.')) {
                    send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: "Internal error: You've hit your weekly limit" } });
                    continue;
                  }
                }
                await new Promise((resolve) => setTimeout(resolve, 100));
                send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
                  update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'heard it' } } } });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
            process.exit(0);
            """);
        return script;
    }

    /// <summary>The stub's command: it answers the sign-in question for the stub's accounts, as the protocol stand-in's owner.</summary>
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
}
