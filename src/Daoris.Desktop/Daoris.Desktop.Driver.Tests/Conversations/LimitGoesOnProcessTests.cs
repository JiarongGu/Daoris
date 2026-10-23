using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LIMITGOON1 (the limit-goes-on design §2; D197, D189 point 3) with real conversations: a conversation whose account answers a limit
/// sentence mid-conversation goes on, at that turn's end, in a new part of its line on the next account of the same agent, handed
/// Daoris's brief and then the words the refused turn held, as the person's. The old part ends saying where it went on, the new one
/// opens saying where it came from, each by its code, and the machine log says the move. For a repository's chat, in its tree with its
/// record continuing the old by <c>limit</c>, asked by <c>daoris</c>; for Ask Daoris, in its room with its history naming the old.
/// </summary>
/// <remarks>
/// The conversation runs on the <c>acp-stub</c> adapter (a node stand-in on the protocol door), whose accounts are the stub's (TOOL4j),
/// two in the set; the stand-in reads which one it runs as from the stub's variable, and on <c>account-1</c> answers every prompt with
/// Claude Code's limit sentence, as the protocol door's JSON-RPC error carries it. No model, no account, no sign-in.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class LimitGoesOnProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-limit-proc-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public LimitGoesOnProcessTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
        foreach (var account in new[] { "account-1", "account-2" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", account));
        }

        new HarnessSettings().WithRotation("stub", ["account-1", "account-2"]).Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Heard => Path.Combine(_home, "heard.txt");

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private static readonly HelpMachine Machine = new()
    {
        Adapter = "acp-stub",
        Helper = "acp-stub",
        Cap = 1,
        Repositories = [new("engine", "default") { Checkout = true, Drivable = true }],
    };

    private DriverConfig Config()
    {
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>>
            {
                ["acp-stub"] = ["node", Agent(), Heard],
                ["stub"] = ["node", TextAgent()],
            },
        };
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        return config;
    }

    private string Seen(ChatLedger ledger, SessionEvents events, string id) =>
        $"heard [{string.Join(" | ", StubFile.Lines(Heard))}], records {ledger.Count}, state {ledger.State(id)}, "
        + $"record [{string.Join(" | ", events.After(id, 0).Events.Select(e => $"{e.Kind}:{e.Origin}:{e.Text}"))}]";

    private static bool Ended(string state) => state is "completed" or "stopped" or "failed";

    private static NotePart? Coded(SessionEvent e) => e.Kind == SessionEventKind.Note ? e.Parts?.FirstOrDefault(part => part.Code is not null) : null;

    /// <summary>
    /// 🔴 A repository's chat: the limit refuses its turn on account-1, and the same conversation goes on on account-2 in its tree, its
    /// record continuing the old by the limit, handed the brief and the words the refused turn held; each part says the move once.
    /// </summary>
    [Fact]
    public async Task A_chat_whose_account_answers_a_limit_goes_on_on_the_next_account_with_the_held_words()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        var events = Events;
        using var client = ledger.Client();
        var lines = new List<AccountLine>();
        client.AccountLined += line => { lock (lines) lines.Add(line); };
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses));
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: events, harnesses: roster);

        var old = (await runner.StartAsync("engine", "acp-stub", config, profile: "account-1")).SessionId!;
        await Poll.Until(() => ledger.State(old) == "working", () => Seen(ledger, events, old));
        Assert.True(runner.Say(old, "Make the port configurable."));

        // The limit refuses its turn; that turn's end moves it to account-2, in its tree, continuing it by the limit.
        await Poll.Until(() => ledger.Count == 2 && Ended(ledger.State(old)), () => Seen(ledger, events, old));
        var next = (await client.RecordAsync("c2"))!;
        Assert.Equal(("acp-stub", "account-2", _root), (next.Adapter, next.Profile, next.Tree));
        Assert.Equal((old, HandOverWhy.Limit, HandOverBy.Daoris), (next.From, next.FromWhy, next.FromBy));
        Assert.NotNull(roster.CoolingOf("acp-stub", "account-1"));

        // On account-2 it is handed the brief, then the words the refused turn held, in one prompt.
        await Poll.Until(
            () => StubFile.Lines(Heard).Any(line => line.StartsWith("prompt[account-2]: ", StringComparison.Ordinal) && line.Contains("Make the port configurable.")),
            () => Seen(ledger, events, next.Session));
        var handed = StubFile.Lines(Heard).Single(line => line.StartsWith("prompt[account-2]: ", StringComparison.Ordinal));
        Assert.Contains($"You continue session `{old}`, which ran on acp-stub", handed);
        Assert.True(handed.IndexOf("You continue session", StringComparison.Ordinal) < handed.IndexOf("Make the port configurable.", StringComparison.Ordinal));
        await Poll.Until(() => events.After(next.Session, 0).Events.Any(e => e.Kind == SessionEventKind.Turn), () => Seen(ledger, events, next.Session));

        // The new part keeps the words as the person's, after the brief as Daoris's.
        var kept = events.After(next.Session, 0).Events.Where(e => e.Kind == SessionEventKind.User).ToList();
        Assert.Equal(["target", "person"], kept.Select(e => e.Origin));
        Assert.Equal("Make the port configurable.", kept[1].Text);

        // Said once on each part by its code, why `limit`, accounts by their ids; and in the machine log.
        var came = events.After(next.Session, 0).Events.Select(Coded).OfType<NotePart>().Single(part => part.Code == "moved.from");
        Assert.Equal((old, "account-1", HandOverWhy.Limit), (came.Value("session"), came.Value("account"), came.Value("why")));
        var went = events.After(old, 0).Events.Select(Coded).OfType<NotePart>().Single(part => part.Code == "moved.on");
        Assert.Equal(("c2", "account-2", HandOverWhy.Limit), (went.Value("session"), went.Value("account"), went.Value("why")));
        lock (lines)
        {
            var moved = Assert.Single(lines, line => line.Event == "session.moved");
            Assert.Equal(
                new object?[] { "c2", old, "acp-stub", "acp-stub", "account-1", "account-2", HandOverWhy.Limit, true },
                moved.Data.Select(field => field.Value));
            var rotated = Assert.Single(lines, line => line.Event == "account.rotated" && (string?)line.Data[0].Value == "c2");
            Assert.Equal(old, rotated.Data.Single(field => field.Key == "carries").Value);
        }

        // Its next words go on the new part, on account-2, and nothing moves again.
        Assert.True(runner.Say(next.Session, "Now the tests."));
        await Poll.Until(() => StubFile.Lines(Heard).Contains("prompt[account-2]: Now the tests."), () => Seen(ledger, events, next.Session));
        Assert.Equal(2, ledger.Count);
    }

    /// <summary>
    /// 🔴 Ask Daoris: the limit refuses its turn on account-1, and the same conversation goes on on account-2 in its room, its history
    /// naming the one it continues and what it was handed, the held words its first turn; the log's opening line says what it carries.
    /// </summary>
    [Fact]
    public async Task Ask_Daoris_whose_account_answers_a_limit_goes_on_in_its_room_on_the_next_account_with_the_held_words()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        var events = Events;
        using var client = ledger.Client();
        var lines = new List<AccountLine>();
        client.AccountLined += line => { lock (lines) lines.Add(line); };
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses));
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: events, harnesses: roster);

        var old = (await runner.StartHelpAsync("acp-stub", config, Machine)).SessionId!;
        await Poll.Until(() => ledger.State(old) == "working", () => Seen(ledger, events, old));
        Assert.Equal("account-1", (await client.RecordAsync(old))!.Profile);
        Assert.True(runner.Say(old, "how do I drive a repository?"));

        await Poll.Until(() => ledger.Count == 2 && Ended(ledger.State(old)), () => Seen(ledger, events, old));
        var next = (await client.RecordAsync("h2"))!;
        Assert.Equal(("acp-stub", "account-2", HelpRoom.Repository), (next.Adapter, next.Profile, next.Repository));

        await Poll.Until(
            () => StubFile.Lines(Heard).Any(line => line.StartsWith("prompt[account-2]: ", StringComparison.Ordinal) && line.Contains("how do I drive a repository?")),
            () => Seen(ledger, events, "h2"));
        var kept = new HelpConversations(_home).Read("h2");
        Assert.Equal((old, HelpConversations.Brief), (kept.From, kept.Handed));
        Assert.Contains($"You continue session `{old}`", StubFile.Lines(Heard).Single(line => line.StartsWith("prompt[account-2]: ", StringComparison.Ordinal)));

        var went = events.After(old, 0).Events.Select(Coded).OfType<NotePart>().Single(part => part.Code == "moved.on");
        Assert.Equal(("h2", HandOverWhy.Limit), (went.Value("session"), went.Value("why")));
        Assert.Single(events.After("h2", 0).Events.Select(Coded).OfType<NotePart>(), part => part.Code == "moved.from");
        lock (lines)
        {
            var rotated = Assert.Single(lines, line => line.Event == "account.rotated" && (string?)line.Data[0].Value == "h2");
            Assert.Equal(old, rotated.Data.Single(field => field.Key == "carries").Value);
        }
    }

    /// <summary>
    /// A stand-in ACP agent: names its conversation, writes down each prompt with the stub account it runs as, and on <c>account-1</c>
    /// answers every prompt with the protocol door's limit refusal; elsewhere it answers after a moment.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-limit-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { basename } from 'node:path';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [heard] = process.argv.slice(2);
            const home = process.env.DAORIS_STUB_CONFIG_DIR ?? '';
            const account = home ? basename(home) : '(own)';
            const note = (text) => appendFileSync(heard, text + '\n');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            let conversation = null;
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                conversation = 'conv-' + account;
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/prompt') {
                const text = frame.params.prompt.filter((block) => block.type === 'text').map((block) => block.text).join(' | ');
                note(`prompt[${account}]: ` + text.replace(/\r?\n/g, ' '));
                if (account === 'account-1') {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: "Internal error: You've hit your weekly limit" } });
                  return;
                }
                await new Promise((resolve) => setTimeout(resolve, 100));
                send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
                  update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'heard it' } } } });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.method === 'session/close') {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }

    /// <summary>The stub's command: it answers the sign-in question for the stub's accounts, as the protocol stand-in's owner.</summary>
    private string TextAgent()
    {
        var script = Path.Combine(_home, "text-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('stub 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const lines = createInterface({ input: process.stdin });
            lines.on('line', (line) => console.log('heard: ' + line));
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }
}
