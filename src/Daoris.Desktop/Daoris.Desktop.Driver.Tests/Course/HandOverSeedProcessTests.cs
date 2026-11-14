using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// With real conversations on the protocol door: a hand-over to another agent whose provider seeds hands its new part the session written as
/// that agent's own conversation in its account's home, resumed by its id with Daoris's word for the move's reason and no brief; the part it
/// left says its conversation is written there, never carried.
/// </summary>
/// <remarks>
/// The first conversation runs on <c>acp-stub</c>; it is handed to <c>alt-acp-stub</c>, the second stand-in's protocol door, whose one account
/// is a home of Daoris's. A stand-in seeding bound for that provider writes each conversation as a file there, and the second stand-in resumes
/// only a conversation it finds in its home, so a resume proves the seed. No model, no account, no sign-in.
/// </remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Harness)]
public sealed class HandOverSeedProcessTests : IDisposable
{
    private static readonly ProviderId Alt = new("alt-stub");

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-handover-seed-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    private readonly Seeding _seeding = new();

    public HandOverSeedProcessTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(AltHome);
        new HarnessSettings().WithRotation("alt-stub", ["account-1"]).Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
        ConversationSeedings.Bind(Alt, _seeding);
    }

    public void Dispose()
    {
        ConversationSeedings.Unbind(Alt);
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The stand-in's seeding: the session's words and context entries as a file in its sessions folder, resumed under the seed's id.</summary>
    private sealed class Seeding : IConversationSeeding
    {
        public List<SeedAsk> Asked { get; } = [];

        public bool Seeds(string door) => door == "alt-acp-stub";

        public SeedPlacement Place(SeedAsk ask)
        {
            Asked.Add(ask);
            var name = Path.Combine("alt-sessions", ask.Conversation);
            Directory.CreateDirectory(Path.Combine(ask.Home, "alt-sessions"));
            File.WriteAllLines(Path.Combine(ask.Home, name), [.. ask.History.Where(e => e.Text is { Length: > 0 }).Select(e => $"{e.Kind}: {e.Text}"), .. ask.Context.Select(e => $"{e.Kind}: {e.Text}")]);
            return new SeedPlacement(ask.Conversation, [name], null);
        }

        public void Remove(string home, SeedPlacement placed)
        {
            foreach (var name in placed.Files) File.Delete(Path.Combine(home, name));
        }
    }

    private string AltHome => HarnessSettings.ProfileHome(_home, "alt-stub", "account-1");

    private string Log => Path.Combine(_home, "agent.log");

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    /// <summary>
    /// 🔴 The person's press hands the conversation to the other agent as that agent's own conversation: written in its account's home from
    /// the session, resumed by its id with the press's word and no brief; seeded on both parts and in the machine log as not briefed.
    /// </summary>
    [Fact]
    public async Task A_hand_over_to_another_agent_hands_it_the_session_as_its_own_conversation()
    {
        var ledger = new ChatLedger { Opens = 2 }.Register("engine", _root);
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>>
            {
                ["acp-stub"] = ["node", Agent(), Log],
                ["alt-acp-stub"] = ["node", Agent(), Log],
                ["stub"] = ["node", TextAgent()],
                ["alt-stub"] = ["node", TextAgent()],
            },
        };
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        using var client = ledger.Client();
        var lines = new List<AccountLine>();
        client.AccountLined += line => { lock (lines) lines.Add(line); };
        var adapters = ProviderRegistry.Built();
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: Events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
        var old = (await runner.StartAsync("engine", "acp-stub", config)).SessionId!;
        await Poll.Until(() => ledger.State(old) == "working", () => Seen(ledger, old));
        Assert.True(runner.Say(old, "Make the port configurable."));
        await Poll.Until(() => Events.After(old, 0).Events.Any(e => e.Kind == SessionEventKind.Turn), () => Seen(ledger, old));
        await Poll.Until(() => !runner.Taking(old), () => Seen(ledger, old));

        var handed = await runner.HandOverAsync(old, "alt-acp-stub", null, HandOverWhy.Press, HandOverBy.Person, config);

        Assert.True(handed.Kept, handed.Message);
        var next = handed.SessionId!;
        Assert.Equal(("alt-acp-stub", "account-1"), ((await client.RecordAsync(next))!.Adapter, (await client.RecordAsync(next))!.Profile));
        await Poll.Until(() => Prompts("account-1").Any(), () => Seen(ledger, next));
        var seed = Assert.Single(_seeding.Asked);
        Assert.Equal(("alt-acp-stub", AltHome, _root), (seed.Door, seed.Home, seed.Tree));
        Assert.Contains(seed.Context, e => e.Kind == ContextKind.Work && e.Text == "Make the port configurable.");
        Assert.Contains("Make the port configurable.", File.ReadAllText(Path.Combine(AltHome, "alt-sessions", seed.Conversation)));
        var resumed = Assert.Single(Heard(), line => line["method"]!.GetValue<string>() is "session/resume" or "session/load");
        Assert.Equal((seed.Conversation, "account-1"), (Text(resumed, "conversation"), Text(resumed, "account")));
        Assert.DoesNotContain(Heard(), line => line["method"]!.GetValue<string>() == "session/new" && Text(line, "account") == "account-1");
        var blocks = Blocks(Prompts("account-1").First());
        Assert.Contains("This conversation goes on here because the person asked for it.", blocks);
        Assert.DoesNotContain("You continue session", blocks);

        Assert.Contains(Codes(old), part => part.Code == "moved.seeded-on" && Equals(part.Value("why"), "press"));
        Assert.DoesNotContain(Codes(old), part => part.Code is "moved.carried-on" or "chat.handed-over");
        Assert.Contains(Codes(next), part => part.Code == "moved.seeded-from" && Equals(part.Value("session"), old));
        Assert.DoesNotContain(Codes(next), part => part.Code is "chat.taken-over" or "moved.from");
        lock (lines)
        {
            var moved = Assert.Single(lines, line => line.Event == "session.moved");
            Assert.Contains(moved.Data, field => field is ("briefed", false));
            Assert.Contains(moved.Data, field => field is ("carry", "seeded"));
        }

        await Poll.Until(() => new MoveStore(_home).Standing(old) is null, () => $"the store still holds {old}'s hand-over; {Seen(ledger, old)}");
        Assert.Equal(("delivered", next), (Assert.Single(new MoveStore(_home).Of(old)).PhaseWord, Assert.Single(new MoveStore(_home).Of(old)).Destination));
    }

    /// <summary>
    /// A hand-over to another account of its own agent (what the curve's move is) is resumed as a conversation written in the new account's
    /// home, no brief: seeded on both parts and in the machine log as not briefed.
    /// </summary>
    [Fact]
    public async Task A_hand_over_to_another_account_of_its_own_agent_resumes_a_seeded_conversation_and_no_brief()
    {
        new HarnessSettings().WithRotation("alt-stub", ["account-1", "account-2"]).Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
        var second = HarnessSettings.ProfileHome(_home, "alt-stub", "account-2");
        Directory.CreateDirectory(second);
        var ledger = new ChatLedger { Opens = 2 }.Register("engine", _root);
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>>
            {
                ["alt-acp-stub"] = ["node", Agent(), Log],
                ["alt-stub"] = ["node", TextAgent()],
            },
        };
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        using var client = ledger.Client();
        var adapters = ProviderRegistry.Built();
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: Events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
        var old = (await runner.StartAsync("engine", "alt-acp-stub", config, null, "account-1")).SessionId!;
        await Poll.Until(() => ledger.State(old) == "working", () => Seen(ledger, old));
        Assert.True(runner.Say(old, "Make the port configurable."));
        await Poll.Until(() => Events.After(old, 0).Events.Any(e => e.Kind == SessionEventKind.Turn), () => Seen(ledger, old));
        await Poll.Until(() => !runner.Taking(old), () => Seen(ledger, old));

        var handed = await runner.HandOverAsync(old, "alt-acp-stub", "account-2", HandOverWhy.Press, HandOverBy.Person, config);

        Assert.True(handed.Kept, handed.Message);
        var next = handed.SessionId!;
        await Poll.Until(() => Prompts("account-2").Any(), () => Seen(ledger, next));
        var seed = Assert.Single(_seeding.Asked);
        Assert.Equal(("alt-acp-stub", second), (seed.Door, seed.Home));
        var resumed = Assert.Single(Heard(), line => line["method"]!.GetValue<string>() is "session/resume" or "session/load");
        Assert.Equal((seed.Conversation, "account-2"), (Text(resumed, "conversation"), Text(resumed, "account")));
        Assert.DoesNotContain(Heard(), line => line["method"]!.GetValue<string>() == "session/new" && Text(line, "account") == "account-2");
        Assert.DoesNotContain("You continue session", Blocks(Prompts("account-2").First()));
        Assert.Contains(Codes(old), part => part.Code == "moved.seeded-on");
    }

    private JsonObject[] Heard() => [.. StubFile.Lines(Log).Select(line => JsonNode.Parse(line)!.AsObject())];

    private IEnumerable<JsonObject> Prompts(string account) =>
        Heard().Where(line => line["method"]!.GetValue<string>() == "session/prompt" && Text(line, "account") == account);

    private static string? Text(JsonObject line, string key) => line[key]?.GetValue<string>();

    private static string Blocks(JsonObject prompt) => string.Join("\n", prompt["blocks"]!.AsArray().Select(block => block!.GetValue<string>()));

    private IEnumerable<NotePart> Codes(string session) => Events.After(session, 0).Events.SelectMany(e => e.Parts ?? []);

    private string Seen(ChatLedger ledger, string id) =>
        $"records {ledger.Count}, state {ledger.State(id)}, record [{string.Join(" | ", Events.After(id, 0).Events.Select(e => $"{e.Kind}:{e.Origin}:{e.Text}"))}]"
        + $"\nheard: {string.Join("\n", StubFile.Lines(Log))}";

    /// <summary>
    /// Both stand-ins on the protocol door: each keeps its conversations as files under its own home, the first's in the tool's own (named
    /// <c>own</c>), the second's under its account's, and resumes only one it finds there.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-seed-agent.mjs");
        File.WriteAllText(script, $$"""
            import { createInterface } from 'node:readline';
            import { appendFileSync, existsSync, mkdirSync, writeFileSync } from 'node:fs';
            import { basename, join } from 'node:path';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [log] = process.argv.slice(2);
            const alt = process.env.{{AltStubProvider.ConfigVariable}};
            const home = alt ?? process.env.{{StubProvider.ConfigVariable}} ?? '';
            const account = alt ? basename(alt) : 'own';
            const kept = (id) => join(home, 'alt-sessions', id);
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const heard = (what) => appendFileSync(log, JSON.stringify({ ...what, account }) + '\n');
            let conversation = null;
            for await (const line of createInterface({ input: process.stdin })) {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1,
                  agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                conversation = 'conv-' + account;
                heard({ method: 'session/new', conversation });
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/resume' || frame.method === 'session/load') {
                const id = frame.params?.sessionId;
                heard({ method: frame.method, conversation: id });
                if (home && existsSync(kept(id))) {
                  conversation = id;
                  send({ jsonrpc: '2.0', id: frame.id, result: {} });
                } else {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32002, message: 'Resource not found: ' + id } });
                }
              } else if (frame.method === 'session/prompt') {
                const blocks = (frame.params?.prompt ?? []).filter((block) => block.type === 'text').map((block) => block.text);
                heard({ method: 'session/prompt', conversation: frame.params?.sessionId, blocks });
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

    /// <summary>The native doors' command: it answers the sign-in question for each stand-in's accounts.</summary>
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
