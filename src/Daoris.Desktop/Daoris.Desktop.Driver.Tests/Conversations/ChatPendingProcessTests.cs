using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MODELROLE1d2 (D205 §3.5(b), §13's tests): a conversation's first open re-asks within the same press. Its agent refusing its actor's
/// model at the open, before any prompt, the refused record ends <c>choice-not-offered</c>, the walk is asked again with that account
/// passed, and its answer opens in the same call, linked to the refused record, the person's words moved to it and reaching it once;
/// with nothing left, the words wait on the refused record. A pick tries only its own agent's other entries on its own account.
/// </summary>
/// <remarks>
/// The conversation runs on the <c>acp-stub</c> adapter (a node stand-in on the protocol door) over the stub's two accounts; the stand-in
/// lists three models and refuses at the set the ones its argument names for its account, as Claude's door was measured to (MODELROLE1a).
/// The ledger is <see cref="ChatLedger"/>, in-process.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class ChatPendingProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-chat-pending-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public ChatPendingProcessTests()
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

    /// <summary>The stub's accounts, in the machine's list, and the stand-in refusing <paramref name="refuses"/> (<c>account:model</c>, comma-joined).</summary>
    private DriverConfig Config(string refuses, string actor, IReadOnlyList<ActorChoice> order, params string[] accounts)
    {
        foreach (var account in accounts) Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", account));
        new HarnessSettings().WithRotation("stub", accounts).Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
        var config = ActorChoices.With(
            DriverConfig.Empty with
            {
                Adapter = "acp-stub",
                HelperAdapter = "acp-stub",
                Commands = new Dictionary<string, IReadOnlyList<string>>
                {
                    ["acp-stub"] = ["node", Agent(), Heard, refuses],
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
        var adapters = AdapterSet.Built();
        var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: Events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
        var replaced = new List<(string From, string To)>();
        runner.Replaced += (from, to) =>
        {
            lock (replaced) replaced.Add((from, to));
        };
        return (runner, ledger, client, replaced);
    }

    private string Seen(ChatLedger ledger, string id) =>
        $"heard [{string.Join(" | ", StubFile.Lines(Heard))}], records {ledger.Count}, state {ledger.State(id)}, note {ledger.Note(id)}, "
        + $"record [{string.Join(" | ", Events.After(id, 0).Events.Select(e => $"{e.Kind}:{e.Text}"))}]";

    private string[] Prompts(string account) =>
        [.. StubFile.Lines(Heard).Where(line => line.StartsWith($"{account} prompt: ", StringComparison.Ordinal)).Select(line => line[$"{account} prompt: ".Length..])];

    /// <summary>
    /// 🔴 A Chat whose first account refuses its entry's model at the open: one press, two records. The refused one ends
    /// <c>choice-not-offered</c> with the reading kept and no prompt; the replacement opens on the second account, continuing it by
    /// <c>not-offered</c>; the first words, and a word said while the replacement opens, reach it once each, in the order said; the window
    /// is told to follow it, and a word said to the press's id afterwards reaches the replacement.
    /// </summary>
    [Fact]
    public async Task A_chat_its_first_account_refuses_at_the_open_goes_on_in_the_same_press_with_its_words_once()
    {
        var config = Config("account-1:opus", "chat", [new ActorChoice("acp-stub", "opus")], "account-1", "account-2");
        var (runner, ledger, client, replaced) = Runner();
        using var r = runner;
        using var c = client;
        // The replacement's open held until the test has said a word to the refused conversation, so that word is seen joining the moved ones.
        var opening = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var opens = 0;
        ledger.BeforeRequest = async (request, _) =>
        {
            if (request.Method == HttpMethod.Post && request.RequestUri!.AbsolutePath == "/api/sessions/chat" && Interlocked.Increment(ref opens) == 2)
            {
                await opening.Task;
            }
        };

        var started = await runner.StartWithAsync(
            "engine", "acp-stub", config, "Make the port configurable.", [], null, order: ActorChoices.Effective("chat", config));
        var issued = started.SessionId ?? throw new InvalidOperationException(started.Message);

        await Poll.Until(() => ledger.State(issued) == "failed" && Volatile.Read(ref opens) == 2, () => Seen(ledger, issued));
        Assert.True(runner.Say(issued, "Then the tests."));
        opening.SetResult();

        await Poll.Until(() => Prompts("account-2").Length == 2, () => Seen(ledger, "c2"));
        Assert.Equal(["Make the port configurable.", "Then the tests."], Prompts("account-2"));
        Assert.Empty(Prompts("account-1"));

        // The refused record: `choice-not-offered`, its reading kept for the next start's walk.
        Assert.StartsWith("Not started: `acp-stub` did not offer `opus`", ledger.Note(issued), StringComparison.Ordinal);
        var version = ledger.Record(issued)["harnessVersion"]?.GetValue<string>();
        Assert.Equal(AgentOffers.RefusedCode, AgentOffers.Judge(AgentOffers.Read(_home), "acp-stub", version, "account-1", "opus", DateTimeOffset.UtcNow));
        // The replacement: the walk's answer, on the second account, continuing the refused one.
        var next = (await client.RecordAsync("c2"))!;
        Assert.Equal(("acp-stub", "account-2", issued, HandOverWhy.NotOffered, HandOverBy.Daoris), (next.Adapter, next.Profile, next.From, next.FromWhy, next.FromBy));
        Assert.Contains("set: model=opus", StubFile.Lines(Heard).Where(line => line.StartsWith("account-2 ", StringComparison.Ordinal)).Select(line => line["account-2 ".Length..]));
        lock (replaced) Assert.Equal([(issued, "c2")], replaced);

        // The press's id answers for the replacement now.
        Assert.Equal("c2", runner.CurrentOf(issued));
        Assert.True(runner.Say(issued, "And the docs."));
        await Poll.Until(() => Prompts("account-2").Length == 3, () => Seen(ledger, "c2"));
        Assert.Equal(2, ledger.Count);
    }

    /// <summary>
    /// 🔴 Ask Daoris refused at the open after its press was answered (<c>START_HELP</c> placed it and returned): the words said to it
    /// reach the replacement exactly once, in the same workspace's room, its history linking the two, and the window is told to follow it.
    /// </summary>
    [Fact]
    public async Task Ask_Daoris_refused_after_its_press_returned_goes_on_in_its_room_with_its_words_once()
    {
        var config = Config("account-1:opus", "ask-daoris", [new ActorChoice("acp-stub", "opus")], "account-1", "account-2");
        var (runner, ledger, client, replaced) = Runner();
        using var r = runner;
        using var c = client;

        var placed = await runner.PlaceHelpAsync("acp-stub", config, order: ActorChoices.Effective("ask-daoris", config));
        var issued = placed.SessionId ?? throw new InvalidOperationException(placed.Message);
        var start = await runner.RunHelpAsync(placed, config, _ => Task.FromResult<HelpMachine?>(Machine));
        Assert.Equal(issued, start.SessionId);
        Assert.True(runner.Say(issued, "how do I drive a repository?"));

        await Poll.Until(() => ledger.Count == 2 && Prompts("account-2").Length == 1, () => Seen(ledger, issued));
        Assert.Equal(["how do I drive a repository?"], Prompts("account-2"));
        Assert.Empty(Prompts("account-1"));
        Assert.Equal("failed", ledger.State(issued));
        Assert.StartsWith("Not started: `acp-stub` did not offer `opus`", ledger.Note(issued), StringComparison.Ordinal);

        var next = (await client.RecordAsync("h2"))!;
        Assert.Equal(("acp-stub", "account-2", HelpRoom.Repository), (next.Adapter, next.Profile, next.Repository));
        Assert.Equal((await client.RecordAsync(issued))!.Workspace, next.Workspace);
        var kept = new HelpConversations(_home).Read("h2");
        Assert.Equal((issued, HelpConversations.NotOffered), (kept.From, kept.Handed));
        lock (replaced) Assert.Equal([(issued, "h2")], replaced);
    }

    /// <summary>
    /// Nothing left to open: the only account refuses the only choice. The press's record ends <c>choice-not-offered</c>, no second record
    /// opens, the conversation says why it did not go on, and the first words wait on the refused record for its go-on.
    /// </summary>
    [Fact]
    public async Task A_chat_with_nothing_left_after_its_refusal_keeps_its_words_waiting_on_the_refused_record()
    {
        var config = Config("account-1:opus", "chat", [new ActorChoice("acp-stub", "opus")], "account-1");
        var (runner, ledger, client, replaced) = Runner();
        using var r = runner;
        using var c = client;

        var started = await runner.StartWithAsync(
            "engine", "acp-stub", config, "Make the port configurable.", [], null, order: ActorChoices.Effective("chat", config));
        var issued = started.SessionId ?? throw new InvalidOperationException(started.Message);

        await Poll.Until(
            () => ledger.State(issued) == "failed" && ledger.Said(issued).Count == 1
                  && Events.After(issued, 0).Events.Any(e => e.Parts?.Any(part => part.Code == NoteCodes.ChatLimitNotMoved.Code) == true),
            () => Seen(ledger, issued));
        Assert.Equal(1, ledger.Count);
        Assert.Empty(Prompts("account-1"));
        lock (replaced) Assert.Empty(replaced);
    }

    /// <summary>
    /// 🔴 A pick is never walked afresh (§3.5(d)): its account refuses the picked model, and the next entry of Chat's order for the same
    /// agent is set on the same session, on the same account, its record's choice still the pick; no other account opens.
    /// </summary>
    [Fact]
    public async Task A_pick_refused_at_the_open_tries_its_agent_s_next_entry_on_its_own_account()
    {
        var config = Config(
            "account-1:opus", "chat", [new ActorChoice("acp-stub", "opus"), new ActorChoice("acp-stub", "sonnet")], "account-1", "account-2");
        var (runner, ledger, client, replaced) = Runner();
        using var r = runner;
        using var c = client;

        var started = await runner.StartWithAsync(
            "engine", "acp-stub", config, "Make the port configurable.", [], null, profile: "account-1",
            order: ActorChoices.Effective("chat", config, pick: "acp-stub"));
        var issued = started.SessionId ?? throw new InvalidOperationException(started.Message);

        await Poll.Until(() => Prompts("account-1").Length == 1, () => Seen(ledger, issued));
        var heard = StubFile.Lines(Heard).Where(line => line.StartsWith("account-1 ", StringComparison.Ordinal)).Select(line => line["account-1 ".Length..]).ToList();
        Assert.True(heard.IndexOf("refused: model=opus") < heard.IndexOf("set: model=sonnet"), string.Join(" | ", heard));
        Assert.Equal(1, ledger.Count);
        Assert.Equal("working", ledger.State(issued));
        Assert.Equal("pick", ledger.Record(issued)["choice"]?.GetValue<string>());
        Assert.Empty(Prompts("account-2"));
        lock (replaced) Assert.Empty(replaced);
    }

    /// <summary>
    /// A record the app stopped after its agent refused it at the open, before its replacement opened: ended <c>choice-not-offered</c> on
    /// account-1 with the refusal kept as that account's reading, its <paramref name="choice"/> and the words said to it waiting.
    /// </summary>
    private async Task<string> RefusedAsync(
        ChatLedger ledger, ServiceClient client, string choice, ContinuedFrom? from = null, string repository = "engine")
    {
        var id = repository == "engine" ? "c1" : "h1";
        ledger.Chat(id, "working", "acp-stub", repository == "engine" ? _root : HelpRoom.PathOf(_home), repository: repository, profile: "account-1", from: from);
        await client.TryKeepActorAsync(id, null, choice, new ActorEffective("acp-stub", "opus", null, choice).Asked);
        var ended = Observation.ChoiceNotOffered("acp-stub", new ModelRefusal("model", "opus", AgentOffers.RefusedCode));
        await client.AdvanceAsync(id, ended.State, ended.Note, parts: ended.Parts);
        Assert.Null(AgentOffers.Refused(_home, "acp-stub", null, "account-1", "opus", DateTimeOffset.UtcNow));
        ledger.For(id).Say("w1", "Make the port configurable.");
        return id;
    }

    /// <summary>
    /// 🔴 Interrupted between its two records (§3.5(b)): the refused record's go-on, the come-up's, is a fresh start of its actor, never a
    /// resume: the walk's answer opens on account-2, continuing it by <c>not-offered</c>, its waiting words its first message, once, and
    /// taken off the refused record by their ids.
    /// </summary>
    [Fact]
    public async Task A_refused_chat_s_go_on_after_the_app_stopped_is_a_fresh_start_of_its_actor()
    {
        var config = Config("account-1:opus", "chat", [new ActorChoice("acp-stub", "opus")], "account-1", "account-2");
        var (runner, ledger, client, _) = Runner();
        using var r = runner;
        using var c = client;
        var refused = await RefusedAsync(ledger, client, "1");

        await runner.TakeUpAsync();

        await Poll.Until(() => Prompts("account-2").Length == 1, () => Seen(ledger, refused));
        // After the process a conversation continuing another in its tree is told (MAKERTRAIL1), its words: no brief, since none began.
        var handed = Assert.Single(Prompts("account-2"));
        Assert.EndsWith("Make the port configurable.", handed, StringComparison.Ordinal);
        Assert.DoesNotContain("You continue session", handed, StringComparison.Ordinal);
        Assert.DoesNotContain(StubFile.Lines(Heard), line => line.StartsWith("account-1 ", StringComparison.Ordinal));
        var next = (await client.RecordAsync("c2"))!;
        Assert.Equal(("account-2", refused, HandOverWhy.NotOffered, _root), (next.Profile, next.From, next.FromWhy, next.Tree));
        await Poll.Until(() => ledger.Said(refused).Count == 0, () => Seen(ledger, refused));
        Assert.Equal("failed", ledger.State(refused));
    }

    /// <summary>
    /// 🔴 A pick is never walked afresh (round 4, Codex 2): a picked start interrupted after its refusal, account-2 free and offering the
    /// model, the come-up's go-on never runs it there: with no other entry of the picked agent, it waits saying why, its words kept.
    /// </summary>
    [Fact]
    public async Task A_refused_pick_s_go_on_never_runs_on_another_account()
    {
        var config = Config("account-1:opus", "chat", [new ActorChoice("acp-stub", "opus")], "account-1", "account-2");
        var (runner, ledger, client, _) = Runner();
        using var r = runner;
        using var c = client;
        var refused = await RefusedAsync(ledger, client, "pick");

        var taken = await runner.TakeUpAsync();

        Assert.All(taken, start => Assert.Null(start.SessionId));
        Assert.Equal(1, ledger.Count);
        Assert.Equal(["w1"], ledger.Said(refused));
        Assert.Empty(StubFile.Lines(Heard));
        Assert.Contains(Events.After(refused, 0).Events, e => e.Parts?.Any(part => part.Code == NoteCodes.ChatLimitNotMoved.Code) == true);
    }

    /// <summary>
    /// 🔴 A refused continuation rebuilds its brief (round 4, Codex 3): part A held a conversation, replacement B was refused before its first
    /// prompt, and the app stopped before C opened. C's first prompt carries a fact only A's history held, then the words, once.
    /// </summary>
    [Fact]
    public async Task A_refused_continuation_s_go_on_is_handed_the_brief_of_its_line_then_its_words()
    {
        var config = Config("account-1:opus", "chat", [new ActorChoice("acp-stub", "opus")], "account-1", "account-2");
        var (runner, ledger, client, _) = Runner();
        using var r = runner;
        using var c = client;
        ledger.Chat("c0", "completed", "acp-stub", _root, profile: "account-1");
        var events = Events;
        events.Keep("c0", new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = "The port lives in deploy/ports.yaml." }, say: null);
        events.Keep("c0", new SessionEvent { Kind = SessionEventKind.Message, Text = "Noted: deploy/ports.yaml holds the port." }, say: null);
        var refused = await RefusedAsync(ledger, client, "1", new ContinuedFrom("c0", HandOverWhy.Limit, HandOverBy.Daoris));

        await runner.TakeUpAsync();

        await Poll.Until(() => Prompts("account-2").Length == 1, () => Seen(ledger, refused));
        var handed = Assert.Single(Prompts("account-2"));
        Assert.Contains("deploy/ports.yaml", handed, StringComparison.Ordinal);
        Assert.True(
            handed.IndexOf("deploy/ports.yaml", StringComparison.Ordinal) < handed.IndexOf("Make the port configurable.", StringComparison.Ordinal),
            handed);
        Assert.Single(StubFile.Lines(Heard), line => line.Contains("Make the port configurable.", StringComparison.Ordinal));
        // The ledger's third record: part A, the refused B, and C.
        var next = (await client.RecordAsync("c3"))!;
        Assert.Equal((refused, HandOverWhy.NotOffered, "account-2"), (next.From, next.FromWhy, next.Profile));
    }

    /// <summary>
    /// A stand-in ACP agent on the stub's accounts: it writes each method and prompt down with its account, lists three models, and refuses
    /// at the set each model its second argument names for its account.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-pending-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { basename } from 'node:path';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [heard, refuses = ''] = process.argv.slice(2);
            const home = process.env.DAORIS_STUB_CONFIG_DIR ?? '';
            const account = home ? basename(home) : '(own)';
            const refused = new Set(refuses.split(',').filter((pair) => pair.startsWith(account + ':')).map((pair) => pair.split(':')[1]));
            const note = (text) => appendFileSync(heard, `${account} ${text}\n`);
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            let model = 'default';
            const options = () => [{ id: 'model', name: 'Model', category: 'model', type: 'select', currentValue: model,
              options: [{ value: 'default', name: 'Default' }, { value: 'opus', name: 'Opus' }, { value: 'sonnet', name: 'Sonnet' }] }];
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                note('session/new');
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-pending', configOptions: options() } });
              } else if (frame.method === 'session/set_config_option') {
                const value = frame.params.value;
                if (refused.has(value)) {
                  note(`refused: model=${value}`);
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: 'Internal error',
                    data: { details: `Invalid value for config option model: ${value}` } } });
                } else {
                  note(`set: model=${value}`);
                  model = value;
                  send({ jsonrpc: '2.0', id: frame.id, result: { configOptions: options() } });
                }
              } else if (frame.method === 'session/prompt') {
                const text = frame.params.prompt.filter((block) => block.type === 'text').map((block) => block.text).join(' | ');
                // On one line, so a brief's lines and the words after it read as the one prompt they are.
                note('prompt: ' + text.replace(/\s*\n\s*/g, ' '));
                await new Promise((resolve) => setTimeout(resolve, 50));
                send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: 'acp-pending',
                  update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'heard ' + text } } } });
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
