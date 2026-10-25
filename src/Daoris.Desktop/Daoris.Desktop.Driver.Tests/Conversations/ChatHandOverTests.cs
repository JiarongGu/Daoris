using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1 a-ii (D212, the design §3.2 step 4): a start the person names an account for keeps, on its binding in the desktop store,
/// that they picked it. The doors that name one are <c>START_CHAT</c>'s <c>profile</c> (with words or without), the terminal's account
/// and the person's hand-over press with an account; an agent's ask, the curve's move and a hand-over with no account named keep none, and
/// a named account on those keeps today's reading. The pick is never a field on the service's record, which the ledger stand-in shows
/// by never being sent one.
/// </summary>
/// <remarks>
/// The conversations run on the <c>acp-stub</c> and <c>stub</c> adapters, node stand-ins on the two doors whose accounts are the stub's, as in
/// <see cref="HandOverProcessTests"/>: no model, no account, no sign-in, no real agent binary. Every case has its own scratch home under
/// <c>_fixtures/</c> and sets no variable of the process's environment, <c>DAORIS_HOME</c> least.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class ChatHandOverTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "chat-hand-over", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public ChatHandOverTests()
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
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private string Heard => Path.Combine(_home, "heard.txt");

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private HarnessConversations Conversations => new(_home);

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

    private ChatRunner Runner(ServiceClient client, SessionEvents events)
    {
        var adapters = AdapterSet.Built();
        return new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    private string Seen(ChatLedger ledger, SessionEvents events, string id) =>
        $"heard [{string.Join(" | ", StubFile.Lines(Heard))}], records {ledger.Count}, state {ledger.State(id)}, picks {string.Join(",", Picked())}, "
        + $"record [{string.Join(" | ", events.After(id, 0).Events.Select(e => $"{e.Kind}:{e.Text}"))}]";

    /// <summary>Every session the store holds a pick for, with its account, as the binding keeps it.</summary>
    private IReadOnlyList<string> Picked()
    {
        using var reader = DesktopStoreReader.Open(DesktopStore.LocationIn(_home));
        return reader is null || reader.Version < 3
            ? []
            : reader.Query("SELECT session || '=' || account FROM binding_picks ORDER BY session", row => row.GetString(0));
    }

    // ——— A hand-over.

    /// <summary>
    /// 🔴 The person's press with an account named is their pick of it: the new conversation opens on it and keeps, on its binding, that the
    /// person picked it. An agent's ask and the curve's move name an account that keeps today's reading, and a press naming none is the walk's
    /// choice: none of them keeps a pick, though each opens on the account the walk or the ask gave.
    /// </summary>
    [Theory]
    [InlineData(HandOverWhy.Press, HandOverBy.Person, "account-2", true)]
    [InlineData(HandOverWhy.Press, HandOverBy.Person, null, false)]
    [InlineData(HandOverWhy.Agent, HandOverBy.Agent, "account-2", false)]
    [InlineData(HandOverWhy.Curve, HandOverBy.Daoris, "account-2", false)]
    public async Task A_hand_over_press_with_an_account_keeps_the_pick_and_no_other_hand_over_does(string why, string by, string? account, bool picked)
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        var events = Events;
        using var client = ledger.Client();
        using var runner = Runner(client, events);
        var old = (await runner.StartAsync("engine", "acp-stub", config, profile: "account-1")).SessionId!;
        await Poll.Until(() => ledger.State(old) == "working", () => Seen(ledger, events, old));
        Assert.True(runner.Say(old, "Make the port configurable."));
        await Poll.Until(() => events.After(old, 0).Events.Any(e => e.Kind == SessionEventKind.Turn), () => Seen(ledger, events, old));
        await Poll.Until(() => !runner.Taking(old), () => Seen(ledger, events, old));

        var handed = await runner.HandOverAsync(old, "stub", account, why, by, config);

        Assert.True(handed.Kept, handed.Message);
        var next = handed.SessionId!;
        // It opened on the account asked for, or the walk's where none was; and a pick is kept only where the person named it.
        var ran = (await client.RecordAsync(next))!.Profile;
        if (account is not null) Assert.Equal(account, ran);
        Assert.Equal(picked, Conversations.PickedOn(next, ran));
        Assert.Equal(picked ? [$"{next}=account-2"] : [], Picked());
        // The old conversation, started with no account named, holds none either.
        Assert.False(Conversations.PickedOn(old, "account-1"));
    }

    /// <summary>
    /// PICKCARRY1: a hand-over of a conversation the person picked an account for to another agent opens a part on that agent's account, which
    /// is not the one the person picked (its name may be the same), so the part keeps no pick.
    /// </summary>
    [Fact]
    public async Task A_hand_over_of_a_picked_conversation_to_another_agent_keeps_no_pick()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        var events = Events;
        using var client = ledger.Client();
        using var runner = Runner(client, events);
        var old = (await runner.StartAsync("engine", "acp-stub", config, profile: "account-1", picked: true)).SessionId!;
        await Poll.Until(() => ledger.State(old) == "working", () => Seen(ledger, events, old));
        Assert.True(runner.Say(old, "Make the port configurable."));
        await Poll.Until(() => events.After(old, 0).Events.Any(e => e.Kind == SessionEventKind.Turn), () => Seen(ledger, events, old));
        await Poll.Until(() => !runner.Taking(old), () => Seen(ledger, events, old));

        // The other agent's walk lands on account-1, the name the person picked, which is another agent's account.
        AccountCooling.Cool(_home, new CoolingEntry("stub", "account-2", DateTimeOffset.UtcNow.AddDays(4), Stated: true, "weekly", DateTimeOffset.UtcNow, null), DateTimeOffset.UtcNow);
        var handed = await runner.HandOverAsync(old, "stub", account: null, HandOverWhy.Press, HandOverBy.Person, config);

        Assert.True(handed.Kept, handed.Message);
        Assert.Equal("account-1", (await client.RecordAsync(handed.SessionId!))!.Profile);
        Assert.False(Conversations.PickedOn(handed.SessionId!, "account-1"), Seen(ledger, events, handed.SessionId!));
        Assert.True(Conversations.PickedOn(old, "account-1"));
    }

    // ——— A start.

    /// <summary>
    /// 🔴 <c>START_CHAT</c>'s <c>profile</c>, with the opening words or without, and the terminal's <c>--profile</c>, are the person's pick: each
    /// keeps it on its binding. A start that names no account, and a start whose account is not the person's (a carry-on's, a replacement's,
    /// a hand-over's from an agent), keeps none.
    /// </summary>
    [Fact]
    public async Task A_start_the_person_names_an_account_for_keeps_the_pick_and_one_they_name_none_for_keeps_none()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        var events = Events;
        using var client = ledger.Client();
        using var runner = Runner(client, events);

        var bare = (await runner.StartAsync("engine", "stub", config, profile: "account-1", picked: true)).SessionId!;
        var words = (await runner.StartWithAsync("engine", "stub", config, "Hello.", [], requestId: null, profile: "account-2")).SessionId!;
        var walked = (await runner.StartAsync("engine", "stub", config)).SessionId!;
        var named = (await runner.StartAsync("engine", "stub", config, profile: "account-1")).SessionId!;

        Assert.True(Conversations.PickedOn(bare, "account-1"), Seen(ledger, events, bare));
        Assert.True(Conversations.PickedOn(words, "account-2"), Seen(ledger, events, words));
        Assert.False(Conversations.PickedOn(walked, (await client.RecordAsync(walked))!.Profile), Seen(ledger, events, walked));
        // An account named by a door that is not the person's: the record runs on it, and no pick is kept.
        Assert.Equal("account-1", (await client.RecordAsync(named))!.Profile);
        Assert.False(Conversations.PickedOn(named, "account-1"), Seen(ledger, events, named));
        Assert.Equal(new[] { $"{bare}=account-1", $"{words}=account-2" }.Order(), Picked().Order());
    }

    // ——— The stand-ins.

    /// <summary>A stand-in ACP agent: names its conversation <c>conv-1</c>, writes down each method and prompt, answers a prompt after a moment.</summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-pick-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [heard] = process.argv.slice(2);
            const note = (text) => appendFileSync(heard, text + '\n');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            let conversation = null;
            const say = (text) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
              update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text } } } });
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                note('initialize');
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                note('session/new');
                conversation = 'conv-1';
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/prompt') {
                const text = frame.params.prompt.filter((block) => block.type === 'text').map((block) => block.text).join(' | ');
                note('prompt: ' + text);
                await new Promise((resolve) => setTimeout(resolve, 100));
                say('heard ' + text);
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.method === 'session/close') {
                note('session/close');
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            lines.on('close', () => { note('stdin ended'); process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }

    /// <summary>A text-only agent, the stub's door: it echoes each line it reads.</summary>
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
