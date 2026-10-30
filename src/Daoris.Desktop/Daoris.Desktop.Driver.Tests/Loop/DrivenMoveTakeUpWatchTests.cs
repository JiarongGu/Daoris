using Microsoft.Data.Sqlite;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FOUNDATION1f-iv part b (D212; the foundation design §3.3, §19.1): the watch's first look takes up a dead loop's driven move through the
/// driver's holder, before it plans the look's starts, so a quest whose carry-on a dead loop's move held is carried on by this one.
/// </summary>
/// <remarks>
/// In-process over the stand-in ledger, as the DEV3 tests drive the watch: the dead loop is a run of the move store whose recorded start no
/// longer matches its process, as the lease declares one dead. Each phase's take-up is <see cref="DrivenCarryOnStoreTests"/>'.
/// </remarks>
public sealed class DrivenMoveTakeUpWatchTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "driven-take-up-watch", Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(Poll.Patience);

    public DrivenMoveTakeUpWatchTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "repos", "engine"));
    }

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();
        SqliteConnection.ClearAllPools();
        GitSeed.Discard(_home);
    }

    private MoveRecord? MoveFrom(string left) => new MoveStore(_home, () => StoreSettled.Yes, run: "run-probe", kind: StoreProcess.Host).Of(left).LastOrDefault();

    /// <summary>The run declared dead as the lease declares one: alive under another start time.</summary>
    private void Die(string run)
    {
        using var store = DesktopStore.Open(DesktopStore.LocationIn(_home), new DesktopStoreOptions { Settled = () => StoreSettled.Yes });
        store.Write("probe", write => write.Execute("UPDATE store_runs SET started = 1 WHERE run = $run", ("$run", run)));
    }

    /// <summary>A dead loop's carry-on move standing for each of <paramref name="lines"/>, in order, its run declared dead.</summary>
    private void LeaveDead(string run, params string[] lines)
    {
        var dying = new MoveStore(_home, () => StoreSettled.Yes, run: run, kind: StoreProcess.Host);
        foreach (var line in lines)
        {
            var decision = CarryOnRecord.Decision(new ContinuedFrom(line, HandOverWhy.Retry, HandOverBy.Daoris), "stub", null)!;
            Assert.NotNull(dying.Decide(decision).Claim);
        }

        Die(run);
    }

    private string Configured()
    {
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        File.WriteAllText(config, """{ "drivable": ["engine"], "adapter": "stub", "pollSeconds": 60, "trees": ["engine"] }""");
        return config;
    }

    private async Task<DriverWatch> LookedAsync(DriverWatch watch, Func<bool> done, Func<string> said)
    {
        var watching = Task.Run(() => watch.RunAsync((_, _) => Task.CompletedTask, onError: null, _closing.Token));
        try
        {
            await Poll.Until(done, said, Poll.Patience);
        }
        finally
        {
            await _closing.CancelAsync();
            try { await watching; } catch (OperationCanceledException) { }
        }

        return watch;
    }

    /// <summary>
    /// SHAREMOVES1 (D212): a <c>--share</c> loop's first look takes up only the lines it runs, so another line's dead move stands for the
    /// lease holder; the dead loop's moves are visited oldest first, so the line it runs closing means the other was passed over.
    /// </summary>
    [Fact]
    public async Task A_shared_loop_s_first_look_takes_up_its_own_line_and_leaves_another_line_s_move_standing()
    {
        LeaveDead("run-gone", "other", "mine");
        var watch = new DriverWatch(
            _ledger.Client(), Configured(), _home, new SessionProcesses(), sync: null,
            harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses))) { Share = true };
        watch.Running.Live["q-mine"] = "mine";

        await LookedAsync(watch, () => MoveFrom("mine") is { Stands: false }, () => $"mine stood at {MoveFrom("mine")?.PhaseWord}");

        Assert.Equal(("closed", MoveCloseWhy.Gone), (MoveFrom("mine")!.PhaseWord, MoveFrom("mine")!.ClosedWhy));
        Assert.Equal("old-ended", MoveFrom("other")!.PhaseWord);
    }

    /// <summary>SHAREMOVES1: the lease holder's first look still takes up every dead run's driven move, whichever line runs it.</summary>
    [Fact]
    public async Task The_lease_holder_s_first_look_takes_up_every_dead_run_s_driven_move()
    {
        LeaveDead("run-gone", "other", "mine");
        var watch = new DriverWatch(
            _ledger.Client(), Configured(), _home, new SessionProcesses(), sync: null,
            harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)));
        watch.Running.Live["q-mine"] = "mine";

        await LookedAsync(watch, () => MoveFrom("mine") is { Stands: false }, () => $"mine stood at {MoveFrom("mine")?.PhaseWord}");

        Assert.Equal(("closed", MoveCloseWhy.Gone), (MoveFrom("other")!.PhaseWord, MoveFrom("other")!.ClosedWhy));
    }

    /// <summary>
    /// 🔴 A loop that died with its carry-on's move standing at <c>old-ended</c> leaves its line held at each look: the first look of the next
    /// loop takes the move up and ends it by its phase, the line staying on its old part, so the carry-on it plans is not held by it.
    /// </summary>
    [Fact]
    public async Task A_dead_loop_s_driven_move_is_taken_up_at_the_watch_s_first_look()
    {
        var dying = new MoveStore(_home, () => StoreSettled.Yes, run: "run-gone", kind: StoreProcess.Host);
        var decision = CarryOnRecord.Decision(new ContinuedFrom("s1", HandOverWhy.Retry, HandOverBy.Daoris), "stub", null)!;
        Assert.NotNull(dying.Decide(decision).Claim);
        Die("run-gone");
        Assert.Equal("old-ended", MoveFrom("s1")!.PhaseWord);

        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        File.WriteAllText(config, """{ "drivable": ["engine"], "adapter": "stub", "pollSeconds": 60, "trees": ["engine"] }""");
        var watch = new DriverWatch(
            _ledger.Client(), config, _home, new SessionProcesses(), sync: null,
            harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)));

        var watching = Task.Run(() => watch.RunAsync((_, _) => Task.CompletedTask, onError: null, _closing.Token));
        try
        {
            await Poll.Until(() => MoveFrom("s1") is { Stands: false }, () => $"the move stood at {MoveFrom("s1")?.PhaseWord}", Poll.Patience);

            // The ledger holds no record `s1`, so the take-up closes the move `gone`; the phases it ends a standing record's move by are
            // `DrivenCarryOnStoreTests`'.
            var taken = MoveFrom("s1")!;
            Assert.Equal(("closed", MoveCloseWhy.Gone), (taken.PhaseWord, taken.ClosedWhy));
        }
        finally
        {
            await _closing.CancelAsync();
            try { await watching; } catch (OperationCanceledException) { }
        }
    }
}
