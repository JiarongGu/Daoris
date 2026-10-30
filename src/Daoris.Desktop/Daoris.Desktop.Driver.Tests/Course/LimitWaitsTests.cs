using Microsoft.Data.Sqlite;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1d-ii part 4 (the foundation design §3.4 rule 3, O6-F2, O6-F8, O6-F13): the limit's moves against a host whose keyed open moves
/// the old part's words, and against one that does not. The ended move's body, its delivery and its box, and the live move, compose from the
/// new part's moved words, never the old record's ids: the first prompt is the brief, the refused turns' words, then every moved word with its
/// file, each shown on the new part under its id; the moved words leave the new part once on the wire, and the box goes only once the new part
/// holds none of them. Against a mark-1 host the old record is read again and its words taken, as LIMITWORD1 made it.
/// </summary>
/// <remarks>
/// The conversations run on the <c>acp-stub</c> adapter, a node stand-in on the protocol door whose accounts are the stub's (TOOL4j), which a
/// gate file makes refuse a prompt with the limit's sentence; no model, no account, no sign-in. Every case has its own scratch home under
/// <c>_fixtures/</c>, and sets no variable of the process's environment.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class LimitWaitsTests : IDisposable
{
    private const string Limit = "Internal error: You've hit your weekly limit";

    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "limit-waits", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public LimitWaitsTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(Gate);
        foreach (var account in new[] { "account-1", "account-2" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", account));
        }

        new HarnessSettings().WithRotation("stub", ["account-1", "account-2"]).Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private string Heard => Path.Combine(_home, "heard.txt");

    private string Gate => Path.Combine(_home, "gate");

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private DriverConfig Config()
    {
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>>
            {
                ["acp-stub"] = ["node", Agent(), Heard, Gate],
                ["stub"] = ["node", TextAgent()],
            },
        };
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        return config;
    }

    private (ChatRunner Runner, ChatLedger Ledger, ServiceClient Client) Runner(ChatLedger ledger)
    {
        var client = ledger.Client();
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => _now };
        var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: Events, harnesses: roster);
        return (runner, ledger, client);
    }

    private void Limited(string account) => File.WriteAllText(Path.Combine(Gate, "limited-" + account), "");

    private void Cooling(string account, DateTimeOffset until) =>
        AccountCooling.Cool(_home, new CoolingEntry("stub", account, until, Stated: true, "weekly", _now, null), _now);

    private IReadOnlyList<string> Prompts => [.. StubFile.Lines(Heard).Where(line => line.StartsWith("prompt[", StringComparison.Ordinal))];

    private string Seen(ChatLedger ledger, string id) =>
        $"heard [{string.Join(" | ", StubFile.Lines(Heard))}], records {ledger.Count}, said [{string.Join(",", ledger.Said(id))}], "
        + $"taken [{string.Join(" | ", ledger.TakenBy)}], record [{string.Join(" | ", Events.After(id, 0).Events.Select(e => $"{e.Kind}:{e.Origin}:{e.Id}:{e.Text}"))}]";

    private static int Count(string text, string part)
    {
        var count = 0;
        for (var at = text.IndexOf(part, StringComparison.Ordinal); at >= 0; at = text.IndexOf(part, at + part.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    private static bool Before(string text, params string[] parts)
    {
        for (var at = 1; at < parts.Length; at++)
        {
            if (text.IndexOf(parts[at - 1], StringComparison.Ordinal) >= text.IndexOf(parts[at], StringComparison.Ordinal)) return false;
        }

        return true;
    }

    /// <summary>
    /// An ended conversation held by its limit on account-1, its conversation kept and its last turn refused with <c>Make the port configurable.</c>;
    /// account-1 cooling for a day and account-2 ready, so the wake moves it there.
    /// </summary>
    private ChatLedger HeldEnded(int opens, params (string Id, string Text, string[] Files)[] waiting)
    {
        var ledger = new ChatLedger { Opens = opens }.Register("engine", _root);
        var words = ledger.Chat("c1", "stopped", "acp-stub", _root, profile: "account-1");
        foreach (var (id, text, files) in waiting) words.Say(id, text, files);
        new HarnessConversations(_home).Keep("c1", "acp-stub", "conv-account-1");
        var events = Events;
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Earlier answered exchange." });
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Make the port configurable." });
        events.Append("c1", new SessionEvent
        {
            Kind = SessionEventKind.Note, Text = "its account is cooling", Parts = [new NotePart { Code = NoteCodes.AccountCoolingWindow.Code }],
        });
        events.Append("c1", new SessionEvent { Kind = SessionEventKind.Turn, StopReason = "error" });
        Cooling("account-1", _now.AddDays(1));
        return ledger;
    }

    /// <summary>The first open of a chat asked keeps a word of the person's on <paramref name="session"/> just before it, the move's box already written.</summary>
    private static void LateWord(ChatLedger ledger, string session, string id, string text, Action? check = null)
    {
        var late = 0;
        ledger.BeforeRequest = (request, _) =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/sessions/chat" && Interlocked.Exchange(ref late, 1) == 0)
            {
                check?.Invoke();
                ledger.For(session).Say(id, text);
            }

            return Task.CompletedTask;
        };
    }

    /// <summary>
    /// 🔴 The ended move's body from the emptied predecessor: a word kept after the move's decision, the open moving it with the one the move
    /// named and the old record left holding none. The first prompt is the brief, the refused turn's words, then every moved word in order with
    /// its file linked from the new part's folder; each moved word is shown on the new part by its own event under its id before the agent's
    /// answer, the refused turn's words as one; the moved words leave the new part once on the wire, nothing is taken off the old record, and
    /// the move is delivered with the new part holding none of them, no box written at any point (FOUNDATION1f-vi part a).
    /// </summary>
    [Fact]
    public async Task The_ended_moves_first_prompt_is_the_refused_words_then_every_moved_word_with_its_file_a_late_one_among_them()
    {
        var ledger = HeldEnded(2, ("w1", "Make it fast.", ["trace.txt"]));
        ChatFiles.Keep(_home, "c1", [new ChatUpload("trace.txt", Encoding.UTF8.GetBytes("exit 3"))]);
        LateWord(ledger, "c1", "w2", "And add a test.", () => Assert.Equal("opening", Moved("c1")?.PhaseWord));
        var config = Config();
        var (runner, _, client) = Runner(ledger);
        using var r = runner;
        using var c = client;

        var woke = Assert.Single(await runner.WakeLimitsAsync(config));

        Assert.Equal("c2", woke.SessionId);
        await Poll.Until(() => Prompts.Count > 0 && ledger.TakenBy.Any(taken => taken.Session == "c2"), () => Seen(ledger, "c2"));
        var handed = Assert.Single(Prompts);
        Assert.StartsWith("prompt[account-2]", handed, StringComparison.Ordinal);
        Assert.Equal((1, 1, 1), (Count(handed, "Make the port configurable."), Count(handed, "Make it fast."), Count(handed, "And add a test.")));
        Assert.True(Before(handed, "You continue session `c1`", "Make the port configurable.", "Make it fast.", "And add a test."), handed);
        Assert.Contains(ChatFiles.Find(_home, "c2", "trace.txt")!.Path.Replace('\\', '/'), handed.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
        var moves = Assert.Single(ledger.Opened).Moves!;
        Assert.Equal(("w1", "w1"), (string.Join(',', moves["words"]!.AsArray()), string.Join(',', moves["read"]!.AsArray())));

        Assert.Empty(ledger.Said("c1"));
        Assert.DoesNotContain(ledger.TakenBy, taken => taken.Session == "c1");
        Assert.Contains(("c2", "w1,w2", (string?)null), ledger.TakenBy);
        Assert.Empty(ledger.Said("c2"));
        await Poll.Until(() => Moved("c1")?.PhaseWord == "delivered", () => Seen(ledger, "c2"));
        Assert.Empty(new LimitBoxes(_home).Pending());

        await Poll.Until(() => Events.After("c2", 0).Events.Any(e => e.Kind == SessionEventKind.Message), () => Seen(ledger, "c2"));
        var shown = Events.After("c2", 0).Events.ToList();
        var answer = shown.FindIndex(e => e.Kind == SessionEventKind.Message);
        var refused = Assert.Single(shown, e => e.Kind == SessionEventKind.User && e.Origin == "person" && e.Id is null);
        Assert.Equal("Make the port configurable.", refused.Text);
        var w1 = shown.FindIndex(e => e.Kind == SessionEventKind.User && e.Id == "w1");
        var w2 = shown.FindIndex(e => e.Kind == SessionEventKind.User && e.Id == "w2");
        Assert.True(w1 >= 0 && w2 > w1 && w2 < answer, Seen(ledger, "c2"));
        Assert.Equal(["trace.txt"], shown[w1].Files!);
    }

    /// <summary>
    /// 🔴 The move ends only on what the new part holds after the delivery's take, never on the take's answer, since a take naming words that do
    /// not wait answers success: a new part that still holds its moved words once the move is on the wire leaves the move standing, never
    /// closed <c>delivered</c>, and the old record, which holds none of them, is never asked to take them. No box is written for it
    /// (FOUNDATION1f-vi part a).
    /// </summary>
    [Fact]
    public async Task The_ended_move_is_not_delivered_while_the_new_part_still_holds_its_moved_words()
    {
        var ledger = HeldEnded(2, ("w1", "Make it fast.", []));
        ledger.Keeps.Add("c2");
        var config = Config();
        var (runner, _, client) = Runner(ledger);
        using var r = runner;
        using var c = client;

        Assert.Equal("c2", Assert.Single(await runner.WakeLimitsAsync(config)).SessionId);

        await Poll.Until(() => ledger.TakenBy.Any(taken => taken.Session == "c2"), () => Seen(ledger, "c2"));
        Assert.Equal(["w1"], ledger.Said("c2"));
        Assert.DoesNotContain(ledger.TakenBy, taken => taken.Session == "c1");
        Assert.Empty(new LimitBoxes(_home).Pending());
        var move = Moved("c1");
        Assert.NotNull(move);
        Assert.Equal("c2", move!.Destination);
        Assert.NotEqual("delivered", move.PhaseWord);
    }

    /// <summary>The ended conversation's move record, read from the home's store.</summary>
    private MoveRecord? Moved(string left) => new MoveStore(_home).Of(left).LastOrDefault();

    /// <summary>
    /// 🔴 The live move: a word the person said to the old part after its end and before the new part's open is moved by the open, and reaches the
    /// new part once, after the refused turn's words, the old part keeping none and never asked to take it.
    /// </summary>
    [Fact]
    public async Task A_word_kept_after_the_live_moves_end_and_before_its_open_reaches_the_new_part_once_after_the_refused_words()
    {
        var ledger = new ChatLedger { Opens = 2 }.Register("engine", _root);
        var config = Config();
        var (runner, _, client) = Runner(ledger);
        using var r = runner;
        using var c = client;
        var chat = (await runner.StartAsync("engine", "acp-stub", config, profile: "account-1")).SessionId!;
        await Poll.Until(() => ledger.State(chat) == "working", () => Seen(ledger, chat));
        Assert.True(runner.Say(chat, "Earlier answered exchange."));
        await Poll.Until(() => Events.After(chat, 0).Events.Any(e => e.Kind == SessionEventKind.Turn), () => Seen(ledger, chat));
        LateWord(ledger, chat, "k1", "And add a test.");
        Limited("account-1");

        Assert.True(runner.Say(chat, "Make the port configurable."));

        await Poll.Until(() => Prompts.Any(line => line.StartsWith("prompt[account-2]", StringComparison.Ordinal)), () => Seen(ledger, chat));
        var handed = Prompts.Single(line => line.StartsWith("prompt[account-2]", StringComparison.Ordinal));
        Assert.Equal((1, 1), (Count(handed, "Make the port configurable."), Count(handed, "And add a test.")));
        Assert.True(Before(handed, "Make the port configurable.", "And add a test."), handed);
        await Poll.Until(() => ledger.TakenBy.Any(taken => taken.Session == "c2"), () => Seen(ledger, "c2"));
        Assert.Contains(("c2", "k1", (string?)null), ledger.TakenBy);
        Assert.DoesNotContain(ledger.TakenBy, taken => taken.Session == chat && taken.Ids.Contains("k1", StringComparison.Ordinal));
        Assert.Empty(ledger.Said(chat));
        Assert.Single(Events.After("c2", 0).Events, e => e.Kind == SessionEventKind.User && e.Id == "k1");
    }

    /// <summary>
    /// 🔴 Against a host whose keyed open moves nothing (its mark 1), the ended move reads the old record again at its go-on (LIMITWORD1), hands
    /// the late word with the rest, and takes them off the old record named as taken by the new part, as before.
    /// </summary>
    [Fact]
    public async Task Against_a_mark_1_host_the_ended_move_reads_the_old_record_again_and_takes_its_words_as_before()
    {
        var ledger = HeldEnded(1, ("w1", "Make it fast.", []));
        LateWord(ledger, "c1", "w2", "And add a test.");
        var config = Config();
        var (runner, _, client) = Runner(ledger);
        using var r = runner;
        using var c = client;

        Assert.Equal("c2", Assert.Single(await runner.WakeLimitsAsync(config)).SessionId);

        await Poll.Until(() => Prompts.Count > 0 && ledger.Said("c1").Count == 0, () => Seen(ledger, "c1"));
        var handed = Assert.Single(Prompts);
        Assert.Equal((1, 1), (Count(handed, "Make it fast."), Count(handed, "And add a test.")));
        Assert.Null(Assert.Single(ledger.Opened).Moves);
        Assert.Contains(ledger.TakenBy, taken => taken.Session == "c1" && taken.Ids == "w1,w2" && taken.By == "c2");
        Assert.Empty(ledger.Said("c2"));
    }

    /// <summary>
    /// A stand-in ACP agent: names its conversation, writes down each prompt with the stub account and its file links, and answers a prompt
    /// with the protocol door's limit refusal while the gate says its account is limited.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-limit-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync, existsSync } from 'node:fs';
            import { basename, join } from 'node:path';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [heard, gate] = process.argv.slice(2);
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
              } else if (frame.method === 'session/resume' || frame.method === 'session/load') {
                conversation = frame.params.sessionId;
                note(`resumed[${account}]: ${conversation}`);
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.method === 'session/prompt') {
                const text = frame.params.prompt.filter((block) => block.type === 'text').map((block) => block.text).join(' | ');
                const links = frame.params.prompt.filter((block) => block.type === 'resource_link').map((block) => ' | link: ' + block.uri).join('');
                note(`prompt[${account}]: ` + text.replace(/\r?\n/g, ' ') + links);
                if (existsSync(join(gate, 'limited-' + account)) && text !== 'Earlier answered exchange.') {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: "Internal error: You've hit your weekly limit" } });
                  return;
                }
                await new Promise((resolve) => setTimeout(resolve, 50));
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
