using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT1a-ii (D163 point 1): the driver's look takes a quest's session hand-over, whose move the ask decided in the move store
/// (FOUNDATION1f-v part c; an older build's box is imported into it). The move is judged again, the
/// running session is stopped as the person's with a line of its own, the quest is held on the named agent in
/// <c>&lt;home&gt;/handed/&lt;quest&gt;.json</c>, and the next look carries it on, the stop read as released; the store goes when the quest
/// leaves this machine.
/// </summary>
/// <remarks>In-process: the service is the stand-in ledger and each start's run a stand-in. The fast half; the same over a real node harness is the Process half.</remarks>
public sealed class HandOverLookTests : IDisposable
{
    // A missing tick answer fails the test; this bound makes no claim about elapsed product time.
    private static readonly TimeSpan Bound = Poll.Patience;

    private static readonly DateTimeOffset T0 = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-hand-look-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly List<(string Session, Noted Note)> _stopped = [];

    private readonly List<Consideration> _starts = [];

    public HandOverLookTests()
    {
        Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));
        _ledger.Register("engine", Path.Combine(_home, "engine"));
        _ledger.Publish("q1", "engine");
        _ledger.Driven("s1", "q1", Path.Combine(_home, "tree-s1"), adapter: "claude-code", profile: "account-1");
    }

    public void Dispose()
    {
        _ledger.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static DriverConfig Config() => DriverConfig.Empty with { Drivable = ["engine"], Adapter = "stub", Cap = 3, PollSeconds = 1, Trees = ["engine"] };

    private readonly HashSet<string> _dead = new(StringComparer.Ordinal);

    /// <summary>The home's move record under one run, dead where the case says so and alive otherwise; the loop's is <c>run-loop</c>, a host's.</summary>
    private MoveStore Store(string run = "run-loop", string kind = StoreProcess.Host) =>
        new(_home, () => StoreSettled.Yes, each => !_dead.Contains(each.Run), run, kind);

    private MoveRecord? MoveFrom(string left) => Store("run-probe").Of(left).LastOrDefault();

    /// <summary>The loop's driven holder (FOUNDATION1f-v part c): the lease holder's scope over the home's store, under its own run.</summary>
    private ConversationMoves Moves() =>
        new(_home, _ledger.Client(), new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions)), store: Store(), scope: MoveTakeUpScope.LeaseHolder);

    /// <summary>A driver for a look, its stop landing at once as a real run's record does once its process exited, and its starts only said.</summary>
    private Daoris.Driver.Driver Driver()
    {
        var service = _ledger.Client();
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(service, Config(), adapters, _home, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Moves = Moves(),
            Stops = (session, note) =>
            {
                _stopped.Add((session, note));
                _phaseAtStop = MoveFrom(session)?.PhaseWord;
                service.AdvanceAsync(session, "stopped", note).GetAwaiter().GetResult();
                return true;
            },
            Runner = (start, _, _) =>
            {
                _starts.Add(start);
                return Task.FromResult(new StartRun($"stand-in  #{start.Quest.Id}", false));
            },
        };
    }

    private string? _phaseAtStop;

    /// <summary>The ask as the terminal or an agent's go makes it: judged, and decided as a driven move in the store, no box.</summary>
    private async Task Ask(string session, string agent = "codex-acp") =>
        Assert.True((await QuestHandOver.AskAsync(
            _ledger.Client(), Moves(), QuestHandOver.Roster(AdapterSet.Built(), _home), session, agent, null, HandOverWhy.Limit, HandOverBy.Person)).Kept);

    /// <summary>
    /// 🔴 A running quest session with a move decided is stopped at once with its own coded line, the quest is held on the named agent, and the
    /// move goes on to <c>old-ended</c> for the carry-on to continue (FOUNDATION1f-v part c), written <c>quiescing</c> while the session ran and
    /// never as a box; the next look reads that stop as released and carries the quest on, with a sentence that says it was handed over.
    /// </summary>
    [Fact]
    public async Task A_running_quest_session_is_stopped_the_quest_held_on_the_named_agent_and_carried_on_at_the_next_look()
    {
        await Ask("s1");
        Assert.Equal("decided", MoveFrom("s1")!.PhaseWord);

        var first = await Driver().TickAsync().WaitAsync(Bound);

        var stop = Assert.Single(_stopped);
        Assert.Equal("s1", stop.Session);
        Assert.Equal(["ended.handed-over"], NoteAssert.Codes(stop.Note.Parts));
        Assert.Equal("codex-acp", stop.Note.Parts[0].Value("agent"));
        Assert.Equal(("codex-acp", "s1", HandOverWhy.Limit), (new HandedQuests(_home).Read("q1")!.Agent, new HandedQuests(_home).Read("q1")!.From, new HandedQuests(_home).Read("q1")!.Why));
        Assert.Null(new HandOverBox(_home).Read("s1"));
        Assert.Equal("quiescing", _phaseAtStop);
        var move = Assert.Single(Store("run-probe").Of("s1"));
        Assert.Equal(("old-ended", "run-loop", MoveExecutors.Driven), (move.PhaseWord, move.ClaimRun, move.Executor));
        Assert.Contains(first.Events, line => line == "hand-over  session `s1` (#q1): stopped, and `#q1` goes on with codex-acp.");
        Assert.Equal("stopped", State("s1"));

        // The person's stop that handed it over: no strike, no hold, the quest carried on in the tree it worked in.
        var next = await Driver().TickAsync().WaitAsync(Bound);

        var carry = Assert.Single(next.Considerations, each => each.Quest.Id == "q1");
        Assert.Equal(StartVerdict.Start, carry.Verdict);
        Assert.Equal("s1", carry.Resumes!.Session);
        Assert.Equal("carrying on in `engine` — session `s1` was handed over to codex-acp.", carry.Reason);
        Assert.Equal(["q1"], _starts.Select(start => start.Quest.Id));
    }

    /// <summary>A box for a conversation is the shell's, not this look's: nothing is stopped and the box stays for its runner, which imports it.</summary>
    [Fact]
    public async Task A_conversation_s_box_is_left_to_the_shell()
    {
        _ledger.Working("c0nv0001", "engine", Path.Combine(_home, "tree-c"));
        new HandOverBox(_home).Write("c0nv0001", new HandOverRequest("codex-acp", null, HandOverWhy.Limit, HandOverBy.Person, T0));

        await Driver().TickAsync().WaitAsync(Bound);

        Assert.Empty(_stopped);
        Assert.NotNull(new HandOverBox(_home).Read("c0nv0001"));
        Assert.Empty(new HandedQuests(_home).All());
        Assert.Empty(Store("run-probe").Of("c0nv0001"));
    }

    /// <summary>A move judged again as it is taken: a quest closed meanwhile stops nothing and keeps nothing, and the move ends on its old part.</summary>
    [Fact]
    public async Task A_move_whose_quest_closed_meanwhile_stops_nothing_and_says_why()
    {
        await Ask("s1");
        _ledger.Move("q1", "Done");

        var look = await Driver().TickAsync().WaitAsync(Bound);

        Assert.Empty(_stopped);
        Assert.Empty(new HandedQuests(_home).All());
        var move = Assert.Single(Store("run-probe").Of("s1"));
        Assert.Equal(("fell-back", ConversationMoves.NotOpened), (move.PhaseWord, move.FellBackCode));
        Assert.Contains(look.Events, line => line.StartsWith("hand-over  session `s1` (#q1): #q1 is not open or taken any more", StringComparison.Ordinal));
    }

    /// <summary>An agent this build has not is refused at the take, as at the ask, nothing is stopped, and the move is ended, not taken again at every look.</summary>
    [Fact]
    public async Task A_move_naming_an_agent_this_build_does_not_have_is_refused()
    {
        Store().Decide(new MoveDecision("s1", MoveExecutors.Driven, MoveHows.Brief, HandOverWhy.Limit, HandOverBy.Person) { Agent = "no-such-agent" });

        var look = await Driver().TickAsync().WaitAsync(Bound);

        Assert.Empty(_stopped);
        Assert.Contains(look.Events, line => line == "hand-over  session `s1` (#q1): no agent here is called no-such-agent.");
        Assert.Equal("fell-back", MoveFrom("s1")!.PhaseWord);
    }

    /// <summary>
    /// 🔴 FOUNDATION1f-v part c: an older build's quest box is imported once by the driven take-up and set aside, each look that finds one pending,
    /// and its move is taken as an ask's is: the session stopped, the quest held on the agent named, no box taken as a file.
    /// </summary>
    [Fact]
    public async Task An_older_build_s_quest_box_is_imported_once_by_the_look_and_set_aside_and_its_move_taken()
    {
        var box = new HandOverBox(_home);
        box.Write("s1", new HandOverRequest("codex-acp", null, HandOverWhy.Limit, HandOverBy.Person, T0));

        var first = await Driver().TickAsync().WaitAsync(Bound);

        Assert.Null(box.Read("s1"));
        Assert.True(File.Exists(box.PathOf("s1") + ".imported"));
        Assert.Equal("s1", Assert.Single(_stopped).Session);
        Assert.Equal("codex-acp", new HandedQuests(_home).Read("q1")!.Agent);
        var move = Assert.Single(Store("run-probe").Of("s1"));
        Assert.Equal(("old-ended", "run-loop", HandOverWhy.Limit, HandOverBy.Person), (move.PhaseWord, move.ClaimRun, move.Why, move.By));
        Assert.Contains(first.Events, line => line == "hand-over  session `s1` (#q1): stopped, and `#q1` goes on with codex-acp.");

        // Once: another look imports and stops nothing more.
        await Driver().TickAsync().WaitAsync(Bound);
        Assert.Single(_stopped);
        Assert.Single(Store("run-probe").Of("s1"));
    }

    /// <summary>
    /// 🔴 FOUNDATION1f-v part c: a move the terminal decided is claimed by the terminal's run, which goes no further with it, so the loop's look
    /// takes it whether that run is gone (it quit at once) or still lives, and a live shell's own decision is left to it.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_move_the_terminal_decided_is_taken_by_the_loop_s_look_whether_the_terminal_lives_or_not(bool gone)
    {
        var terminal = Store("run-verb", StoreProcess.Verb);
        var decided = terminal.Decide(new MoveDecision("s1", MoveExecutors.Driven, MoveHows.Brief, HandOverWhy.Press, HandOverBy.Person) { Agent = "codex-acp" });
        if (gone) _dead.Add("run-verb");

        await Driver().TickAsync().WaitAsync(Bound);

        Assert.Equal("s1", Assert.Single(_stopped).Session);
        var move = Assert.Single(Store("run-probe").Of("s1"));
        Assert.Equal(("old-ended", "run-loop", 2L), (move.PhaseWord, move.ClaimRun, move.ClaimGeneration));
        Assert.Throws<MoveClaimLostException>(() => terminal.Advance(decided.Claim!, MoveRecordPhase.Quiescing));
    }

    /// <summary>
    /// 🔴 A hand-over a dead loop decided and never took is taken by the next loop (§13.5's first row): at the first look's take-up it is left
    /// standing under the new run, not fallen back, and the look takes it, so the quest's hand-over is not lost with the loop.
    /// </summary>
    [Fact]
    public async Task A_dead_loop_s_decided_hand_over_is_left_standing_by_the_take_up_and_taken_by_the_look()
    {
        var old = Store("run-old");
        var decided = old.Decide(new MoveDecision("s1", MoveExecutors.Driven, MoveHows.Brief, HandOverWhy.Press, HandOverBy.Person) { Agent = "codex-acp" });
        _dead.Add("run-old");
        using (var store = DesktopStore.Open(DesktopStore.LocationIn(_home), new DesktopStoreOptions { Settled = () => StoreSettled.Yes }))
        {
            store.Write("probe", write => write.Execute("UPDATE store_runs SET started = 1 WHERE run = 'run-old'"));
        }

        var driver = Driver();
        await driver.TakeUpMovesAsync(CancellationToken.None);
        var standing = Store("run-probe").Read(decided.Move.Id)!;
        Assert.Equal(("decided", "run-loop"), (standing.PhaseWord, standing.ClaimRun));

        await driver.TickAsync().WaitAsync(Bound);

        Assert.Equal("s1", Assert.Single(_stopped).Session);
        Assert.Equal("old-ended", MoveFrom("s1")!.PhaseWord);
    }

    /// <summary>
    /// The store is held until the quest leaves this machine (design §2): a quest that closed is dropped at the look, and one taken
    /// elsewhere is too, so the machine's own agent carries any later start.
    /// </summary>
    [Fact]
    public async Task The_store_goes_when_the_quest_closes_or_is_taken_elsewhere()
    {
        var store = new HandedQuests(_home);
        store.Write("q1", new HandedQuest("codex-acp", null, "s1", HandOverWhy.Press, HandOverBy.Person, T0));
        _ledger.Move("q1", "Done");

        var closed = await Driver().TickAsync().WaitAsync(Bound);

        Assert.Empty(store.All());
        Assert.Contains(closed.Events, line => line == "hand-over  `#q1` has closed: it goes back to this machine's agent.");

        // Taken elsewhere: the person's stop is the last run here (so a look reads its claim), and the claim says it was lost.
        _ledger.Publish("q2", "engine");
        _ledger.Driven("s2", "q2", Path.Combine(_home, "tree-s2"), state: "stopped");
        store.Write("q2", new HandedQuest("codex-acp", null, "s2", HandOverWhy.Press, HandOverBy.Person, T0));
        _ledger.Claim = "lost";

        var away = await Driver().TickAsync().WaitAsync(Bound);

        Assert.Empty(store.All());
        Assert.Contains(away.Events, line => line == "hand-over  `#q2` is taken elsewhere: it goes back to this machine's agent.");
    }

    /// <summary>🔴 FABRIC-CLOSE1c20: a take lost to the person's reopen drops the store in the reopen's words, never as another machine's take.</summary>
    [Fact]
    public async Task The_store_goes_in_the_reopens_words_when_the_take_was_lost_to_it()
    {
        var store = new HandedQuests(_home);
        _ledger.Publish("q2", "engine");
        _ledger.Driven("s2", "q2", Path.Combine(_home, "tree-s2"), state: "stopped");
        store.Write("q2", new HandedQuest("codex-acp", null, "s2", HandOverWhy.Press, HandOverBy.Person, T0));
        _ledger.Claim = "lost";
        _ledger.ClaimWhy = "source-reopened";

        var away = await Driver().TickAsync().WaitAsync(Bound);

        Assert.Empty(store.All());
        Assert.Contains(away.Events, line => line.StartsWith("hand-over  `#q2` was lost to your reopen", StringComparison.Ordinal));
        Assert.DoesNotContain(away.Events, line => line.Contains("taken elsewhere", StringComparison.Ordinal));
    }

    /// <summary>The store stays while the quest is this machine's: another look changes nothing.</summary>
    [Fact]
    public async Task The_store_stays_while_the_quest_is_this_machine_s()
    {
        await Ask("s1");
        await Driver().TickAsync().WaitAsync(Bound);

        await Driver().TickAsync().WaitAsync(Bound);
        await Driver().TickAsync().WaitAsync(Bound);

        Assert.Equal("codex-acp", new HandedQuests(_home).Read("q1")!.Agent);
    }

    /// <summary>
    /// 🔴 MIXAGENT1a-iii (D163 point 4): the session a quest's start continues, for its record. The carry-on of the session the quest
    /// was handed over from names it with the hand-over's why and asker; a carry-on onto another account after a limit names the
    /// cut-off session as Daoris's move. SESSCTX1b (D189 point 4): every other carry-on names its link too, `carry-on`, so the
    /// brief follows the line; a first start, a resume and a fresh start carry nothing on and name none.
    /// </summary>
    [Fact]
    public void A_quest_s_start_continues_the_session_it_was_handed_from_or_a_limit_cut_off_on_another_account()
    {
        var handed = new HandedQuest("codex-acp", null, "s1", HandOverWhy.Near, HandOverBy.Person, T0);
        PriorSession Prior(string session, bool limit = false, string? profile = "account-1") =>
            new(session, null, "stopped") { Limit = limit, Profile = profile };

        Assert.Equal(new ContinuedFrom("s1", "near", "person"), Daoris.Driver.Driver.ContinuesFrom(handed, Prior("s1"), "account-1"));
        Assert.Equal(new ContinuedFrom("s1", "limit", "daoris"), Daoris.Driver.Driver.ContinuesFrom(null, Prior("s1", limit: true), "account-2"));
        // The handed quest's own later carry-on, cut off by a limit too, is the limit's move from that later session.
        Assert.Equal(new ContinuedFrom("s2", "limit", "daoris"), Daoris.Driver.Driver.ContinuesFrom(handed, Prior("s2", limit: true), "account-2"));

        // A carry-on on the same account, or one no limit moved, is Daoris's carry-on.
        Assert.Equal(new ContinuedFrom("s2", "carry-on", "daoris"), Daoris.Driver.Driver.ContinuesFrom(handed, Prior("s2"), "account-1"));
        Assert.Equal(new ContinuedFrom("s1", "carry-on", "daoris"), Daoris.Driver.Driver.ContinuesFrom(null, Prior("s1", limit: true), "account-1"));
        Assert.Equal(new ContinuedFrom("s1", "carry-on", "daoris"), Daoris.Driver.Driver.ContinuesFrom(null, Prior("s1"), "account-2"));
        Assert.Null(Daoris.Driver.Driver.ContinuesFrom(handed, null, "account-1"));
        Assert.Null(Daoris.Driver.Driver.ContinuesFrom(null, null, "account-1"));
    }

    /// <summary>
    /// 🔴 WALKAGENT1: a limit's move onto another agent is the limit's move whatever the two accounts are named, since an account belongs to
    /// its agent; the same agent on the same account is still Daoris's carry-on, and an agent left unsaid compares accounts alone.
    /// </summary>
    [Fact]
    public void A_limit_s_move_to_another_agent_is_the_limit_s_move_though_both_accounts_are_named_alike()
    {
        var cut = new PriorSession("s1", null, "failed") { Limit = true, Profile = "work", Adapter = "claude-code-acp" };

        Assert.Equal(new ContinuedFrom("s1", "limit", "daoris"), Daoris.Driver.Driver.ContinuesFrom(null, cut, "work", agent: "codex-acp"));
        Assert.Equal(new ContinuedFrom("s1", "carry-on", "daoris"), Daoris.Driver.Driver.ContinuesFrom(null, cut, "work", agent: "claude-code-acp"));
        Assert.Equal(new ContinuedFrom("s1", "carry-on", "daoris"), Daoris.Driver.Driver.ContinuesFrom(null, cut, "work"));
        Assert.True(AccountIdentity.OnAnotherAccount(cut, "work", "codex-acp"));
        Assert.False(AccountIdentity.OnAnotherAccount(cut, "work", "CLAUDE-CODE-ACP"));
    }

    /// <summary>
    /// 🔴 SESSCTX1b (D189 point 4, D131 point 3): a driven record whose resume was refused goes on in a new part named `retry`; one the
    /// person released with *Try again* or brought forward is their press, `person`; a hand-over and a move onto another account
    /// keep their own why, whichever way it went on.
    /// </summary>
    [Fact]
    public void A_carry_on_names_its_link_retry_where_its_resume_fell_back_and_the_person_where_they_pressed_it()
    {
        PriorSession Prior(string session, bool limit = false, string? profile = "account-1") =>
            new(session, null, "stopped") { Limit = limit, Profile = profile };

        Assert.Equal(new ContinuedFrom("s1", "retry", "daoris"), Daoris.Driver.Driver.ContinuesFrom(null, Prior("s1"), "account-1", retried: true));
        Assert.Equal(new ContinuedFrom("s1", "carry-on", "person"), Daoris.Driver.Driver.ContinuesFrom(null, Prior("s1"), "account-1", pressed: true));
        Assert.Equal(new ContinuedFrom("s1", "limit", "daoris"), Daoris.Driver.Driver.ContinuesFrom(null, Prior("s1", limit: true), "account-2", retried: true));
        Assert.Equal(new ContinuedFrom("s1", "near", "person"), Daoris.Driver.Driver.ContinuesFrom(
            new HandedQuest("codex-acp", null, "s1", HandOverWhy.Near, HandOverBy.Person, T0), Prior("s1"), "account-1", retried: true));
        Assert.Null(Daoris.Driver.Driver.ContinuesFrom(null, null, "account-1", retried: true));
    }

    /// <summary>
    /// 🔴 SWITCH1c-iii (D165 point 3, the switch design §4): a carry-on that starts on the account the curve ranks first, which moves
    /// it off the last session's account, names that session with `curve` and `daoris`; one that stays, one moved for another reason
    /// (a cooling account, a limit reached) and a limit's own move say what they always said.
    /// </summary>
    [Fact]
    public void A_carry_on_the_curve_moves_to_another_account_names_the_curve()
    {
        PriorSession Prior(string session, bool limit = false, string? profile = "account-1") =>
            new(session, null, "stopped") { Limit = limit, Profile = profile };

        // Moved: the walk's first step was an account over its curve, or the most room under it, and the account changed.
        Assert.Equal(new ContinuedFrom("s1", "curve", "daoris"), Daoris.Driver.Driver.ContinuesFrom(null, Prior("s1"), "account-2", OpeningWhy.OverCurve));
        Assert.Equal(new ContinuedFrom("s1", "curve", "daoris"), Daoris.Driver.Driver.ContinuesFrom(null, Prior("s1"), "account-2", OpeningWhy.MostRoom));

        // Stays: the same account, whatever the walk said, is a carry-on (SESSCTX1b).
        var carried = new ContinuedFrom("s1", "carry-on", "daoris");
        Assert.Equal(carried, Daoris.Driver.Driver.ContinuesFrom(null, Prior("s1"), "account-1", OpeningWhy.MostRoom));
        // Moved for another reason than the curve is no curve's move, and nothing is claimed of a start no walk chose.
        Assert.Equal(carried, Daoris.Driver.Driver.ContinuesFrom(null, Prior("s1"), "account-2", OpeningWhy.Cooling));
        Assert.Equal(carried, Daoris.Driver.Driver.ContinuesFrom(null, Prior("s1"), "account-2", OpeningWhy.NearReached));
        Assert.Equal(carried, Daoris.Driver.Driver.ContinuesFrom(null, Prior("s1"), "account-2", null));
        // A limit is the limit's move, and a hand-over its own, whatever the walk then said.
        Assert.Equal(new ContinuedFrom("s1", "limit", "daoris"), Daoris.Driver.Driver.ContinuesFrom(null, Prior("s1", limit: true), "account-2", OpeningWhy.MostRoom));
        Assert.Equal(new ContinuedFrom("s1", "near", "person"), Daoris.Driver.Driver.ContinuesFrom(
            new HandedQuest("codex-acp", null, "s1", HandOverWhy.Near, HandOverBy.Person, T0), Prior("s1"), "account-2", OpeningWhy.MostRoom));
    }

    /// <summary>
    /// SWITCH1c-iii: the opening line of a carry-on the curve moved says why, in English and by code (the page words it in both
    /// languages from the `opening.carried-on` reason it already has): the session, the account and the curve's step, no limit's turn.
    /// </summary>
    [Fact]
    public void A_carry_on_the_curve_moved_opens_saying_the_curve_s_step()
    {
        var why = "it has the most room under its curve, 53 points on its weekly window";
        var selection = new HarnessSelection(null, "account-2")
        {
            Choice = new AccountChoice(WalkStep.Pace, why),
            Steps = [new StepSaid(why, OpeningWhy.MostRoom, [("room", 53), ("window", "weekly")])],
        };

        var line = RotatedOpening.Line(selection, new RotatedOpening.Carried("s1", null, null))!;

        Assert.StartsWith($"carried on from session `s1` on `account-2`; {why}.", line.Note, StringComparison.Ordinal);
        Assert.Equal(["opening.carried-on"], NoteAssert.Codes(line.Parts).Take(1));
        Assert.Equal(OpeningWhy.MostRoom, line.Parts[0].Value("why"));
    }

    /// <summary>The open says the link, read back as the record answers it; an open that names none says none.</summary>
    [Fact]
    public async Task A_quest_s_open_names_the_session_it_continues_on_its_record()
    {
        using var service = _ledger.Client();

        var (carried, _) = await service.OpenSessionAsync("q1", "codex-acp", from: new ContinuedFrom("s1", HandOverWhy.Limit, HandOverBy.Daoris));
        var (plain, _) = await service.OpenSessionAsync("q1", "stub");

        var link = (await service.RecordAsync(carried!))!;
        Assert.Equal(("s1", "limit", "daoris"), (link.From, link.FromWhy, link.FromBy));
        var none = (await service.RecordAsync(plain!))!;
        Assert.Equal(((string?)null, (string?)null, (string?)null), (none.From, none.FromWhy, none.FromBy));
    }

    private string State(string session) => _ledger.Session(session)["state"]!.GetValue<string>();
}
