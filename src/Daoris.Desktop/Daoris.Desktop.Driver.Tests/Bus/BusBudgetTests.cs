using Daoris.Driver;
using Daoris.Testing;
using Xunit.Abstractions;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The bytes the bus log's writes put on disk in a day are held to a budget, a rise past it said once in the machine log however many
/// writes follow, and `daoris bus stats` says how much of the budget each day used. Homes under <c>_fixtures/</c>.
/// </summary>
public sealed class BusBudgetTests(ITestOutputHelper output) : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "bus-budget", Guid.NewGuid().ToString("N")[..8]);

    public void Dispose() => GitSeed.Discard(_home);

    /// <summary>🔴 A day past a small budget is said once however many writes follow it, and the stats say the day's share of the real one.</summary>
    [Fact]
    public async Task A_day_past_the_disk_budget_is_said_once_and_the_stats_say_its_share()
    {
        Directory.CreateDirectory(_home);
        var clock = new MovedClock();
        using var log = new MachineLog(_home, "driver-test", () => clock.GetUtcNow());
        await using (var bus = BusLog.Over(new DesktopBusLogStore(_home), new BusLogOptions { Clock = clock, Log = log, DiskBudget = 8192 }))
        {
            for (var n = 0; n < 4; n++)
            {
                Assert.True(bus.Append(new BusDraft(BusTypes.CheckPassed, $"run-{n}") { Commit = true, Payload = [("invocation", $"i-{n}")] }));
                await bus.FlushAsync();
            }
        }

        using var file = new FileStream(
            Directory.GetFiles(DaorisHome.Entry(_home, HomeEntry.Logs), "*.jsonl").Single(), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var lines = new StreamReader(file).ReadToEnd().Split('\n');
        Assert.Single(lines, line => line.Contains("\"bus.over-budget\"", StringComparison.Ordinal));

        Assert.Equal(0, BusStatsCommand.Run(["stats"], _home, () => clock.GetUtcNow(), out var said));
        output.WriteLine(said);
        Assert.Matches(@"% of the day's 128 MiB budget", said);
        Assert.Equal(128L * 1024 * 1024, BusLog.DiskBudget);
    }
}
