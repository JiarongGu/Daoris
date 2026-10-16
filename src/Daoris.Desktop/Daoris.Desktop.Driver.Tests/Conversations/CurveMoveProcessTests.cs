using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SWITCH1c-ii (D165 point 3; the switch design §4) with real conversations: a conversation whose account is over its usage curve
/// moves on its own at its turn's end to another account of the same agent that has room. The old ends, a new one opens in its tree
/// on that account, told Daoris's briefing; its record continues the old by <c>curve</c>, asked by <c>daoris</c>; the old one's last
/// line is <c>chat.moved</c> and the new one's first <c>chat.moved-from</c>. Moved once: within thirty minutes of that move, a turn's
/// end over the curve again does not move it.
/// </summary>
/// <remarks>
/// The conversation runs on the <c>acp-stub</c> adapter (a node stand-in on the protocol door), whose accounts are the stub's
/// (TOOL4j: <c>accountOf</c>), two of them in the machine's list; the stub's command answers the sign-in question. What each account
/// said is written as a read would write it, and the roster's clock stands ten minutes after the in-process ledger's records were
/// made, so the hold is counted from the new record's <c>created</c>. Written without a run: the merge's Process half runs it.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class CurveMoveProcessTests : IDisposable
{
    /// <summary>Ten minutes after the ledger's records are made (<c>2026-10-03T08:00:00Z</c>): a move made then holds until 08:30.</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 8, 10, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-curve-proc-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    public CurveMoveProcessTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
        foreach (var account in new[] { "account-1", "account-2" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "stub", account));
        }

        new HarnessSettings().WithRotation("stub", ["account-1", "account-2"]).Save(DaorisHome.Entry(_home, HomeEntry.Harnesses));
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Heard => Path.Combine(_home, "heard.txt");

    private SessionEvents Events => new(DaorisHome.Entry(_home, HomeEntry.Sessions));

    /// <summary>The protocol stand-in's command, and the stub's, which answers the sign-in question for the stub's accounts.</summary>
    private DriverConfig Config()
    {
        var config = DriverConfig.Empty with
        {
            Commands = new Dictionary<string, IReadOnlyList<string>>
            {
                ["acp-stub"] = ["node", Agent(), Heard],
                ["stub"] = ["node", TextAgent()],
            },
        };
        config.Save(DaorisHome.Entry(_home, HomeEntry.DriverConfig));
        return config;
    }

    /// <summary>
    /// What an account said of its windows, as a read writes it: its week half gone (the smart curve allows 70% of it now) at
    /// <paramref name="weekly"/> used, its five hours at 10%.
    /// </summary>
    private void Said(string account, double weekly, int secondsAgo) =>
        AccountWindows.Said(
            _home, "stub", account,
            [new WindowReading("session", 0.10, Now.AddHours(1)), new WindowReading("weekly", weekly, Now.AddDays(3.5))],
            Now.AddSeconds(-secondsAgo), session: null, source: AccountWindows.SourceRead);

    private string Seen(ChatLedger ledger, SessionEvents events, string id) =>
        $"heard [{string.Join(" | ", StubFile.Lines(Heard))}], records {ledger.Count}, state {ledger.State(id)}, "
        + $"record [{string.Join(" | ", events.After(id, 0).Events.Select(e => $"{e.Kind}:{e.Text}"))}]";

    private static bool Ended(string state) => state is "completed" or "stopped" or "failed";

    private static NotePart? Coded(SessionEvent e) => e.Kind == SessionEventKind.Note ? e.Parts?.FirstOrDefault(part => part.Code is not null) : null;

    /// <summary>
    /// 🔴 A turn's end over the curve moves the conversation once, to the account with room, with both lines said and its record's
    /// `from` naming the old by the curve; a turn's end within thirty minutes, its new account over the curve, holds.
    /// </summary>
    [Fact]
    public async Task A_conversation_over_its_curve_moves_at_its_turn_s_end_once_and_is_held_after()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        var config = Config();
        var events = Events;
        using var client = ledger.Client();
        var adapters = AdapterSet.Built();
        var roster = new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)) { Clock = () => Now };
        using var runner = new ChatRunner(
            client, adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), events: events, harnesses: roster);
        var judged = new List<(string Session, SwitchChoice Choice)>();
        runner.CurveJudged += (session, choice) =>
        {
            lock (judged) judged.Add((session, choice));
        };

        // account-1 has used 75% of its week where its curve allows 70; account-2 30%.
        Said("account-1", 0.75, secondsAgo: 30);
        Said("account-2", 0.30, secondsAgo: 30);
        var old = (await runner.StartAsync("engine", "acp-stub", config, profile: "account-1")).SessionId!;
        await Poll.Until(() => ledger.State(old) == "working", () => Seen(ledger, events, old));
        Assert.True(runner.Say(old, "Make the port configurable."));

        // Its turn's end moves it: the old ends, and a new one opens in its tree on account-2 of the same agent.
        await Poll.Until(() => ledger.Count == 2 && Ended(ledger.State(old)), () => Seen(ledger, events, old));
        var next = (await client.RecordAsync("c2"))!;
        Assert.Equal(("acp-stub", "account-2", _root), (next.Adapter, next.Profile, next.Tree));
        Assert.Equal((old, "curve", "daoris"), (next.From, next.FromWhy, next.FromBy));
        // The take clears the box last, after the new one is briefed: its record can stand before then.
        await Poll.Until(() => new HandOverBox(_home).Read(old) is null, () => $"the box still holds {old}'s move; {Seen(ledger, events, old)}");

        // Said where it happened, as SWITCH1c-i's pair: the old one's last line, the new one's first.
        await Poll.Until(() => events.After(next.Session, 0).Events.Any(e => Coded(e)?.Code == "chat.moved-from"), () => Seen(ledger, events, next.Session));
        var opened = events.After(next.Session, 0).Events.Select(Coded).OfType<NotePart>().ToList();
        Assert.DoesNotContain(opened, part => part.Code is "chat.taken-over" or "chat.handed-over");
        var came = opened.First(part => part.Code == "chat.moved-from");
        // A value read back from the record is the number JSON held, not the int the note was made with: compare as numbers.
        Assert.Equal(
            ("chat.moved-from", "account-1", "weekly", 75, 70),
            (came.Code, came.Value("from"), came.Value("window"), Convert.ToInt32(came.Value("used")), Convert.ToInt32(came.Value("curve"))));
        var went = events.After(old, 0).Events.Select(Coded).OfType<NotePart>().Last();
        Assert.Equal(("chat.moved", "account-2", "account-1"), (went.Code, went.Value("to"), went.Value("from")));
        await Poll.Until(
            () => StubFile.Text(Heard).Contains($"You continue the acp-stub conversation `{old}`"), () => Seen(ledger, events, next.Session));

        // Ten minutes after that move, its new account over the curve and the old one with room, a turn's end holds it there.
        Said("account-2", 0.75, secondsAgo: 20);
        Said("account-1", 0.30, secondsAgo: 20);
        await Poll.Until(() => !runner.Taking(next.Session), () => Seen(ledger, events, next.Session));
        Assert.True(runner.Say(next.Session, "Now the tests."));
        await Poll.Until(
            () =>
            {
                lock (judged) return judged.Any(each => each.Session == next.Session && each.Choice == new SwitchChoice.Stay(SwitchStay.Held));
            },
            () => $"judged [{string.Join(" | ", judged.Select(each => $"{each.Session}: {each.Choice}"))}]; " + Seen(ledger, events, next.Session));

        Assert.Equal(2, ledger.Count);
        Assert.Equal("working", ledger.State(next.Session));
        Assert.Null(new HandOverBox(_home).Read(next.Session));
        lock (judged) Assert.Single(judged, each => each.Choice is SwitchChoice.MoveTo);
    }

    /// <summary>A stand-in ACP agent: names its conversation <c>conv-1</c>, writes down each method and prompt, answers a prompt after a moment.</summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "acp-curve-agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync } from 'node:fs';
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('acp-stub 1.0.0'); process.exit(0); }
            const [heard] = process.argv.slice(2);
            const note = (text) => appendFileSync(heard, text + '\n');
            const send = (frame) => process.stdout.write(JSON.stringify(frame) + '\n');
            let conversation = null;
            const say = (text) => send({ jsonrpc: '2.0', method: 'session/update', params: { sessionId: conversation,
              update: { sessionUpdate: 'agent_message_chunk', content: { type: 'text', text } } } });
            const answer = (id, stopReason) => send({ jsonrpc: '2.0', id, result: { stopReason } });
            const lines = createInterface({ input: process.stdin });
            lines.on('line', async (line) => {
              const frame = JSON.parse(line);
              if (frame.method === 'initialize') {
                note('initialize');
                send({ jsonrpc: '2.0', id: frame.id, result: { protocolVersion: 1, agentCapabilities: { loadSession: true, sessionCapabilities: { resume: {}, close: {} } } } });
              } else if (frame.method === 'session/new') {
                note('session/new');
                conversation = 'conv-1';
                send({ jsonrpc: '2.0', id: frame.id, result: { sessionId: conversation } });
              } else if (frame.method === 'session/prompt') {
                const text = frame.params.prompt.filter((block) => block.type === 'text').map((block) => block.text).join(' | ');
                note('prompt: ' + text);
                await new Promise((resolve) => setTimeout(resolve, 100));
                say('heard ' + text.split('\n')[0]);
                answer(frame.id, 'end_turn');
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

    /// <summary>The stub's command: it answers the sign-in question for the stub's accounts, as the protocol stand-in's owner.</summary>
    private string TextAgent()
    {
        var script = Path.Combine(_home, "text-agent.mjs");
        File.WriteAllText(script, """
            import { createInterface } from 'node:readline';
            if (process.argv.includes('--version')) { console.log('stub 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const lines = createInterface({ input: process.stdin });
            lines.on('line', (line) => console.log('heard: ' + line));
            lines.on('close', () => { process.exitCode = 0; process.stdin.destroy(); });
            """);
        return script;
    }
}
