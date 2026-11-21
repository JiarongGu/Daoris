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
    /// 🔴 An owed opinion still waiting (its chain's later step `q2` not done) is judged at the start, which moves the gate's view once: a
    /// run until idle looks again for it and then returns, never looking on forever.
    /// </summary>
    [Fact]
    public async Task A_run_until_idle_returns_while_an_owed_opinion_still_waits()
    {
        _ledger.Publish("q1", "engine");
        _ledger.Move("q1", "Done");
        _ledger.Publish("q2", "engine", parent: "q1");
        _ledger.Move("q2", "Taken");
        new OpinionDues(_home).Due(new OpinionDue("s1", "q1", "engine", "default", "tree-1", OpinionRules.Landing, At));
        var judged = 0;
        await using var bus = Log();
        Daoris.Driver.Driver driver = null!;
        driver = Headless(bus, ct => OpinionGateRun.StartAsync(bus, new OpinionGateConsumer(
            (_, _, _) =>
            {
                // As the judge keeps its view: a judgment that moved it wakes the plan.
                Interlocked.Increment(ref judged);
                driver.Running.FactsMoved();
                return Task.FromResult<OpinionFact?>(new OpinionFact(new Dictionary<string, OpinionSit>(), null, null));
            },
            (_, _) => Task.FromResult<OpinionView?>(null), () => new OpinionDues(_home).Open(), Machine, (_, _) => { }), _home, ct));

        var reports = await driver.RunUntilIdleAsync().WaitAsync(Poll.Patience);
        Assert.Equal(1, Volatile.Read(ref judged));
        Assert.InRange(reports.Count, 1, 2);
        Assert.NotEmpty(new OpinionDues(_home).Open());
    }

    /// <summary>🔴 A look takes the facts' wake before it reads them, so the watch's wait after it waits for its pace, not looking back to back.</summary>
    [Fact]
    public async Task A_look_takes_the_facts_wake_so_the_wait_after_it_waits()
    {
        await using var bus = Log();
        var driver = Headless(bus);
        driver.Running.FactsMoved();

        await driver.TickAsync().WaitAsync(Poll.Patience);

        Assert.False(driver.Running.FactsAhead);
        using var stop = new CancellationTokenSource();
        var next = driver.Running.NextAsync(TimeSpan.FromMinutes(1), stop.Token);
        await Assert.ThrowsAsync<TimeoutException>(() => next.WaitAsync(TimeSpan.FromMilliseconds(400)));
        await stop.CancelAsync();
        await next.WaitAsync(Poll.Patience);
    }

    /// <summary>The machine's clock, which the log's writer waits on.</summary>
    private static readonly TimeProvider Machine = TimeProvider.System;

    private static readonly DateTimeOffset At = new(2026, 11, 22, 9, 0, 0, TimeSpan.Zero);

    private BusLog Log() =>
        BusLog.Over(new DesktopBusLogStore(_home), new BusLogOptions { Clock = Machine, Interval = TimeSpan.FromMilliseconds(20), Home = _home });

    /// <summary>
    /// A done quest `q1` on `engine` owed a second opinion under `steps`, its tree gone, nothing else open; the rule reads `engine` at
    /// `steps`. The headless driver holds the home's bus log, and no look of its judges the opinion.
    /// </summary>
    private (Daoris.Driver.Driver Driver, BusLog Bus) Owed()
    {
        _ledger.Publish("q1", "engine");
        _ledger.Move("q1", "Done");
        new OpinionDues(_home).Due(new OpinionDue(
            "s1", "q1", "engine", "default", DaorisHome.Entry(_home, HomeEntry.Trees, "default", "engine", "s-1"), OpinionRules.Steps, At));
        var bus = Log();
        return (Headless(bus), bus);
    }

    /// <summary>The headless driver over the stand-in service, holding <paramref name="bus"/>; <paramref name="gates"/> stands in for its gate where given.</summary>
    private Daoris.Driver.Driver Headless(BusLog bus, Func<CancellationToken, Task<OpinionGateRun?>>? gates = null)
    {
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"], Actors = StaffedRoles.On("stub"), Cap = 3, PollSeconds = 60,
            Opinions = new Dictionary<string, OpinionRule>(StringComparer.OrdinalIgnoreCase) { ["engine"] = new(["codex-acp"], [OpinionRules.Steps, OpinionRules.Landing], false) },
        };
        var service = _ledger.Client();
        var runs = new StandInRuns(service, _ledger);
        var driver = new Daoris.Driver.Driver(
            service, config, ProviderRegistry.Built(), _home, new SessionProcesses(),
            harnesses: new HarnessRoster(ProviderRegistry.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Runner = runs.Runner, Stops = runs.Stops, Bus = bus, Gates = gates,
        };
        runs.Keeping = driver.Running;
        return driver;
    }
}
