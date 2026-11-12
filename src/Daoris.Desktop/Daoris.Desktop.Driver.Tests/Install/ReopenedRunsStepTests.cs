using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The migration's reopening of the runs an older build ended under a step still to run after a landing: such a run's end lapses so its
/// monitor or release is read again, while one that ended with its landing never reached, or with nothing standing, keeps its end, and the
/// step run again changes nothing.
/// </summary>
public sealed class ReopenedRunsStepTests : NodeRunFixture
{
    /// <summary>A landing, its push and pull request, then the monitor of that pull request, a tidy and the person's close after it.</summary>
    private static readonly string LandedThenWatched = MonitorNodeTests.Graph(
        MonitorNodeTests.Watching, """{"id":"m","kind":"after","from":"pull-request","to":"monitor"},""" + MonitorNodeTests.Routes);

    private void Ended(string run, string landing, string monitor, string? why)
    {
        var unit = RunUnits.IdOf(run);
        NodeStatus Of(string node, string state) => new(unit, node, state) { Attempt = 1, Tries = 1 };
        Files.Write(new NodeRecord(unit)
        {
            Statuses = new Dictionary<string, NodeStatus>
            {
                ["work"] = Of("work", NodeStates.Reached), ["landing"] = Of("landing", landing), ["push"] = Of("push", NodeStates.Reached),
                ["pull-request"] = Of("pull-request", NodeStates.Reached), ["monitor"] = Of("monitor", monitor),
                ["tidy"] = Of("tidy", NodeStates.Blocked), ["closed"] = Of("closed", NodeStates.Blocked),
            },
            Ended = new RunEnd(run, _now) { Outcome = "completed", Why = why },
        });
    }

    private HomeMigrationRun Sweep()
    {
        var run = new HomeMigrationRun(_home, HomeMigration.Name);
        new ReopenedRunsStep().Run(run);
        return run;
    }

    [Fact]
    public void A_run_ended_branch_gone_or_on_line_under_a_monitor_after_a_reached_landing_is_reopened_counted_and_the_rest_are_left()
    {
        var digest = RunHome.SaveGraph(_home, LandedThenWatched);
        foreach (var run in new[] { "gone", "online", "unlanded", "settled", "plain" }) RunHome.Bind(_home, run, digest, _now);
        Ended("gone", NodeStates.Reached, NodeStates.Active, ClosedRunEnds.BranchGone);
        Ended("online", NodeStates.Reached, NodeStates.Active, ClosedRunEnds.OnLine);
        Ended("unlanded", NodeStates.Active, NodeStates.Blocked, ClosedRunEnds.BranchGone);
        Ended("settled", NodeStates.Reached, NodeStates.Reached, ClosedRunEnds.BranchGone);
        Ended("plain", NodeStates.Reached, NodeStates.Active, null);

        var swept = Sweep();

        Assert.Null(Files.Read(RunUnits.IdOf("gone"))!.Ended);
        Assert.Null(Files.Read(RunUnits.IdOf("online"))!.Ended);
        Assert.NotNull(Files.Read(RunUnits.IdOf("unlanded"))!.Ended);
        Assert.NotNull(Files.Read(RunUnits.IdOf("settled"))!.Ended);
        Assert.NotNull(Files.Read(RunUnits.IdOf("plain"))!.Ended);
        Assert.Equal(2, swept.Counts["reopened-runs.reopened"]);
        Assert.Equal(2, swept.Changed.Count);
        Assert.Empty(Sweep().Changed);
    }

    [Fact]
    public void The_step_is_one_of_the_releases_migration_after_the_closed_runs_step()
    {
        var steps = HomeMigration.Steps.ToList();
        Assert.True(steps.FindIndex(step => step is ReopenedRunsStep) > steps.FindIndex(step => step is ClosedRunsStep));
    }
}
