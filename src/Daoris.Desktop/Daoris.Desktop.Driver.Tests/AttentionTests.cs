using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What is worth interrupting a person for (SURF5b, working-surface design §4): a session that
/// parked, and a session that ended without them asking for it.
/// </summary>
/// <remarks>
/// <para>This is the whole judgement, and it is here rather than in the shell because a toast is the
/// *delivery* and a machine with no screen still has to answer the same question (D50).</para>
///
/// <para><b>The two halves are found differently, and that is the design.</b> A park is a state change
/// on the record that nothing local performs, so it is seen by diffing what is active. An end is
/// something the driver concluded, so it is KNOWN — including whose decision it was.</para>
/// </remarks>
public sealed class AttentionTests
{
    private static SessionView Session(string id, string state, string repository = "engine") =>
        new(id, repository, state);

    private static TickReport Tick(
        IReadOnlyList<SessionView> active, params SessionEnded[] concluded) =>
        new([], [], Progressed: false, Active: active, Concluded: concluded);

    [Fact]
    public void A_session_that_parks_is_worth_saying()
    {
        var watch = new AttentionWatch();
        watch.Observe(Tick([Session("s1", "working")]));

        var events = watch.Observe(Tick([Session("s1", "awaiting-person")]));

        var parked = Assert.Single(events);
        Assert.Equal(AttentionKind.Parked, parked.Kind);
        Assert.Equal("s1", parked.Session);
        Assert.Equal("engine", parked.Repository);
    }

    /// <summary>
    /// A parked session stays parked for as long as nobody answers it, and every tick sees it. Saying
    /// so once is a notification; saying so every fifteen seconds is what teaches people to turn
    /// notifications off.
    /// </summary>
    [Fact]
    public void It_says_so_once_and_not_every_tick_for_as_long_as_it_sits()
    {
        var watch = new AttentionWatch();
        watch.Observe(Tick([Session("s1", "working")]));
        Assert.Single(watch.Observe(Tick([Session("s1", "awaiting-person")])));

        Assert.Empty(watch.Observe(Tick([Session("s1", "awaiting-person")])));
        Assert.Empty(watch.Observe(Tick([Session("s1", "awaiting-person")])));
    }

    /// <summary>
    /// The first tick after a launch sees a world it has no previous view of. A session already
    /// parked then is one the person has been told about on some earlier run — and a shell that
    /// toasted every parked session on every start would be one people close.
    /// </summary>
    [Fact]
    public void The_first_look_is_a_baseline_and_not_a_backlog_of_notifications()
    {
        var watch = new AttentionWatch();

        var events = watch.Observe(Tick([Session("s1", "awaiting-person"), Session("s2", "working")]));

        Assert.Empty(events);
    }

    /// <summary>A session answered and parked again is news the second time too.</summary>
    [Fact]
    public void A_session_that_carries_on_and_parks_again_is_worth_saying_again()
    {
        var watch = new AttentionWatch();
        watch.Observe(Tick([Session("s1", "working")]));
        Assert.Single(watch.Observe(Tick([Session("s1", "awaiting-person")])));

        Assert.Empty(watch.Observe(Tick([Session("s1", "working")])));
        Assert.Single(watch.Observe(Tick([Session("s1", "awaiting-person")])));
    }

    [Fact]
    public void A_session_the_driver_concluded_is_worth_saying()
    {
        var watch = new AttentionWatch();
        watch.Observe(Tick([Session("s1", "working")]));

        var events = watch.Observe(Tick([], new SessionEnded("s1", "engine", "completed", ByPerson: false)));

        var ended = Assert.Single(events);
        Assert.Equal(AttentionKind.Ended, ended.Kind);
        Assert.Equal("completed", ended.State);
        Assert.Equal("engine", ended.Repository);
    }

    /// <summary>
    /// 🔴 The rule design §4 states outright: **never for an ending the person caused**, because a
    /// toast telling you what you just pressed is how people learn to dismiss toasts unread. The
    /// driver already knows whose decision it was — `WasStopRequested` is the flag, and the driver's
    /// own comment says only it knows.
    /// </summary>
    [Fact]
    public void An_ending_the_person_caused_is_never_worth_saying()
    {
        var watch = new AttentionWatch();
        watch.Observe(Tick([Session("s1", "working")]));

        var events = watch.Observe(Tick([], new SessionEnded("s1", "engine", "stopped", ByPerson: true)));

        Assert.Empty(events);
    }

    /// <summary>
    /// A session that left the active list without the driver concluding it ended some other way —
    /// a resolve, an ended chat, a stop — and every one of those is the person, on this machine,
    /// pressing something. There is nothing to suppress, because there is nothing to report.
    /// </summary>
    [Fact]
    public void A_session_that_simply_vanished_is_not_reported_as_an_ending()
    {
        var watch = new AttentionWatch();
        watch.Observe(Tick([Session("s1", "working")]));

        Assert.Empty(watch.Observe(Tick([])));
    }

    /// <summary>
    /// A session that parks and then ends is two pieces of news, and the second must survive the
    /// first — the state it was remembered in must not make it look unchanged.
    /// </summary>
    [Fact]
    public void A_parked_session_that_then_ends_says_both()
    {
        var watch = new AttentionWatch();
        watch.Observe(Tick([Session("s1", "working")]));
        Assert.Single(watch.Observe(Tick([Session("s1", "awaiting-person")])));

        var events = watch.Observe(Tick([], new SessionEnded("s1", "engine", "failed", ByPerson: false)));

        Assert.Equal(AttentionKind.Ended, Assert.Single(events).Kind);
    }

    /// <summary>
    /// Several at once is a normal tick on a machine driving a family. Every one is reported, so the
    /// count a person sees matches what actually happened.
    /// </summary>
    [Fact]
    public void Everything_that_happened_in_one_tick_is_reported()
    {
        var watch = new AttentionWatch();
        watch.Observe(Tick([Session("s1", "working"), Session("s2", "working", "tools")]));

        var events = watch.Observe(Tick(
            [Session("s1", "awaiting-person")],
            new SessionEnded("s2", "tools", "completed", ByPerson: false)));

        Assert.Equal(2, events.Count);
        Assert.Contains(events, e => e.Kind == AttentionKind.Parked && e.Session == "s1");
        Assert.Contains(events, e => e.Kind == AttentionKind.Ended && e.Session == "s2");
    }

    /// <summary>
    /// The buffer of remembered states is bounded by what is active, so a long-lived driver does not
    /// accumulate a state for every session it ever saw.
    /// </summary>
    [Fact]
    public void It_remembers_only_what_is_still_active()
    {
        var watch = new AttentionWatch();
        watch.Observe(Tick([Session("s1", "working"), Session("s2", "working")]));
        watch.Observe(Tick([Session("s1", "working")]));

        Assert.Equal(["s1"], watch.Watching);
    }

    /// <summary>
    /// A conversation that parks is reported like anything else: the tick sees a state, not a way in.
    /// Its ENDING is not, deliberately — see the remarks on <see cref="AttentionWatch"/>. Asserted so
    /// the half that IS reported cannot be dropped along with the half that is not.
    /// </summary>
    [Fact]
    public void A_conversation_that_parks_is_reported_like_any_other_session()
    {
        var watch = new AttentionWatch();
        watch.Observe(Tick([Session("chat1", "working", "tools")]));

        var events = watch.Observe(Tick([Session("chat1", "awaiting-person", "tools")]));

        Assert.Equal(AttentionKind.Parked, Assert.Single(events).Kind);
    }

    /// <summary>
    /// The sentence is composed here so both doors say the same thing — a toast on a machine with a
    /// screen, a console line on one without (D50). It leads with the repository, which is what a
    /// person glancing at a corner of their screen is actually identifying.
    /// </summary>
    [Fact]
    public void One_sentence_serves_a_toast_and_a_terminal()
    {
        var parked = new AttentionEvent(
            AttentionKind.Parked, "s1", "engine", Note: "Two ways forward; I recommend capping.");
        Assert.Equal("engine — a session needs you", parked.Headline);
        Assert.Equal("Two ways forward; I recommend capping.", parked.Detail);
        Assert.Equal("engine — a session needs you: Two ways forward; I recommend capping.", parked.Line);

        var ended = new AttentionEvent(AttentionKind.Ended, "s2", "tools", "failed", "the gate went red.");
        Assert.Equal("tools — a session failed", ended.Headline);
    }

    /// <summary>
    /// A session that parked without explaining itself is a real state, so a surface that required a
    /// note would have nothing to show for it. Whitespace is the same absence as nothing at all.
    /// </summary>
    [Fact]
    public void A_session_that_said_nothing_still_has_a_sentence()
    {
        var quiet = new AttentionEvent(AttentionKind.Parked, "s1", "engine");
        Assert.Null(quiet.Detail);
        Assert.Equal("engine — a session needs you", quiet.Line);

        Assert.Null(new AttentionEvent(AttentionKind.Parked, "s1", "engine", Note: "   ").Detail);
    }

    private static readonly QuestView Q1 = new("q1", "game", "engine", "Expose a streaming budget", "A body.", "Taken");

    private static Consideration Exhausted(QuestView quest) => new(quest, StartVerdict.Exhausted, "3 session(s) have failed.");

    private static TickReport Considered(params Consideration[] considered) => new(considered, [], Progressed: false);

    private static QuestPark Park(string session, int? strikes = 3, string? note = "You've hit your limit.") =>
        new("q1", "engine") { Session = session, Strikes = strikes, Note = note };

    /// <summary>
    /// SESSUX1i (D126 §4.7): the owner's work stood parked on 1 October and nothing said so. A quest that parks on its
    /// failed sessions here is said once, with how many failed and the last failure's note.
    /// </summary>
    [Fact]
    public void A_quest_that_parks_on_its_failed_sessions_is_worth_saying()
    {
        var watch = new AttentionWatch();
        watch.Observe(Considered(new Consideration(Q1, StartVerdict.Start, "starting")), []);

        var events = watch.Observe(Considered(Exhausted(Q1)), [Park("s3")]);

        var parked = Assert.Single(events);
        Assert.Equal(AttentionKind.QuestParked, parked.Kind);
        Assert.Equal("q1", parked.Quest);
        Assert.Equal("s3", parked.Session);
        Assert.Equal("engine", parked.Repository);
        Assert.Equal("engine — `#q1` parked after 3 failed sessions", parked.Headline);
        Assert.Equal("You've hit your limit.", parked.Detail);
    }

    /// <summary>
    /// A park lasts every look until Try again, and a door reads the parks only when they change. Neither a look that
    /// passed none nor one that passed the same park again is news.
    /// </summary>
    [Fact]
    public void A_quest_s_park_is_said_once_and_not_every_look_for_as_long_as_it_sits()
    {
        var watch = new AttentionWatch();
        watch.Observe(Considered(), []);
        Assert.Single(watch.Observe(Considered(Exhausted(Q1)), [Park("s3")]));

        Assert.Empty(watch.Observe(Considered(Exhausted(Q1))));
        Assert.Empty(watch.Observe(Considered(Exhausted(Q1)), [Park("s3")]));
    }

    /// <summary>
    /// 🔴 A park outranks every hold but the person's own (the planner checks a held repository first), so a quest may read
    /// held for a look and parked again at the next. That is the same park: its last session has not changed.
    /// </summary>
    [Fact]
    public void A_park_hidden_for_a_look_by_a_hold_is_not_said_again()
    {
        var watch = new AttentionWatch();
        watch.Observe(Considered(), []);
        Assert.Single(watch.Observe(Considered(Exhausted(Q1)), [Park("s3")]));

        Assert.Empty(watch.Observe(Considered(new Consideration(Q1, StartVerdict.Held, "`engine` is held by the person.")), []));
        Assert.Empty(watch.Observe(Considered(Exhausted(Q1)), [Park("s3")]));
    }

    /// <summary>Tried again, and parked again by sessions that failed since: a new last session, and news again.</summary>
    [Fact]
    public void A_quest_tried_again_that_parks_again_is_worth_saying_again()
    {
        var watch = new AttentionWatch();
        watch.Observe(Considered(), []);
        Assert.Single(watch.Observe(Considered(Exhausted(Q1)), [Park("s3")]));

        Assert.Empty(watch.Observe(Considered(new Consideration(Q1, StartVerdict.Start, "starting")), []));
        Assert.Single(watch.Observe(Considered(Exhausted(Q1)), [Park("s6")]));
    }

    /// <summary>
    /// Never again on a relaunch (§4.7): a quest already parked when the loop starts is one the person was told about
    /// before, and the first look is a baseline, never a backlog, as a session's park is. Nor when its records could not
    /// be read at that first look, and are read at a later one.
    /// </summary>
    [Fact]
    public void A_quest_already_parked_at_the_first_look_is_never_said()
    {
        var watch = new AttentionWatch();
        Assert.Empty(watch.Observe(Considered(Exhausted(Q1)), [Park("s3")]));
        Assert.Empty(watch.Observe(Considered(Exhausted(Q1)), [Park("s3")]));

        var unread = new AttentionWatch();
        Assert.Empty(unread.Observe(Considered(Exhausted(Q1))));
        Assert.Empty(unread.Observe(Considered(Exhausted(Q1)), [Park("s3")]));
    }

    /// <summary>
    /// The park says the last failure's note, so that failure's own end in the same look is not said beside it: one
    /// thing happened to the person's work, and two notices of one note is the second saying nothing.
    /// </summary>
    [Fact]
    public void The_last_failure_is_said_by_its_quest_s_park_and_not_twice()
    {
        var watch = new AttentionWatch();
        watch.Observe(Tick([Session("s3", "working")]), []);

        var events = watch.Observe(
            new TickReport([Exhausted(Q1)], [], Progressed: false,
                Concluded: [new SessionEnded("s3", "engine", "failed", ByPerson: false, "You've hit your limit.", Quest: "q1"),
                            new SessionEnded("s4", "tools", "failed", ByPerson: false, "the gate went red.")]),
            [Park("s3")]);

        Assert.Equal(2, events.Count);
        Assert.Equal(AttentionKind.QuestParked, events[0].Kind);
        Assert.Equal("s4", Assert.Single(events, e => e.Kind == AttentionKind.Ended).Session);
    }

    /// <summary>
    /// 🔴 Never for a hold the person caused (§4.7): their stop holds its quest until Try again (SESSUX1b), and a notice
    /// telling them what they just pressed is how people learn to dismiss notices unread. The parks are read from the
    /// planner's park alone, so a stop never reaches the watch.
    /// </summary>
    [Fact]
    public void A_quest_the_person_s_stop_holds_is_never_said()
    {
        var stopped = new Consideration(Q1, StartVerdict.Stopped, "you stopped session `s3`; Try again carries it on.")
        {
            HeldBy = new PriorSession("s3", null, "stopped"),
        };
        var records = """[{"id":"s3","repository":"engine","state":"stopped","kind":"driven","quest":"q1","created":"2026-10-01T09:00:00Z","updated":"2026-10-01T09:05:00Z"}]""";
        var watch = new AttentionWatch();
        watch.Observe(Considered(), []);

        var parks = SessionGroups.Parks(SessionLook.From(records, [], [stopped], _ => 0));

        Assert.Empty(watch.Observe(Considered(stopped), parks));
    }

    /// <summary>A park the records name no number for still says it parked, and leaves the number unsaid; one says it in the singular.</summary>
    [Fact]
    public void A_park_with_no_number_or_one_says_so_in_its_own_words()
    {
        Assert.Equal(
            "engine — `#q1` parked after its failed sessions",
            new AttentionEvent(AttentionKind.QuestParked, "s3", "engine") { Quest = "q1" }.Headline);
        Assert.Equal(
            "engine — `#q1` parked after 1 failed session",
            new AttentionEvent(AttentionKind.QuestParked, "s3", "engine") { Quest = "q1", Strikes = 1 }.Line);
    }

    /// <summary>
    /// WSSETUP11: the machine log counts a park where the ledger is moved into it (<c>SessionLog</c>), and this
    /// watch says one when it sees it from the active list. Both read <see cref="SessionStates.IsParked"/>, and
    /// <c>SessionLogTests</c> holds the same rows, so what the log counts is what the person was told about.
    /// </summary>
    [Theory]
    [InlineData("awaiting-person", true)]
    [InlineData("working", false)]
    [InlineData("starting", false)]
    [InlineData("completed", false)]
    [InlineData("failed", false)]
    public void A_park_is_the_state_the_machine_log_counts_as_one(string state, bool parked)
    {
        var watch = new AttentionWatch();
        watch.Observe(Tick([Session("s1", "queued")]));

        var events = watch.Observe(Tick([Session("s1", state)]));

        Assert.Equal(parked, events.Any(e => e.Kind == AttentionKind.Parked));
    }
}
