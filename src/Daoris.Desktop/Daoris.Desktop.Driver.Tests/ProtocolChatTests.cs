using Daoris.Driver;
using StandInService = Daoris.Desktop.Driver.Tests.OrphanedSessionTests.StandInService;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A conversation on the protocol door (CONV3b, D76 §4): one ACP session for the whole conversation,
/// each message a turn on it, and the person's words in the record.
/// </summary>
/// <remarks>
/// <para>🔴 <b>The door was broken, not merely unrecorded.</b> A chat on an ACP harness was spawned as a
/// pipe — no <c>initialize</c>, no <c>session/new</c> — and a person's message was written into the
/// JSON-RPC stream as raw text. The agent here writes down any line that is not a frame, which is what
/// that message was.</para>
///
/// <para>A real process (node) speaking the wire, and a stand-in service on a loopback port. No model,
/// no account.</para>
/// </remarks>
public sealed class ProtocolChatTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-acp-chat-" + Guid.NewGuid().ToString("N")[..8]);

    public ProtocolChatTests() => Directory.CreateDirectory(Path.Combine(_home, "engine"));

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Heard => Path.Combine(_home, "heard.txt");

    private string[] HeardLines() => File.Exists(Heard) ? File.ReadAllLines(Heard) : [];

    /// <summary>
    /// The whole conversation, end to end: one handshake and one session; two messages sent back to back
    /// become two turns, the second waiting for the first; finishing closes the session and the agent
    /// exits on its own. The record reads as the conversation happened.
    /// </summary>
    [Fact]
    public async Task A_chat_on_the_protocol_door_is_one_session_and_each_message_a_turn_in_order()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Heard] },
        };
        var adapters = AdapterSet.Built();
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        using var client = new ServiceClient(service.Url, null);
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(Path.Combine(_home, "sessions")), events: events,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));

        var id = (await runner.StartAsync("engine", "acp-stub", config)).SessionId;
        Assert.NotNull(id);
        string Seen() => $"heard [{string.Join(" | ", HeardLines())}], state {service.State(id!)}, "
                         + $"record [{string.Join(" | ", events.Page(id!).Events.Select(e => e.Kind))}]";
        await Until(() => service.State(id!) == "working", Seen);

        Assert.True(runner.Say(id!, "first"));
        Assert.True(runner.Say(id!, "second"));
        await Until(() => HeardLines().Any(line => line == "prompt: second") && service.State(id!) == "working"
                          && events.Page(id!).Events.Count(e => e.Kind == SessionEventKind.Turn) == 2, Seen);

        Assert.True(runner.Finish(id!));
        await Until(() => service.State(id!) == "completed", Seen);

        Assert.Equal(
            ["initialize", "session/new", "prompt: first", "prompt: second", "session/close", "stdin ended"],
            HeardLines());

        var said = events.Page(id!).Events
            .Where(e => e.Kind is SessionEventKind.User or SessionEventKind.Message or SessionEventKind.Turn)
            .Select(e => e.Kind switch
            {
                SessionEventKind.User => $"{e.Origin}: {e.Text}",
                SessionEventKind.Message => $"agent: {e.Text}",
                _ => $"turn: {e.StopReason}",
            });
        Assert.Equal(
            ["person: first", "agent: heard first", "turn: end_turn", "person: second", "agent: heard second", "turn: end_turn"],
            said);
        Assert.False(runner.Say(id!, "too late"));
    }

    /// <summary>
    /// 🔴 REV3: a turn the agent refused (a JSON-RPC error — auth expired, overloaded) recorded a note and
    /// never a turn's end, so the page drew *working…* under the driver's own failure note until the
    /// next message, while the composer beside it said nothing was running. A refused turn ENDS.
    /// </summary>
    [Fact]
    public async Task A_turn_the_agent_refused_ends_in_the_record_and_the_next_one_is_taken()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Heard] },
        };
        var adapters = AdapterSet.Built();
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        using var client = new ServiceClient(service.Url, null);
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(Path.Combine(_home, "sessions")), events: events,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));

        var id = (await runner.StartAsync("engine", "acp-stub", config)).SessionId!;
        string Seen() => $"record [{string.Join(" | ", events.Page(id).Events.Select(e => $"{e.Kind}:{e.StopReason}"))}]";
        await Until(() => service.State(id) == "working", Seen);

        Assert.True(runner.Say(id, "refuse"));
        Assert.True(runner.Say(id, "after"));
        await Until(() => events.Page(id).Events.Count(e => e.Kind == SessionEventKind.Turn) == 2, Seen);

        var turns = events.Page(id).Events.Where(e => e.Kind == SessionEventKind.Turn).Select(e => e.StopReason).ToList();
        Assert.Equal(["error", "end_turn"], turns);
        Assert.Contains(events.Page(id).Events, e => e.Kind == SessionEventKind.Note && e.Text!.Contains("overloaded"));
        Assert.True(runner.Finish(id));
    }

    /// <summary>
    /// A text-only door keeps the person's words where it keeps the agent's — the console — so its
    /// record holds no half a conversation: questions with no answers beside them would read as an
    /// agent that never replied.
    /// </summary>
    [Fact]
    public async Task A_text_only_chat_records_no_half_a_conversation()
    {
        await using var service = StandInService.Start(Path.Combine(_home, "engine"));
        var echo = Path.Combine(_home, "echo.mjs");
        File.WriteAllText(echo, """
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('stub 1.0.0'); process.exit(0); }
            for await (const line of createInterface({ input: process.stdin })) console.log('heard ' + line);
            """);
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", echo] },
        };
        var adapters = AdapterSet.Built();
        var events = new SessionEvents(Path.Combine(_home, "sessions"));
        using var client = new ServiceClient(service.Url, null);
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(Path.Combine(_home, "sessions")), events: events,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));

        var id = (await runner.StartAsync("engine", "stub", config)).SessionId!;
        await Until(() => service.State(id) == "working", () => $"state {service.State(id)}");
        Assert.True(runner.Say(id, "hello"));
        Assert.True(runner.Finish(id));
        await Until(() => service.State(id) == "completed", () => $"state {service.State(id)}");

        Assert.Empty(events.Page(id).Events);
        Assert.Contains("heard hello", File.ReadAllText(Path.Combine(_home, "sessions", $"{id}.log")));
    }

    /// <summary>
    /// A stand-in ACP agent: it writes down every method it is called with and every prompt's words,
    /// answers each prompt a moment later so a second message arrives mid-turn, and writes down any
    /// line that is not a frame — the raw text the broken door used to send.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-chat-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const heard = process.argv[2];
            const note = (text) => appendFileSync(heard, text + '\n');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            for await (const line of createInterface({ input: process.stdin })) {
              let frame;
              try { frame = JSON.parse(line); } catch { note('RAW: ' + line); continue; }
              if (frame.method === 'initialize') {
                note('initialize');
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                note('session/new');
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-chat' } });
              } else if (frame.method === 'session/prompt') {
                const said = frame.params.prompt[0].text;
                note('prompt: ' + said);
                if (said === 'refuse') {
                  send({ jsonrpc: '2.0', id: frame.id, error: { code: -32603, message: 'overloaded' } });
                  continue;
                }
                await new Promise((resolve) => setTimeout(resolve, 150));
                send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: 'acp-chat',
                  update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'heard ' + said } } } });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.method === 'session/close') {
                note('session/close');
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
            note('stdin ended');
            """);
        return script;
    }

    /// <summary>
    /// Wait for a condition, bounded for a loaded machine — the suite runs classes in parallel, each
    /// spawning node — and saying what it saw when it gives up: this test failed once in a full gate
    /// run (2026-09-25) and passed five full runs after, and a bare timeout said nothing about why.
    /// </summary>
    private static async Task Until(Func<bool> condition, Func<string>? seen = null)
    {
        for (var waited = 0; !condition(); waited += 50)
        {
            if (waited > 30_000) throw new TimeoutException($"the condition never held — {seen?.Invoke() ?? "nothing more to say"}");
            await Task.Delay(50);
        }
    }
}
