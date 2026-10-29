using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// AGENTPROVIDER1 row b, part 1 (D213's row b note; the design §11 row b's proof): the stand-ins on the contract, through the shim,
/// open, send and stop on each door and <c>acp-stub</c> resumes, in real processes, and each run answers what the same run on the old
/// classes answers. No model, no account. A separate test door states a native resume and no native dialect, and its resume's early
/// failed exit is read by the plain capture and not tried again (the retry is the structured capture's: <c>Driver.cs</c>'s
/// <c>structured is not null</c>).
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class StandInShimProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-shim-" + Guid.NewGuid().ToString("N")[..8]);

    public StandInShimProcessTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private SessionTarget Target() => new("q-1", "title", "body", "asker", "repo", _home, "http://svc");

    private ChatTarget Chat() => new("repo", _home, "http://svc");

    private string Script(string name, string text)
    {
        var path = Path.Combine(_home, name);
        File.WriteAllText(path, text);
        return path;
    }

    private string Text() => Script("text.mjs", """
        import { createInterface } from 'node:readline';
        console.log('target: ' + (process.env.DAORIS_TARGET ?? '').split('\n')[0].length);
        const lines = createInterface({ input: process.stdin });
        lines.on('line', (line) => console.log('heard: ' + line));
        lines.on('close', () => process.exit(0));
        """);

    /// <summary>A protocol agent that keeps the methods it was sent in a file, one per line, and answers the spec's calls.</summary>
    private string Acp() => Script("acp.mjs", """
        import { appendFileSync } from 'node:fs';
        import { createInterface } from 'node:readline';
        const [seen] = process.argv.slice(2);
        const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
        createInterface({ input: process.stdin }).on('line', (line) => {
          const frame = JSON.parse(line);
          if (!frame.method) return;
          appendFileSync(seen, frame.method + '\n');
          const answer = (result) => frame.id === undefined ? 0 : send({ jsonrpc: '2.0', id: frame.id, result });
          if (frame.method === 'initialize') answer({ protocolVersion: 1, agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } });
          else if (frame.method === 'session/new') answer({ sessionId: 's-1' });
          else if (frame.method === 'session/prompt') {
            send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: frame.params.sessionId, update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text: 'carrying on' } } } });
            answer({ stopReason: 'end_turn' });
          } else answer({});
        }).on('close', () => process.exit(0));
        """);

    private static async Task<(string Out, int? Exit)> Ran(ProcessStartInfo info, string? stdin = null)
    {
        using var process = Process.Start(info)!;
        if (stdin is not null) await process.StandardInput.WriteLineAsync(stdin);
        if (info.RedirectStandardInput) process.StandardInput.Close();
        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync().WaitAsync(Poll.Patience);
        return (output.Replace("\r\n", "\n"), process.ExitCode);
    }

    private static IReadOnlyList<string> Command(string script, params string[] more) => ["node", script, .. more];

    /// <summary>The pipe stand-in: a driven start and a conversation's words answer as the class does, and a stop ends the process.</summary>
    [Fact]
    public async Task The_pipe_stand_in_through_the_shim_starts_hears_and_stops_as_the_class_does()
    {
        ISessionAdapter old = new StubAdapter();
        var door = AdapterSet.Built().Resolve("stub");
        var command = Command(Text());

        var started = (await Ran(door.Prepare(Target(), command)), await Ran(old.Prepare(Target(), command)));
        var spoken = (await Ran(door.PrepareChat(Chat(), command), "hello"), await Ran(old.PrepareChat(Chat(), command), "hello"));

        Assert.Equal(started.Item2, started.Item1);
        Assert.Equal(spoken.Item2, spoken.Item1);
        Assert.Contains("heard: hello", spoken.Item1.Out);
        Assert.Equal(0, spoken.Item1.Exit);

        // A stop: the process is ended while it waits on its stdin.
        using var waiting = Process.Start(door.PrepareChat(Chat(), command))!;
        await Task.Delay(200);
        waiting.Kill(entireProcessTree: true);
        Assert.True(waiting.WaitForExit(10_000));
    }

    /// <summary>The protocol stand-in: open, send and close on the wire, then a resume by the agent's own answer, equal to the class's.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_protocol_stand_in_through_the_shim_opens_sends_and_resumes_as_the_class_does(bool resume)
    {
        ISessionAdapter old = new AcpStubAdapter();
        var door = AdapterSet.Built().Resolve("acp-stub");

        var methodsOld = await Wire(old, "old.txt", resume);
        var methodsNew = await Wire(door, "new.txt", resume);

        Assert.Equal(methodsOld, methodsNew);
        Assert.Equal(resume ? ["initialize", "session/resume", "session/prompt", "session/close"] : ["initialize", "session/new", "session/prompt", "session/close"], methodsNew);
    }

    private async Task<IReadOnlyList<string>> Wire(ISessionAdapter adapter, string seen, bool resume)
    {
        var path = Path.Combine(_home, seen);
        using var process = Process.Start(adapter.Prepare(Target(), Command(Acp(), path)))!;
        var lines = new List<string>();
        var outcome = await new AcpSession(process.StandardOutput, process.StandardInput, lines.Add)
            .RunAsync(_home, "Go on.", CancellationToken.None, resume: resume ? "s-0" : null);
        process.StandardInput.Close();
        Assert.True(process.WaitForExit(10_000));
        Assert.Contains(lines, line => line.Contains("carrying on"));
        Assert.Equal(resume ? "s-0" : "s-1", outcome.SessionId);
        return [.. File.ReadAllLines(path)];
    }

    /// <summary>The protocol stand-in stopped: ended while it waits for frames.</summary>
    [Fact]
    public async Task The_protocol_stand_in_through_the_shim_is_stopped_by_ending_its_process()
    {
        var door = AdapterSet.Built().Resolve("acp-stub");
        using var process = Process.Start(door.Prepare(Target(), Command(Acp(), Path.Combine(_home, "stopped.txt"))))!;
        await Task.Delay(200);
        process.Kill(entireProcessTree: true);
        Assert.True(process.WaitForExit(10_000));
    }

    /// <summary>
    /// A door stating a native resume and no native dialect (defined here, never in production): it resumes, is read by the plain capture
    /// (no mapper, so Driver's retry guard stays false), and a resume that exits early and failed runs once and is not tried again.
    /// </summary>
    [Fact]
    public async Task A_native_resume_with_no_native_dialect_is_read_by_the_plain_capture_and_not_tried_again()
    {
        var count = Path.Combine(_home, "ran.txt");
        var script = Script("fail.mjs", $"import {{ appendFileSync }} from 'node:fs'; appendFileSync({System.Text.Json.JsonSerializer.Serialize(count)}, 'ran\\n'); process.exit(3);");
        var door = new LegacyDoor(Provider(), ResumingDoor());
        var info = door.PrepareResume(Target(), Command(script), "c-1", "go on")!;

        Assert.True(door.Resumes);
        Assert.Null(door.StructuredOutput());
        var (_, exit) = await Ran(info);
        Assert.Equal(3, exit);
        Assert.Equal(["ran"], File.ReadAllLines(count));
    }

    private static AgentDoor ResumingDoor() => new(
        new DoorId("resuming"), Transport.Native, new DoorIdentity(null, null, null), Known<ProgramFacts>.Absent,
        new DoorHome(null, true, null, false), LentPart.None, Known<PortRef>.Absent, new PosturesPart(null, null, null),
        Known<ISpawn>.Declared(new ResumingSpawn(), "a test door"), Known<IDialect>.Absent,
        new AbilitiesPart(Known<bool>.Declared(true, "t"), Known<bool>.Declared(true, "a native resume"), Known<bool>.Declared(false, "t")),
        new DoorTablesPart(null, null), null);

    private static Provider Provider() => new(
        new ProviderId("resuming"), new ProviderIdentity(null, null, null, false, []), [ResumingDoor()], Known<ProgramFacts>.Absent,
        Known<AccountsPart>.Absent, Known<TablesPart>.Absent, Known<UsagePart>.Absent, Known<LentPart>.Absent, Known<PortRef>.Absent,
        Known<IChildVocabulary>.Absent, Known<PortRef>.Absent, Known<PortRef>.Absent, Known<IConversationHome>.Absent, Reach.Everywhere,
        Known<VocabularyPart>.Absent);

    private sealed class ResumingSpawn : ISpawn
    {
        public ProcessStartInfo Start(SessionTarget target, IReadOnlyList<string>? command) => Spawning.InRoot(target, command![0], command.Skip(1));

        public ProcessStartInfo Chat(ChatTarget target, IReadOnlyList<string>? command) => Spawning.ChatInRoot(target, command![0], command.Skip(1));

        public ProcessStartInfo? Resume(SessionTarget target, IReadOnlyList<string>? command, string conversation, string prompt) =>
            Spawning.InRoot(target, command![0], command.Skip(1).Append(conversation).Append(prompt));
    }
}
