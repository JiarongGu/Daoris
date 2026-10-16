using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT1a-i (D163 §3) with real conversations: a conversation handed to another agent ends, and a new one starts in the same
/// tree on that agent, told Daoris's briefing and none of the person's words; both records say where it went and where it came
/// from. Idle it is taken at once; while a turn runs the box waits and is taken as the turn ends; a box left on disk is taken as
/// a new runner comes up.
/// </summary>
/// <remarks>
/// The first conversation runs on the <c>acp-stub</c> adapter (a node stand-in on the protocol door) and is handed to the
/// built-in <c>stub</c> adapter (a text-only node stand-in that echoes each line it reads): the two agents the fixtures can
/// register as different ones. The echo is what the new conversation's transcript shows it was told. The in-process ledger of
/// <see cref="ChatGoOnTests"/>; a real process each. Written without a run: the merge's Process half runs them.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class HandOverProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-handover-proc-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public HandOverProcessTests()
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

    /// <summary>The machine's choices, saved where a runner coming up reads them: both stand-ins' commands.</summary>
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
        config.Save(DaorisHome.Entry(_home, HomeEntry.Driver));
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
        $"heard [{string.Join(" | ", StubFile.Lines(Heard))}], records {ledger.Count}, state {ledger.State(id)}, "
        + $"record [{string.Join(" | ", events.After(id, 0).Events.Select(e => $"{e.Kind}:{e.Text}"))}]";

    private static bool Ended(string state) => state is "completed" or "stopped" or "failed";

    private static string? CodeOf(SessionEvent e) => e.Kind == SessionEventKind.Note ? e.Parts?.FirstOrDefault(part => part.Code is not null)?.Code : null;

    private string NewSessionOf(ChatLedger ledger, string old)
    {
        var events = Events;
        var handed = events.After(old, 0).Events.Last(e => CodeOf(e) == "chat.handed-over");
        return (string)handed.Parts![0].Value("session")!;
    }

    /// <summary>
    /// 🔴 Idle, a hand-over is taken at once: the first record ends, the second starts in the old one's tree on the other agent,
    /// its first prompt is the briefing (Daoris's, never the person's words) and its opening note is `chat.taken-over`; the old
    /// record's last line is `chat.handed-over`.
    /// </summary>
    [Fact]
    public async Task An_idle_conversation_is_handed_over_at_once_to_a_new_one_in_its_tree_told_the_briefing()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        var events = Events;
        using var client = ledger.Client();
        using var runner = Runner(client, events);
        var old = (await runner.StartAsync("engine", "acp-stub", config)).SessionId!;
        await Poll.Until(() => ledger.State(old) == "working", () => Seen(ledger, events, old));
        Assert.True(runner.Say(old, "Make the port configurable."));
        await Poll.Until(() => events.After(old, 0).Events.Any(e => e.Kind == SessionEventKind.Turn), () => Seen(ledger, events, old));
        await Poll.Until(() => !runner.Taking(old), () => Seen(ledger, events, old));

        var handed = await runner.HandOverAsync(old, "stub", null, HandOverWhy.Press, HandOverBy.Person, config);

        Assert.True(handed.Kept, handed.Message);
        var next = handed.SessionId!;
        await Poll.Until(() => Ended(ledger.State(old)), () => Seen(ledger, events, old));
        Assert.Equal(2, ledger.Count);
        Assert.Equal(_root, (await client.RecordAsync(next))!.Tree);
        // The new record names the one it continues, why and who asked (MIXAGENT1a-iii).
        var link = (await client.RecordAsync(next))!;
        Assert.Equal((old, "press", "person"), (link.From, link.FromWhy, link.FromBy));
        // The briefing is said after the old conversation ended, so its note is waited for.
        await Poll.Until(() => events.After(next, 0).Events.Any(e => CodeOf(e) == "chat.taken-over"), () => Seen(ledger, events, next));
        var record = events.After(next, 0).Events;
        Assert.Equal("chat.taken-over", record.Select(CodeOf).First(code => code is not null));
        Assert.DoesNotContain(record, e => e.Kind == SessionEventKind.User && e.Origin == "person");
        var opening = record.First(e => CodeOf(e) == "chat.taken-over").Parts![0];
        Assert.Equal(("acp-stub", old, "press"), (opening.Value("agent"), opening.Value("session"), opening.Value("why")));
        await Poll.Until(
            () => StubFile.Text(DaorisHome.Entry(_home, HomeEntry.Sessions,$"{next}.log")).Contains($"heard: You continue the acp-stub conversation `{old}`"),
            () => Seen(ledger, events, next));
        Assert.Contains("Make the port configurable.", StubFile.Text(DaorisHome.Entry(_home, HomeEntry.Sessions,$"{next}.log")));
        Assert.Equal("chat.handed-over", events.After(old, 0).Events.Select(CodeOf).Last(code => code is not null));
        Assert.Equal(next, NewSessionOf(ledger, old));
        // The take clears the box last, after the old one's last line: poll, as CurveMoveProcessTests does.
        await Poll.Until(() => new HandOverBox(_home).Read(old) is null, () => $"the box still holds {old}'s hand-over; {Seen(ledger, events, old)}");
    }

    /// <summary>
    /// While a turn runs the box waits, the conversation untouched; as the turn ends (here stopped, which the stub answers
    /// cancelled) the box is taken: the first record ends and the second starts.
    /// </summary>
    [Fact]
    public async Task A_hand_over_asked_during_a_turn_waits_and_is_taken_as_the_turn_ends()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        var events = Events;
        using var client = ledger.Client();
        using var runner = Runner(client, events);
        var old = (await runner.StartAsync("engine", "acp-stub", config)).SessionId!;
        await Poll.Until(() => ledger.State(old) == "working", () => Seen(ledger, events, old));
        Assert.True(runner.Say(old, "hold"));
        await Poll.Until(() => StubFile.Lines(Heard).Contains("prompt: hold") && runner.Taking(old), () => Seen(ledger, events, old));

        var asked = await runner.HandOverAsync(old, "stub", null, HandOverWhy.Press, HandOverBy.Person, config);

        Assert.Equal((null, true, null), (asked.SessionId, asked.Kept, asked.Why));
        Assert.NotNull(new HandOverBox(_home).Read(old));
        Assert.Equal("working", ledger.State(old));
        Assert.Equal(1, ledger.Count);

        await runner.CancelTurnAsync(old);

        await Poll.Until(() => ledger.Count == 2 && Ended(ledger.State(old)) && new HandOverBox(_home).Read(old) is null, () => Seen(ledger, events, old));
        Assert.Equal("chat.handed-over", events.After(old, 0).Events.Select(CodeOf).Last(code => code is not null));
    }

    /// <summary>A box left on disk for an idle conversation (asked while no runner heard it) is taken as a new runner comes up.</summary>
    [Fact]
    public async Task A_box_left_on_disk_is_taken_when_a_new_runner_comes_up()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        var events = Events;
        using var client = ledger.Client();
        string old;
        using (var first = Runner(client, events))
        {
            old = (await first.StartAsync("engine", "acp-stub", config)).SessionId!;
            await Poll.Until(() => ledger.State(old) == "working", () => Seen(ledger, events, old));
            Assert.True(first.Finish(old));
            await Poll.Until(() => ledger.State(old) == "completed", () => Seen(ledger, events, old));
        }

        new HandOverBox(_home).Write(old, new HandOverRequest("stub", null, HandOverWhy.Near, HandOverBy.Daoris, DateTimeOffset.UtcNow));
        using var next = Runner(client, events);

        var taken = await next.TakeUpAsync();

        var started = Assert.Single(taken);
        Assert.NotNull(started.SessionId);
        Assert.Equal(2, ledger.Count);
        Assert.Null(new HandOverBox(_home).Read(old));
        await Poll.Until(() => events.After(started.SessionId!, 0).Events.Any(e => CodeOf(e) == "chat.taken-over"), () => Seen(ledger, events, started.SessionId!));
        var opening = events.After(started.SessionId!, 0).Events.First(e => CodeOf(e) == "chat.taken-over").Parts![0];
        Assert.Equal("near", opening.Value("why"));
        Assert.Equal("chat.handed-over", events.After(old, 0).Events.Select(CodeOf).Last(code => code is not null));
    }

    /// <summary>A stand-in ACP agent: names its conversation <c>conv-1</c>, writes down each method and prompt, answers a prompt after a moment; a prompt <c>hold</c> runs until a cancel answers it.</summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-handover-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [heard] = process.argv.slice(2);
            const note = (text) => appendFileSync(heard, text + '\n');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            let conversation = null;
            const held = [];
            const say = (text) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
              update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text } } } });
            const answer = (id, stopReason) => send({ jsonrpc: '2.0', id, result: { stopReason } });
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
                if (text === 'hold') { held.push(frame.id); return; }
                await new Promise((resolve) => setTimeout(resolve, 100));
                say('heard ' + text);
                answer(frame.id, 'end_turn');
              } else if (frame.method === 'session/cancel') {
                note('session/cancel');
                while (held.length > 0) answer(held.shift(), 'cancelled');
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

    /// <summary>A text-only agent, the stub's door: it echoes each line it reads, so the transcript shows what it was told.</summary>
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
