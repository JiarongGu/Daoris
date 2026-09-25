using System.Text;
using Daoris.Driver;
using StandInService = Daoris.Desktop.Driver.Tests.OrphanedSessionTests.StandInService;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What a person attaches to a message (CONV4c): kept for the session under the home, granted read at
/// spawn, and referenced the way each door reads best — measured first
/// (docs/2026-09-25-message-content-evidence.md).
/// </summary>
/// <remarks>
/// The stand-in harnesses write down what reached them — the message, the blocks, and the settings they
/// were handed — so each assertion is about the wire, not about what the driver meant to send.
/// </remarks>
public sealed class ChatAttachmentTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-attach-" + Guid.NewGuid().ToString("N")[..8]);

    public ChatAttachmentTests() => Directory.CreateDirectory(Path.Combine(_home, "engine"));

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Heard => Path.Combine(_home, "heard.txt");

    private string HeardText() => StubFile.Text(Heard);

    private static ChatUpload Upload(string name, string content) => new(name, Encoding.UTF8.GetBytes(content));

    /// <summary>
    /// Kept under the session, one file per content, by a name that cannot leave the folder: a path
    /// given as a name loses its directories, two different screenshots both called image.png stay two
    /// files, and the same file dropped twice is one.
    /// </summary>
    [Fact]
    public void A_file_is_kept_under_its_session_by_its_hash_and_a_safe_name()
    {
        var kept = ChatFiles.Keep(_home, "s1a2b3c4", [
            Upload("../../outside.txt", "not outside"),
            Upload("image.png", "first"),
            Upload("image.png", "second"),
            Upload("again.png", "first"),
        ]);

        var folder = ChatFiles.Folder(_home, "s1a2b3c4");
        Assert.Equal(["outside.txt", "image.png", "image.png"], kept.Select(file => file.Name));
        Assert.All(kept, file => Assert.Equal(folder, Path.GetDirectoryName(file.Path)));
        Assert.Equal(3, kept.Select(file => file.Path).Distinct().Count());
        Assert.Equal("not outside", File.ReadAllText(kept[0].Path));
        Assert.EndsWith("-outside.txt", kept[0].Path);
    }

    /// <summary>The limits are a quest's (10 files, 20 MB together), refused in a sentence, with nothing kept.</summary>
    [Fact]
    public void More_than_a_message_carries_is_refused_and_nothing_is_kept()
    {
        var many = Enumerable.Range(0, ChatFiles.MaxFiles + 1).Select(i => Upload($"f{i}.txt", $"content {i}")).ToList();
        var tooMany = Assert.Throws<DriverException>(() => ChatFiles.Keep(_home, "s1a2b3c4", many));
        Assert.Contains($"at most {ChatFiles.MaxFiles} files", tooMany.Message);

        var large = new ChatUpload("big.bin", new byte[ChatFiles.MaxBytes + 1]);
        var tooLarge = Assert.Throws<DriverException>(() => ChatFiles.Keep(_home, "s1a2b3c4", [large]));
        Assert.Contains("20 MB", tooLarge.Message);

        Assert.False(Directory.Exists(ChatFiles.Folder(_home, "s1a2b3c4")));
    }

    /// <summary>An id is a record's, and one that is not an id names no folder.</summary>
    [Fact]
    public void A_session_id_that_is_not_an_id_keeps_nothing()
    {
        Assert.Throws<DriverException>(() => ChatFiles.Keep(_home, "../escape", [Upload("a.txt", "a")]));
    }

    /// <summary>
    /// The native door: the file is named by its path on a line after the person's words, which the
    /// agent reads with its own tool (measured); the session was handed a read of exactly the session's
    /// folder at spawn; and the record keeps the person's words and the file's name — never the path
    /// line Daoris added.
    /// </summary>
    [Fact]
    public async Task On_the_native_door_a_file_is_named_by_its_path_under_a_read_granted_at_spawn()
    {
        await using var session = await Chat("claude-code");

        Assert.True(session.Runner.Say(session.Id, "what does this log say?", [Upload("run.log", "exit 3")]));
        await Until(() => Turns(session) == 1, () => HeardText());

        var kept = Directory.GetFiles(ChatFiles.Folder(_home, session.Id)).Single();
        Assert.Equal("exit 3", File.ReadAllText(kept));
        var heard = HeardText();
        Assert.Contains("user: what does this log say?", heard);
        Assert.Contains(kept, heard);
        Assert.Contains(PermissionRules.ReadRule(ChatFiles.Folder(_home, session.Id)).Replace("\\", "\\\\"), heard);

        var asked = Assert.Single(session.Events.Page(session.Id).Events, e => e.Kind == SessionEventKind.User);
        Assert.Equal("what does this log say?", asked.Text);
        Assert.Equal(["run.log"], asked.Files);
    }

    /// <summary>
    /// The protocol door: a <c>resource_link</c> per file, the protocol's baseline block, pointing at the
    /// kept file. The read is granted the same way as on the native door — the rules are composed once,
    /// whatever the door — and rides <c>session/new</c> for an adapter that takes Claude's rules
    /// (<c>PermissionSeamTests</c>). This stand-in takes none, so it is handed none.
    /// </summary>
    [Fact]
    public async Task On_the_protocol_door_a_file_is_a_resource_link_to_the_kept_file()
    {
        await using var session = await Chat("acp-stub");

        Assert.True(session.Runner.Say(session.Id, "and this screenshot?", [Upload("shot.png", "png bytes")]));
        await Until(() => Turns(session) == 1, () => HeardText());

        var kept = Directory.GetFiles(ChatFiles.Folder(_home, session.Id)).Single();
        var heard = HeardText();
        Assert.Contains("prompt: and this screenshot?", heard);
        Assert.Contains($"link: shot.png {new Uri(kept).AbsoluteUri}", heard);
        // Named once: no path line beside the link, which would hand the agent the same file twice.
        Assert.DoesNotContain(kept, heard.Replace(new Uri(kept).AbsoluteUri, ""));

        var asked = Assert.Single(session.Events.Page(session.Id).Events, e => e.Kind == SessionEventKind.User);
        Assert.Equal("and this screenshot?", asked.Text);
        Assert.Equal(["shot.png"], asked.Files);
    }

    /// <summary>
    /// A message waiting behind a turn carries its files, and a stop hands both back — the page shows
    /// what is attached to each queued message and says which files were not sent.
    /// </summary>
    [Fact]
    public async Task A_waiting_message_keeps_its_files_and_a_stop_hands_them_back()
    {
        await using var session = await Chat("acp-stub");

        Assert.True(session.Runner.Say(session.Id, "hold"));
        Assert.True(session.Runner.Say(session.Id, "then this", [Upload("plan.md", "# plan")]));
        await Until(() => HeardText().Contains("prompt: hold"), () => HeardText());

        var waiting = Assert.Single(session.Runner.Queue(session.Id).Queued);
        Assert.Equal(("then this", "plan.md"), (waiting.Text, Assert.Single(waiting.Files).Name));

        var stop = await session.Runner.CancelTurnAsync(session.Id);
        var back = Assert.Single(stop.Withdrawn);
        Assert.Equal(("then this", "plan.md"), (back.Text, Assert.Single(back.Files).Name));
    }

    // ------------------------------------------------------------------ the harness behind each door

    private sealed record Session(
        string Id, ChatRunner Runner, StandInService Service, SessionEvents Events, ServiceClient Client) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            Runner.Dispose();
            Client.Dispose();
            await Service.DisposeAsync();
        }
    }

    private async Task<Session> Chat(string adapter)
    {
        var service = StandInService.Start(Path.Combine(_home, "engine"));
        var command = adapter == "claude-code"
            ? new[] { "node", NativeHarness(), Heard }
            : ["node", ProtocolAgent(), Heard];
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

        var start = await runner.StartAsync("engine", adapter, config);
        var id = start.SessionId ?? throw new InvalidOperationException(start.Message);
        await Until(() => service.State(id) == "working", () => $"state {service.State(id)}");
        return new Session(id, runner, service, events, client);
    }

    private static int Turns(Session session) =>
        session.Events.Page(session.Id).Events.Count(e => e.Kind == SessionEventKind.Turn);

    /// <summary>
    /// A stand-in Claude Code on its structured wire. It writes down the settings it was handed and every
    /// message's whole text, and answers each with a result.
    /// </summary>
    private string NativeHarness()
    {
        var script = Path.Combine(_home, "claude-attach.mjs");
        File.WriteAllText(script, """
            import { appendFileSync, readFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            const argv = process.argv.slice(2);
            if (argv.includes('--version')) { console.log('2.1.281 (Claude Code)'); process.exit(0); }
            if (argv.includes('auth')) { console.log('{"loggedIn": true}'); process.exit(0); }
            const heard = argv[0];
            const note = (text) => appendFileSync(heard, text + '\n');
            const at = argv.indexOf('--settings');
            note('settings: ' + (at >= 0 ? readFileSync(argv[at + 1], 'utf8') : '(none)'));
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            for await (const line of createInterface({ input: process.stdin })) {
              const frame = JSON.parse(line);
              note('user: ' + frame.message.content.map((b) => b.text ?? '[' + b.type + ']').join(''));
              send({ type: 'assistant', message: { id: 'm', content: [{ type: 'text', text: 'read it' }] } });
              send({ type: 'result', subtype: 'success', is_error: false, result: 'read it' });
            }
            """);
        return script;
    }

    /// <summary>
    /// A stand-in ACP agent. It writes down the rules <c>session/new</c> carried and each prompt's blocks,
    /// and holds a prompt that says <c>hold</c> until it is cancelled.
    /// </summary>
    private string ProtocolAgent()
    {
        var script = Path.Combine(_home, "acp-attach-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync, readFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const heard = process.argv[2];
            const note = (text) => appendFileSync(heard, text + '\n');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            let holding = null;
            const lines = createInterface({ input: process.stdin });
            lines.on('line', (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: {} } });
              } else if (frame.method === 'session/new') {
                const settings = frame.params?._meta?.claudeCode?.options?.settings;
                note('settings: ' + (settings ? readFileSync(settings, 'utf8') : '(none)'));
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: 'acp-attach' } });
              } else if (frame.method === 'session/prompt') {
                for (const block of frame.params.prompt) {
                  if (block.type === 'text') note('prompt: ' + block.text);
                  else if (block.type === 'resource_link') note('link: ' + block.name + ' ' + block.uri);
                  else note('block: ' + block.type);
                }
                if (frame.params.prompt[0].text === 'hold') { holding = frame.id; return; }
                send({ jsonrpc: '2.0', id: frame.id, result: { stopReason: 'end_turn' } });
              } else if (frame.method === 'session/cancel') {
                if (holding !== null) send({ jsonrpc: '2.0', id: holding, result: { stopReason: 'cancelled' } });
                holding = null;
              } else if (frame.id !== undefined && frame.method) {
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              }
            });
            """);
        return script;
    }

    private static Task Until(Func<bool> condition, Func<string>? seen = null) => Poll.Until(condition, seen);
}
