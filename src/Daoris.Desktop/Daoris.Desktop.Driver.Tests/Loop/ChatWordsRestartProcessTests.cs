using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// With a real conversation: words said to a conversation while its
/// turn runs are kept on its record before they wait, so a shell that goes away under them, closed or cut off, leaves them there, and the
/// next shell goes on with them in their order, in the same conversation, taking each off the record once it goes.
/// </summary>
/// <remarks>
/// A real process (node) speaking the protocol door, with no model and no account, over <see cref="ChatLedger"/>, the in-process ledger
/// the conversation tests share, which keeps a running record's words where the driver that runs it asks (its say door).
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class ChatWordsRestartProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-chat-restart-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public ChatWordsRestartProcessTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
        Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "claude-code", "daoris"));
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Heard => Path.Combine(_home, "heard.txt");

    private string[] HeardLines() => StubFile.Lines(Heard);

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private SessionProcesses Processes() => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

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
            client, adapters, _home, processes, events: events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    private string Seen(ChatLedger ledger, string id) =>
        $"heard [{string.Join(" | ", HeardLines())}], state {ledger.State(id)}, moves [{string.Join(",", ledger.Moves(id))}], "
        + $"said [{string.Join(",", ledger.Said(id))}], taken [{string.Join(";", ledger.Taken.Select(each => each.Ids))}]";

    /// <summary>
    /// 🔴 Trap 7, a conversation: two words said while its turn runs wait on its record, kept before they wait. The shell closes under
    /// them, and they stay there, Daoris's restart word after them; the next shell comes up and goes on with all three in their order, as the same conversation's next turn,
    /// and they leave the record. Then a shell cut off with a word kept for its run (the record still working, nothing running it):
    /// the next shell's sweep ends the record, and the conversation goes on with that word too.
    /// </summary>
    [Fact]
    public async Task A_conversations_waiting_words_survive_a_shell_restart_and_go_in_their_order()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        var events = Events;
        using var client = ledger.Client();
        string id;
        using (var first = Runner(client, events, Processes()))
        {
            id = (await first.StartAsync("engine", "acp-stub", config)).SessionId!;
            await Poll.Until(() => ledger.State(id) == "working", () => Seen(ledger, id));
            Assert.True(first.Say(id, "hold"));
            await Poll.Until(() => HeardLines().Contains("prompt: hold") && first.Taking(id), () => Seen(ledger, id));

            var said = await first.SayKeptAsync(id, "Also log the port.", null, null, MessageLevel.Urgent, "press/1");
            var again = await first.SayKeptAsync(id, "No, 9090.", null, null, MessageLevel.Ordinary, "press/2");

            Assert.Equal(("turn-end", "k1"), (said!.Reaches, said.Word));
            Assert.Equal(("turn-end", "k2"), (again!.Reaches, again.Word));
            Assert.Equal(["k1", "k2"], ledger.Said(id));
            Assert.Equal(["k1", "k2"], first.Queue(id).Queued.Select(word => word.Id));
        }

        // The close cut its turn, so Daoris's restart word waits after them.
        await Poll.Until(() => ledger.State(id) == "stopped", () => Seen(ledger, id));
        Assert.Equal(["k1", "k2", "k3"], ledger.Said(id));
        Assert.DoesNotContain("prompt: Also log the port.", string.Join("\n", HeardLines()));

        using (var next = Runner(client, events, Processes()))
        {
            await next.TakeUpAsync();
            await Poll.Until(
                () => HeardLines().Contains($"prompt: Also log the port. | No, 9090. | {ShellClose.Words}") && ledger.Said(id).Count == 0,
                () => Seen(ledger, id));
            Assert.Contains("session/resume: conv-1", HeardLines());
            Assert.Equal([(id, "k1,k2,k3")], ledger.Taken);
            Assert.True(next.Finish(id));
            await Poll.Until(() => ledger.State(id) == "completed", () => Seen(ledger, id));
        }

        // Cut off: the record still says it runs, and nothing on this machine runs it. On it wait a word its shell had written to the
        // agent (attempted), which may have reached it, and a word kept for its run and never attempted.
        ledger.Edit(id, record =>
        {
            record["state"] = "working";
            record["said"]!.AsArray().Add(new System.Text.Json.Nodes.JsonObject
            {
                ["id"] = "k8", ["text"] = "Use port 9090.", ["at"] = "2026-10-03T09:59:00Z", ["files"] = new System.Text.Json.Nodes.JsonArray(),
                ["reopens"] = false, ["level"] = "urgent", ["key"] = "press/8", ["attempt"] = "2026-10-03T09:59:30Z",
            });
            record["said"]!.AsArray().Add(new System.Text.Json.Nodes.JsonObject
            {
                ["id"] = "k9", ["text"] = "And the readme.", ["at"] = "2026-10-03T10:00:00Z", ["files"] = new System.Text.Json.Nodes.JsonArray(),
                ["reopens"] = false, ["level"] = "urgent", ["key"] = "press/9",
            });
        });

        var processes = Processes();
        using var after = Runner(client, events, processes);
        await after.TakeUpAsync();
        var swept = await Orphans.EndAsync(client, processes);

        // 🔴 Trap 8: the attempted word is never sent again; it leaves the record, said uncertain under its id, and the other goes.
        Assert.Equal(id, Assert.Single(swept).Id);
        await Poll.Until(() => HeardLines().Contains("prompt: And the readme.") && ledger.Said(id).Count == 0, () => Seen(ledger, id));
        Assert.DoesNotContain(HeardLines(), line => line.Contains("Use port 9090.", StringComparison.Ordinal));
        Assert.Equal([(id, "k8")], ledger.TakenBack);
        Assert.Equal([(id, "k1,k2,k3"), (id, "k9")], ledger.Taken);
        var lost = Assert.Single(events.After(id, 0).Events, e => e.Code == SessionEventCodes.Lost);
        Assert.Equal(["k8"], lost.Words);
        Assert.True(after.Finish(id));
        await Poll.Until(() => ledger.State(id) == "completed", () => Seen(ledger, id));
    }

    /// <summary>
    /// 🔴 A close that is no update's (the person's, or a restart's) cuts one conversation mid-turn: its record ends as Daoris's stop with
    /// Daoris's restart word kept, and the next shell goes on with it in its own conversation with no word from the person, its line saying
    /// Daoris restarted. One idle at the close starts no turn, and one the person stopped stays stopped.
    /// </summary>
    [Fact]
    public async Task A_conversation_a_close_cut_mid_turn_goes_on_by_itself_and_an_idle_or_stopped_one_does_not()
    {
        var ledger = new ChatLedger().Register("engine", _root).Register("mill", Root("mill")).Register("dock", Root("dock"));
        var config = Config();
        var events = Events;
        using var client = ledger.Client();
        string cut, idle, stopped;
        var processes = Processes();
        using (var first = Runner(client, events, processes))
        {
            cut = (await first.StartAsync("engine", "acp-stub", config)).SessionId!;
            idle = (await first.StartAsync("mill", "acp-stub", config)).SessionId!;
            stopped = (await first.StartAsync("dock", "acp-stub", config)).SessionId!;
            await Poll.Until(() => new[] { cut, idle, stopped }.All(id => ledger.State(id) == "working"), () => Seen(ledger, cut));
            Assert.True(first.Say(idle, "first"));
            await Poll.Until(() => HeardLines().Contains("prompt: first") && !first.Taking(idle), () => Seen(ledger, idle));
            Assert.True(first.Say(stopped, "hold"));
            Assert.True(first.Say(cut, "hold"));
            await Poll.Until(() => HeardLines().Count(line => line == "prompt: hold") == 2 && first.Taking(cut), () => Seen(ledger, cut));
            Assert.True(processes.Stop(stopped));
            await Poll.Until(() => ledger.State(stopped) == "stopped", () => Seen(ledger, stopped));
        }

        Assert.Equal(["stopped", "stopped", "stopped"], new[] { cut, idle, stopped }.Select(ledger.State));
        Assert.Contains("chat.cut", ledger.Record(cut).ToJsonString());
        Assert.Equal([1, 0, 0], new[] { cut, idle, stopped }.Select(id => Restarts(ledger, id)));

        using var next = Runner(client, events, Processes());
        await next.TakeUpAsync();
        await Poll.Until(() => HeardLines().Contains("prompt: " + ShellClose.Words) && ledger.Said(cut).Count == 0, () => Seen(ledger, cut));
        Assert.Single(HeardLines(), line => line == "prompt: " + ShellClose.Words);
        Assert.Contains(events.After(cut, 0).Events, e => e.Parts?.Any(part => part.Code == "resumed.opening"
            && Equals(part.Value("why")?.ToString(), ResumedWhy.Restart)) == true);
        Assert.Equal(["stopped", "stopped"], new[] { idle, stopped }.Select(ledger.State));
        Assert.Single(HeardLines(), line => line == "prompt: first");
    }

    private string Root(string name) => Directory.CreateDirectory(Path.Combine(_home, name)).FullName;

    private static int Restarts(ChatLedger ledger, string id) =>
        ledger.Record(id)["said"]?.AsArray().Count(word => DaorisKeys.IsRestart(word?["key"]?.GetValue<string>())) ?? 0;

    /// <summary>
    /// Through a real conversation: a producer's retry of a word the line settled, said to the
    /// running conversation, is answered settled by the record: nothing is kept on the record, queued, handed to the turn or sent to the agent,
    /// and the answer is the runner's own, <see cref="ChatSaid.Settled"/>, with no reach and no word.
    /// </summary>
    [Fact]
    public async Task A_settled_retry_said_to_a_running_conversation_queues_and_sends_nothing()
    {
        var ledger = new ChatLedger().Register("engine", _root).Settle("press/7", "c0");
        var config = Config();
        using var client = ledger.Client();
        using var runner = Runner(client, Events, Processes());
        var id = (await runner.StartAsync("engine", "acp-stub", config)).SessionId!;
        await Poll.Until(() => ledger.State(id) == "working", () => Seen(ledger, id));
        Assert.True(runner.Say(id, "hold"));
        await Poll.Until(() => HeardLines().Contains("prompt: hold") && runner.Taking(id), () => Seen(ledger, id));

        var said = await runner.SayKeptAsync(id, "Also log the port.", null, null, MessageLevel.Urgent, "press/7");

        Assert.True(said!.Settled);
        Assert.Null(said.Reaches);
        Assert.Null(said.Word);
        Assert.Empty(ledger.Said(id));
        Assert.Empty(runner.Queue(id).Queued);
        Assert.Empty(ledger.Taken);
        Assert.DoesNotContain(HeardLines(), line => line.Contains("Also log the port.", StringComparison.Ordinal));

        // A key the line never settled is kept and queued.
        var kept = await runner.SayKeptAsync(id, "No, 9090.", null, null, MessageLevel.Ordinary, "press/8");
        Assert.False(kept!.Settled);
        Assert.Equal(("turn-end", "k1"), (kept.Reaches, kept.Word));
        Assert.Equal(["k1"], ledger.Said(id));
        Assert.Equal(["k1"], runner.Queue(id).Queued.Select(word => word.Id));
    }

    /// <summary>
    /// A stand-in ACP agent, as <c>ChatGoesOnProcessTests</c>' is: it names its conversation <c>conv-1</c> and resumes it, writes down
    /// each method, a resume's id and each prompt's text blocks joined by <c> | </c>, and answers each prompt after a moment; a prompt
    /// <c>hold</c> runs until its input ends.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-restart-agent.mjs");
        File.WriteAllText(script, StubPlace.Js + """
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
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1,
                  agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                note('session/new');
                conversation = 'conv-1';
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/resume' || frame.method === 'session/load') {
                note(`${frame.method}: ${frame.params.sessionId}`);
                conversation = frame.params.sessionId;
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.method === 'session/prompt') {
                const raw = frame.params.prompt.filter((block) => block.type === 'text').map((block) => block.text).join(' | ');
                // The standing block Daoris appends after the words is kept apart, its routing standing set aside (StubFile.WithoutBlock, StubFile.Routings).
                const text = placeApart(raw.replace(/(\n\n| \| )?Daoris's standing block for this session: [\s\S]*$/, '')).said;
                if (/Routing standing here: /.test(raw)) appendFileSync(heard + '.routing', /Routing standing here: ([^.]*)\./.exec(raw)[1] + '\n');
                note('prompt: ' + text);
                if (text === 'hold') return;
                await new Promise((resolve) => setTimeout(resolve, 100));
                say('heard ' + text);
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.method === 'session/cancel') {
                note('session/cancel');
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
}
