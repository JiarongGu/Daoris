using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT1c (D163 point 3; the mixed agents design §2, §5; D191's settling): the driver's look settles an agent's ask to hand its
/// session over by the person's `handOver` rule. `ask` holds it for the person's word with the agent it would go to and says so on the
/// session's record; `off` refuses it, saying why; `on` takes it to the next agent in the person's order: a conversation's box is kept
/// for its turn's end (or handed to the shell's chat runner where one is wired), a running quest session's ask waits for its turn's end,
/// and a quest session whose turn ended is handed over at once, its note carried to the next start.
/// </summary>
/// <remarks>In-process: the service is the stand-in ledger, the roster's doors a stand-in, each start's run a stand-in.</remarks>
public sealed class HandOverAskLookTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

    private static readonly DateTimeOffset T0 = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-handask-look-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly List<(string Session, string Agent)> _handed = [];

    public HandOverAskLookTests()
    {
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));
        _ledger.Register("engine", Path.Combine(_home, "engine"));
        _ledger.Publish("q1", "engine");
    }

    public void Dispose()
    {
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static DriverConfig Config(string? mode = null) =>
        (DriverConfig.Empty with { Drivable = ["engine"], Adapter = "stub", Cap = 3, PollSeconds = 1, Trees = ["engine"] }) is var config && mode is not null
            ? HandOverRules.Apply(config, new HandOverEdit { Repository = "engine", Set = new HandOverSet { Mode = mode, ModeGiven = true } })
            : config;

    /// <summary>A driver for a look: the roster's doors a stand-in, its starts only said, and the chat runner's take where <paramref name="wired"/>.</summary>
    private Daoris.Driver.Driver Driver(string? mode = null, bool wired = false)
    {
        var service = _ledger.Client();
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(service, Config(mode), adapters, _home, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Stops = (session, note) =>
            {
                service.AdvanceAsync(session, "stopped", note).GetAwaiter().GetResult();
                return true;
            },
            Runner = (start, _, _) => Task.FromResult(new StartRun($"stand-in  #{start.Quest.Id}", false)),
            HandOverDoors = _ => Task.FromResult<IReadOnlyList<HandOverDoor>>([new("stub", true, null), new("codex-acp", true, "codex")]),
            HandsOver = wired
                ? (session, agent, _) =>
                {
                    _handed.Add((session, agent));
                    return Task.FromResult(new HandedOver("n3wc0nv1", true, null, "taken"));
                }
                : null,
        };
    }

    private void Asks(string session, string id = "a1b2c3d4", string note = "Account near its limit; ParserTests are left.") =>
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Sessions, session + HandOverAskFiles.AskSuffix),
            $$"""{ "id": "{{id}}", "session": "{{session}}", "note": "{{note}}", "askedBy": "agent", "at": "{{Moments.Write(T0)}}" }""");

    private HandOverAskState? State(string session) => new HandOverAskFiles(_home).ReadState(session);

    private string Tree(string name)
    {
        var tree = Path.Combine(_home, name);
        Directory.CreateDirectory(tree);
        return tree;
    }

    private IReadOnlyList<string> Notes(string session) =>
        [.. new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After(session, 0).Events
            .Where(e => e.Kind == SessionEventKind.Note).SelectMany(e => e.Parts ?? []).Select(part => part.Code ?? "")];

    /// <summary>🔴 `ask`, the default: the ask waits for the person's word, naming the next agent in their order, and its record says so once.</summary>
    [Fact]
    public async Task Under_ask_the_ask_waits_for_the_persons_word_naming_the_agent_and_its_record_says_so_once()
    {
        _ledger.Working("c0nv0001", "engine", Tree("tree-c"));
        Asks("c0nv0001");

        var look = await Driver().TickAsync().WaitAsync(Bound);
        await Driver().TickAsync().WaitAsync(Bound);

        Assert.Equal((HandOverAskStates.Waiting, "codex-acp"), (State("c0nv0001")!.State, State("c0nv0001")!.Agent));
        Assert.Equal(["handover.asked"], Notes("c0nv0001"));
        Assert.Null(new HandOverBox(_home).Read("c0nv0001"));
        Assert.Contains(look.Events, line => line.Contains("waits for the person's yes or no in What needs you", StringComparison.Ordinal));
    }

    /// <summary>🔴 `off`: refused, the session's record saying why.</summary>
    [Fact]
    public async Task Under_off_the_ask_is_refused_and_its_record_says_why()
    {
        _ledger.Working("c0nv0001", "engine", Tree("tree-c"));
        Asks("c0nv0001");

        await Driver(HandOverRules.Off).TickAsync().WaitAsync(Bound);

        Assert.Equal((HandOverAskStates.Refused, HandOverAskWhy.Off), (State("c0nv0001")!.State, State("c0nv0001")!.Code));
        Assert.Equal(["handover.refused"], Notes("c0nv0001"));
        Assert.Null(new HandOverBox(_home).Read("c0nv0001"));
    }

    /// <summary>
    /// 🔴 `on`, a conversation: the hand-over's box is kept for its turn's end as the agent's ask (why and asker `agent`), to the next
    /// agent in the person's order, and the record says so; where the shell's chat runner is wired, it takes the ask itself.
    /// </summary>
    [Fact]
    public async Task Under_on_a_conversations_ask_is_kept_as_a_box_for_its_turns_end_or_handed_to_the_chat_runner()
    {
        _ledger.Working("c0nv0001", "engine", Tree("tree-c"));
        Asks("c0nv0001");

        await Driver(HandOverRules.On).TickAsync().WaitAsync(Bound);

        var box = new HandOverBox(_home).Read("c0nv0001")!;
        Assert.Equal(("codex-acp", HandOverWhy.Agent, HandOverBy.Agent), (box.Agent, box.Why, box.AskedBy));
        Assert.Equal((HandOverAskStates.Taken, "codex-acp"), (State("c0nv0001")!.State, State("c0nv0001")!.Agent));
        Assert.Equal(["handover.taking"], Notes("c0nv0001"));

        _ledger.Working("c0nv0002", "engine", Tree("tree-d"));
        Asks("c0nv0002", id: "b1b2c3d4");
        await Driver(HandOverRules.On, wired: true).TickAsync().WaitAsync(Bound);

        Assert.Equal([("c0nv0002", "codex-acp")], _handed);
        Assert.Null(new HandOverBox(_home).Read("c0nv0002"));
        Assert.Equal(HandOverAskStates.Taken, State("c0nv0002")!.State);
    }

    /// <summary>🔴 `on`, a quest's session still working its turn: never stopped mid-turn; the ask goes at the turn's end.</summary>
    [Fact]
    public async Task Under_on_a_running_quest_sessions_ask_waits_for_its_turns_end()
    {
        _ledger.Driven("s1", "q1", Tree("tree-s1"), adapter: "stub");
        Asks("s1");

        await Driver(HandOverRules.On).TickAsync().WaitAsync(Bound);

        Assert.Equal((HandOverAskStates.Approved, "codex-acp"), (State("s1")!.State, State("s1")!.Agent));
        Assert.Equal("working", _ledger.Session("s1")["state"]!.GetValue<string>());
        Assert.Null(new HandOverBox(_home).Read("s1"));
        Assert.Empty(new HandedQuests(_home).All());
        Assert.Equal(["handover.taking"], Notes("s1"));
    }

    /// <summary>
    /// 🔴 `on`, a quest's session whose turn ended holding its quest (parked, STANDDOWN2): handed over at once, its record stopped with its
    /// own line, the quest held on the next agent with the agent's note, which the carry-on is told as that agent's claim.
    /// </summary>
    [Fact]
    public async Task Under_on_a_quest_session_that_ended_its_turn_is_handed_over_with_its_note()
    {
        _ledger.Park("s1", "q1", Tree("tree-s1"));
        Asks("s1");

        var look = await Driver(HandOverRules.On).TickAsync().WaitAsync(Bound);

        Assert.Equal("stopped", _ledger.Session("s1")["state"]!.GetValue<string>());
        var handed = new HandedQuests(_home).Read("q1")!;
        Assert.Equal(("codex-acp", "s1", HandOverWhy.Agent, HandOverBy.Agent), (handed.Agent, handed.From, handed.Why, handed.AskedBy));
        Assert.Equal("Account near its limit; ParserTests are left.", handed.Note);
        Assert.Equal(HandOverAskStates.Taken, State("s1")!.State);
        Assert.Contains(look.Events, line => line == "hand-over  session `s1` (#q1): stopped, and `#q1` goes on with codex-acp.");
    }

    /// <summary>The person's yes under `ask`: the agent the look named is the one it goes to, as `on` would take it.</summary>
    [Fact]
    public async Task The_persons_yes_lets_a_waiting_ask_go_to_the_agent_named()
    {
        _ledger.Working("c0nv0001", "engine", Tree("tree-c"));
        Asks("c0nv0001");
        await Driver().TickAsync().WaitAsync(Bound);

        var yes = HandOverAskWord.Approve(new HandOverAskFiles(_home), "c0nv0001", "a1b2c3d4");
        await Driver().TickAsync().WaitAsync(Bound);

        Assert.True(yes.Stands);
        Assert.Equal("codex-acp", new HandOverBox(_home).Read("c0nv0001")!.Agent);
        Assert.Equal(HandOverAskStates.Taken, State("c0nv0001")!.State);
    }

    /// <summary>🔴 No second hand-over without a turn between: a part a hand-over started asks before its first turn ended, whatever the rule.</summary>
    [Fact]
    public async Task A_part_a_hand_over_started_cannot_ask_before_its_first_turn_ends()
    {
        _ledger.Working("c0nv0001", "engine", Tree("tree-c"));
        _ledger.Continues("c0nv0001", "c0nv0000", HandOverWhy.Agent, HandOverBy.Agent);
        Asks("c0nv0001");

        await Driver(HandOverRules.On).TickAsync().WaitAsync(Bound);

        Assert.Equal((HandOverAskStates.Refused, HandOverAskWhy.JustHanded), (State("c0nv0001")!.State, State("c0nv0001")!.Code));
        Assert.Null(new HandOverBox(_home).Read("c0nv0001"));

        // Its first turn ended before a later ask: that one is the rule's.
        var events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
        events.Append("c0nv0001", new SessionEvent { Kind = SessionEventKind.Turn, Text = "end_turn", At = T0.AddMinutes(1) });
        File.Delete(DaorisHome.Entry(_home, HomeEntry.Sessions, "c0nv0001" + HandOverAskFiles.AskSuffix));
        File.WriteAllText(DaorisHome.Entry(_home, HomeEntry.Sessions, "c0nv0001" + HandOverAskFiles.AskSuffix),
            $$"""{ "id": "e1e2e3e4", "session": "c0nv0001", "note": "Again.", "askedBy": "agent", "at": "{{Moments.Write(T0.AddMinutes(2))}}" }""");

        await Driver(HandOverRules.On).TickAsync().WaitAsync(Bound);

        Assert.Equal(("e1e2e3e4", HandOverAskStates.Taken), (State("c0nv0001")!.Id, State("c0nv0001")!.State));
    }

    /// <summary>A session gone from this machine leaves nothing: its ask and state are taken away, and no record is written to.</summary>
    [Fact]
    public async Task An_ask_whose_session_is_gone_is_cleared()
    {
        Asks("g0n3s3ss");

        await Driver(HandOverRules.On).TickAsync().WaitAsync(Bound);

        Assert.Null(new HandOverAskFiles(_home).Ask("g0n3s3ss"));
        Assert.Null(State("g0n3s3ss"));
    }
}
