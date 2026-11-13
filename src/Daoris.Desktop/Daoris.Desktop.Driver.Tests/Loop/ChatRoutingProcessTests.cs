using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A chat in a repository is told its routing standing in its standing block, with its first message, and the record keeps a note of
/// it; a go-on is never handed it again. A real process (node) on the protocol door, and the ledger of <see cref="ChatGoOnTests"/>.
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Git)]
[Trait(Category.Needs, Needed.Harness)]
public sealed class ChatRoutingProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "chat-routing", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public ChatRoutingProcessTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
    }

    public void Dispose() => GitSeed.Discard(_home);

    private string Heard => Path.Combine(_home, "heard.txt");

    /// <summary>Read shared for writing: a read that denies the stand-in's append makes it throw, and its note is lost.</summary>
    private string HeardText()
    {
        if (!File.Exists(Heard)) return "";
        using var file = new FileStream(Heard, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(file);
        return reader.ReadToEnd();
    }

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    private DriverConfig Config(bool driven, bool trees = false)
    {
        var place = driven ? DriverConfig.Empty.WithDrivable("engine", true) : DriverConfig.Empty;
        var config = (trees ? place.Isolating(_home, "engine") : place) with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Heard] },
        };
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        return config;
    }

    private static IReadOnlyList<SessionEvent> Told(SessionEvents events, string id) =>
        [.. events.Page(id).Events.Where(e => e.Parts?.Any(part => part.Code == "chat.told-block") == true)];

    private ChatRunner Runner(ChatLedger ledger, SessionEvents events)
    {
        var adapters = ProviderRegistry.Built();
        return new ChatRunner(
            ledger.Client(), adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>The first message tells a driven repository's chat its standing block after the person's words, once, and the record keeps the note.</summary>
    [Fact]
    public async Task A_new_chats_first_message_is_told_its_standing_once_and_the_record_keeps_a_note_of_it()
    {
        var ledger = new ChatLedger().Register("engine", _root, "core", "web");
        var config = Config(driven: true);
        var events = Events;
        using var runner = Runner(ledger, events);

        var id = (await runner.StartAsync("engine", "acp-stub", config)).SessionId!;
        await Poll.Until(() => ledger.State(id) == "working", () => HeardText());
        Assert.True(runner.Say(id, "open the board"));
        await Poll.Until(() => HeardText().Contains("open the board", StringComparison.Ordinal), () => HeardText());
        Assert.True(runner.Say(id, "and the next page"));
        await Poll.Until(() => HeardText().Contains("and the next page", StringComparison.Ordinal), () => HeardText());

        var heard = HeardText();
        var told = "Routing standing here: one at a time.";
        Assert.Contains(told, heard);
        Assert.Contains("`engine:core`, `engine:web`", heard);
        var block = heard.IndexOf(StubFile.BlockLead, StringComparison.Ordinal);
        Assert.True(block > heard.IndexOf("open the board", StringComparison.Ordinal) && block < heard.IndexOf(told, StringComparison.Ordinal));
        Assert.Equal(heard.IndexOf(told, StringComparison.Ordinal), heard.LastIndexOf(told, StringComparison.Ordinal));
        Assert.Equal(block, heard.LastIndexOf(StubFile.BlockLead, StringComparison.Ordinal));

        var note = Assert.Single(Told(events, id));
        Assert.Contains(told, note.Text);
        Assert.DoesNotContain(events.Page(id).Events, e => e.Parts?.Any(part => part.Code == "chat.told-routing") == true);
        Assert.Equal(
            ["open the board", "and the next page"], events.Page(id).Events.Where(e => e.Kind == SessionEventKind.User).Select(e => e.Text));
    }

    /// <summary>A repository nothing drives here is told so, not left to guess, and a repository the registry gives no lanes is told it has none.</summary>
    [Theory]
    [InlineData(false, "not driven")]
    [InlineData(true, "no lanes")]
    public async Task A_chat_in_a_repository_that_cannot_be_routed_to_is_told_why(bool driven, string said)
    {
        var ledger = driven ? new ChatLedger().Register("engine", _root) : new ChatLedger().Register("engine", _root, "core");
        var config = Config(driven);
        var events = Events;
        using var runner = Runner(ledger, events);

        var id = (await runner.StartAsync("engine", "acp-stub", config)).SessionId!;
        await Poll.Until(() => ledger.State(id) == "working", () => HeardText());
        Assert.True(runner.Say(id, "hello"));
        await Poll.Until(() => HeardText().Contains("hello", StringComparison.Ordinal), () => HeardText());

        Assert.Contains($"Routing standing here: {said}.", HeardText());
        Assert.Contains($"Routing standing here: {said}.", Assert.Single(Told(events, id)).Text);
    }

    /// <summary>
    /// A go-on resumes the same conversation, which its first message told the standing block: it is
    /// handed the person's words alone, whatever <c>driver.json</c> says of the drive by then, and the record keeps no second note.
    /// </summary>
    [Fact]
    public async Task A_go_on_is_never_handed_the_standing_block_again()
    {
        var ledger = new ChatLedger().Register("engine", _root, "core");
        var events = Events;
        using var runner = Runner(ledger, events);

        var id = (await runner.StartAsync("engine", "acp-stub", Config(driven: true))).SessionId!;
        await Poll.Until(() => ledger.State(id) == "working", () => HeardText());
        Assert.True(runner.Say(id, "first"));
        await Poll.Until(() => HeardText().Contains("first", StringComparison.Ordinal), () => HeardText());
        Assert.True(runner.Finish(id));
        await Poll.Until(() => ledger.State(id) == "completed", () => HeardText());
        Assert.Contains("Routing standing here: one at a time.", Assert.Single(Told(events, id)).Text);

        Config(driven: false);
        ledger.For(id).Say("w1", "Also log the port.");
        events.Append(id, new SessionEvent
        {
            Kind = SessionEventKind.User, Origin = "person", Id = "w1", Text = "Also log the port.", Reaches = "resume", Door = "screen",
        });
        await Poll.Until(() => HeardText().Contains("prompt: Also log the port.", StringComparison.Ordinal), () => HeardText());
        Assert.True(runner.Finish(id));
        await Poll.Until(() => ledger.State(id) == "completed" && ledger.Moves(id).Count(move => move == "completed") == 2, () => HeardText());

        var heard = HeardText();
        Assert.Contains("session/resume: conv-1", heard);
        Assert.Contains("prompt: Also log the port.\n", heard);
        Assert.DoesNotContain("Routing standing here: not driven.", heard);
        Assert.Single(Told(events, id));
    }

    /// <summary>The protocol door's stand-in: it names its conversation <c>conv-1</c>, resumes it, and writes each prompt's blocks down.</summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-routing-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [heard] = process.argv.slice(2);
            const note = (text) => appendFileSync(heard, text + '\n');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            let conversation = null;
            const lines = createInterface({ input: process.stdin });
            lines.on('line', (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1,
                  agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                conversation = 'conv-1';
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/resume' || frame.method === 'session/load') {
                note(`${frame.method}: ${frame.params.sessionId}`);
                conversation = frame.params.sessionId;
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.method === 'session/prompt') {
                const text = frame.params.prompt.filter((block) => block.type === 'text').map((block) => block.text).join(' | ');
                note('prompt: ' + text);
                send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
                  update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'heard' } } } });
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.method === 'session/close') {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            // stdin closed: the ending. Never process.exit (STUB1): the process ends when the loop drains.
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }
}
