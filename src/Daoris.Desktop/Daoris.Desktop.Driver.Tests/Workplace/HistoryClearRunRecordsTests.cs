using Daoris.Driver;
using static Daoris.Desktop.Driver.Tests.HistoryStandIn;
using StandIn = Daoris.Desktop.Driver.Tests.HistoryStandIn;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>A history clear takes a run's node record with its binding, counted in the plan's bytes, and the left-over sweep takes both.</summary>
public sealed class HistoryClearRunRecordsTests : IDisposable
{
    private static readonly DateTimeOffset At = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-history-runs-" + Guid.NewGuid().ToString("N")[..8]);

    public HistoryClearRunRecordsTests() => Directory.CreateDirectory(DaorisHome.Entry(_home, HomeEntry.Sessions));

    public void Dispose()
    {
        if (Directory.Exists(_home)) Directory.Delete(_home, recursive: true);
    }

    private HistoryWorld World(StandIn service, Func<DateTimeOffset>? clock = null)
    {
        service.Home = _home;
        return new HistoryWorld(service.Client(), _home, DaorisHome.Entry(_home, HomeEntry.DriverConfig), new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions)))
        {
            Clock = clock ?? (() => DateTimeOffset.UtcNow),
        };
    }

    /// <summary>Run <paramref name="run"/> bound, with its node record: the binding's path and the record's.</summary>
    private (string Binding, string Record) Recorded(string run)
    {
        Assert.True(WorkflowRunBindings.Bind(_home, new WorkflowRunBinding(run, "engine", "default", WorkflowSelection.Current, "default", At)));
        new NodeRecordFiles(_home, () => true).Write(new NodeRecord(RunUnits.IdOf(run)) { Ended = new RunEnd(run, At) });
        return (WorkflowRunBindings.PathOf(_home, run), NodeRecordFiles.PathOf(_home, RunUnits.IdOf(run)));
    }

    [Fact]
    public async Task A_cleared_quests_run_record_goes_with_its_binding_and_its_bytes_are_counted()
    {
        var mine = Recorded("q1");
        var other = Recorded("q2");
        var service = new StandIn { Quests = [Quest("q1"), Quest("q2")] };
        service.Listings["quest=q1"] = [Unit("quest", "q1", quests: ["q1"])];

        var plan = await HistoryClearing.PlanAsync(World(service), HistoryScope.Quest, "#q1");
        var unit = Assert.Single(plan.Units);
        Assert.True(unit.Bytes.Total >= new FileInfo(mine.Binding).Length + new FileInfo(mine.Record).Length);
        var outcome = await HistoryClearing.ClearAsync(World(service), HistoryScope.Quest, "q1", [unit.Name], PluginEvents.Screen);

        Assert.False(File.Exists(mine.Record));
        Assert.False(File.Exists(mine.Binding));
        Assert.True(File.Exists(other.Record));
        Assert.Equal(unit.Bytes.Total, outcome.Bytes);
    }

    [Fact]
    public async Task A_workspaces_clear_takes_a_left_over_bindings_record_too()
    {
        var now = DateTimeOffset.UtcNow + TimeSpan.FromHours(2);
        var orphan = Recorded("qgone");
        File.SetLastWriteTimeUtc(orphan.Binding, DateTime.UtcNow - TimeSpan.FromHours(3));
        File.SetLastWriteTimeUtc(orphan.Record, DateTime.UtcNow - TimeSpan.FromHours(3));
        var service = new StandIn { Quests = [Quest("q1", workspace: "aurora")] };
        service.Listings["workspace=aurora"] = [Unit("quest", "q1", "aurora", quests: ["q1"])];
        var world = World(service, () => now);

        var plan = await HistoryClearing.PlanAsync(world, HistoryScope.Workspace, "aurora");
        var outcome = await HistoryClearing.ClearAsync(
            world, HistoryScope.Workspace, "aurora", [.. plan.Units.Where(unit => unit.Clearable).Select(unit => unit.Name)], PluginEvents.Screen);

        Assert.False(File.Exists(orphan.Record));
        Assert.Equal(plan.Reading!.Takes.Bytes, outcome.Bytes);
    }
}
