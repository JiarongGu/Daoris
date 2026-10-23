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

    /// <summary>
    /// AUTONOMY1e (D184 point 4): Daoris puts nothing between a session's question and the person but the notice that it waits,
    /// so each question is said. An answered park stays parked with the words until the look takes it up (ANSWER1b) and waits on
    /// nobody (ANSWER1c); its resumed turn can end on a second question before any look sees it working, and that one is said too.
    /// </summary>
    [Fact]
    public void An_answered_park_that_asks_again_is_said_again_though_no_look_saw_it_working()
    {
        var watch = new AttentionWatch();
        watch.Observe(Tick([Session("s1", "working")]));
        Assert.Single(watch.Observe(Tick([Session("s1", "awaiting-person")])));
        Assert.Empty(watch.Observe(Tick([Session("s1", "awaiting-person") with { Answer = "Port 8080." }])));

        var again = Assert.Single(watch.Observe(Tick([Session("s1", "awaiting-person") with { Note = "Which host should it bind?" }])));

        Assert.Equal(AttentionKind.Parked, again.Kind);
        Assert.Equal("Which host should it bind?", again.Detail);
    }

    /// <summary>
    /// A park the person answered before any look saw it waiting waits on nobody (ANSWER1c): a notice would tell them what they
    /// just did, which is how people learn to dismiss notices unread.
    /// </summary>
    [Fact]
    public void A_park_already_answered_when_first_seen_is_not_said()
    {
        var watch = new AttentionWatch();
        watch.Observe(Tick([Session("s1", "working")]));

        Assert.Empty(watch.Observe(Tick([Session("s1", "awaiting-person") with { Answer = "yes, the second" }])));
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
    /// NOTIFY2f (the notification design §2.5): an end carries the workspace its session was filed under at the last look that saw it
    /// active, which its notice keeps; one no look saw active carries none, and its notice is every window's.
    /// </summary>
    [Fact]
    public void An_end_carries_the_workspace_its_session_was_filed_under()
    {
        var watch = new AttentionWatch();
        watch.Observe(Tick([Session("s1", "working") with { Workspace = "games" }]));

        var events = watch.Observe(Tick([], new SessionEnded("s1", "engine", "failed", ByPerson: false), new SessionEnded("s2", "engine", "failed", ByPerson: false)));

        Assert.Equal([("s1", "games"), ("s2", null)], events.Select(item => (item.Session, item.Workspace)));
        var notice = Notices.Of(events[0], DateTimeOffset.UnixEpoch)!;
        Assert.Equal(new Notice("ended-s1", Notices.Ended, DateTimeOffset.UnixEpoch, Workspace: "games", Session: "s1", Repository: "engine", State: "failed"), notice);
        Assert.Null(Notices.Of(new AttentionEvent(AttentionKind.Parked, "s1", "engine"), DateTimeOffset.UnixEpoch));
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
    /// LIMITRESUME1a (D206 point 1; the limit-resume design §3.1): an end an account's limit made is no failure and no news of its
    /// own. Its line goes on by itself, and the line's own news says where; an <i>Ended</i> for each part a limit cut off was the
    /// install's three failures in a row. A failure beside it in the same look is still said.
    /// </summary>
    [Fact]
    public void A_limited_conclusion_raises_no_ended()
    {
        var watch = new AttentionWatch();
        watch.Observe(Tick([Session("s1", "working"), Session("s2", "working")]));

        var events = watch.Observe(Tick(
            [],
            new SessionEnded("s1", "engine", "failed", ByPerson: false, "You've hit your limit.", Quest: "q1") { Limit = true },
            new SessionEnded("s2", "engine", "failed", ByPerson: false, "the gate went red.")));

        Assert.Equal("s2", Assert.Single(events, e => e.Kind == AttentionKind.Ended).Session);
        Assert.DoesNotContain(events, e => e.Session == "s1");
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
    /// A park's notice is the session's own question (NOTICEPLAIN1, D184): Daoris's coded lead is no part of it, and a short
    /// balloon cannot cut the question off behind it. A note with no agent part says what it always said.
    /// </summary>
    [Fact]
    public void A_parks_detail_is_the_agents_question_alone()
    {
        const string Lead = "It stopped with its quest still taken, to ask you:";
        IReadOnlyList<NotePart> parts =
        [
            new() { Code = NoteCodes.EndedParkedAsked.Code, Text = Lead },
            NotePart.Said("\n\n", NoteBy.Before),
            NotePart.Said("Which port?", NoteBy.Agent),
        ];
        var watch = new AttentionWatch();
        watch.Observe(Tick([Session("s1", "working")]));

        var events = watch.Observe(Tick([Session("s1", "awaiting-person") with { Note = Lead + "\n\nWhich port?", NoteParts = parts }]));

        var parked = Assert.Single(events);
        Assert.Equal("Which port?", parked.Detail);
        Assert.Equal("engine — a session needs you: Which port?", parked.Line);

        var plain = new AttentionEvent(AttentionKind.Parked, "s1", "engine", Note: Lead + "\n\nWhich port?");
        Assert.Equal(Lead + "\n\nWhich port?", plain.Detail);
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

    private static readonly DateTimeOffset Ended1 = new(2026, 10, 15, 9, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Ended2 = Ended1.AddMinutes(2);

    /// <summary>A conversation's turns as its runner tells them: one running, or none with the last one's end.</summary>
    private static ChatQueue Running(DateTimeOffset? last = null, params string[] queued) =>
        new(true, [.. queued.Select(text => new ChatMessage(text, []))], last);

    private static ChatQueue Idle(DateTimeOffset last) => new(false, [], last);

    /// <summary>
    /// NOTIFY1a: the person said something to a conversation and looked away; the turn their words started has ended, and
    /// that answer is what they wait on. Said with the driver's own sentence (D24), and nothing beside it.
    /// </summary>
    [Fact]
    public void A_conversation_that_answers_the_person_s_words_is_worth_saying()
    {
        var watch = new AttentionWatch();
        watch.Asked("c1");

        Assert.Null(watch.Turned("c1", "engine", Running()));
        var answered = watch.Turned("c1", "engine", Idle(Ended1));

        Assert.NotNull(answered);
        Assert.Equal(
            (AttentionKind.Answered, "c1", "engine", "engine — a conversation answered you", (string?)null),
            (answered.Kind, answered.Session, answered.Repository, answered.Headline, answered.Detail));
    }

    /// <summary>
    /// A turn's end nobody's words started — a conversation taken up by itself, a hand-over's briefing, one the window came up
    /// beside — is no answer to anybody: a watch that has not heard the person since says nothing.
    /// </summary>
    [Fact]
    public void A_turn_s_end_the_person_s_words_did_not_start_says_nothing()
    {
        var watch = new AttentionWatch();

        Assert.Null(watch.Turned("c1", "engine", Idle(Ended1)));
        Assert.Null(watch.Turned("c1", "engine", Running(Ended1)));
        Assert.Null(watch.Turned("c1", "engine", Idle(Ended2)));
    }

    /// <summary>
    /// Said once per turn: the same end told again (a queue's other change) is not a second answer, and the next turn's end
    /// is one only if the person spoke again.
    /// </summary>
    [Fact]
    public void An_answer_is_said_once_and_the_next_needs_the_person_s_words_again()
    {
        var watch = new AttentionWatch();
        watch.Asked("c1");
        watch.Turned("c1", "engine", Running());
        Assert.NotNull(watch.Turned("c1", "engine", Idle(Ended1)));

        Assert.Null(watch.Turned("c1", "engine", Idle(Ended1)));
        watch.Turned("c1", "engine", Running(Ended1));
        Assert.Null(watch.Turned("c1", "engine", Idle(Ended2)));

        watch.Asked("c1");
        watch.Turned("c1", "engine", Running(Ended2));
        Assert.NotNull(watch.Turned("c1", "engine", Idle(Ended2.AddMinutes(1))));
    }

    /// <summary>
    /// Words the person queued behind a running turn are answered by the turn that takes them: the end between the two is not
    /// what they wait on, and the last one's is said once.
    /// </summary>
    [Fact]
    public void Words_queued_behind_a_turn_are_answered_at_the_last_turn_s_end()
    {
        var watch = new AttentionWatch();
        watch.Asked("c1");
        watch.Turned("c1", "engine", Running());
        watch.Asked("c1");
        watch.Turned("c1", "engine", Running(null, "And the tests."));

        Assert.Null(watch.Turned("c1", "engine", Running(Ended1)));
        Assert.Single(new[] { watch.Turned("c1", "engine", Idle(Ended2)) }.OfType<AttentionEvent>());
    }

    /// <summary>
    /// A turn the person stopped ends on their own press: telling them it answered is telling them what they just did, as an
    /// end they asked for never is (SURF5b).
    /// </summary>
    [Fact]
    public void A_turn_the_person_stopped_is_no_answer()
    {
        var watch = new AttentionWatch();
        watch.Asked("c1");
        watch.Turned("c1", "engine", Running());
        watch.Stopped("c1");

        Assert.Null(watch.Turned("c1", "engine", Idle(Ended1)));
    }

    /// <summary>Each conversation is its own: the person's words to one are no answer awaited from another.</summary>
    [Fact]
    public void The_person_s_words_are_waited_on_from_the_conversation_they_went_to()
    {
        var watch = new AttentionWatch();
        watch.Asked("c1");

        Assert.Null(watch.Turned("c2", "engine", Idle(Ended1)));
        Assert.NotNull(watch.Turned("c1", "engine", Idle(Ended1)));
    }

    /// <summary>A conversation that ended here is forgotten: what it was waited on for goes with it.</summary>
    [Fact]
    public void A_conversation_forgotten_is_waited_on_for_nothing()
    {
        var watch = new AttentionWatch();
        watch.Asked("c1");
        watch.Forget("c1");

        Assert.Null(watch.Turned("c1", "engine", Idle(Ended1)));
    }

    /// <summary>Ask Daoris's room is no repository the person knows by its name: its answer is said by the room's.</summary>
    [Fact]
    public void Ask_Daoris_answering_says_so_by_its_own_name()
    {
        Assert.Equal(
            "Ask Daoris answered you",
            new AttentionEvent(AttentionKind.Answered, "h1", HelpRoom.Repository).Headline);
    }
}
