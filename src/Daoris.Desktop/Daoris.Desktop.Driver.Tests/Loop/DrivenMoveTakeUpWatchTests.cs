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
