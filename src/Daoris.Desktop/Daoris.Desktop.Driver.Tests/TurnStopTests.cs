using Daoris.Driver;
using StandInService = Daoris.Desktop.Driver.Tests.OrphanedSessionTests.StandInService;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A conversation's turns on both doors (CONV4a): one at a time, a message sent mid-turn waiting for
/// its own, and the person able to stop the turn without ending the session.
/// </summary>
/// <remarks>
/// <para>🔴 <b>The native door wrote a mid-turn message to Claude Code's stdin at once</b>, where the
/// binary may fold it into the turn still running, and the record took it in the middle of that turn.
/// The protocol door already queued (CONV3b). Both doors now share one queue.</para>
///
/// <para>Each stand-in harness writes down what reached it <b>as it arrives</b>, never awaiting inside
/// its reader. A reader that waited out each turn would hide exactly the ordering these tests are
/// about.</para>
///
/// <para>The stand-ins speak the shapes the real binaries do: Claude Code's interrupt is the control
/// request the probe measured (docs/2026-09-25-stream-json-evidence.md, § Stopping a turn), and ACP's
/// is <c>session/cancel</c>, answered by the prompt's own <c>cancelled</c>.</para>
/// </remarks>
public sealed class TurnStopTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-turns-" + Guid.NewGuid().ToString("N")[..8]);

    public TurnStopTests() => Directory.CreateDirectory(Path.Combine(_home, "engine"));

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Heard => Path.Combine(_home, "heard.txt");

    private string[] HeardLines() => StubFile.Lines(Heard);

    /// <summary>
    /// 🔴 The native door holds a mid-turn message until the turn's <c>result</c>, and the record takes
    /// it when it is sent — so the conversation reads as it happened, the same as the protocol door's.
    /// </summary>
    [Fact]
    public async Task A_message_sent_mid_turn_on_the_native_door_waits_for_the_turn_to_end()
    {
        await using var session = await Chat("claude-code");

        Assert.True(session.Runner.Say(session.Id, "first"));
        Assert.True(session.Runner.Say(session.Id, "second"));
        Assert.Equal(["second"], Texts(session.Runner.Queue(session.Id).Queued));

        await Until(() => Turns(session) == 2, () => session.Seen());

        Assert.Equal(["user: first", "result: first", "user: second", "result: second"], HeardLines());
        Assert.Equal(
            ["person: first", "agent: heard first", "turn: end_turn", "person: second", "agent: heard second", "turn: end_turn"],
            Said(session));
        Assert.Empty(session.Runner.Queue(session.Id).Queued);
        // The queue was told as it moved, which is what the page shows as queued.
        // …and whether a turn was in flight, which is what the page's stop-the-turn control follows.
        Assert.Contains(session.Queues, queue => queue.Taking && Texts(queue.Queued).SequenceEqual(["second"]));
        Assert.False(session.Queues[^1].Taking);
        Assert.Empty(session.Queues[^1].Queued);
    }

    /// <summary>
    /// Stop the turn on the native door: the harness's own interrupt, the turn ending as the protocol
    /// door calls it, what was waiting withdrawn and handed back, and the session kept for the next
    /// message.
    /// </summary>
    [Fact]
    public async Task Stopping_a_turn_on_the_native_door_interrupts_it_and_keeps_the_session()
    {
        await using var session = await Chat("claude-code");

        Assert.False(session.Runner.Taking(session.Id));
        Assert.True(session.Runner.Say(session.Id, "hold"));
        Assert.True(session.Runner.Say(session.Id, "after"));
        await Until(() => Said(session).Contains("agent: holding"), () => session.Seen());
        // What the terminal's Ctrl+C asks before it claims the key press.
        Assert.True(session.Runner.Taking(session.Id));

        var stop = await session.Runner.CancelTurnAsync(session.Id);

        Assert.True(stop.Cancelled);
        Assert.Equal(["after"], Texts(stop.Withdrawn));
        await Until(() => Turns(session) == 1, () => session.Seen());
        Assert.Equal("turn: cancelled", Said(session)[^1]);
        await Until(() => !session.Runner.Taking(session.Id), () => session.Seen());

        Assert.True(session.Runner.Say(session.Id, "again"));
        await Until(() => Turns(session) == 2, () => session.Seen());

        Assert.Equal(["user: hold", "interrupt", "result: hold (interrupted)", "user: again", "result: again"], HeardLines());
        Assert.DoesNotContain("person: after", Said(session));
        // The queue is told a turn ended AFTER the record holds it (ChatTurns' own ordering), so the
        // last notice is waited for, as the first turn's is above — read at once, it raced (REV3).
        await Until(() => LastQueue(session) is { Taking: false }, () => session.Seen());
        Assert.Empty(LastQueue(session)!.Queued);
    }

    private static ChatQueue? LastQueue(Session session)
    {
        lock (session.Queues) return session.Queues.Count > 0 ? session.Queues[^1] : null;
    }

    /// <summary>The same stop on the protocol door: <c>session/cancel</c>, and the turn ends on the agent's own word.</summary>
    [Fact]
    public async Task Stopping_a_turn_on_the_protocol_door_cancels_it_and_keeps_the_session()
    {
        await using var session = await Chat("acp-stub");

        Assert.True(session.Runner.Say(session.Id, "hold"));
        Assert.True(session.Runner.Say(session.Id, "after"));
        await Until(() => Said(session).Contains("agent: holding"), () => session.Seen());

        var stop = await session.Runner.CancelTurnAsync(session.Id);

        Assert.True(stop.Cancelled);
        Assert.Equal(["after"], Texts(stop.Withdrawn));
        await Until(() => Turns(session) == 1, () => session.Seen());
        Assert.Equal("turn: cancelled", Said(session)[^1]);

        Assert.True(session.Runner.Say(session.Id, "again"));
        await Until(() => Turns(session) == 2, () => session.Seen());

        Assert.Equal(
            ["initialize", "session/new", "prompt: hold", "session/cancel", "prompt: again"],
            HeardLines());
    }

    /// <summary>
    /// A stop that arrives before the turn reached the agent — the session still opening — withdraws
    /// it rather than sending it: nothing the person said stop to ever starts.
    /// </summary>
    [Fact]
    public async Task A_stop_before_the_turn_reached_the_agent_withdraws_it_instead_of_sending_it()
    {
        await using var session = await Chat("acp-stub", slowOpen: true);

        Assert.True(session.Runner.Say(session.Id, "early"));
        var stop = await session.Runner.CancelTurnAsync(session.Id);

        Assert.False(stop.Cancelled);
        Assert.Equal(["early"], Texts(stop.Withdrawn));

        Assert.True(session.Runner.Say(session.Id, "later"));
        await Until(() => Turns(session) == 1, () => session.Seen());
        Assert.DoesNotContain("prompt: early", HeardLines());
        Assert.Contains("prompt: later", HeardLines());
    }

    /// <summary>Nothing running is an answer, never an error: the page asked a moment late.</summary>
    [Fact]
    public async Task Stopping_when_no_turn_runs_stops_nothing_and_says_so()
    {
        await using var session = await Chat("claude-code");

        var stop = await session.Runner.CancelTurnAsync(session.Id);

        Assert.False(stop.Cancelled);
        Assert.Empty(stop.Withdrawn);
        Assert.Equal(TurnStop.Nothing, await session.Runner.CancelTurnAsync("no-such-session"));
    }

    /// <summary>
    /// Finishing on the native door runs what was waiting first, then ends the input: the person
    /// finishing is not the person withdrawing what they already sent.
    /// </summary>
    [Fact]
    public async Task Finishing_on_the_native_door_runs_the_waiting_turns_before_ending_input()
    {
        await using var session = await Chat("claude-code");

        Assert.True(session.Runner.Say(session.Id, "first"));
        Assert.True(session.Runner.Say(session.Id, "second"));
        Assert.True(session.Runner.Finish(session.Id));
        Assert.False(session.Runner.Say(session.Id, "too late"));

        await Until(() => session.Service.State(session.Id) == "completed", () => session.Seen());
        Assert.Equal(["user: first", "result: first", "user: second", "result: second", "stdin ended"], HeardLines());
    }

    /// <summary>
    /// A door that carries only text has no turn to stop — the driver cannot tell where one ends — and
    /// says so rather than pretending.
    /// </summary>
    [Fact]
    public async Task A_text_only_door_refuses_to_stop_a_turn_it_cannot_see()
    {
        await using var session = await Chat("stub");

        var refused = await Assert.ThrowsAsync<DriverException>(() => session.Runner.CancelTurnAsync(session.Id));

        Assert.Contains("only text", refused.Message);
    }

    /// <summary>
    /// 🔴 REV3, the conversation's half of the driver's reaper: the record refused to move to `working`
    /// (as it does once a stop pressed during `starting` has ended it), the watch concluded the record,
    /// and the harness ran on — untracked, unmarked, until the application exited.
    /// </summary>
    [Fact]
    public async Task A_conversation_the_record_will_not_let_work_does_not_outlive_it()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"), refuses: "working");
        var beat = Path.Combine(_home, "beat.txt");
        var script = Path.Combine(_home, "beating.mjs");
        File.WriteAllText(script, """
            import { writeFileSync } from 'node:fs';
            if (process.argv.includes('--version')) { console.log('stub 1.0.0'); process.exit(0); }
            setInterval(() => writeFileSync(process.argv[2], String(Date.now())), 100).unref();
            await new Promise((resolve) => setTimeout(resolve, 60000));
            """);
        var adapters = AdapterSet.Built();
        using var client = new ServiceClient(service.Url, null);
        var processes = new SessionProcesses(Path.Combine(_home, "sessions"));
        using var runner = new ChatRunner(
            client, adapters, _home, processes,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", script, beat] },
        };

        var start = await runner.StartAsync("engine", "stub", config);
        var id = start.SessionId ?? throw new InvalidOperationException(start.Message);
        await Until(() => service.State(id) == "failed", () => $"state {service.State(id)}");
        await Until(() => processes.Running.Count == 0);

        var before = File.Exists(beat) ? File.ReadAllText(beat) : null;
        await Task.Delay(700);
        var after = File.Exists(beat) ? File.ReadAllText(beat) : null;
        Assert.True(before == after, "the conversation's harness is still beating after its record concluded");
    }

    /// <summary>
    /// 🔴 REV3: "servers every session is handed" (D64) — a conversation on the protocol door got the
    /// plugins' servers on `session/new`, and a driven or intake session on the pipe got a file, but a
    /// conversation on Claude Code's own door got nothing. It is handed the same file now.
    /// </summary>
    [Fact]
    public async Task A_conversation_on_the_native_door_is_handed_the_plugins_servers()
    {
        var plugin = Path.Combine(_home, "plugins", "browser");
        Directory.CreateDirectory(plugin);
        File.WriteAllText(Path.Combine(plugin, "plugin.json"),
            """{ "id": "browser", "servers": [ { "name": "browser", "command": ["npx", "-y", "@playwright/mcp@latest"] } ] }""");
        var argvFile = Path.Combine(_home, "argv.json");
        var script = Path.Combine(_home, "argv-claude.mjs");
        File.WriteAllText(script, """
            import { writeFileSync } from 'node:fs';
            const argv = process.argv.slice(2);
            if (argv.includes('--version')) { console.log('2.1.281 (Claude Code)'); process.exit(0); }
            if (argv.includes('auth')) { console.log('{"loggedIn": true}'); process.exit(0); }
            writeFileSync(argv[0], JSON.stringify(argv));
            process.stdin.resume();
            process.stdin.on('end', () => process.exit(0));
            """);

        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        var adapters = AdapterSet.Built();
        using var client = new ServiceClient(service.Url, null);
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(Path.Combine(_home, "sessions")),
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["claude-code"] = ["node", script, argvFile] },
        };

        var start = await runner.StartAsync("engine", "claude-code", config);
        var id = start.SessionId ?? throw new InvalidOperationException(start.Message);
        await Until(() => File.Exists(argvFile), () => $"state {service.State(id)}");

        var argv = System.Text.Json.JsonSerializer.Deserialize<string[]>(File.ReadAllText(argvFile))!;
        var at = Array.IndexOf(argv, "--mcp-config");
        Assert.True(at >= 0, string.Join(" ", argv));
        Assert.Contains("@playwright/mcp@latest", File.ReadAllText(argv[at + 1]));

        runner.Finish(id);
        await Until(() => service.State(id) is "completed" or "stopped");
    }

    // ------------------------------------------------------------------ the harness behind each door

    private sealed record Session(
        string Id, ChatRunner Runner, StandInService Service, SessionEvents Events, ServiceClient Client,
        List<ChatQueue> Queues, Func<string> Seen) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Runner.Dispose();
            Client.Dispose();
            await Service.DisposeAsync();
        }
    }

    private async Task<Session> Chat(string adapter, bool slowOpen = false)
    {
        var service = StandInService.Start(Path.Combine(_home, "engine"));
        var command = adapter switch
        {
            "claude-code" => new[] { "node", NativeHarness(), Heard },
            "acp-stub" => ["node", ProtocolAgent(), Heard, slowOpen ? "slow" : "quick"],
            _ => ["node", TextHarness()],
        };
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { [adapter] = command },
        };
        var adapters = AdapterSet.Built();
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        var client = new ServiceClient(service.Url, null);
        var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(Path.Combine(_home, "sessions")), events: events,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
        var queues = new List<ChatQueue>();
        runner.QueueChanged += (_, queue) => { lock (queues) queues.Add(queue); };

        var start = await runner.StartAsync("engine", adapter, config);
        var id = start.SessionId ?? throw new InvalidOperationException(start.Message);
        string Seen() => $"heard [{string.Join(" | ", HeardLines())}], state {service.State(id)}, "
                         + $"record [{string.Join(" | ", Said(events, id))}]";
        await Until(() => service.State(id) == "working", Seen);

        return new Session(id, runner, service, events, client, queues, Seen);
    }

    private static string[] Said(Session session) => Said(session.Events, session.Id);

    /// <summary>The words of each message — what these tests are about; attachments are CONV4c's own tests.</summary>
    private static string[] Texts(IEnumerable<ChatMessage> messages) => [.. messages.Select(message => message.Text)];

    private static string[] Said(SessionEvents events, string id) => [.. events.Page(id).Events
        .Where(e => e.Kind is SessionEventKind.User or SessionEventKind.Message or SessionEventKind.Turn)
        .Select(e => e.Kind switch
        {
            SessionEventKind.User => $"{e.Origin}: {e.Text}",
            SessionEventKind.Message => $"agent: {e.Text}",
            _ => $"turn: {e.StopReason}",
        })];

    private static int Turns(Session session) =>
        session.Events.Page(session.Id).Events.Count(e => e.Kind == SessionEventKind.Turn);

    /// <summary>
    /// A stand-in Claude Code on its structured wire: a message is answered after a moment, and
    /// <c>hold</c> streams a word and waits for the interrupt control request, which it answers the way
    /// the binary did — a control response, then an <c>error_during_execution</c> result that says the
    /// stream was aborted.
    /// </summary>
    private string NativeHarness()
    {
        var script = Path.Combine(_home, "claude-stream.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            const argv = process.argv.slice(2);
            if (argv.includes('--version')) { console.log('2.1.281 (Claude Code)'); process.exit(0); }
            if (argv.includes('auth')) { console.log('{"loggedIn": true}'); process.exit(0); }
            const heard = argv[0];
            const note = (text) => appendFileSync(heard, text + '\n');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            let n = 0;
            let holding = null;
            const words = (text) => {
              const id = 'msg_' + (++n);
              send({ type: 'stream_event', event: { type: 'message_start', message: { id } } });
              send({ type: 'stream_event', event: { type: 'content_block_delta', delta: { type: 'text_delta', text } } });
              send({ type: 'assistant', message: { id, content: [{ type: 'text', text }] } });
            };
            const lines = createInterface({ input: process.stdin });
            lines.on('line', (line) => {
              const frame = JSON.parse(line);
              if (frame.type === 'control_request' && frame.request?.subtype === 'interrupt') {
                note('interrupt');
                send({ type: 'control_response', response: { subtype: 'success', request_id: frame.request_id, response: { still_queued: [] } } });
                if (holding) {
                  note('result: ' + holding + ' (interrupted)');
                  holding = null;
                  send({ type: 'user', message: { role: 'user', content: [{ type: 'text', text: '[Request interrupted by user]' }] } });
                  send({ type: 'result', subtype: 'error_during_execution', is_error: true, terminal_reason: 'aborted_streaming' });
                }
                return;
              }
              const said = frame.message.content[0].text;
              note('user: ' + said);
              if (said === 'hold') { holding = said; words('holding'); return; }
              setTimeout(() => {
                words('heard ' + said);
                note('result: ' + said);
                send({ type: 'result', subtype: 'success', is_error: false, result: 'heard ' + said, terminal_reason: 'completed' });
              }, 200);
            });
            lines.on('close', () => { note('stdin ended'); setTimeout(() => process.exit(0), 250); });
            """);
        return script;
    }

    /// <summary>
    /// A stand-in ACP agent: it answers each prompt after a moment, and <c>hold</c> streams a word and
    /// waits for <c>session/cancel</c>, then answers the prompt <c>cancelled</c> — the protocol's own
    /// ending for a cancelled turn. <c>slow</c> takes its time over <c>session/new</c>.
    /// </summary>
    private string ProtocolAgent()
    {
        var script = Path.Combine(_home, "acp-turns-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const heard = process.argv[2];
            const slow = process.argv[3] === 'slow';
            const note = (text) => appendFileSync(heard, text + '\n');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            const words = (text) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: 'acp-turns',
              update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text } } } });
            let holding = null;
            const lines = createInterface({ input: process.stdin });
            lines.on('line', (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                note('initialize');
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                note('session/new');
                setTimeout(() => send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-turns' } }), slow ? 800 : 0);
              } else if (frame.method === 'session/prompt') {
                const said = frame.params.prompt[0].text;
                note('prompt: ' + said);
                if (said === 'hold') { holding = frame.id; words('holding'); return; }
                setTimeout(() => {
                  words('heard ' + said);
                  send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
                }, 200);
              } else if (frame.method === 'session/cancel') {
                note('session/cancel');
                if (holding !== null) {
                  send({ jsonrpc: '2.0', id: holding, result: { stopReason: 'cancelled' } });
                  holding = null;
                }
              } else if (frame.method === 'session/close') {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            """);
        return script;
    }

    /// <summary>A harness that carries only text: it echoes each line and knows no turns.</summary>
    private string TextHarness()
    {
        var script = Path.Combine(_home, "echo.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('stub 1.0.0'); process.exit(0); }
            for await (const line of createInterface({ input: process.stdin })) console.log('heard ' + line);
            """);
        return script;
    }

    /// <summary>Wait for a condition, bounded for a loaded machine, saying what it saw when it gives up.</summary>
    private static Task Until(Func<bool> condition, Func<string>? seen = null) => Poll.Until(condition, seen);
}
