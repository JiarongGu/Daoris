using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// LIMITGOON2 (D205 §3.5(e)) with real conversations: a conversation whose agent has no account left goes on with its actor's next
/// choice on another agent, as a new part handed Daoris's brief and then the words the refused turn held, the move said once naming the
/// agent it went to. A repository's chat continues the old by <c>limit</c> in its tree, told the earlier maker's trailer where that part
/// left work uncommitted (MAKERTRAIL2); Ask Daoris's goes on in its workspace's room, its history naming the old, and a person's
/// hand-over of it is still refused. A conversation on the person's pick, or with no other choice, waits, saying why.
/// </summary>
/// <remarks>
/// The first agent is <c>acp-stub</c> (a node stand-in on the protocol door) with one stub account, <c>account-1</c>, which answers every
/// prompt but the first exchange with Claude Code's limit sentence, so every account of the agent is cooling once it does. The second is
/// a plugin's protocol door, <c>acp-second</c>, on the same stand-in, which runs in its own home and answers every prompt. No model, no
/// account, no sign-in.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class LimitGoesElsewhereProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-limit-else-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public LimitGoesElsewhereProcessTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", "account-1"));
        new HarnessSettings().WithRotation("stub", ["account-1"]).Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
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

    /// <summary>The built stand-ins, and a plugin's protocol door as the second agent.</summary>
    private static AdapterSet Adapters() => new(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
    {
        ["stub"] = new StubAdapter(),
        ["acp-stub"] = new AcpStubAdapter(),
        ["acp-second"] = new DeclaredAcpAdapter(new PluginHarness("acp-second", ["node"]), "second-plugin"),
    });

    /// <summary>The machine's choices, with <paramref name="actor"/>'s order: the first stand-in, then the second.</summary>
    private DriverConfig Config(string actor, IReadOnlyList<ActorChoice> order)
    {
        var agent = Agent();
        var config = ActorChoices.With(
            DriverConfig.Empty with
            {
                Adapter = "acp-stub",
                HelperAdapter = "acp-stub",
                Commands = new Dictionary<string, IReadOnlyList<string>>
                {
                    ["acp-stub"] = ["node", agent, Heard],
                    ["acp-second"] = ["node", agent, Heard, "second"],
                    ["stub"] = ["node", TextAgent()],
                },
            },
            actor, order);
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        return config;
    }

    private static readonly IReadOnlyList<ActorChoice> Both = [new ActorChoice("acp-stub"), new ActorChoice("acp-second")];

    private string Seen(ChatLedger ledger, SessionEvents events, string id) =>
        $"heard [{string.Join(" | ", StubFile.Lines(Heard))}], records {ledger.Count}, state {ledger.State(id)}, "
        + $"record [{string.Join(" | ", events.After(id, 0).Events.Select(e => $"{e.Kind}:{e.Origin}:{e.Text}"))}]";

    private static bool Ended(string state) => state is "completed" or "stopped" or "failed";

    private static NotePart? Coded(SessionEvent e) => e.Kind == SessionEventKind.Note ? e.Parts?.FirstOrDefault(part => part.Code is not null) : null;

    private static bool Coded(SessionEvent e, string code) => e.Kind == SessionEventKind.Note && e.Parts?.Any(part => part.Code == code) == true;

    private (ChatRunner Runner, ChatLedger Ledger, ServiceClient Client, HarnessRoster Roster, List<AccountLine> Lines) Runner()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var client = ledger.Client();
        var lines = new List<AccountLine>();
        client.AccountLined += line => { lock (lines) lines.Add(line); };
        var adapters = Adapters();
        var roster = new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses));
        var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: Events, harnesses: roster);
        return (runner, ledger, client, roster, lines);
    }

    /// <summary>
    /// 🔴 A repository's chat on Chat's first choice: the limit refuses its turn on the agent's only account, and the conversation goes
    /// on with Chat's second choice, another agent, in its tree, continuing the old by the limit; handed the brief of its line, which
    /// holds the earlier exchange, then the refused turn's words, and told the earlier maker's trailer for the work left uncommitted.
    /// The old part says once that it went on with the second agent; its next words go there.
    /// </summary>
    [Fact]
    public async Task A_chat_whose_agent_has_no_account_left_goes_on_with_its_actor_s_next_choice_on_another_agent()
    {
        Git("init", "-q");
        Git("config", "user.email", "limit@example.invalid");
        Git("config", "user.name", "LIMITGOON2");
        File.WriteAllText(Path.Combine(_root, "port.txt"), "8080\n");
        Git("add", ".");
        Git("commit", "-q", "-m", "first");
        File.WriteAllText(Path.Combine(_root, "port.txt"), "9090\n");

        var config = Config("chat", Both);
        var events = Events;
        var (runner, ledger, client, roster, lines) = Runner();
        using var _ = runner;
        using var __ = client;

        var old = (await runner.StartAsync("engine", "acp-stub", config, order: ActorChoices.Effective("chat", config))).SessionId!;
        await Poll.Until(() => ledger.State(old) == "working", () => Seen(ledger, events, old));
        Assert.Equal(("acp-stub", "account-1"), ((await client.RecordAsync(old))!.Adapter, (await client.RecordAsync(old))!.Profile));
        Assert.True(runner.Say(old, "Earlier answered exchange."));
        await Poll.Until(() => events.After(old, 0).Events.Any(e => e.Kind == SessionEventKind.Turn), () => Seen(ledger, events, old));
        Assert.True(runner.Say(old, "Make the port configurable."));

        // Every account of acp-stub cools; that turn's end moves it to acp-second, in its tree, continuing it by the limit.
        await Poll.Until(() => ledger.Count == 2 && Ended(ledger.State(old)), () => Seen(ledger, events, old));
        var next = (await client.RecordAsync("c2"))!;
        Assert.Equal(("acp-second", _root), (next.Adapter, next.Tree));
        Assert.Equal((old, HandOverWhy.Limit, HandOverBy.Daoris), (next.From, next.FromWhy, next.FromBy));
        Assert.NotNull(roster.CoolingOf("acp-stub", "account-1"));

        // On acp-second: the brief, holding the earlier exchange, then the refused turn's words, in one prompt, with both trailers.
        await Poll.Until(
            () => StubFile.Lines(Heard).Any(line => line.StartsWith("prompt[second]: ", StringComparison.Ordinal) && line.Contains("Make the port configurable.")),
            () => Seen(ledger, events, next.Session));
        var handed = StubFile.Lines(Heard).Single(line => line.StartsWith("prompt[second]: ", StringComparison.Ordinal));
        Assert.Contains($"You continue session `{old}`, which ran on acp-stub", handed);
        Assert.Contains("Earlier answered exchange.", handed);
        Assert.True(handed.IndexOf("You continue session", StringComparison.Ordinal) < handed.IndexOf("Make the port configurable.", StringComparison.Ordinal));
        Assert.Contains("This start runs on `acp-second`", handed);
        Assert.Contains("The part of this line before you ran on `acp-stub`", handed);
        Assert.Single(StubFile.Lines(Heard), line => line.Contains("Make the port configurable.") && line.StartsWith("prompt[second]: ", StringComparison.Ordinal));
        await Poll.Until(() => events.After(next.Session, 0).Events.Any(e => e.Kind == SessionEventKind.Turn), () => Seen(ledger, events, next.Session));
        // Its record keeps the choice it took: Chat's second (D205 §9.3).
        await Poll.Until(() => ledger.Record("c2")["choice"]?.GetValue<string>() == "2", () => Seen(ledger, events, "c2"));

        // Said once on the old part, naming the agent it went to; once on the new, naming where it came from; and in the machine log.
        var went = Assert.Single(events.After(old, 0).Events.Select(Coded).OfType<NotePart>(), part => part.Code == "moved.on");
        Assert.Equal(("c2", "acp-second", HandOverWhy.Limit), (went.Value("session"), went.Value("agent"), went.Value("why")));
        var came = Assert.Single(events.After(next.Session, 0).Events.Select(Coded).OfType<NotePart>(), part => part.Code == "moved.from");
        Assert.Equal((old, "acp-stub", "account-1"), (came.Value("session"), came.Value("agent"), came.Value("account")));
        lock (lines)
        {
            var moved = Assert.Single(lines, line => line.Event == "session.moved");
            Assert.Equal("acp-second", moved.Data.Select(field => field.Value).ElementAt(3));
        }

        // Its next words go on the new part, on acp-second, and nothing moves again.
        Assert.True(runner.Say(next.Session, "Now the tests."));
        await Poll.Until(() => StubFile.Lines(Heard).Contains("prompt[second]: Now the tests."), () => Seen(ledger, events, next.Session));
        Assert.Equal(2, ledger.Count);
    }

    /// <summary>
    /// 🔴 Ask Daoris on its first choice: every account of its agent cools mid-turn, and it goes on with its second choice, another agent,
    /// in the same workspace's room, its history linking the two, the brief and the held words delivered once; a person's hand-over of
    /// the new part to another agent is still refused, as Ask Daoris's own conversation.
    /// </summary>
    [Fact]
    public async Task Ask_Daoris_whose_agent_has_no_account_left_goes_on_with_its_next_choice_in_its_room()
    {
        var config = Config("ask-daoris", Both);
        var events = Events;
        var (runner, ledger, client, _, _) = Runner();
        using var r = runner;
        using var c = client;

        var old = (await runner.StartHelpAsync("acp-stub", config, Machine, order: ActorChoices.Effective("ask-daoris", config))).SessionId!;
        await Poll.Until(() => ledger.State(old) == "working", () => Seen(ledger, events, old));
        Assert.True(runner.Say(old, "Earlier answered exchange."));
        await Poll.Until(() => events.After(old, 0).Events.Any(e => e.Kind == SessionEventKind.Turn), () => Seen(ledger, events, old));
        Assert.True(runner.Say(old, "how do I drive a repository?"));

        await Poll.Until(() => ledger.Count == 2 && Ended(ledger.State(old)), () => Seen(ledger, events, old));
        var next = (await client.RecordAsync("h2"))!;
        Assert.Equal(("acp-second", HelpRoom.Repository), (next.Adapter, next.Repository));
        Assert.Equal((await client.RecordAsync(old))!.Workspace, next.Workspace);

        await Poll.Until(
            () => StubFile.Lines(Heard).Any(line => line.StartsWith("prompt[second]: ", StringComparison.Ordinal) && line.Contains("how do I drive a repository?")),
            () => Seen(ledger, events, "h2"));
        var handed = Assert.Single(StubFile.Lines(Heard), line => line.StartsWith("prompt[second]: ", StringComparison.Ordinal));
        Assert.Contains($"You continue session `{old}`", handed);
        Assert.Contains("Earlier answered exchange.", handed);
        await Poll.Until(() => ledger.Record("h2")["choice"]?.GetValue<string>() == "2", () => Seen(ledger, events, "h2"));
        var kept = new HelpConversations(_home).Read("h2");
        Assert.Equal((old, HelpConversations.Brief), (kept.From, kept.Handed));

        var went = Assert.Single(events.After(old, 0).Events.Select(Coded).OfType<NotePart>(), part => part.Code == "moved.on");
        Assert.Equal(("h2", "acp-second"), (went.Value("session"), went.Value("agent")));

        // The person's *Go on with another agent…* on it is still refused: only the limit moves Ask Daoris's conversation.
        var refused = await runner.HandOverAsync("h2", "acp-stub", null, HandOverWhy.Press, HandOverBy.Person, config);
        Assert.Equal(WordsNever.Help, refused.Why);
    }

    /// <summary>
    /// 🔴 A chat on the person's pick, and one whose actor has no other choice: every account of its agent cools, and each waits, its
    /// words with it, saying why it does not go on with another agent before its wait's line. Nothing else opens.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_chat_on_a_pick_or_with_no_other_choice_waits_saying_why(bool picked)
    {
        var config = Config("chat", picked ? Both : [new ActorChoice("acp-stub")]);
        var code = picked ? NoteCodes.ChatLimitPicked.Code : NoteCodes.ChatLimitNoOther.Code;
        var events = Events;
        var (runner, ledger, client, _, _) = Runner();
        using var r = runner;
        using var c = client;

        var order = ActorChoices.Effective("chat", config, pick: picked ? "acp-stub" : null);
        var session = (await runner.StartAsync("engine", "acp-stub", config, order: order)).SessionId!;
        await Poll.Until(() => ledger.State(session) == "working", () => Seen(ledger, events, session));
        Assert.True(runner.Say(session, "Make the port configurable."));

        await Poll.Until(
            () => events.After(session, 0).Events.Any(e => Coded(e, NoteCodes.ChatLimitWaits.Code)), () => Seen(ledger, events, session));
        var said = events.After(session, 0).Events.Where(e => e.Kind == SessionEventKind.Note).ToList();
        var why = Assert.Single(said, e => Coded(e, code));
        Assert.Equal("acp-stub", Coded(why)!.Value("owner"));
        Assert.True(said.IndexOf(why) < said.FindIndex(e => Coded(e, NoteCodes.ChatLimitWaits.Code)));
        Assert.Equal(1, ledger.Count);
        Assert.DoesNotContain(StubFile.Lines(Heard), line => line.StartsWith("prompt[second]: ", StringComparison.Ordinal));
    }

    private void Git(params string[] arguments)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = _root, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var git = Process.Start(start)!;
        git.WaitForExit();
        Assert.Equal(0, git.ExitCode);
    }

    /// <summary>
    /// A stand-in ACP agent: names its conversation, writes down each prompt with the stub account it runs as, or the tag it was given,
    /// and on <c>account-1</c> answers every prompt but the first exchange with the protocol door's limit refusal; elsewhere it answers
    /// after a moment.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-limit-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { basename } from 'node:path';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [heard, tag] = process.argv.slice(2);
            const home = process.env.DAORIS_STUB_CONFIG_DIR ?? '';
            const account = tag ?? (home ? basename(home) : '(own)');
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
                if (account === 'account-1' && text !== 'Earlier answered exchange.') {
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
