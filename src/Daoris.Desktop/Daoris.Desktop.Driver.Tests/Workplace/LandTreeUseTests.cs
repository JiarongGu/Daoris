using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Whether a session on a run's tree keeps its <c>land</c> from landing: a live session uses the tree, but the run's own part whose turn
/// ended waiting on the run's next step does not, since it waits on that landing. A landing that waits says why in the machine log once per
/// change of reason, never at every pass.
/// </summary>
public sealed class LandTreeUseTests
{
    private static readonly LandStart Start =
        new(RunUnits.IdOf("q1"), "q1", "landing", "branch:engine/s1", ["s1"], "engine", "aurora", "t1", "digest", null)
        {
            Authority = StepAuthority.OfDoor("q1", "landing", 1),
        };

    private static SessionRecord On(string id, string state, string? quest = "q1") => new(id, "engine", state) { Quest = quest, Tree = "X:/trees/s1" };

    [Fact]
    public void The_runs_own_part_waiting_on_its_step_does_not_use_the_tree()
    {
        Assert.False(LandTreeUse.Uses(On("s1", SessionStates.AwaitingStep), Start, "q1"));
        Assert.False(LandTreeUse.Uses(On("s2", SessionStates.AwaitingStep), Start, "q1"));
    }

    [Fact]
    public void A_working_part_and_another_runs_waiting_session_use_it_and_an_ended_one_does_not()
    {
        Assert.True(LandTreeUse.Uses(On("s1", "working"), Start, "q1"));
        Assert.True(LandTreeUse.Uses(On("s9", SessionStates.AwaitingStep, quest: "q9"), Start, "q1"));
        Assert.True(LandTreeUse.Uses(On("s9", SessionStates.AwaitingStep, quest: null), Start, "q1"));
        Assert.False(LandTreeUse.Uses(On("s9", "completed", quest: "q9"), Start, "q1"));
    }

    [Fact]
    public void A_wait_is_said_once_per_change_of_reason_and_again_after_a_try()
    {
        var home = Guid.NewGuid().ToString("N");

        Assert.True(NodeWaits.Changed(home, "run:q1", "landing", LandWaits.InUse));
        Assert.False(NodeWaits.Changed(home, "run:q1", "landing", LandWaits.InUse));
        Assert.True(NodeWaits.Changed(home, "run:q1", "landing", LandWaits.Unchanged));
        Assert.True(NodeWaits.Changed(home, "run:q1", "other", LandWaits.Unchanged));
        NodeWaits.Tried(home, "run:q1", "landing");
        Assert.True(NodeWaits.Changed(home, "run:q1", "landing", LandWaits.Unchanged));
    }

    [Fact]
    public void Its_line_names_the_run_the_node_and_the_reason_in_codes()
    {
        var line = LandWaits.Line(Start, LandWaits.InUse);

        Assert.Equal(LandWaits.Event, line.Event);
        Assert.Equal(
            [("run", (object?)"q1"), ("node", "landing"), ("repository", "engine"), ("workspace", "aurora"), ("reason", LandWaits.InUse)],
            line.Data);
    }
}
