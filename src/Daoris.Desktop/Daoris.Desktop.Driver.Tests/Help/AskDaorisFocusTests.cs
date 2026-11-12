using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Two Ask Daoris conversations of one workspace on different focuses, started together, each read their own in their first words'
/// standing block, the room they share naming neither; one going on afterwards resumes its own conversation in that room.
/// </summary>
/// <remarks>A real process (node) speaking the protocol door, and the in-process ledger of <see cref="ChatGoOnTests"/>. No model, no account.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Harness)]
public sealed class AskDaorisFocusTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-ask-focus-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public AskDaorisFocusTests()
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

    private static readonly HelpFocus Engine = HelpFocus.OfRepository("default", "engine");

    private string Heard => Path.Combine(_home, "heard.txt");

    private string[] HeardLines() => StubFile.Lines(Heard);

    private string Room => File.ReadAllText(Path.Combine(HelpRoom.PathOf(_home), "AGENTS.md"));

    /// <summary>The first prompt each stand-in conversation heard, by the words the person said in it.</summary>
    private string FirstPrompt(string words) =>
        JsonSerializer.Deserialize<string>(Assert.Single(HeardLines(), line => line.StartsWith($"first {words}: ", StringComparison.Ordinal))[$"first {words}: ".Length..])!;

    [Fact]
    public async Task Two_conversations_started_together_on_different_focuses_and_a_resume_each_read_their_own()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Heard] },
        };
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        var processes = new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions));
        using var client = ledger.Client();
        var adapters = ProviderRegistry.Built();
        using var runner = new ChatRunner(
            client, adapters, _home, processes, events: events, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
        string Seen() => $"heard [{string.Join(" | ", HeardLines())}]";

        // Placed one after the other as two presses are, each its focus kept, then started together.
        var placedMachine = await runner.PlaceHelpAsync("acp-stub", config);
        var placedEngine = await runner.PlaceHelpAsync("acp-stub", config);
        var (machine, engine) = (placedMachine.SessionId!, placedEngine.SessionId!);
        var focusing = new HelpFocusing(_home);
        focusing.Begin(machine, HelpFocus.Machine);
        focusing.Begin(engine, Engine);
        var started = await Task.WhenAll(
            runner.RunHelpAsync(placedMachine, config, _ => Task.FromResult<HelpMachine?>(Machine)),
            runner.RunHelpAsync(placedEngine, config, _ => Task.FromResult<HelpMachine?>(Machine)));
        Assert.All(started, start => Assert.NotNull(start.SessionId));
        await Poll.Until(() => ledger.State(machine) == "working" && ledger.State(engine) == "working", Seen);

        Assert.True(runner.Say(machine, "one"));
        Assert.True(runner.Say(engine, "two"));
        await Poll.Until(() => HeardLines().Count(line => line.StartsWith("first ", StringComparison.Ordinal)) == 2, Seen);

        var one = FirstPrompt("one");
        var two = FirstPrompt("two");
        Assert.Contains("Its focus is this machine: every workspace.", one, StringComparison.Ordinal);
        Assert.DoesNotContain("`engine` in workspace", one, StringComparison.Ordinal);
        Assert.Contains("Its focus is repository `engine` in workspace `default`", two, StringComparison.Ordinal);
        Assert.Contains("## In focus: repository `engine`", two, StringComparison.Ordinal);
        Assert.DoesNotContain("Its focus is this machine", two, StringComparison.Ordinal);
        // The room both read names neither.
        Assert.DoesNotContain("Its focus is", Room, StringComparison.Ordinal);
        Assert.DoesNotContain("## In focus", Room, StringComparison.Ordinal);

        // The first goes on after the second started on its own focus: its own conversation resumed, in a room still naming none.
        Assert.True(processes.Stop(machine));
        await Poll.Until(() => ledger.State(machine) == "stopped", Seen);
        ledger.For(machine).Say("w1", "and now?");
        events.Append(machine, new SessionEvent
        {
            Kind = SessionEventKind.User, Origin = "person", Id = "w1", Text = "and now?", Reaches = "resume", Door = "screen",
        });
        await Poll.Until(() => HeardLines().Any(line => line.StartsWith("again ", StringComparison.Ordinal)), Seen);

        var conversation = Assert.Single(HeardLines(), line => line.StartsWith("opened one ", StringComparison.Ordinal))["opened one ".Length..];
        Assert.Contains($"session/resume: {conversation}", HeardLines());
        Assert.Contains($"again {conversation}: \"and now?\"", HeardLines());
        Assert.DoesNotContain("Its focus is", Room, StringComparison.Ordinal);
        Assert.True(runner.Finish(machine));
        Assert.True(runner.Finish(engine));
    }

    /// <summary>
    /// A protocol-door agent whose conversations each keep an id of their own: it notes the first prompt of each by the words in it, whole,
    /// the conversation it opened for them, and a resumed conversation's prompt by the conversation's id.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-focus-agent.mjs");
        File.WriteAllText(script, StubPlace.Js + """
            import { appendFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const heard = process.argv[2];
            const note = (text) => appendFileSync(heard, text + '\n');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            let conversation = null;
            let resumed = false;
            let first = true;
            const update = (body) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation, update: body } });
            for await (const line of createInterface({ input: process.stdin })) {
              let frame;
              try { frame = JSON.parse(line); } catch { continue; }
              if (frame.method === 'initialize') {
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1,
                  agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                conversation = 'conv-' + process.pid;
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/resume') {
                note('session/resume: ' + frame.params.sessionId);
                conversation = frame.params.sessionId;
                resumed = true;
                send({ jsonrpc: '2.0', id: frame.id, result: {} });
              } else if (frame.method === 'session/prompt') {
                const whole = frame.params.prompt.filter((block) => block.type === 'text').map((block) => block.text).join(' | ');
                const said = wordsOf(whole);
                if (resumed) note('again ' + conversation + ': ' + JSON.stringify(whole));
                else if (first) { note('first ' + said + ': ' + JSON.stringify(whole)); note('opened ' + said + ' ' + conversation); }
                first = false;
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
