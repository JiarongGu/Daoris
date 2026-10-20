using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A session's end is OBSERVED, never self-reported (D46 §4): the exit code and the quest's own state
/// are the two signals, because they are the two signals outside work also produces. This table is the
/// whole mapping, so this table is the whole test.
/// </summary>
public sealed class ObservationTests
{
    /// <summary>
    /// 🔴 FLAKE3 with D104: the driver's close records itself, stopped and interrupted, only where no stop came first. A stop made
    /// before it decided the end, so the record says that stop and is never interrupted: the driver's, stood down for its reason;
    /// the person's, stopped with what its door said.
    /// </summary>
    [Fact]
    public void A_close_records_the_stop_made_before_it_and_itself_only_where_none_was()
    {
        var (closed, interrupted) = Observation.Closed(null);
        Assert.Equal(("stopped", "ended.driver-closed", true), (closed.State, closed.Parts![0].Code, interrupted));

        (closed, interrupted) = Observation.Closed(new SessionStop(Daoris.Driver.Driver.LostClaimNoted, Note: null));
        Assert.Equal(("stood-down", "ended.lost-claim", false), (closed.State, closed.Parts![0].Code, interrupted));

        (closed, interrupted) = Observation.Closed(new SessionStop(Reason: null, Note: null));
        Assert.Equal(("stopped", "ended.stopped", false), (closed.State, closed.Parts![0].Code, interrupted));

        var paused = Noted.Of(NoteCodes.EndedStopped, "stopped for the pause.");
        (closed, interrupted) = Observation.Closed(new SessionStop(Reason: null, paused));
        Assert.Equal(("stopped", "stopped for the pause.", false), (closed.State, closed.Note, interrupted));
    }

    /// <summary>
    /// 🔴 BGWAIT2: a run the timeout killed after its work brought its quest to done ended its work, so it is completed, as a
    /// done outranks a messy exit, and its note says both the done and the timeout. The install's session committed and closed
    /// its quest done, then waited 48 minutes on a prompt its adapter never answered, and was recorded failed. Only a done this
    /// run reached counts: one whose quest had closed before it went on with the person's words is judged on those (MSG1b), and
    /// a quest still taken at the timeout is failed, which D80 carries on.
    /// </summary>
    [Fact]
    public void A_timeout_after_the_run_brought_its_quest_to_done_completes_and_says_both()
    {
        foreach (var startedOn in new[] { "Open", "Taken" })
        {
            var done = Observation.AfterTimeout(90, startedOn, "Done");
            Assert.Equal("completed", done.State);
            Assert.Equal(["ended.done", "ended.timeout"], done.Parts!.Select(part => part.Code));
            Assert.Equal("the quest reached done. timed out after 90 minutes and was killed.", done.Note);
            Assert.Equal(90, done.Parts![1].Value("minutes"));
        }

        foreach (var (startedOn, status) in new[] { ("Taken", "Taken"), ("Open", "Open"), ("Done", "Done"), ("Taken", "Declined") })
        {
            var failed = Observation.AfterTimeout(90, startedOn, status);
            Assert.Equal(("failed", "ended.timeout"), (failed.State, Assert.Single(failed.Parts!).Code));
            Assert.Equal("timed out after 90 minutes and was killed.", failed.Note);
        }
    }

    /// <summary>
    /// FABRIC-CLOSE1h5 (D173's CLOSELAND1 note): in a closure graph a quest stays taken at its agent's <c>ready</c>, so a session that took
    /// it and ended there would read as parked, waiting on the person, and never be due. Its submission is its result in: completed, as a
    /// done is, before a messy exit or a failed turn, a first run's and a carried-on run's alike; a new question still asks first.
    /// </summary>
    [Fact]
    public void A_session_ending_at_its_quests_submission_completes_where_it_would_have_parked()
    {
        var parked = Observation.Conclude(0, "Taken", took: true);
        Assert.Equal("awaiting-person", parked.State);

        foreach (var submitted in new[]
                 {
                     Observation.Conclude(0, "Taken", took: true, submitted: true),
                     Observation.Conclude(0, "Taken", turnFailed: "the connection was reset", submitted: true),
                     Observation.Resumed(0, "awaiting-person", "Taken", "Taken", took: true, submitted: true),
                 })
        {
            Assert.Equal(("completed", "ended.submitted"), (submitted.State, submitted.Parts![0].Code));
        }

        Assert.Equal(["ended.submitted", "ended.exit"], Observation.Conclude(3, "Taken", submitted: true).Parts!.Select(part => part.Code));
        Assert.Equal("ended.awaits", Observation.Conclude(0, "Taken", awaitsAfter: "q9", submitted: true).Parts![0].Code);
    }

    /// <summary>
    /// ACCESSSWAP1b (D183's note): a run Daoris ended at its turn's end because its repository's permission prompts changed is neither a
    /// park nor a failure. Where it ended cleanly with its quest as a park or an untouched quest would read, a first run's and a resumed
    /// one's alike, it is completed, saying the change and that it goes on in the same conversation under the new setup; where its work
    /// had reached an ending of its own (done, declined, a wait) or its door failed, that ending stands, the line after it.
    /// </summary>
    [Fact]
    public void A_run_ended_for_a_changed_setup_completes_where_it_would_have_parked_and_says_the_change()
    {
        var swap = new SetupSwap(AccessRules.Asking, AccessRules.Unrestricted);
        foreach (var observed in new[]
                 {
                     Observation.Conclude(0, "Taken", took: true),
                     Observation.Conclude(0, "Open"),
                     Observation.Resumed(0, "awaiting-person", "Taken", "Taken", took: true),
                 })
        {
            var swapped = Observation.SetupChanged(observed, swap);
            Assert.Equal(("completed", "ended.setup-changed"), (swapped.State, Assert.Single(swapped.Parts!).Code));
            Assert.Equal(AccessRules.Asking, swapped.Parts![0].Value("from"));
            Assert.Equal(AccessRules.Unrestricted, swapped.Parts![0].Value("to"));
            Assert.Equal(
                "its permission prompts changed while it worked, from `asking` to `unrestricted`: Daoris ended it at its turn's end, never "
                + "mid-turn, and it goes on in the same conversation under the new setup.",
                swapped.Note);
        }

        foreach (var (observed, state, first) in new[]
                 {
                     (Observation.Conclude(0, "Done"), "completed", "ended.done"),
                     (Observation.Conclude(0, "Declined"), "declined", "ended.declined"),
                     (Observation.Conclude(0, "Taken", awaitsAfter: "q9"), "completed", "ended.awaits"),
                     (Observation.Conclude(0, "Taken", took: true, turnFailed: "the connection was reset"), "failed", "ended.turn-failed-taken"),
                     (Observation.Resumed(0, "completed", "Done", "Done"), "completed", "ended.went-on"),
                 })
        {
            var swapped = Observation.SetupChanged(observed, swap);
            Assert.Equal((state, first, "ended.setup-changed"), (swapped.State, swapped.Parts![0].Code, swapped.Parts[^1].Code));
        }
    }

    /// <summary>Whose decision a stop was (SURF5b): the person's where the driver gave no reason of its own.</summary>
    [Fact]
    public void A_stop_with_the_drivers_reason_is_the_drivers_and_any_other_the_persons()
    {
        Assert.False(new SessionStop(Daoris.Driver.Driver.LostClaimNoted, Note: null).ByPerson);
        Assert.True(new SessionStop(Reason: null, Note: null).ByPerson);
    }

    /// <summary>
    /// 🔴 A refused credential is OBSERVED in the tool's own words (AGT3b). Measured on Claude Code
    /// 2.1.280 with an invalid key: a text-mode run prints nothing for 189 s while it retries, then
    /// this one line, and exits 1. Read only where the harness itself said the run failed (AGT3c).
    /// </summary>
    [Fact]
    public void A_provider_s_refusal_is_read_from_the_tool_s_own_last_words()
    {
        const string measured = "Failed to authenticate. API Error: 401 API key is invalid.";
        const string pattern = "API Error: 401";

        Assert.True(Observation.Refused(new HarnessEnding(1, null, ["some work", measured]), pattern));
        Assert.False(Observation.Refused(new HarnessEnding(1, null, ["API Error: 529 overloaded", "exit"]), pattern));
        // A tool that declares no such words has no refusal to observe.
        Assert.False(Observation.Refused(new HarnessEnding(1, null, [measured]), null));
    }

    /// <summary>
    /// AGT3c: the door's failure is the harness's own word, read whatever the exit; its own lines are read only beside a
    /// failure it reported, so a run it ended normally holds nothing, whatever its lines say.
    /// </summary>
    [Fact]
    public void A_provider_s_refusal_is_read_only_where_the_harness_said_the_run_failed()
    {
        const string measured = "Failed to authenticate. API Error: 401 API key is invalid.";
        const string pattern = "API Error: 401";

        Assert.True(Observation.Refused(new HarnessEnding(0, measured, []), pattern));
        Assert.True(Observation.Refused(new HarnessEnding(0, "Overloaded", [measured]), pattern));
        Assert.False(Observation.Refused(new HarnessEnding(0, null, [measured]), pattern));
        Assert.False(Observation.Refused(new HarnessEnding(null, null, [measured]), pattern));
        Assert.False(Observation.Refused(new HarnessEnding(1, "Overloaded", []), pattern));
    }

    [Fact]
    public void A_done_quest_is_a_completed_session()
    {
        Assert.Equal("completed", Observation.Conclude(0, "Done").State);
    }

    /// <summary>The quest reaching done outranks a messy exit — the work landed; the exit is noted.</summary>
    [Fact]
    public void A_done_quest_with_a_bad_exit_still_completed_but_says_so()
    {
        var conclusion = Observation.Conclude(1, "Done");

        Assert.Equal("completed", conclusion.State);
        Assert.Contains("exit 1", conclusion.Note);
    }

    [Fact]
    public void A_declined_quest_is_a_declined_session()
    {
        Assert.Equal("declined", Observation.Conclude(0, "Declined").State);
    }

    /// <summary>
    /// A clean exit with the quest taken is the stand-down shape: the session found someone else's
    /// claim and finished without touching anything (D46 §3).
    /// </summary>
    [Fact]
    public void A_clean_exit_with_the_quest_taken_is_a_stand_down()
    {
        Assert.Equal("stood-down", Observation.Conclude(0, "Taken").State);
    }

    [Fact]
    public void A_bad_exit_with_the_quest_taken_is_a_failure_that_names_the_exit()
    {
        var conclusion = Observation.Conclude(2, "Taken");

        Assert.Equal("failed", conclusion.State);
        Assert.Contains("exit 2", conclusion.Note);
    }

    /// <summary>Exited having never claimed its target: nothing happened, and that is a failure to explain.</summary>
    [Fact]
    public void Any_exit_with_the_quest_still_open_is_a_failure()
    {
        Assert.Equal("failed", Observation.Conclude(0, "Open").State);
        Assert.Equal("failed", Observation.Conclude(3, "Open").State);
    }

    // ——— Ask and wait (D79).

    /// <summary>
    /// A session that asked another repository and waited ended well: its quest stays taken, marked with
    /// the question, and the driver resumes it. Not a failure, and no strike.
    /// </summary>
    [Fact]
    public void A_session_that_asked_and_waited_completed()
    {
        var conclusion = Observation.Conclude(0, "Taken", awaitsBefore: null, awaitsAfter: "q1");

        Assert.Equal("completed", conclusion.State);
        Assert.Contains("#q1", conclusion.Note);
    }

    /// <summary>A resumed session that asks again waits again — a new question, the same good ending.</summary>
    [Fact]
    public void A_resumed_session_that_waits_on_a_new_question_completed_too()
    {
        Assert.Equal("completed", Observation.Conclude(0, "Taken", awaitsBefore: "q1", awaitsAfter: "q2").State);
    }

    // ——— A turn the agent refused (ACPEND1).
    //
    // 🔴 Measured on the first real run: the account's spend limit refused the turn mid-edit, the
    // protocol door wrote so, and then the adapter exited 0 once stdin closed. With the quest still
    // taken, the record said "stood-down — someone else has it", which was false and hid the reason.

    private const string SpendLimit =
        "the ACP agent refused the call: Internal error: You've hit your individual spend limit";

    [Fact]
    public void A_turn_the_agent_refused_with_the_quest_still_taken_failed_in_its_words()
    {
        var conclusion = Observation.Conclude(0, "Taken", turnFailed: SpendLimit);

        Assert.Equal("failed", conclusion.State);
        Assert.Contains("spend limit", conclusion.Note);
        Assert.DoesNotContain("someone else", conclusion.Note);
    }

    [Fact]
    public void A_turn_the_agent_refused_before_the_take_failed_in_its_words_too()
    {
        var conclusion = Observation.Conclude(0, "Open", turnFailed: SpendLimit);

        Assert.Equal("failed", conclusion.State);
        Assert.Contains("spend limit", conclusion.Note);
    }

    /// <summary>Where the work reached its close, or its wait, before the failure, that ending stands.</summary>
    [Fact]
    public void A_turn_that_failed_after_the_close_or_the_wait_keeps_that_ending()
    {
        Assert.Equal("completed", Observation.Conclude(0, "Done", turnFailed: SpendLimit).State);
        Assert.Equal("declined", Observation.Conclude(0, "Declined", turnFailed: SpendLimit).State);
        Assert.Equal("completed", Observation.Conclude(0, "Taken", awaitsAfter: "q1", turnFailed: SpendLimit).State);
    }

    // ——— A session that holds its own quest and stops (STANDDOWN2).
    //
    // 🔴 FG5's verify session took its quest, did what it could, and ended its turn holding it open
    // with three questions for the person. The record said "stood-down — someone else has it". It was
    // waiting on the person, and nothing told them. Its own take is written on its record now.

    private const string Asked = "Three things from you and I can run the whole checklist.";

    [Fact]
    public void A_session_that_took_its_quest_and_ended_holding_it_is_waiting_on_the_person()
    {
        var conclusion = Observation.Conclude(0, "Taken", took: true, lastWords: Asked);

        Assert.Equal("awaiting-person", conclusion.State);
        Assert.Contains(Asked, conclusion.Note);
        Assert.DoesNotContain("someone else", conclusion.Note);
    }

    /// <summary>Only a session that took the quest itself: one that found it taken still stood down.</summary>
    [Fact]
    public void A_session_that_found_its_quest_taken_still_stood_down()
    {
        Assert.Equal("stood-down", Observation.Conclude(0, "Taken", took: false, lastWords: Asked).State);
    }

    /// <summary>
    /// A resume (D79) or a carry-on (D80) that ends holding the quest is waiting on the person too —
    /// its take was this machine's before it began. A park is never resumed by itself, so nothing
    /// loops; a messy exit is still a failure, which the strikes bound.
    /// </summary>
    [Fact]
    public void A_resumed_or_carried_on_session_that_ends_holding_its_quest_waits_on_the_person()
    {
        Assert.Equal("awaiting-person", Observation.Conclude(0, "Taken", resumed: true, lastWords: Asked).State);
        Assert.Equal("awaiting-person", Observation.Conclude(0, "Taken", awaitsBefore: "q1", awaitsAfter: "q1").State);
        Assert.Equal("failed", Observation.Conclude(1, "Taken", resumed: true).State);
        Assert.Equal("completed", Observation.Conclude(0, "Done", resumed: true).State);
        Assert.Equal("completed", Observation.Conclude(0, "Taken", awaitsAfter: "q9", resumed: true).State);
    }

    /// <summary>
    /// FABRIC-CLOSE1c2 (§E5): a session told at its step that the person reopened the work its quest grew from stops holding the
    /// quest. Not a park, since the person's choice is the step's to take, not an answer to it, and not a stand-down, since the take is
    /// its own: a good ending, as a wait is, that the person's go-on carries on in the same tree. A messy exit, a failed turn and a
    /// close made first are read as before.
    /// </summary>
    [Fact]
    public void A_session_whose_source_was_reopened_ends_holding_its_quest_for_the_persons_choice()
    {
        foreach (var conclusion in new[]
                 {
                     Observation.Conclude(0, "Taken", took: true, lastWords: Asked, reopenedFrom: "q1"),
                     Observation.Conclude(0, "Taken", resumed: true, reopenedFrom: "q1"),
                     Observation.Conclude(0, "Taken", reopenedFrom: "q1"),
                 })
        {
            Assert.Equal("completed", conclusion.State);
            var part = Assert.Single(conclusion.Parts!);
            Assert.Equal((NoteCodes.EndedSourceReopened.Code, "q1"), (part.Code, part.Value("quest")?.ToString()));
            Assert.Equal(
                "stopped with its quest still taken: the work of `#q1` it grew from was reopened, and it goes on in the same tree once "
                + "the person goes on with it on the new work.",
                conclusion.Note);
        }

        Assert.Equal("failed", Observation.Conclude(1, "Taken", took: true, reopenedFrom: "q1").State);
        Assert.Equal("failed", Observation.Conclude(0, "Taken", turnFailed: "limit reached", took: true, reopenedFrom: "q1").State);
        Assert.Equal("completed", Observation.Conclude(0, "Done", took: true, reopenedFrom: "q1").State);
        Assert.Equal("awaiting-person", Observation.Conclude(0, "Taken", took: true, lastWords: Asked).State);
    }

    private const string PicksUp = "The gates are running in the background (lint, then the tests); I'll pick up when they finish.";

    /// <summary>
    /// BGWAIT1c (D83's BGWAIT1 note, "the native door"): on the native door the harness ends a session's own background work with
    /// its turn, so a turn that ended on that work is no question and has nothing to wait for. Where it would have parked, quoting
    /// what it said of the work, it is cut off by a code of its own, failed, so D80 carries it on in the same tree and the strikes
    /// bound it, its note naming the work. A start and a resumed run alike; the person's reopen, a close, a wait, a messy exit and a
    /// failed turn are read as before, and a turn whose work the harness did not cut parks as before.
    /// </summary>
    [Fact]
    public void A_native_turn_that_ended_on_work_its_harness_cut_is_cut_off_not_parked()
    {
        foreach (var conclusion in new[]
                 {
                     Observation.Conclude(0, "Taken", took: true, lastWords: PicksUp, backgroundCut: ["npm run gates"]),
                     Observation.Conclude(0, "Taken", resumed: true, lastWords: PicksUp, backgroundCut: ["npm run gates"]),
                     Observation.Resumed(0, "awaiting-person", "Taken", "Taken", took: true, lastWords: PicksUp, backgroundCut: ["npm run gates"]),
                 })
        {
            Assert.Equal("failed", conclusion.State);
            var part = Assert.Single(conclusion.Parts!);
            Assert.Equal(NoteCodes.EndedBackgroundCut.Code, part.Code);
            Assert.Equal(["npm run gates"], Assert.IsAssignableFrom<IReadOnlyList<string>>(part.Value("works")));
            Assert.Equal(
                "its turn ended while its own background work ran: `npm run gates`. Its agent ends such work with the turn, so it "
                + "was cut off before that work finished, and is carried on in the same tree.",
                conclusion.Note);
            Assert.DoesNotContain(PicksUp, conclusion.Note);
        }

        Assert.Equal("completed", Observation.Conclude(0, "Done", took: true, backgroundCut: ["npm run gates"]).State);
        Assert.Equal("completed", Observation.Conclude(0, "Taken", awaitsAfter: "q9", took: true, backgroundCut: ["npm run gates"]).State);
        Assert.Equal(
            NoteCodes.EndedSourceReopened.Code,
            Observation.Conclude(0, "Taken", took: true, reopenedFrom: "q1", backgroundCut: ["npm run gates"]).Parts![0].Code);
        Assert.Equal(NoteCodes.EndedTakenExit.Code, Observation.Conclude(1, "Taken", took: true, backgroundCut: ["npm run gates"]).Parts![0].Code);
        Assert.Equal(
            NoteCodes.EndedTurnFailedTaken.Code,
            Observation.Conclude(0, "Taken", turnFailed: "limit reached", took: true, backgroundCut: ["npm run gates"]).Parts![0].Code);
        Assert.Equal("stood-down", Observation.Conclude(0, "Taken", took: false, backgroundCut: ["npm run gates"]).State);
        Assert.Equal("awaiting-person", Observation.Conclude(0, "Taken", took: true, lastWords: PicksUp, backgroundCut: []).State);
    }

    /// <summary>With no last words to quote, the park still says where they would be.</summary>
    [Fact]
    public void A_park_with_no_last_words_points_at_the_transcript()
    {
        Assert.Contains("transcript", Observation.Conclude(0, "Taken", took: true).Note);
    }

    /// <summary>
    /// BGWAIT1: whether a turn's clean end would park on the person is one rule, read twice — by the conclusion, and by the
    /// protocol door when a turn ends while the session's own background work runs, to keep such a session working rather
    /// than park it. Held to agree with <see cref="Observation.Conclude"/> on every quest state and take this table covers.
    /// </summary>
    [Theory]
    [InlineData("Taken", null, null, false, true, true)]
    [InlineData("Taken", null, null, true, false, true)]
    [InlineData("Taken", "q1", "q1", false, false, true)]
    [InlineData("Taken", null, null, false, false, false)]
    [InlineData("Taken", null, "q9", false, true, false)]
    [InlineData("Taken", "q1", "q9", true, false, false)]
    [InlineData("Done", null, null, false, true, false)]
    [InlineData("Declined", null, null, true, false, false)]
    [InlineData("Open", null, null, false, false, false)]
    public void Whether_a_clean_end_parks_on_the_person_is_the_conclusions_rule(
        string status, string? awaitsBefore, string? awaitsAfter, bool resumed, bool took, bool parks)
    {
        Assert.Equal(parks, Observation.Parks(status, awaitsBefore, awaitsAfter, resumed, took));
        Assert.Equal(parks, Observation.Conclude(0, status, awaitsBefore, awaitsAfter, resumed: resumed, took: took).State == "awaiting-person");
    }
}
