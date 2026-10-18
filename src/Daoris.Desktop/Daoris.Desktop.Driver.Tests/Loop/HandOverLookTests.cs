using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// MIXAGENT1a-ii (D163 point 1): the driver's look takes a quest's session hand-over. A box the terminal kept is judged again, the
/// running session is stopped as the person's with a line of its own, the quest is held on the named agent in
/// <c>&lt;home&gt;/handed/&lt;quest&gt;.json</c>, and the next look carries it on, the stop read as released; the store goes when the quest
/// leaves this machine.
/// </summary>
/// <remarks>In-process: the service is the stand-in ledger and each start's run a stand-in. The fast half; the same over a real node harness is the Process half.</remarks>
public sealed class HandOverLookTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(10);

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
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static DriverConfig Config() => DriverConfig.Empty with { Drivable = ["engine"], Adapter = "stub", Cap = 3, PollSeconds = 1, Trees = ["engine"] };

    /// <summary>A driver for a look, its stop landing at once as a real run's record does once its process exited, and its starts only said.</summary>
    private Daoris.Driver.Driver Driver()
    {
        var service = _ledger.Client();
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(service, Config(), adapters, _home, harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Stops = (session, note) =>
            {
                _stopped.Add((session, note));
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

    private void Ask(string session, string agent = "codex-acp") =>
        new HandOverBox(_home).Write(session, new HandOverRequest(agent, null, HandOverWhy.Limit, HandOverBy.Person, T0));

    /// <summary>
    /// 🔴 A running quest session with a box is stopped at once with its own coded line, the quest is held on the named agent, and the
    /// box is gone; the next look reads that stop as released and carries the quest on, with a sentence that says it was handed over.
    /// </summary>
    [Fact]
    public async Task A_running_quest_session_is_stopped_the_quest_held_on_the_named_agent_and_carried_on_at_the_next_look()
    {
        Ask("s1");

        var first = await Driver().TickAsync().WaitAsync(Bound);

        var stop = Assert.Single(_stopped);
        Assert.Equal("s1", stop.Session);
        Assert.Equal(["ended.handed-over"], NoteAssert.Codes(stop.Note.Parts));
        Assert.Equal("codex-acp", stop.Note.Parts[0].Value("agent"));
        Assert.Equal(("codex-acp", "s1", HandOverWhy.Limit), (new HandedQuests(_home).Read("q1")!.Agent, new HandedQuests(_home).Read("q1")!.From, new HandedQuests(_home).Read("q1")!.Why));
        Assert.Null(new HandOverBox(_home).Read("s1"));
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

    /// <summary>A box for a conversation is the shell's, not this look's: nothing is stopped and the box stays for its runner.</summary>
    [Fact]
    public async Task A_conversation_s_box_is_left_to_the_shell()
    {
        _ledger.Working("c0nv0001", "engine", Path.Combine(_home, "tree-c"));
        Ask("c0nv0001");

        await Driver().TickAsync().WaitAsync(Bound);

        Assert.Empty(_stopped);
        Assert.NotNull(new HandOverBox(_home).Read("c0nv0001"));
        Assert.Empty(new HandedQuests(_home).All());
    }

    /// <summary>A box judged again as it is taken: a quest closed meanwhile stops nothing and keeps nothing, and the box does not stay.</summary>
    [Fact]
    public async Task A_box_whose_quest_closed_meanwhile_stops_nothing_and_says_why()
    {
        Ask("s1");
        _ledger.Move("q1", "Done");

        var look = await Driver().TickAsync().WaitAsync(Bound);

        Assert.Empty(_stopped);
        Assert.Null(new HandOverBox(_home).Read("s1"));
        Assert.Empty(new HandedQuests(_home).All());
        Assert.Contains(look.Events, line => line.StartsWith("hand-over  session `s1` (#q1): #q1 is not open or taken any more", StringComparison.Ordinal));
    }

    /// <summary>An agent this build has not is refused at the take, as at the ask, and nothing is stopped.</summary>
    [Fact]
    public async Task A_box_naming_an_agent_this_build_does_not_have_is_refused()
    {
        Ask("s1", agent: "no-such-agent");

        var look = await Driver().TickAsync().WaitAsync(Bound);

        Assert.Empty(_stopped);
        Assert.Contains(look.Events, line => line == "hand-over  session `s1` (#q1): no agent here is called no-such-agent.");
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
        Ask("s1");
        await Driver().TickAsync().WaitAsync(Bound);

        await Driver().TickAsync().WaitAsync(Bound);
        await Driver().TickAsync().WaitAsync(Bound);

        Assert.Equal("codex-acp", new HandedQuests(_home).Read("q1")!.Agent);
    }

    /// <summary>
    /// 🔴 MIXAGENT1a-iii (D163 point 4): the session a quest's start continues, for its record. The carry-on of the session the quest
    /// was handed over from names it with the hand-over's why and asker; a carry-on onto another account after a limit names the
    /// cut-off session as Daoris's move; any other start names none.
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

        Assert.Null(Daoris.Driver.Driver.ContinuesFrom(handed, Prior("s2"), "account-1"));
        Assert.Null(Daoris.Driver.Driver.ContinuesFrom(null, Prior("s1", limit: true), "account-1"));
        Assert.Null(Daoris.Driver.Driver.ContinuesFrom(null, Prior("s1"), "account-2"));
        Assert.Null(Daoris.Driver.Driver.ContinuesFrom(handed, null, "account-1"));
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

        // Stays: the same account, whatever the walk said, says nothing.
        Assert.Null(Daoris.Driver.Driver.ContinuesFrom(null, Prior("s1"), "account-1", OpeningWhy.MostRoom));
        // Moved for another reason than the curve says nothing, and nothing is claimed of a start no walk chose.
        Assert.Null(Daoris.Driver.Driver.ContinuesFrom(null, Prior("s1"), "account-2", OpeningWhy.Cooling));
        Assert.Null(Daoris.Driver.Driver.ContinuesFrom(null, Prior("s1"), "account-2", OpeningWhy.NearReached));
        Assert.Null(Daoris.Driver.Driver.ContinuesFrom(null, Prior("s1"), "account-2", null));
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
