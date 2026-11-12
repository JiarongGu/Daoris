using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The processes a run started, kept by run, process id and creation time: stopped at the run's end only while the id still names the
/// process that run started, and a process the run did not start is named and never stopped.
/// </summary>
public sealed class RunProcessesTests
{
    private static readonly long Second = TimeSpan.TicksPerSecond;

    [Fact]
    public void A_run_stops_what_it_started_and_only_while_the_id_is_the_same_process()
    {
        var alive = new Dictionary<int, long> { [10] = 1_000 * Second, [11] = 2_000 * Second, [12] = 3_000 * Second };
        var killed = new List<int>();
        var owned = new RunProcesses(pid => alive.TryGetValue(pid, out var created) ? created : null, killed.Add);
        owned.Track("run1", 10, 1_000 * Second);
        owned.Track("run1", 11, 2_000 * Second);
        owned.Track("run1", 13, 4_000 * Second);
        owned.Track("run2", 12, 3_000 * Second);
        // Process 11's id now names another process: the run's own has ended.
        alive[11] = 9_999 * Second;

        var stopped = owned.Stop("run1");

        Assert.Equal([10], killed);
        Assert.Equal([10], stopped.Select(each => each.Pid));
        Assert.Equal([12], owned.Standing("run2").Select(each => each.Pid));
        Assert.Empty(owned.Standing("run1"));
    }

    [Fact]
    public void A_process_the_run_did_not_start_is_named_and_never_stopped()
    {
        var alive = new Dictionary<int, long> { [10] = 1_000 * Second, [20] = 5_000 * Second };
        var killed = new List<int>();
        var owned = new RunProcesses(pid => alive.TryGetValue(pid, out var created) ? created : null, killed.Add);
        owned.Track("run1", 10, 1_000 * Second);

        var left = owned.Left("run1", [(10, "node"), (20, "dotnet")]);

        Assert.Equal([(10, true), (20, false)], left.Select(each => (each.Pid, each.Owned)));
        owned.Stop("run1");
        Assert.Equal([10], killed);
    }
}
