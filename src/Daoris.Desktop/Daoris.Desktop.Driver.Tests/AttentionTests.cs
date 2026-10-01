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
