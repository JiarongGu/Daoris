using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The headless runs that must return (<c>drive --once</c>, <c>--until-idle</c>) start the opinion's gate over the lease holder's bus
/// log beside their looks and drain it before they end, so an owed opinion is judged in them as in the watch, never left "not judged
/// yet". Over the real client, planner and judge with the service's doors and each start's run standing in, in-process.
/// </summary>
public sealed class OpinionGateHeadlessTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-gate-headless-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    public OpinionGateHeadlessTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "repos", "engine"));
    }

    public void Dispose()
    {
        _ledger.Dispose();
        GitSeed.Discard(_home);
    }

    /// <summary>🔴 A once-run judges the owed opinion at the bus's facts and drains them before it ends: here the judgment closes it, its tree gone.</summary>
    [Fact]
    public async Task A_once_run_judges_an_owed_opinion_and_drains_before_it_ends()
    {
        var (driver, bus) = Owed();
        await using (bus)
        {
            await driver.RunOnceAsync().WaitAsync(Poll.Patience);
            Assert.DoesNotContain(new OpinionDues(_home).Open(), due => due.Session == "s1");
        }
    }

    /// <summary>🔴 A run until idle judges the owed opinion too, and is idle only once the gate's facts are drained.</summary>
    [Fact]
    public async Task A_run_until_idle_judges_an_owed_opinion_and_drains_before_it_ends()
    {
        var (driver, bus) = Owed();
        await using (bus)
        {
            await driver.RunUntilIdleAsync().WaitAsync(Poll.Patience);
            Assert.DoesNotContain(new OpinionDues(_home).Open(), due => due.Session == "s1");
        }
    }

    /// <summary>
    /// A done quest `q1` on `engine` owed a second opinion under `steps`, its tree gone, nothing else open; the rule reads `engine` at
    /// `steps`. The headless driver holds the home's bus log, and no look of its judges the opinion.
    /// </summary>
    private (Daoris.Driver.Driver Driver, BusLog Bus) Owed()
    {
        _ledger.Publish("q1", "engine");
        _ledger.Move("q1", "Done");
        new OpinionDues(_home).Due(new OpinionDue(
            "s1", "q1", "engine", "default", DaorisHome.Entry(_home, HomeEntry.Trees, "default", "engine", "s-1"), OpinionRules.Steps,
            new DateTimeOffset(2026, 11, 22, 9, 0, 0, TimeSpan.Zero)));
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"], Actors = StaffedRoles.On("stub"), Cap = 3, PollSeconds = 60,
            Opinions = new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase) { ["engine"] = new(["codex-acp"], [OpinionRules.Steps], false) },
        };
        var bus = BusLog.Over(new DesktopBusLogStore(_home), new BusLogOptions { Clock = TimeProvider.System, Interval = TimeSpan.FromMilliseconds(20), Home = _home });
        var service = _ledger.Client();
        var runs = new StandInRuns(service, _ledger);
        var driver = new Daoris.Driver.Driver(
            service, config, ProviderRegistry.Built(), _home, new SessionProcesses(),
            harnesses: new HarnessRoster(ProviderRegistry.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Runner = runs.Runner, Stops = runs.Stops, Bus = bus,
        };
        runs.Keeping = driver.Running;
        return (driver, bus);
    }
}
