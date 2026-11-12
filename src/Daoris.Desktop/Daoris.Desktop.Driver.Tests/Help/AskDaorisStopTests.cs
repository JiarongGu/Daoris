using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Ask Daoris's *Stop* ends the reply, never the conversation: stopped in the middle of a tool call, the next word goes on in the same
/// record and the same harness conversation; and a conversation a stop ended under an earlier runner, as an older build's stop did, is
/// continued by the next word under the runner that comes up after it.
/// </summary>
/// <remarks>A real process (node) speaking the protocol door, and the in-process ledger of <see cref="ChatGoOnTests"/>. No model, no account.</remarks>
[Trait(Category.Name, Category.Process)]
public sealed class AskDaorisStopTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-ask-stop-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public AskDaorisStopTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
        SeededRules.In(_home);
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "claude-code", "daoris"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static readonly HelpMachine Machine = new()
    {
        Adapter = "acp-stub",
        Helper = "acp-stub",
        Cap = 1,
        Repositories = [new("engine", "default") { Checkout = true, Drivable = true }],
    };

    private string Heard => Path.Combine(_home, "heard.txt");

    private string[] HeardLines() => StubFile.Lines(Heard);

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private DriverConfig Config()
    {
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Heard] },
        };
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        return config;
    }

    private ChatRunner Runner(ServiceClient client, SessionEvents events, SessionProcesses processes)
    {
        var adapters = ProviderRegistry.Built();
        return new ChatRunner(
            client, adapters, _home, processes, events: events, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    private string Seen(ChatLedger ledger, SessionEvents events, string id) =>
        $"heard [{string.Join(" | ", HeardLines())}], state {ledger.State(id)}, records {ledger.Count}, "
        + $"record [{string.Join(" | ", events.After(id, 0).Events.Select(e => $"{e.Kind}:{e.Text}"))}]";

    /// <summary>The person's word as the say door leaves it on a conversation no reply runs in: kept on the record and shown.</summary>
    private static void Say(ChatLedger ledger, SessionEvents events, string session, string id, string text)
    {
        ledger.For(session).Say(id, text);
        events.Append(session, new SessionEvent
        {
            Kind = SessionEventKind.User, Origin = "person", Id = id, Text = text, Reaches = "resume", Door = "screen",
        });
    }

    /// <summary>
    /// 🔴 The owner's case: a tool call hangs, the person presses *Stop*, and the next word continues the same conversation: the same
    /// record, the harness's conversation resumed by its kept id, the word its next prompt.
    /// </summary>
    [Fact]
    public async Task Stopped_mid_tool_the_next_word_continues_the_same_conversation()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        var events = Events;
        var processes = new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions));
        using var client = ledger.Client();
        using var runner = Runner(client, events, processes);

        var id = (await runner.StartHelpAsync("acp-stub", config, Machine)).SessionId!;
        await Poll.Until(() => ledger.State(id) == "working", () => Seen(ledger, events, id));
        Assert.True(runner.Say(id, "browse"));
        await Poll.Until(() => HeardLines().Contains("tool: browse"), () => Seen(ledger, events, id));

        Assert.True(processes.Stop(id));
        await Poll.Until(() => ledger.State(id) == "stopped", () => Seen(ledger, events, id));

        Say(ledger, events, id, "w1", "go on without the browser");
        await Poll.Until(() => HeardLines().Contains("prompt: go on without the browser"), () => Seen(ledger, events, id));

        Assert.Equal(1, ledger.Count);
        Assert.Contains("session/resume: conv-h", HeardLines());
        await Poll.Until(
            () => events.After(id, 0).Events.Any(e => e.Kind == SessionEventKind.Message && e.Text == "heard go on without the browser"),
            () => Seen(ledger, events, id));
        Assert.True(runner.Finish(id));
    }

    /// <summary>
    /// A conversation a stop ended under an earlier runner, as an older build's stop ended it for good, is continued by the next word
    /// under the runner that comes up after: the same record, its harness conversation resumed.
    /// </summary>
    [Fact]
    public async Task A_conversation_a_stop_ended_under_an_earlier_runner_is_continued_by_the_next_word()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        string id;
        using (var client = ledger.Client())
        {
            var processes = new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions));
            using var runner = Runner(client, Events, processes);
            id = (await runner.StartHelpAsync("acp-stub", config, Machine)).SessionId!;
            await Poll.Until(() => ledger.State(id) == "working", () => Seen(ledger, Events, id));
            Assert.True(runner.Say(id, "first"));
            await Poll.Until(() => Events.After(id, 0).Events.Any(e => e.Kind == SessionEventKind.Turn), () => Seen(ledger, Events, id));
            Assert.True(processes.Stop(id));
            await Poll.Until(() => ledger.State(id) == "stopped", () => Seen(ledger, Events, id));
        }

        var events = Events;
        using var later = ledger.Client();
        using var next = Runner(later, events, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)));
        Say(ledger, events, id, "w1", "and after the stop?");

        await Poll.Until(() => HeardLines().Contains("prompt: and after the stop?"), () => Seen(ledger, events, id));
        Assert.Equal(1, ledger.Count);
        Assert.Equal(2, HeardLines().Count(line => line == "initialize"));
        Assert.Contains("session/resume: conv-h", HeardLines());
        Assert.True(next.Finish(id));
    }

    /// <summary>
    /// A protocol-door agent that keeps its conversation's id, resumes it, answers each prompt, and hangs inside a tool call on
    /// <c>browse</c>, as the in-app browser's call hung, until its process is ended.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-stop-agent.mjs");
        File.WriteAllText(script, StubPlace.Js + """
            import { appendFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const heard = process.argv[2];
            const note = (text) => appendFileSync(heard, text + '\n');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            let conversation = null;
            const update = (body) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation, update: body } });
            for await (const line of createInterface({ input: process.stdin })) {
              let frame;
              try { frame = JSON.parse(line); } catch { continue; }
              if (frame.method === 'initialize') {
                note('initialize');
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1,
                  agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                conversation = 'conv-h';
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/resume') {
                note('session/resume: ' + frame.params.sessionId);
                conversation = frame.params.sessionId;
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.method === 'session/prompt') {
                const said = wordsOf(frame.params.prompt.filter((block) => block.type === 'text').map((block) => block.text).join(' | '));
                if (said === 'browse') {
                  update({ sessionUpdate: 'tool_call', toolCallId: 'call-1', title: 'browser_navigate', status: 'in_progress' });
                  note('tool: browse');
                  // PROCLEAK1: a stand-in its test left behind ends on its own.
                  setTimeout(() => process.exit(3), 120000);
                  continue;
                }
                note('prompt: ' + said);
                update({ sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'heard ' + said } });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            }
            """);
        return script;
    }
}
