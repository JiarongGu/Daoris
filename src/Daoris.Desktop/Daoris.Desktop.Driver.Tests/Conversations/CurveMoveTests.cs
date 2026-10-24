using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SWITCH1c-ii (D165 point 3; the switch design §1.2, §2 and §4): a conversation over its account's usage curve moves on its own,
/// at its turn's end, to another account of the same agent that has room. What the runner judges: a turn just ended with nothing
/// queued, no hand-over waiting or taken, the scope's switch, the account's reading, the others read where the decision asks, and
/// the thirty minutes' hold from the record's <c>created</c> where it moved by the curve. The real client over an in-process ledger
/// and a stand-in agent whose usage question answers in memory, so nothing is spawned: the fast half. A conversation that ends and
/// another that starts are <c>CurveMoveProcessTests</c>'.
/// </summary>
public sealed class CurveMoveTests : IDisposable
{
    /// <summary>Ten minutes after the ledger's records were created (<c>2026-10-03T08:00:00Z</c>).</summary>
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 8, 10, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-curve-move-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _root;

    private DateTimeOffset _now = Now;

    private readonly List<string> _asked = [];

    // What each account's usage question answers, or null where it cannot be had.
    private readonly Dictionary<string, IReadOnlyList<WindowReading>?> _answers = new(StringComparer.OrdinalIgnoreCase);

    public CurveMoveTests()
    {
        _root = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_root);
        File.WriteAllText(Command, "");
        foreach (var account in new[] { "account-1", "account-2", "account-3" })
        {
            Directory.CreateDirectory(HarnessSettings.ProfileHome(_home, "talk", account));
        }

        new HarnessSettings().WithRotation("talk", ["account-1", "account-2", "account-3"]).Save(Settings);
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private string Settings => DaorisHome.Entry(_home, HomeEntry.Harnesses);

    private string Command => Path.Combine(_home, "talk-agent");

    /// <summary>
    /// A conversation's agent on the protocol door with accounts and a usage question, answered in memory: nothing is probed or
    /// spawned. It refuses to prepare a conversation, after its record is open, so a start ends that record failed.
    /// </summary>
    private sealed class Talk(string name, HarnessToolchain toolchain) : ISessionAdapter
    {
        public string Name => name;

        public SessionWire Wire => SessionWire.Acp;

        public bool Interactive => true;

        public HarnessToolchain? Toolchain => toolchain;

        public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("a judgement test spawns nothing");

        public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command) =>
            throw new InvalidOperationException("a judgement test spawns nothing");
    }

    private HarnessRoster Roster()
    {
        var adapters = new AdapterSet(new Dictionary<string, ISessionAdapter>(StringComparer.OrdinalIgnoreCase)
        {
            ["talk"] = new Talk("talk", new HarnessToolchain(
                Binary: [Command], VersionArguments: ["--version"], ProfileVariable: "TALK_HOME", ProbeByPresence: true,
                Usage: new UsageQuestion("talk", ["talk-agent", "usage"], [], UsageKind.Claude))),
        });
        return new HarnessRoster(adapters, Settings)
        {
            Clock = () => _now,
            AskingUsage = (owner, account, _) =>
            {
                lock (_asked) _asked.Add($"{owner}/{account}");
                return Task.FromResult(_answers.GetValueOrDefault(account));
            },
        };
    }

    private string[] Asked()
    {
        lock (_asked) return [.. _asked];
    }

    /// <summary>A week half gone, and five hours with an hour left: the smart curve allows 70% of the week now, and 90% of the five hours.</summary>
    private IReadOnlyList<WindowReading> Windows(double weekly, double session = 0.10) =>
        [new WindowReading("session", session, _now.AddHours(1)), new WindowReading("weekly", weekly, _now.AddDays(3.5))];

    /// <summary>What an account said of its windows, <paramref name="secondsAgo"/> before now.</summary>
    private void Said(string account, double weekly, int secondsAgo = 30) =>
        AccountWindows.Said(_home, "talk", account, Windows(weekly), _now.AddSeconds(-secondsAgo), session: null, source: AccountWindows.SourceRead);

    [Fact]
    public void The_scope_s_accounts_are_candidates_with_what_each_said_and_whether_the_walk_finds_it_ready()
    {
        var roster = Roster();
        Said("account-3", 0.30);
        AccountCooling.Cool(_home, new CoolingEntry("talk", "account-2", _now.AddHours(2), Stated: true, "weekly", _now, Session: null), _now);

        var candidates = roster.SwitchCandidates("talk", ["account-1", "account-2", "account-3", "account-4"]);

        Assert.Equal(
            [("account-1", false, true), ("account-2", false, false), ("account-3", true, true), ("account-4", false, false)],
            candidates.Select(each => (each.Account, each.Said is not null, each.Ready)));
        Assert.Equal(0.30, candidates[2].Said!.Of("weekly")!.Used);
    }

    /// <summary>
    /// The read at a turn's end (the switch design §1.2): each ready account whose reading is older than the question's minute, never
    /// one cooling, and the same account not again within that minute, read or not.
    /// </summary>
    [Fact]
    public async Task The_read_at_a_turn_s_end_asks_each_ready_stale_account_once_a_minute()
    {
        var roster = Roster();
        Said("account-3", 0.30, secondsAgo: 120);
        Said("account-1", 0.30, secondsAgo: 10);
        AccountCooling.Cool(_home, new CoolingEntry("talk", "account-2", _now.AddHours(2), Stated: true, "weekly", _now, Session: null), _now);
        _answers["account-3"] = Windows(0.20);

        await roster.ReadUsageAsync("talk", "talk", ["account-1", "account-2", "account-3"], DriverConfig.Empty);
        await roster.ReadUsageAsync("talk", "talk", ["account-1", "account-2", "account-3"], DriverConfig.Empty);

        Assert.Equal(["talk/account-3"], Asked());
        Assert.Equal(0.20, AccountWindows.SaidOf(_home, "talk", "account-3", _now)!.Of("weekly")!.Used);
    }

    // ——— The runner: when a turn's end is judged, and what the judgement does.

    private static readonly DateTimeOffset Ended = new(2026, 10, 3, 8, 9, 0, TimeSpan.Zero);

    private static readonly ChatMessage Word = new("and the tests", []);

    /// <summary>
    /// 🔴 Judged once per turn's end, and only as one ends with nothing after it (the switch design §4, never mid-turn): a turn
    /// running, the door still opening, a word of the person's waiting, no turn ended yet, or the turn's end already judged, judge
    /// nothing.
    /// </summary>
    [Fact]
    public void A_turn_s_end_is_judged_once_and_only_with_nothing_running_opening_or_queued()
    {
        Assert.True(CurveMove.TurnJustEnded(new ChatQueue(false, [], Ended), judged: null));
        Assert.True(CurveMove.TurnJustEnded(new ChatQueue(false, [], Ended), judged: Ended.AddMinutes(-3)));

        Assert.False(CurveMove.TurnJustEnded(new ChatQueue(false, [], Ended), judged: Ended));
        Assert.False(CurveMove.TurnJustEnded(new ChatQueue(false, [], null), judged: null));
        Assert.False(CurveMove.TurnJustEnded(new ChatQueue(true, [], Ended), judged: null));
        Assert.False(CurveMove.TurnJustEnded(new ChatQueue(false, [], Ended, Opening: true), judged: null));
        Assert.False(CurveMove.TurnJustEnded(new ChatQueue(false, [Word], Ended), judged: null));
    }

    private ChatRunner Runner(ChatLedger ledger, HarnessRoster roster) =>
        new(ledger.Client(), roster.Adapters, _home, new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)), harnesses: roster);

    /// <summary>The conversation, on `account-1` of the stand-in agent, its record made ten minutes before now.</summary>
    private ChatLedger Ledger(ContinuedFrom? from = null)
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c0nv0001", "working", "talk", _root, profile: "account-1", from: from);
        return ledger;
    }

    private Task<SwitchChoice?> JudgeAsync(ChatRunner runner) => runner.MoveByCurveAsync("c0nv0001", DriverConfig.Empty, onEnded: null);

    private IReadOnlyList<SessionEvent> Record(string id) => new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)).After(id, 0).Events;

    /// <summary>
    /// 🔴 Over its curve, with another account of the agent that has room (the switch design §4): the conversation goes on there,
    /// by the hand-over's box with its agent unchanged, why `curve`, asked by `daoris`; the new record continues the old. The stand-in
    /// refuses to prepare, so the new record ends failed: that both lines are written is the Process half's.
    /// </summary>
    [Fact]
    public async Task Over_its_curve_with_another_account_with_room_it_moves_there_by_the_box()
    {
        Said("account-1", 0.75);
        Said("account-2", 0.55);
        Said("account-3", 0.30);
        var ledger = Ledger();
        var roster = Roster();
        using var runner = Runner(ledger, roster);
        using var client = ledger.Client();

        var choice = await JudgeAsync(runner);

        var move = Assert.IsType<SwitchChoice.MoveTo>(choice);
        Assert.Equal(("account-3", "weekly", 0.75), (move.Account, move.Window, move.Used));
        Assert.Equal(2, ledger.Count);
        var moved = (await client.RecordAsync("c2"))!;
        Assert.Equal(("talk", "account-3", _root), (moved.Adapter, moved.Profile, moved.Tree));
        Assert.Equal(("c0nv0001", "curve", "daoris"), (moved.From, moved.FromWhy, moved.FromBy));
        Assert.Null(new HandOverBox(_home).Read("c0nv0001"));
        Assert.Empty(Asked());
    }

    /// <summary>
    /// 🔴 MODELROLE1d2 (D205 §3.5(a)): the curve move never takes an account that does not offer the conversation's model. account-3 has
    /// the most room, but its reading refused the model the record asked, so the move goes to account-2; where the composer's change
    /// names another model, that model is the one judged, and account-3 is moved to.
    /// </summary>
    [Theory]
    [InlineData(null, "account-2")]
    [InlineData("sonnet", "account-3")]
    public async Task A_candidate_whose_reading_refused_the_conversation_s_model_is_not_moved_to(string? overriding, string expected)
    {
        Said("account-1", 0.75);
        Said("account-2", 0.55);
        Said("account-3", 0.30);
        Assert.Null(AgentOffers.Refused(_home, "talk", "1.0.0", "account-3", "opus", _now.AddMinutes(-5)));
        var ledger = Ledger();
        ledger.Edit("c0nv0001", record =>
        {
            record["asked"] = System.Text.Json.Nodes.JsonNode.Parse(
                """{ "agent": "talk", "model": { "source": "entry", "value": "opus", "carried": true }, "effort": { "source": "account-default", "value": null, "carried": false } }""");
            if (overriding is not null) record["override"] = new System.Text.Json.Nodes.JsonObject { ["model"] = overriding };
        });
        using var runner = Runner(ledger, Roster());

        Assert.Equal(expected, Assert.IsType<SwitchChoice.MoveTo>(await JudgeAsync(runner)).Account);
    }

    /// <summary>`switch: off` moves nothing however far over (the switch design §2), and reads nothing.</summary>
    [Fact]
    public async Task Switch_off_moves_nothing()
    {
        new HarnessSettings().WithRotation("talk", ["account-1", "account-2", "account-3"]).WithUse("talk", new UseChange(Switch: "off")).Save(Settings);
        Said("account-1", 0.99);
        Said("account-2", 0.10, secondsAgo: 600);
        var ledger = Ledger();
        using var runner = Runner(ledger, Roster());

        Assert.Equal(new SwitchChoice.Stay(SwitchStay.Off), await JudgeAsync(runner));
        Assert.Equal(1, ledger.Count);
        Assert.Empty(Asked());
        Assert.Null(new HandOverBox(_home).Read("c0nv0001"));
    }

    /// <summary>Under its curve by the margin it stays and reads nothing (§1.2: idle accounts are read only when a switch is near).</summary>
    [Fact]
    public async Task Under_its_curve_by_the_margin_it_stays_and_reads_nothing()
    {
        Said("account-1", 0.40);
        Said("account-2", 0.10, secondsAgo: 600);
        var ledger = Ledger();
        using var runner = Runner(ledger, Roster());

        Assert.Equal(new SwitchChoice.Stay(SwitchStay.Room), await JudgeAsync(runner));
        Assert.Equal(1, ledger.Count);
        Assert.Empty(Asked());
    }

    /// <summary>
    /// 🔴 Within the margin of its curve, the others said long ago are read first, then judged as read (the switch design §1.2):
    /// the one with room now is moved to.
    /// </summary>
    [Fact]
    public async Task Within_the_margin_the_others_said_long_ago_are_read_first_and_judged_as_read()
    {
        Said("account-1", 0.75);
        Said("account-2", 0.90, secondsAgo: 600);
        _answers["account-2"] = Windows(0.20);
        _answers["account-3"] = Windows(0.85);
        var ledger = Ledger();
        using var runner = Runner(ledger, Roster());

        var choice = await JudgeAsync(runner);

        Assert.Equal(["talk/account-2", "talk/account-3"], Asked().Order());
        Assert.Equal("account-2", Assert.IsType<SwitchChoice.MoveTo>(choice).Account);
        Assert.Equal(2, ledger.Count);
    }

    /// <summary>Where no account of the agent has room, even read, it stays and nothing is said: the hand-over's offer is the fallback.</summary>
    [Fact]
    public async Task With_no_account_with_room_it_stays_and_nothing_is_said()
    {
        Said("account-1", 0.75);
        _answers["account-2"] = Windows(0.80);
        var ledger = Ledger();
        using var runner = Runner(ledger, Roster());

        Assert.Equal(new SwitchChoice.Stay(SwitchStay.NoRoom), await JudgeAsync(runner));
        Assert.Equal(["talk/account-2", "talk/account-3"], Asked().Order());
        Assert.Equal(1, ledger.Count);
        Assert.Null(new HandOverBox(_home).Read("c0nv0001"));
        Assert.Empty(Record("c0nv0001"));
    }

    /// <summary>
    /// 🔴 A conversation that moved by the curve less than thirty minutes ago stays, its record's `created` the move (the switch
    /// design §2); thirty minutes on, it moves again.
    /// </summary>
    [Fact]
    public async Task A_conversation_moved_by_the_curve_is_held_for_thirty_minutes_from_its_record_s_making()
    {
        var ledger = Ledger(from: new ContinuedFrom("c0nv0000", HandOverWhy.Curve, HandOverBy.Daoris));
        Said("account-1", 0.75);
        Said("account-2", 0.30);
        using (var runner = Runner(ledger, Roster()))
        {
            Assert.Equal(new SwitchChoice.Stay(SwitchStay.Held), await JudgeAsync(runner));
            Assert.Equal(1, ledger.Count);
        }

        _now = Now.AddMinutes(21);
        Said("account-1", 0.75);
        Said("account-2", 0.30);
        using (var runner = Runner(ledger, Roster()))
        {
            Assert.Equal("account-2", Assert.IsType<SwitchChoice.MoveTo>(await JudgeAsync(runner)).Account);
        }
    }

    /// <summary>A conversation handed over by the person for another reason is not held: only a move by the curve holds the next.</summary>
    [Fact]
    public async Task A_conversation_handed_over_for_another_reason_is_not_held()
    {
        var ledger = Ledger(from: new ContinuedFrom("c0nv0000", HandOverWhy.Press, HandOverBy.Person));
        Said("account-1", 0.75);
        Said("account-2", 0.30);
        using var runner = Runner(ledger, Roster());

        Assert.Equal("account-2", Assert.IsType<SwitchChoice.MoveTo>(await JudgeAsync(runner)).Account);
    }

    /// <summary>
    /// A hand-over already asked, or being taken (its box is kept until the take is done), is the person's or the earlier move's:
    /// nothing is judged, read or asked, and its box stands.
    /// </summary>
    [Fact]
    public async Task A_hand_over_waiting_or_being_taken_judges_nothing()
    {
        Said("account-1", 0.75);
        Said("account-2", 0.30);
        var asked = new HandOverRequest("other", null, HandOverWhy.Press, HandOverBy.Person, _now);
        new HandOverBox(_home).Write("c0nv0001", asked);
        var ledger = Ledger();
        using var runner = Runner(ledger, Roster());

        Assert.Null(await JudgeAsync(runner));
        Assert.Equal(asked, new HandOverBox(_home).Read("c0nv0001"));
        Assert.Equal(1, ledger.Count);
    }

    /// <summary>🔴 The hold reads when a record was made (SWITCH1c-ii): `created`, as the ledger answers it, on the record a run reads.</summary>
    [Fact]
    public async Task A_record_says_when_it_was_created()
    {
        var ledger = new ChatLedger().Register("engine", _root);
        ledger.Chat("c0nv0001", "working", "talk", _root, profile: "account-1");
        using var client = ledger.Client();

        var record = (await client.RecordAsync("c0nv0001"))!;

        Assert.Equal(new DateTimeOffset(2026, 10, 3, 8, 0, 0, TimeSpan.Zero), record.Created);
    }
}
