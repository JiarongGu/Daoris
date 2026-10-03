using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MSG1c (D137 §2.1, §2.2, §4.2) with real conversations: a chat keeps its conversation's id on both doors, and an ended
/// chat the person writes to goes on in its own record and its own conversation, its words its first turn and taken off
/// the record by their ids; an agent that no longer has the conversation sends the record back to how it ended, its words
/// waiting and marked; a chat whose agent takes words at its next step takes one said during a turn at once, and says the
/// agent may have read a word on its way when the person stopped the turn; and a conversation whose agent named no id says
/// so as it ends.
/// </summary>
/// <remarks>
/// A real process (node) speaking each door, with no model and no account, and the in-process ledger of
/// <see cref="ChatGoOnTests"/>. The words reach the record as MSG1d's say door leaves them: kept on its <c>said</c> and
/// shown in its conversation, which is what the chat runner hears.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class ChatGoesOnProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-chat-goes-on-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public ChatGoesOnProcessTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Heard => Path.Combine(_home, "heard.txt");

    private string[] HeardLines() => StubFile.Lines(Heard);

    private SessionEvents Events => new(Path.Combine(_home, "sessions"));

    /// <summary>The machine's choices, saved where the runner reads them when it takes words up by itself.</summary>
    private DriverConfig Config(string adapter, params string[] command)
    {
        var config = DriverConfig.Empty with { Commands = new Dictionary<string, IReadOnlyList<string>> { [adapter] = command } };
        config.Save(Path.Combine(_home, "driver.json"));
        return config;
    }

    private ChatRunner Runner(ServiceClient client, SessionEvents events)
    {
        var adapters = AdapterSet.Built();
        return new ChatRunner(
            client, adapters, _home, new SessionProcesses(Path.Combine(_home, "sessions")), events: events,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
    }

    /// <summary>
    /// The person's words as MSG1d's say door leaves them: kept on the record, and shown in its conversation. All kept
    /// before any is shown, as words said one after another before the runner looks, so what it goes on with is known.
    /// </summary>
    private static void Say(ChatLedger ledger, SessionEvents events, string session, params (string Id, string Text)[] words)
    {
        foreach (var (id, text) in words) ledger.For(session).Say(id, text);
        foreach (var (id, text) in words)
        {
            events.Append(session, new SessionEvent
            {
                Kind = SessionEventKind.User, Origin = "person", Id = id, Text = text, Reaches = "resume", Door = "screen",
            });
        }
    }

    private string Seen(ChatLedger ledger, SessionEvents events, string id) =>
        $"heard [{string.Join(" | ", HeardLines())}], state {ledger.State(id)}, moves [{string.Join(",", ledger.Moves(id))}], "
        + $"record [{string.Join(" | ", events.After(id, 0).Events.Select(e => $"{e.Kind}:{e.Id}:{e.Text}"))}]";

    /// <summary>
    /// 🔴 The owner's case for a chat (D137 §2.2): a conversation that ended is written to, and the same record goes on in
    /// the same conversation. Its id was kept from <c>session/new</c>; its record leaves <c>completed</c> for working; the
    /// agent is sent <c>session/resume</c> on that id with both words as their own blocks; the words leave the record by
    /// their ids and are said again under them; and finishing ends the same record again. One record from start to end.
    /// </summary>
    [Fact]
    public async Task An_ended_chat_written_to_goes_on_in_its_own_conversation_under_the_same_record()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config("acp-stub", "node", Agent(), Heard);
        var events = Events;
        using var client = ledger.Client();
        using var runner = Runner(client, events);

        var id = (await runner.StartAsync("engine", "acp-stub", config)).SessionId!;
        await Poll.Until(() => ledger.State(id) == "working", () => Seen(ledger, events, id));
        Assert.True(runner.Say(id, "first"));
        await Poll.Until(() => events.After(id, 0).Events.Any(e => e.Kind == SessionEventKind.Turn), () => Seen(ledger, events, id));
        Assert.True(runner.Finish(id));
        await Poll.Until(() => ledger.State(id) == "completed", () => Seen(ledger, events, id));
        Assert.Equal(new HarnessConversation("acp-stub", "conv-1"), new HarnessConversations(_home).Read(id));

        Say(ledger, events, id, ("w1", "Also log the port."), ("w2", "No, 9090."));
        await Poll.Until(() => ledger.Taken.Count == 1 && HeardLines().Contains("prompt: Also log the port. | No, 9090."), () => Seen(ledger, events, id));
        Assert.True(runner.Finish(id));
        await Poll.Until(() => ledger.State(id) == "completed" && ledger.Moves(id).Count(move => move == "completed") == 2, () => Seen(ledger, events, id));

        Assert.Equal(
            ["initialize", "session/new", "prompt: first", "session/close", "stdin ended",
             "initialize", "session/resume: conv-1", "prompt: Also log the port. | No, 9090.", "session/close", "stdin ended"],
            HeardLines());
        Assert.Equal(1, ledger.Count);
        Assert.Equal([(id, "w1,w2")], ledger.Taken);
        Assert.Empty(ledger.Said(id));
        var taken = events.After(id, 0).Events
            .Where(e => e.Kind == SessionEventKind.User && e.Reaches is null && e.Id is not null)
            .Select(e => (e.Id, e.Text, e.Origin));
        Assert.Equal([("w1", "Also log the port.", "person"), ("w2", "No, 9090.", "person")], taken);
        Assert.Contains(events.After(id, 0).Events, e => e.Kind == SessionEventKind.Note && e.Text!.Contains("resumed on `acp-stub`"));
        Assert.Contains("carrying on: Also log the port.", StubFile.Text(Path.Combine(_home, "sessions", $"{id}.log")));
        Assert.Contains("heard first", StubFile.Text(Path.Combine(_home, "sessions", $"{id}.log")));
    }

    /// <summary>
    /// An agent that no longer has the conversation refuses to resume it (<c>resource_not_found</c>): nothing went, so the
    /// record goes back to how it ended, its note saying why; the words stay waiting, marked; and its conversation says it
    /// cannot go on in it, by the code the page words (`gone`), with the words' ids.
    /// </summary>
    [Fact]
    public async Task A_conversation_the_agent_no_longer_has_sends_the_record_back_with_its_words_waiting()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config("acp-stub", "node", Agent(), Heard);
        var events = Events;
        using var client = ledger.Client();
        using var runner = Runner(client, events);

        var id = (await runner.StartAsync("engine", "acp-stub", config)).SessionId!;
        await Poll.Until(() => ledger.State(id) == "working", () => Seen(ledger, events, id));
        Assert.True(runner.Finish(id));
        await Poll.Until(() => ledger.State(id) == "completed", () => Seen(ledger, events, id));

        Config("acp-stub", "node", Agent(), Heard, "gone");
        Say(ledger, events, id, ("w1", "Also log the port."));
        await Poll.Until(
            () => events.After(id, 0).Events.Any(e => e.Kind == SessionEventKind.Note && e.Why == ContinueWhy.Gone),
            () => Seen(ledger, events, id));
        await Poll.Until(() => ledger.Moves(id).Count(move => move == "completed") == 2, () => Seen(ledger, events, id));

        Assert.Equal(["starting", "working", "completed", "working", "completed"], ledger.Moves(id));
        Assert.Contains("It cannot go on in this session, because the agent no longer has its conversation.", ledger.Note(id));
        Assert.Equal(["w1"], ledger.Said(id));
        Assert.Empty(ledger.Taken);
        Assert.Equal(ContinueWhy.Gone, new GoOnMarks(_home).Read(id)!.Why);
        Assert.Equal(["w1"], events.After(id, 0).Events.Single(e => e.Why == ContinueWhy.Gone).Words!);
        Assert.DoesNotContain(HeardLines(), line => line.StartsWith("prompt: Also", StringComparison.Ordinal));
    }

    /// <summary>
    /// 🔴 The native door (D137 §4.2): a chat keeps the id its harness's <c>init</c> line names, and an ended one goes on
    /// with <c>--resume &lt;id&gt;</c> and the words as its first message, joined by a blank line, taken off the record once its
    /// harness opened the conversation.
    /// </summary>
    [Fact]
    public async Task A_native_chat_keeps_its_conversation_and_goes_on_with_resume()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var log = Path.Combine(_home, "native.log");
        var config = Config("claude-code", "node", NativeHarness(), log);
        var events = Events;
        using var client = ledger.Client();
        using var runner = Runner(client, events);

        var id = (await runner.StartAsync("engine", "claude-code", config)).SessionId!;
        await Poll.Until(() => ledger.State(id) == "working", () => Seen(ledger, events, id));
        Assert.True(runner.Say(id, "first"));
        await Poll.Until(() => new HarnessConversations(_home).Read(id) is not null, () => Seen(ledger, events, id));
        Assert.True(runner.Finish(id));
        await Poll.Until(() => ledger.State(id) == "completed", () => Seen(ledger, events, id));
        Assert.Equal(new HarnessConversation("claude-code", "native-1"), new HarnessConversations(_home).Read(id));

        Say(ledger, events, id, ("w1", "Also log the port."), ("w2", "No, 9090."));
        await Poll.Until(() => ledger.Taken.Count == 1, () => $"{Seen(ledger, events, id)}; native [{string.Join(" | ", StubFile.Lines(log))}]");
        Assert.True(runner.Finish(id));
        await Poll.Until(() => ledger.Moves(id).Count(move => move == "completed") == 2, () => Seen(ledger, events, id));

        Assert.Equal(
            ["resume=none", "message=first", "resume=native-1", "message=Also log the port.\\n\\nNo, 9090."],
            StubFile.Lines(log));
        Assert.Equal([(id, "w1,w2")], ledger.Taken);
        Assert.Equal(1, ledger.Count);
    }

    /// <summary>
    /// 🔴 A chat on the next-step door (D137 §2.1): its agent says it takes a prompt during a turn, so a word said while a
    /// turn runs goes at once as a second prompt — never queued — shown waiting with its reach, and taken where the agent
    /// hands the turn off to it. The turn lasts until the word is answered.
    /// </summary>
    [Fact]
    public async Task A_chat_whose_agent_takes_words_at_its_next_step_takes_one_said_during_a_turn_at_once()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config("acp-stub", "node", Agent(), Heard, "queues");
        var events = Events;
        var queued = new List<ChatQueue>();
        using var client = ledger.Client();
        using var runner = Runner(client, events);
        runner.QueueChanged += (_, queue) => { lock (queued) queued.Add(queue); };

        var id = (await runner.StartAsync("engine", "acp-stub", config)).SessionId!;
        await Poll.Until(() => ledger.State(id) == "working", () => Seen(ledger, events, id));
        Assert.True(runner.Say(id, "hold"));
        await Poll.Until(() => HeardLines().Contains("prompt: hold"), () => Seen(ledger, events, id));

        Assert.Equal("next-step", runner.Reach(id));
        Assert.True(runner.Say(id, "PINEAPPLE", out var reaches));
        Assert.Equal("next-step", reaches);
        await Poll.Until(() => events.After(id, 0).Events.Count(e => e.Kind == SessionEventKind.Turn) == 2, () => Seen(ledger, events, id));

        var said = events.After(id, 0).Events
            .Where(e => e.Kind is SessionEventKind.User or SessionEventKind.Message or SessionEventKind.Turn)
            .Select(e => e.Kind switch
            {
                SessionEventKind.User => $"{e.Origin}{(e.Reaches is { } reach ? $"({reach})" : "")}: {e.Text}",
                SessionEventKind.Message => $"agent: {e.Text}",
                _ => $"turn: {e.StopReason}",
            });
        Assert.Equal(
            ["person: hold", "person(next-step): PINEAPPLE", "turn: end_turn", "person: PINEAPPLE", "agent: heard PINEAPPLE", "turn: end_turn"],
            said);
        var shown = events.After(id, 0).Events.Where(e => e.Text == "PINEAPPLE" && e.Kind == SessionEventKind.User).Select(e => e.Id).ToList();
        Assert.Equal(2, shown.Count);
        Assert.Equal(shown[0], shown[1]);
        lock (queued) Assert.DoesNotContain(queued, queue => queue.Queued.Any(message => message.Text == "PINEAPPLE"));
        Assert.True(runner.Finish(id));
    }

    /// <summary>
    /// A person's stop is never refused, even with a word on its way (D137 §2.1): the turn stops, and the conversation says
    /// of the word that the agent may have read it and its answer was not kept (STEER1 §3), naming it by its id.
    /// </summary>
    [Fact]
    public async Task A_stop_with_a_word_on_its_way_says_the_agent_may_have_read_it()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config("acp-stub", "node", Agent(), Heard, "queues-hold");
        var events = Events;
        using var client = ledger.Client();
        using var runner = Runner(client, events);

        var id = (await runner.StartAsync("engine", "acp-stub", config)).SessionId!;
        await Poll.Until(() => ledger.State(id) == "working", () => Seen(ledger, events, id));
        Assert.True(runner.Say(id, "hold"));
        await Poll.Until(() => HeardLines().Contains("prompt: hold"), () => Seen(ledger, events, id));
        Assert.True(runner.Say(id, "PINEAPPLE", out _));
        await Poll.Until(() => HeardLines().Contains("prompt: PINEAPPLE"), () => Seen(ledger, events, id));

        var stop = await runner.CancelTurnAsync(id);

        Assert.True(stop.Cancelled);
        var word = events.After(id, 0).Events.First(e => e.Reaches == "next-step").Id!;
        await Poll.Until(
            () => events.After(id, 0).Events.Any(e => e.Kind == SessionEventKind.Note && e.Text!.Contains("may have read")),
            () => Seen(ledger, events, id));
        Assert.Equal([word], events.After(id, 0).Events.Single(e => e.Text?.Contains("may have read") == true).Words!);
        Assert.True(runner.Finish(id));
    }

    /// <summary>
    /// A conversation whose agent named no conversation (a text door) says so as it ends (MSG1c): words written to it once
    /// it ended cannot go on in it, which its record says before anyone writes.
    /// </summary>
    [Fact]
    public async Task A_chat_whose_agent_named_no_conversation_says_so_as_it_ends()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config("stub", "node", TextAgent());
        var events = Events;
        using var client = ledger.Client();
        using var runner = Runner(client, events);

        var id = (await runner.StartAsync("engine", "stub", config)).SessionId!;
        await Poll.Until(() => ledger.State(id) == "working", () => Seen(ledger, events, id));
        Assert.True(runner.Finish(id));
        await Poll.Until(() => ledger.State(id) == "completed", () => Seen(ledger, events, id));

        Assert.EndsWith(ChatRunner.NotKept, ledger.Note(id));
        Assert.Null(new HarnessConversations(_home).Read(id));
    }

    /// <summary>
    /// A stand-in ACP agent: it names its conversation <c>conv-1</c> and advertises <c>session/resume</c>; it writes down
    /// each method, a resume's id, and each prompt's text blocks joined by <c> | </c>, and answers each prompt after a
    /// moment. <c>gone</c> refuses a resume as one it no longer has; <c>queues</c> says it takes prompts during a turn and
    /// hands a held turn off to the next prompt, as <c>claude-agent-acp</c> was measured doing (STEER1); <c>queues-hold</c>
    /// holds both until a cancel answers them <c>cancelled</c>. A prompt <c>hold</c> runs until something hands it off.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-go-on-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [heard, mode] = process.argv.slice(2);
            const note = (text) => appendFileSync(heard, text + '\n');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const queues = mode === 'queues' || mode === 'queues-hold';
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
                const agentCapabilities = { loadSession: true, sessionCapabilities: { resume: {}, close: {} } };
                if (queues) agentCapabilities._meta = { claudeCode: { promptQueueing: true } };
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities } });
              } else if (frame.method === 'session/new') {
                note('session/new');
                conversation = 'conv-1';
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/resume' || frame.method === 'session/load') {
                note(`${frame.method}: ${frame.params.sessionId}`);
                if (mode === 'gone') {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32002, message: 'Resource not found: ' + frame.params.sessionId } });
                } else {
                  conversation = frame.params.sessionId;
                  send({ jsonrpc: '2.0', id: frame.id, result: {} });
                }
              } else if (frame.method === 'session/prompt') {
                const text = frame.params.prompt.filter((block) => block.type === 'text').map((block) => block.text).join(' | ');
                note('prompt: ' + text);
                if (text === 'hold') { held.push(frame.id); return; }
                if (mode === 'queues-hold' && held.length > 0) { held.push(frame.id); return; }
                if (queues && held.length > 0) {
                  // The words folded in at the next step: the held turn handed off, the work going on as the words'.
                  answer(held.shift(), 'end_turn');
                }
                await new Promise((resolve) => setTimeout(resolve, 100));
                say(conversation === 'conv-1' && text.startsWith('Also') ? 'carrying on: ' + text : 'heard ' + text);
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
            // stdin closed: the ending. Never process.exit (STUB1): the process ends when the loop drains.
            lines.on('close', () => { note('stdin ended'); process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }

    /// <summary>
    /// A stand-in for Claude Code's native door in a conversation: it answers its version and its login question; run on
    /// <c>stream-json</c> input, it writes down what it was resumed with and each message, and names its conversation on
    /// an <c>init</c> line as it takes its first message — <c>native-1</c>, or the one it was resumed on.
    /// </summary>
    private string NativeHarness()
    {
        var script = Path.Combine(_home, "claude.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            const [log, ...argv] = process.argv.slice(2);
            if (argv.includes('--version')) { console.log('2.1.0 (Claude Code)'); process.exit(0); }
            if (argv[0] === 'auth' && argv[1] === 'status') { console.log('{"loggedIn": true}'); process.exit(0); }
            const after = (flag) => { const at = argv.indexOf(flag); return at < 0 ? null : argv[at + 1]; };
            const resume = after('--resume');
            const out = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            appendFileSync(log, `resume=${resume ?? 'none'}\n`);
            let named = false;
            const lines = createInterface({ input: process.stdin });
            lines.on('line', (line) => {
              const frame = JSON.parse(line);
              if (frame.type !== 'user') return;
              const text = frame.message.content.filter((block) => block.type === 'text').map((block) => block.text).join('');
              appendFileSync(log, `message=${text.replace(/\n/g, '\\n')}\n`);
              if (!named) {
                out({ type: 'system', subtype: 'init', session_id: resume ?? 'native-1', cwd: process.cwd(), tools: [], model: 'stub' });
                named = true;
              }
              out({ type: 'assistant', message: { role: 'assistant', content: [{ type: 'text', text: 'heard ' + text }] } });
              out({ type: 'result', subtype: 'success', is_error: false, result: 'ok' });
            });
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }

    /// <summary>A text-only agent, the stub's door: it reads lines until its input ends, and names no conversation.</summary>
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
