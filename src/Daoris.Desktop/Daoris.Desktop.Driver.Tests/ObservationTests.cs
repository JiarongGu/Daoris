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
    /// this one line, and exits 1.
    /// </summary>
    [Fact]
    public void A_provider_s_refusal_is_read_from_the_tool_s_own_last_words()
    {
        const string measured = "Failed to authenticate. API Error: 401 API key is invalid.";
        const string pattern = "API Error: 401";

        Assert.True(Observation.Refused(["some work", measured], pattern));
        Assert.False(Observation.Refused(["API Error: 529 overloaded", "exit"], pattern));
        // A tool that declares no such words has no refusal to observe.
        Assert.False(Observation.Refused([measured], null));
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

    /// <summary>
    /// A session carrying on after a cut-off (D80) that ends with the quest still taken did not finish
    /// it. 🔴 Failed, not stood down — the take is this machine's own, so "someone else has it" is false,
    /// and only a failure counts against the strikes that bound carrying on.
    /// </summary>
    [Fact]
    public void A_session_that_carried_on_and_left_the_quest_taken_failed()
    {
        var conclusion = Observation.Conclude(0, "Taken", resumed: true);

        Assert.Equal("failed", conclusion.State);
        Assert.DoesNotContain("someone else", conclusion.Note);
        Assert.Equal("completed", Observation.Conclude(0, "Done", resumed: true).State);
        Assert.Equal("completed", Observation.Conclude(0, "Taken", awaitsAfter: "q9", resumed: true).State);
    }

    /// <summary>
    /// A resumed session that stops with the quest still waiting on the OLD question did not carry on.
    /// 🔴 Failed, not stood down: nothing about the quest changed, so a stand-down would be resumed
    /// again every tick, and only a failure is counted against the strikes.
    /// </summary>
    [Fact]
    public void A_resumed_session_that_leaves_the_old_wait_standing_failed()
    {
        var conclusion = Observation.Conclude(0, "Taken", awaitsBefore: "q1", awaitsAfter: "q1");

        Assert.Equal("failed", conclusion.State);
        Assert.Contains("#q1", conclusion.Note);
    }
}
