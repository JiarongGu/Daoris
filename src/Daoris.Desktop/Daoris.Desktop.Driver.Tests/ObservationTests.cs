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
