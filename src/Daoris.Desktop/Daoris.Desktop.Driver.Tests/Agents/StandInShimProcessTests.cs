using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The stand-ins on the contract, through the shim,
/// open, send and stop on each door and <c>acp-stub</c> resumes, in real processes. No model, no account. A separate test door states a native resume and no native dialect, and its resume's early
/// failed exit is read by the plain capture and not tried again (the retry is the structured capture's: <c>Driver.cs</c>'s
/// <c>structured is not null</c>).
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Harness)]
public sealed class StandInShimProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-shim-" + Guid.NewGuid().ToString("N")[..8]);

    public StandInShimProcessTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private SessionTarget Target() => new("q-1", "title", "body", "asker", "repo", _home, "http://svc");

    private SpawnRequest Chat() => SpawnRequests.Chat("repo", _home, "http://svc", null);

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

    /// <summary>A protocol agent that keeps the methods it is sent in a file, one per line, and answers the spec's calls.</summary>
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

    /// <summary>The pipe stand-in: a driven start and a conversation's words are answered, and a stop ends the process.</summary>
    [Fact]
    public async Task The_pipe_stand_in_starts_hears_and_stops()
    {
        var door = ProviderRegistry.Built().Door("stub");
        var command = Command(Text());

        var started = await Ran(ProviderRegistry.Built().Start(door, Target(), command));
        var spoken = await Ran(ProviderRegistry.Built().Chat(door, Chat(), command), "hello");

        Assert.Equal(0, started.Exit);
        Assert.Contains("heard: hello", spoken.Out);
        Assert.Equal(0, spoken.Exit);

        // A stop: the process is ended while it waits on its stdin.
        using var waiting = Process.Start(ProviderRegistry.Built().Chat(door, Chat(), command))!;
        await Task.Delay(200);
        waiting.Kill(entireProcessTree: true);
        Assert.True(waiting.WaitForExit(10_000));
    }

    /// <summary>The protocol stand-in: open, send and close on the wire, then a resume by the agent's own answer.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_protocol_stand_in_opens_sends_and_resumes(bool resume)
    {
        var door = ProviderRegistry.Built().Door("acp-stub");

        var methodsNew = await Wire(door, "new.txt", resume);

        Assert.Equal(resume ? ["initialize", "session/resume", "session/prompt", "session/close"] : ["initialize", "session/new", "session/prompt", "session/close"], methodsNew);
    }

    private async Task<IReadOnlyList<string>> Wire(AgentDoor adapter, string seen, bool resume)
    {
        var path = Path.Combine(_home, seen);
        using var process = Process.Start(ProviderRegistry.Built().Start(adapter, Target(), Command(Acp(), path)))!;
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
    public async Task The_protocol_stand_in_is_stopped_by_ending_its_process()
    {
        var door = ProviderRegistry.Built().Door("acp-stub");
        using var process = Process.Start(ProviderRegistry.Built().Start(door, Target(), Command(Acp(), Path.Combine(_home, "stopped.txt"))))!;
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
        var registry = TestDoors.Registry(Provider());
        var door = registry.Door("resuming");
        var info = registry.Resume(door, Target(), Command(script), "c-1")!;

        Assert.True(door.Can.Resumes);
        Assert.Null(door.StructuredOutput());
        var (_, exit) = await Ran(info);
        Assert.Equal(3, exit);
        Assert.Equal(["ran"], File.ReadAllLines(count));
    }

    private static AgentDoor ResumingDoor() => new(
        new DoorId("resuming"), Transport.Native, new DoorIdentity(null, null, null), Known<ToolSpec>.Absent,
        new DoorHome(null, true, null), LentPart.None, Known<IDoorOffers>.Absent, new PosturesPart(null, null, null, null),
        Known<ISpawn>.Declared(new ResumingSpawn(), "a test door"), Known<IDialect>.Absent,
        new AbilitiesPart(Known<bool>.Declared(true, "t"), Known<bool>.Declared(true, "a native resume"), Known<bool>.Declared(false, "t")),
        new DoorTablesPart(null, null), null, InstructionsPart.None);

    private static Provider Provider() => new(
        new ProviderId("resuming"), new ProviderIdentity(null, null, null, false, []), [ResumingDoor()], Known<ToolSpec>.Absent,
        Known<AccountsPart>.Absent, Known<TablesPart>.Absent, Known<UsagePart>.Absent, Known<LentPart>.Absent, Known<ISettingsFile>.Absent,
        Known<IChildVocabulary>.Absent, Known<IRuleLanguage>.Absent, Known<ISkillLayout>.Absent, Known<IConversationHome>.Absent, Reach.Everywhere,
        Known<VocabularyPart>.Absent);

    private sealed class ResumingSpawn : ISpawn
    {
        public ProcessRecipe Start(SpawnRequest request) => ProcessRecipe.Of(request.Command!);

        public ProcessRecipe Chat(SpawnRequest request) => ProcessRecipe.Of(request.Command!);

        public ProcessRecipe? Resume(SpawnRequest request) => ProcessRecipe.Of(request.Command!, [request.Resume!]);
    }
}
