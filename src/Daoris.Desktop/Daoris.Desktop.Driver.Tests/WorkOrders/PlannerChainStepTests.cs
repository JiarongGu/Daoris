using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The work a creation chains behind its set-up is published when the set-up closes done, to the place the set-up opened. Where that place's
/// row still reads unadopted, the step is planned as any quest to a registered place is, and is never lost.
/// </summary>
public sealed class PlannerChainStepTests
{
    private static readonly QuestView Step = new("q2", "ask #a1", "Game", "Build the report", "After #q1.", "Open") { Parent = "q1" };

    private static Consideration Plan(RepoView repo, bool unadopted = false, bool drivable = true) =>
        Assert.Single(Planner.Plan(
            new Snapshot([Step], [repo], []).Apart("Game"),
            DriverConfig.Empty with { Drivable = drivable ? ["Game"] : [] },
            unadopted));

    [Fact]
    public void Over_the_pipe_door_the_step_sits_as_a_wait_until_the_follow_registers_the_adoption_then_starts()
    {
        var sitting = Plan(new RepoView("Game", false, "D:/fam/Game"));

        Assert.Equal(StartVerdict.NotAdopted, sitting.Verdict);
        Assert.Equal(new SittingFact(SittingFact.NoConnector), sitting.Said);
        Assert.Equal(StartVerdict.Start, Plan(new RepoView("Game", true, "D:/fam/Game")).Verdict);
    }

    [Fact]
    public void Over_the_protocol_door_the_step_starts_whether_or_not_the_row_reads_adopted()
    {
        Assert.Equal(StartVerdict.Start, Plan(new RepoView("Game", false, "D:/fam/Game"), unadopted: true).Verdict);
        Assert.Equal(StartVerdict.Start, Plan(new RepoView("Game", true, "D:/fam/Game"), unadopted: true).Verdict);
    }

    [Fact]
    public void Where_the_creation_left_driving_off_the_step_waits_for_the_persons_opt_in_as_the_places_other_work_does()
    {
        Assert.Equal(StartVerdict.NotDrivable, Plan(new RepoView("Game", true, "D:/fam/Game"), drivable: false).Verdict);
    }
}
