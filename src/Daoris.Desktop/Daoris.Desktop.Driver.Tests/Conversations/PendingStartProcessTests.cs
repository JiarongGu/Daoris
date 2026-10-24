using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MODELROLE1d2 (D205 §3.5(b)) with real conversations: a new conversation whose agent refuses its model at the open, before any prompt,
/// ends <c>choice-not-offered</c>, and its pending start opens the walk's answer in its place within the same press, the words said to
/// the issued id moved to it and reaching it exactly once; a pick is never walked afresh.
/// </summary>
/// <remarks>
/// The first agent is <c>acp-stub</c> (a node stand-in on the protocol door) on stub accounts, refusing at the set the models its argument
/// names for an account, after a wait at its open so words can be said while it opens. The second is a plugin's protocol door,
/// <c>acp-second</c>, on the same stand-in, in its own home. No model, no account, no sign-in.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class PendingStartProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-pending-start-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public PendingStartProcessTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
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

    /// <summary>The stub's accounts, in this order.</summary>
    private void Accounts(params string[] accounts)
    {
        foreach (var account in accounts) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", account));
        new HarnessSettings().WithRotation("stub", accounts).Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
    }

    /// <summary>The built stand-ins, and a plugin's protocol door as the second agent.</summary>
    private static AdapterSet Adapters() => new(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
    {
        ["stub"] = new StubAdapter(),
        ["acp-stub"] = new AcpStubAdapter(),
        ["acp-second"] = new DeclaredAcpAdapter(new PluginHarness("acp-second", ["node"]), "second-plugin"),
    });

    /// <summary>The machine's choices: <paramref name="actor"/>'s order, and the first stand-in refusing <paramref name="refuses"/>.</summary>
    private DriverConfig Config(string actor, IReadOnlyList<ActorChoice> order, string refuses)
    {
        var agent = Agent();
        var config = ActorChoices.With(
            DriverConfig.Empty with
            {
                Adapter = "acp-stub",
                HelperAdapter = "acp-stub",
                Commands = new Dictionary<string, IReadOnlyList<string>>
                {
                    ["acp-stub"] = ["node", agent, Heard, "-", refuses, "600"],
                    ["acp-second"] = ["node", agent, Heard, "second", "", "0"],
                    ["stub"] = ["node", TextAgent()],
                },
            },
            actor, order);
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        return config;
    }

    private (ChatRunner Runner, ChatLedger Ledger, ServiceClient Client, List<(string From, string To)> Replaced) Runner()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var client = ledger.Client();
        var adapters = Adapters();
        var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: Events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
        var replaced = new List<(string, string)>();
        runner.Replaced += (from, to) =>
        {
            lock (replaced) replaced.Add((from, to));
        };
        return (runner, ledger, client, replaced);
    }

    private string Seen(ChatLedger ledger, string id) =>
        $"heard [{string.Join(" | ", StubFile.Lines(Heard))}], records {ledger.Count}, "
        + $"record [{string.Join(" | ", Events.After(id, 0).Events.Select(e => $"{e.Kind}:{e.Origin}:{e.Text}"))}]";

    private IReadOnlyList<string> Prompts(string on) =>
        [.. StubFile.Lines(Heard).Where(line => line.StartsWith($"prompt[{on}]: ", StringComparison.Ordinal))];

    private static string? EndCode(ChatLedger ledger, string id) =>
        ledger.Record(id)["noteParts"]?.AsArray().Select(part => part?["code"]?.GetValue<string>()).FirstOrDefault(code => code is not null);

    private bool Coded(string id, string code) =>
        Events.After(id, 0).Events.Any(e => e.Kind == SessionEventKind.Note && e.Parts?.Any(part => part.Code == code) == true);

    /// <summary>
    /// 🔴 The row's proof (§3.5(b), round 4): Ask Daoris refused at the open after its press has answered. Placed as <c>START_HELP</c>
    /// places it and run off the request, its first account refuses its model while words are said to it; its record ends
    /// <c>choice-not-offered</c> with the reading kept, the replacement opens in the same workspace's room on the next account, linked in
    /// the room's history, and the words said before and while it opens reach it exactly once. The window is told to follow it.
    /// </summary>
    [Fact]
    public async Task Ask_Daoris_refused_at_the_open_after_its_press_goes_on_in_its_replacement_with_the_words_once()
    {
        Accounts("account-1", "account-2");
        var config = Config("ask-daoris", [new ActorChoice("acp-stub", "opus")], "account-1:opus");
        var (runner, ledger, client, replaced) = Runner();
        using var r = runner;
        using var c = client;

        // As START_HELP: placed and answered, then run off the request.
        var placed = await runner.PlaceHelpAsync("acp-stub", config, "default", order: ActorChoices.Effective("ask-daoris", config));
        Assert.Equal("h1", placed.SessionId);
        await Task.Run(() => runner.RunHelpAsync(placed, config, _ => Task.FromResult<HelpMachine?>(Machine)));
        Assert.True(runner.Say("h1", "how do I drive a repository?"));

        await Poll.Until(() => ledger.Count == 2, () => Seen(ledger, "h1"));
        Assert.True(runner.Say("h1", "and how do I stop one?"));

        await Poll.Until(() => Prompts("account-2").Count == 2, () => Seen(ledger, "h2"));
        Assert.Equal(
            ["prompt[account-2]: how do I drive a repository?", "prompt[account-2]: and how do I stop one?"],
            Prompts("account-2").Select(line => line[..line.IndexOf('?', StringComparison.Ordinal)] + "?"));
        Assert.Empty(Prompts("account-1"));

        // The refused record: ended `choice-not-offered`, the account's reading kept.
        Assert.Equal("failed", ledger.State("h1"));
        Assert.Equal("ended.choice-not-offered", EndCode(ledger, "h1"));
        var version = ledger.Record("h1")["harnessVersion"]?.GetValue<string>();
        Assert.Equal(AgentOffers.RefusedCode, AgentOffers.Judge(AgentOffers.Read(_home), "acp-stub", version, "account-1", "opus", DateTimeOffset.UtcNow));

        // Its replacement: the same workspace's room, the next account, linked to it; the issued id answers for it; the window told.
        var next = (await client.RecordAsync("h2"))!;
        Assert.Equal(("acp-stub", "account-2", HelpRoom.Repository), (next.Adapter, next.Profile, next.Repository));
        Assert.Equal(ledger.Record("h1")["workspace"]!.GetValue<string>(), ledger.Record("h2")["workspace"]!.GetValue<string>());
        Assert.Equal("h1", new HelpConversations(_home).Read("h2").From);
        Assert.Equal("h2", runner.CurrentOf("h1"));
        lock (replaced) Assert.Equal([("h1", "h2")], replaced);
        Assert.True(Coded("h2", NoteCodes.ChatReplaced.Code));
    }

    /// <summary>
    /// 🔴 A Chat whose first entry is refused at the open, its agent's only account refusing: one press, one running conversation, two
    /// records. The replacement runs Chat's second choice on another agent, continuing the refused record by <c>not-offered</c>, and takes
    /// the words said to the issued id.
    /// </summary>
    [Fact]
    public async Task A_chat_whose_first_entry_is_refused_at_the_open_runs_its_second_in_its_place()
    {
        Accounts("account-1");
        var config = Config("chat", [new ActorChoice("acp-stub", "opus"), new ActorChoice("acp-second")], "account-1:opus");
        var (runner, ledger, client, replaced) = Runner();
        using var r = runner;
        using var c = client;

        var started = await runner.StartAsync("engine", "acp-stub", config, order: ActorChoices.Effective("chat", config));
        Assert.Equal("c1", started.SessionId);
        Assert.True(runner.Say("c1", "Make the port configurable."));

        await Poll.Until(() => Prompts("second").Count == 1, () => Seen(ledger, "c1"));
        // Told its own maker's trailer, then the words, once.
        var handed = Assert.Single(Prompts("second"));
        Assert.Contains("This start runs on `acp-second`", handed);
        Assert.EndsWith("Make the port configurable.", handed);
        Assert.Empty(Prompts("account-1"));
        Assert.Equal(2, ledger.Count);

        Assert.Equal(("failed", "ended.choice-not-offered"), (ledger.State("c1"), EndCode(ledger, "c1")));
        var next = (await client.RecordAsync("c2"))!;
        Assert.Equal(("acp-second", "c1", HandOverWhy.NotOffered, HandOverBy.Daoris), (next.Adapter, next.From, next.FromWhy, next.FromBy));
        Assert.Equal("working", ledger.State("c2"));
        await Poll.Until(() => ledger.Record("c2")["choice"]?.GetValue<string>() == "2", () => Seen(ledger, "c2"));
        lock (replaced) Assert.Equal([("c1", "c2")], replaced);

        // Its next words, said to either id, go on there.
        Assert.True(runner.Say("c1", "Now the tests."));
        await Poll.Until(() => Prompts("second").Count == 2, () => Seen(ledger, "c2"));
        Assert.Equal(2, ledger.Count);
    }

    /// <summary>
    /// 🔴 A pick (§3.5(b), (d)): the person picked the agent, whose account refuses both of Chat's entries for it. The pick tries its agent's
    /// other entry on the same account, then waits on its record with its reason: Chat's choice on another agent never runs in its place.
    /// </summary>
    [Fact]
    public async Task A_picked_chat_refused_at_the_open_tries_its_agent_s_entries_on_its_account_and_never_another_agent()
    {
        Accounts("account-1");
        var config = Config(
            "chat", [new ActorChoice("acp-stub", "opus"), new ActorChoice("acp-stub", "sonnet"), new ActorChoice("acp-second")],
            "account-1:opus,account-1:sonnet");
        var (runner, ledger, client, replaced) = Runner();
        using var r = runner;
        using var c = client;

        var started = await runner.StartAsync("engine", "acp-stub", config, order: ActorChoices.Effective("chat", config, pick: "acp-stub"));
        Assert.Equal("c1", started.SessionId);
        Assert.True(runner.Say("c1", "Make the port configurable."));

        await Poll.Until(() => Coded("c1", NoteCodes.ChatChoiceWaits.Code), () => Seen(ledger, "c1"));
        Assert.Equal(
            ["model[account-1]: opus refused", "model[account-1]: sonnet refused"],
            StubFile.Lines(Heard).Where(line => line.StartsWith("model[", StringComparison.Ordinal)));
        Assert.Equal(("failed", "ended.choice-not-offered"), (ledger.State("c1"), EndCode(ledger, "c1")));
        Assert.Equal(1, ledger.Count);
        Assert.Empty(Prompts("second"));
        Assert.Empty(Prompts("account-1"));
        lock (replaced) Assert.Empty(replaced);
        Assert.Contains("does not offer `sonnet`", Events.After("c1", 0).Events.Last(e => e.Kind == SessionEventKind.Note && e.Parts?.Any(part => part.Code == NoteCodes.ChatChoiceWaits.Code) == true).Text);
    }

    // ——— Interrupted between the two records (§3.5(b)): the app closed after the refused record ended, before its replacement opened.

    /// <summary>
    /// A record a refusal at the open ended, as a shell before this one left it: <c>choice-not-offered</c> on <paramref name="account"/>,
    /// the person's words waiting on it, the refusal kept as that account's reading.
    /// </summary>
    private void Refused(
        ChatLedger ledger, string id, string actor, string choice, string account, string model, string tree, string repository = "engine",
        ContinuedFrom? from = null)
    {
        ledger.Chat(id, "failed", "acp-stub", tree, repository: repository, profile: account, from: from)
            .Say("w1", "Make the port configurable.");
        ledger.Edit(id, record =>
        {
            record["actor"] = actor;
            record["choice"] = choice;
            record["workspace"] = "default";
            record["noteParts"] = System.Text.Json.Nodes.JsonNode.Parse(
                $$"""[{ "code": "ended.choice-not-offered", "values": { "agent": "acp-stub", "model": "{{model}}" } }]""");
        });
        Assert.Null(AgentOffers.Refused(_home, "acp-stub", null, account, model, DateTimeOffset.UtcNow));
    }

    /// <summary>A part that ran a conversation and holds a fact only its own history has.</summary>
    private void Ran(ChatLedger ledger, string id, string tree, string repository = "engine")
    {
        ledger.Chat(id, "failed", "acp-stub", tree, repository: repository, profile: "account-1");
        ledger.Edit(id, record =>
        {
            record["actor"] = repository == HelpRoom.Repository ? "ask-daoris" : "chat";
            record["workspace"] = "default";
        });
        Events.Append(id, new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "Where does the service listen?" });
        Events.Append(id, new SessionEvent { Kind = SessionEventKind.Message, Text = "It listens on port 8443, set in listen.json." });
    }

    /// <summary>
    /// 🔴 A Chat its first entry's refusal ended, the app stopped before its replacement opened: its <i>Go on</i> is a fresh start of Chat,
    /// never a resume of the session its open made. The walk passes the refusing account by the reading kept and runs Chat's second choice,
    /// continuing the refused record by <c>not-offered</c>, its first message the words that waited, which leave the refused record.
    /// </summary>
    [Fact]
    public async Task A_refused_chat_s_go_on_after_a_restart_starts_its_actor_s_walk_with_the_words()
    {
        Accounts("account-1");
        var config = Config("chat", [new ActorChoice("acp-stub", "opus"), new ActorChoice("acp-second")], "");
        var (runner, ledger, client, _) = Runner();
        using var r = runner;
        using var c = client;
        Refused(ledger, "c1", "chat", "1", "account-1", "opus", _root);

        var went = await runner.GoOnAsync("c1", config);

        Assert.Equal("c2", went.SessionId);
        await Poll.Until(() => Prompts("second").Count == 1, () => Seen(ledger, "c2"));
        Assert.EndsWith("Make the port configurable.", Assert.Single(Prompts("second")));
        Assert.Empty(Prompts("account-1"));
        var next = (await client.RecordAsync("c2"))!;
        Assert.Equal(("acp-second", "c1", HandOverWhy.NotOffered), (next.Adapter, next.From, next.FromWhy));
        await Poll.Until(() => ledger.Said("c1").Count == 0, () => Seen(ledger, "c1"));
        Assert.Contains(("c1", "w1", "c2"), ledger.TakenBy);
    }

    /// <summary>
    /// 🔴 A pick is never walked afresh (§3.5(b), round 4, Codex 2): a picked start interrupted after its refusal, its agent's every entry
    /// refused on its account, another of Chat's choices free. The come-up's go-on tries only the pick's alternatives on that account, and
    /// waits saying why: the other agent never runs in its place.
    /// </summary>
    [Fact]
    public async Task A_picked_start_interrupted_after_its_refusal_is_never_run_elsewhere_by_the_come_up()
    {
        Accounts("account-1", "account-2");
        var config = Config(
            "chat", [new ActorChoice("acp-stub", "opus"), new ActorChoice("acp-stub", "sonnet"), new ActorChoice("acp-second")], "");
        var (runner, ledger, client, _) = Runner();
        using var r = runner;
        using var c = client;
        Refused(ledger, "c1", "chat", "pick", "account-1", "opus", _root);
        Assert.Null(AgentOffers.Refused(_home, "acp-stub", null, "account-1", "sonnet", DateTimeOffset.UtcNow));

        var taken = await runner.TakeUpAsync();

        Assert.All(taken, start => Assert.Null(start.SessionId));
        Assert.Equal(1, ledger.Count);
        Assert.Empty(StubFile.Lines(Heard));
        Assert.Equal(["w1"], ledger.Said("c1"));
        Assert.Contains(Events.After("c1", 0).Events, e => e.Kind == SessionEventKind.Note && e.Text?.Contains("choice pick", StringComparison.Ordinal) == true);
    }

    /// <summary>
    /// 🔴 A refused continuation rebuilds its brief (§3.5(b), round 4, Codex 3; LIMITGOON2's proof): part A ran a conversation, its
    /// replacement B was refused before its first prompt, and the app restarted before C opened. C's first prompt carries a fact only A's
    /// history held, then the words that waited on B: for a Chat, whose line is its <c>from</c>, and for Ask Daoris, whose line is its room's
    /// history.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_refused_continuation_s_go_on_hands_the_brief_of_its_line_then_the_words(bool help)
    {
        Accounts("account-1");
        var actor = help ? "ask-daoris" : "chat";
        var config = Config(actor, [new ActorChoice("acp-stub", "opus"), new ActorChoice("acp-second")], "");
        var (runner, ledger, client, _) = Runner();
        using var r = runner;
        using var c = client;
        runner.DescribeHelp = _ => Task.FromResult<HelpMachine?>(Machine);
        var room = HelpRoom.PathOf(_home, "default");
        Directory.CreateDirectory(room);
        var (a, b, repository, tree) = help ? ("h1", "h2", HelpRoom.Repository, room) : ("c1", "c2", "engine", _root);
        Ran(ledger, a, tree, repository);
        Refused(ledger, b, actor, "2", "account-1", "opus", tree, repository, help ? null : new ContinuedFrom(a, HandOverWhy.Limit, HandOverBy.Daoris));
        if (help) new HelpConversations(_home).StartedFrom(b, a, HelpConversations.Brief);

        var went = await runner.GoOnAsync(b, config);

        Assert.NotNull(went.SessionId);
        await Poll.Until(() => Prompts("second").Count == 1, () => Seen(ledger, went.SessionId!));
        var handed = Assert.Single(Prompts("second"));
        Assert.Contains("It listens on port 8443", handed);
        Assert.True(handed.IndexOf("port 8443", StringComparison.Ordinal) < handed.IndexOf("Make the port configurable.", StringComparison.Ordinal));
        if (help) Assert.Equal(b, new HelpConversations(_home).Read(went.SessionId!).From);
        else Assert.Equal((b, HandOverWhy.NotOffered), ((await client.RecordAsync(went.SessionId!))!.From, (await client.RecordAsync(went.SessionId!))!.FromWhy));
    }

    /// <summary>
    /// A stand-in ACP agent: answers its open after the wait it is given, listing three models; refuses at the set the models its argument
    /// names for the stub account it runs as, writing each set down; and writes down each prompt with that account, or the tag it was given.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-refusing-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { basename } from 'node:path';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [heard, tag, refuses = '', wait = '0'] = process.argv.slice(2);
            const home = process.env.DAORIS_STUB_CONFIG_DIR ?? '';
            const account = tag && tag !== '-' ? tag : (home ? basename(home) : '(own)');
            const refused = new Set(refuses.split(',').filter((pair) => pair.startsWith(account + ':')).map((pair) => pair.split(':')[1]));
            const note = (text) => appendFileSync(heard, text + '\n');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            let model = 'default';
            let conversation = null;
            const options = () => [{ id: 'model', name: 'Model', category: 'model', type: 'select', currentValue: model,
              options: [{ value: 'default', name: 'Default' }, { value: 'opus', name: 'Opus' }, { value: 'sonnet', name: 'Sonnet' }] }];
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                await new Promise((resolve) => setTimeout(resolve, Number(wait)));
                conversation = 'conv-' + account;
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation, configOptions: options() } });
              } else if (frame.method === 'session/set_config_option') {
                const value = frame.params.value;
                if (refused.has(value)) {
                  note(`model[${account}]: ${value} refused`);
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: 'Internal error',
                    data: { details: `Invalid value for config option model: ${value}` } } });
                } else {
                  model = value;
                  note(`model[${account}]: ${value}`);
                  send({ jsonrpc: '2.0', id: frame.id, result: { configOptions: options() } });
                }
              } else if (frame.method === 'session/prompt') {
                const text = frame.params.prompt.filter((block) => block.type === 'text').map((block) => block.text).join(' | ');
                note(`prompt[${account}]: ` + text.replace(/\r?\n/g, ' '));
                await new Promise((resolve) => setTimeout(resolve, 50));
                send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
                  update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'heard it' } } } });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
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
