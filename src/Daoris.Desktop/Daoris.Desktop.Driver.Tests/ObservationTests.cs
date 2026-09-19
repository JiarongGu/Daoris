using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A session's end is OBSERVED, never self-reported (D46 §4): the exit code and the quest's own state
/// are the two signals, because they are the two signals outside work also produces. This table is the
/// whole mapping, so this table is the whole test.
/// </summary>
public sealed class ObservationTests
{
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
}
