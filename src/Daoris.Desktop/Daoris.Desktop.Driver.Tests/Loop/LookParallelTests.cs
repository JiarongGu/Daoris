using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// PARALLEL1b (D181 point 3): the look's independent work leaves its serial path. The remote sync runs on its own task and clock, so
/// a slow remote holds no local start.
/// </summary>
/// <remarks>
/// Over the real client and planner with the service's doors and each start's run standing in, in-process, as the DEV3 tests drive
/// the watch. The watch runs on the pool, so a part of it that blocks never blocks the test.
/// </remarks>
public sealed class LookParallelTests : IDisposable
{
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(20);

    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-parallel-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly CancellationTokenSource _closing = new(TimeSpan.FromSeconds(90));

    public LookParallelTests()
    {
        Directory.CreateDirectory(_home);
        _ledger.Register("engine", Path.Combine(_home, "engine")).Register("tools", Path.Combine(_home, "tools"));
    }

    public void Dispose()
    {
        _closing.Cancel();
        _closing.Dispose();
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>🔴 A remote that does not answer holds the sync, never the local plan: a quest published meanwhile starts.</summary>
    [Fact]
    public async Task A_slow_remote_sync_holds_no_local_start()
    {
        using var remote = new ManualResetEventSlim(false);
        var passes = -1;
        // A pass reads the remotes map first, here a read that waits for the test, as a remote that does not answer holds a pass.
        // The set's own read as it is made goes through.
        using var sync = RemoteSyncSet.Watching("http://127.0.0.1:9", null, () =>
        {
            if (Interlocked.Increment(ref passes) > 0) remote.Wait(_closing.Token);
            return new Dictionary<string, RemoteTarget>();
        });
        var (watch, runs) = Watch(sync);

        var watching = Task.Run(() => watch.RunAsync((_, _) => Task.CompletedTask, onError: null, _closing.Token));
        try
        {
            await Poll.Until(() => Volatile.Read(ref passes) >= 1, () => "the whole look's sync never began", Bound);

            _ledger.Publish("q1", "engine");
            watch.Nudge(ChangeTopics.Ledger, "quests");
            await Poll.Until(() => runs.Started.Count == 1, () => "q1 waited for the remote", Bound);

            Assert.Equal(["q1"], runs.Started);
        }
        finally
        {
            remote.Set();
            await _closing.CancelAsync();
            await Settled(watching);
        }
    }

    private (DriverWatch Watch, StandInRuns Runs) Watch(RemoteSyncSet? sync = null, int cap = 3, int pollSeconds = 60)
    {
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        File.WriteAllText(config, $$"""
            { "drivable": ["engine", "tools"], "adapter": "stub", "cap": {{cap}}, "pollSeconds": {{pollSeconds}}, "trees": ["engine", "tools"] }
            """);
        var service = _ledger.Client();
        var runs = new StandInRuns(service, _ledger);
        var watch = new DriverWatch(
            service, config, _home, new SessionProcesses(), sync,
            harnesses: new HarnessRoster(AdapterSet.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)))
        {
            Runner = runs.Runner,
            Stops = runs.Stops,
        };
        runs.Keeping = watch.Running;
        return (watch, runs);
    }

    private static async Task Settled(Task running)
    {
        try { await running.WaitAsync(Bound); }
        catch (OperationCanceledException) { }
        catch (TimeoutException) { }
    }
}
