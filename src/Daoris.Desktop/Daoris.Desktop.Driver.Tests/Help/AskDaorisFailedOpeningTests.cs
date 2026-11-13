using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A conversation whose start failed at its opening began no conversation, so the person's next word continues it as a refused start's
/// does: a fresh start of its actor, never a resume and never a brief, the word its first message, answered once its servers come up.
/// </summary>
/// <remarks>A real process (node) speaking the protocol door, a stand-in server that never answers, and the in-process ledger of <see cref="ChatGoOnTests"/>.</remarks>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
[Trait(Category.Needs, Needed.Harness)]
public sealed class AskDaorisFailedOpeningTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-ask-failed-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public AskDaorisFailedOpeningTests()
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

    /// <summary>Where the stand-in looks to learn its servers came up: absent, it waits on them for ever.</summary>
    private string Up => Path.Combine(_home, "servers-up");

    private string[] HeardLines() => StubFile.Lines(Heard);

    [Fact]
    public async Task A_start_failed_at_its_opening_is_continued_by_the_next_word()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = DriverConfig.Empty with
        {
            Actors = StaffedRoles.On("acp-stub", helper: "acp-stub"),
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Heard, Up] },
        };
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        using var client = ledger.Client();
        var adapters = ProviderRegistry.Built();
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            OpeningBound = TimeSpan.FromSeconds(2),
            DescribeHelp = _ => Task.FromResult<HelpMachine?>(Machine),
        };
        string Seen(string id) => $"heard [{string.Join(" | ", HeardLines())}], state {ledger.State(id)}, records {ledger.Count}, "
            + $"record [{string.Join(" | ", events.After(id, 0).Events.Select(e => $"{e.Kind}:{e.Text}"))}]";

        var id = (await runner.StartHelpAsync("acp-stub", config, Machine)).SessionId!;
        await Poll.Until(() => ledger.State(id) == "failed", () => Seen(id));
        Assert.Single(events.After(id, 0).Events, e => e.Parts?.Any(part => part.Code == "chat.help-not-up") == true);

        // Its servers come up, and the person's next word, kept on the record as the say door keeps it, continues it.
        File.WriteAllText(Up, "");
        ledger.For(id).Say("w1", "are you there now?");
        events.Append(id, new SessionEvent
        {
            Kind = SessionEventKind.User, Origin = "person", Id = "w1", Text = "are you there now?", Reaches = "resume", Door = "screen",
        });

        const string fresh = "h2";
        await Poll.Until(
            () => ledger.Count == 2 && events.After(fresh, 0).Events.Any(e => e.Kind == SessionEventKind.Message && e.Text == "heard are you there now?"),
            () => Seen(id) + $"; fresh {(ledger.Count == 2 ? Seen(fresh) : "none")}");
        Assert.Equal(["initialize", "session/new waits", "initialize", "session/new opened", "prompt: are you there now?"], HeardLines());
        Assert.DoesNotContain(events.After(id, 0).Events, e => e.Text?.Contains("cannot go on", StringComparison.Ordinal) == true);
        Assert.True(runner.Finish(fresh));
    }

    /// <summary>A word kept on the record while the conversation starts is taken by its door once it opens, in the order kept, once.</summary>
    [Fact]
    public async Task A_word_kept_on_the_record_while_it_starts_is_taken_by_its_door_once_and_only_once()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = DriverConfig.Empty with
        {
            Actors = StaffedRoles.On("acp-stub", helper: "acp-stub"),
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["acp-stub"] = ["node", Agent(), Heard, Up] },
        };
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        File.WriteAllText(Up, "");
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        using var client = ledger.Client();
        var adapters = ProviderRegistry.Built();
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: events,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            OpeningBound = TimeSpan.FromSeconds(20),
        };

        var placed = await runner.PlaceHelpAsync("acp-stub", config);
        var id = placed.SessionId!;
        ledger.For(id).Say("w1", "are you there?");
        ledger.For(id).Say("w2", "and now?");
        var start = await runner.RunHelpAsync(placed, config, _ => Task.FromResult<HelpMachine?>(Machine));
        Assert.Equal(id, start.SessionId);

        Assert.Equal(2, await runner.TakeKeptAsync(id));
        // Handed again, nothing goes twice: the turns know each word by its record id.
        await runner.TakeKeptAsync(id);
        await Poll.Until(
            () => HeardLines().Contains("prompt: and now?"), () => $"heard [{string.Join(" | ", HeardLines())}], taken [{string.Join(" | ", ledger.Taken)}]");
        Assert.Equal(["initialize", "session/new opened", "prompt: are you there?", "prompt: and now?"], HeardLines());
        Assert.True(runner.Finish(id));
    }

    /// <summary>
    /// A protocol-door harness that opens its conversation only once its servers are up: until <c>servers-up</c> exists it waits on them for
    /// ever, as one handed a server that never answers does; after, it opens at once and answers each prompt.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-failed-agent.mjs");
        File.WriteAllText(script, StubPlace.Js + """
            import { appendFileSync, existsSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [heard, up] = process.argv.slice(2);
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
              } else if (frame.method === 'session/new' || frame.method === 'session/resume') {
                note(frame.method + (existsSync(up) ? ' opened' : ' waits'));
                if (!existsSync(up)) { setTimeout(() => process.exit(3), 120000); continue; }
                conversation = frame.params.sessionId ?? 'conv-f';
                send({ jsonrpc: '2.0', id: frame.id, result: frame.method === 'session/new' ? { sessionId: conversation } : {} });
              } else if (frame.method === 'session/prompt') {
                const said = wordsOf(frame.params.prompt.filter((block) => block.type === 'text').map((block) => block.text).join(' | '));
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
