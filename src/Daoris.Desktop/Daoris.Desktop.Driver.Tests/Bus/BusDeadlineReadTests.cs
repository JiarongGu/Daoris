using Daoris.Driver;
using Daoris.Testing;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The read BUS2b3 is: `daoris bus stats` sets each obliged type's deadline beside the most its kind's <c>handled</c> measure holds over the
/// days read, and names the deadline that most would set (twice the most, up to five minutes), so a provisional deadline is replaced from one
/// command's output and none is worked out by hand. Homes under <c>_fixtures/</c>.
/// </summary>
public sealed class BusDeadlineReadTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "bus-deadline-read", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    /// <summary>🔴 The figures the registry already holds are what the rule gives from the times they were read from.</summary>
    [Theory]
    [InlineData(3_091_955L, 105)]
    [InlineData(867_000L, 30)]
    [InlineData(1L, 5)]
    [InlineData(150_000L, 5)]
    [InlineData(150_001L, 10)]
    public void Twice_the_most_rounds_up_to_five_minutes(long mostMs, int minutes) =>
        Assert.Equal(TimeSpan.FromMinutes(minutes), BusDeadline.Reading(mostMs));

    /// <summary>🔴 A provisional type's line carries its kind's handled count and most, summed and maxed over the days, with the deadline that most would set;
    /// a type with no handled measure says so, and a measured one is not offered another figure.</summary>
    [Fact]
    public async Task The_stats_set_each_provisional_deadline_beside_its_handled_most()
    {
        Directory.CreateDirectory(_home);
        var clock = new MovedClock();
        using var log = new MachineLog(_home, "driver-test", () => clock.GetUtcNow());
        await using (var bus = BusLog.Over(new DesktopBusLogStore(_home), new BusLogOptions { Clock = clock, Log = log }))
        {
            bus.Measured(BusMeasures.Handled, $"{BusTypes.Step}:{BusTypes.CheckPassed.Kind}", 120_000);
            bus.Measured(BusMeasures.Handled, $"{BusTypes.Step}:{BusTypes.CheckPassed.Kind}", 400_000);
            clock.Advance(TimeSpan.FromDays(1));
            bus.Measured(BusMeasures.Handled, $"{BusTypes.Step}:{BusTypes.CheckPassed.Kind}", 200_000);
            bus.Measured(BusMeasures.Handled, $"{BusTypes.Checks}:{BusTypes.CheckStarted.Kind}", 5_000);
            Assert.True(bus.Append(new BusDraft(BusTypes.CheckPassed, "run-1") { Commit = true, Payload = [("invocation", "i-1")] }));
            await bus.FlushAsync();
        }

        Assert.Equal(0, BusStatsCommand.Run(["stats"], _home, () => clock.GetUtcNow(), out var said));
        output.WriteLine(said);

        var lines = said.Split('\n').Select(line => line.Trim()).ToList();
        Assert.Contains(lines, line => line == "deadlines, each obliged type's against its kind's handled most over these days:");
        Assert.Contains(lines, line => line.StartsWith("check.passed/1: provisional 10 min; handled n=3 most 400000 ms, which sets 15 min", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("step.settled/1: provisional 10 min; no handled measure yet", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("check.started/1: measured 105 min", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.StartsWith("check.started/1", StringComparison.Ordinal) && line.Contains("which sets", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => line.StartsWith("bus.stalled/1", StringComparison.Ordinal));
    }
}
