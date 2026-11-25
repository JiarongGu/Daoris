using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// What an update's drain waits on is the turns that run, not the sessions that live: a driven session whose turn has ended and
/// which waits on its own background work runs no turn, so a session whose next turn always follows its last never keeps a staged
/// build from installing.
/// </summary>
public sealed class UpdateBetweenTurnsTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-between-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();

    private readonly SessionEvents _events;

    public UpdateBetweenTurnsTests()
    {
        Directory.CreateDirectory(_home);
        _events = new SessionEvents(DaorisHome.Entry(_home, HomeEntry.Sessions));
    }

    public void Dispose()
    {
        foreach (var held in _held) held.Dispose();
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private readonly List<IDisposable> _held = [];

    private DriverWatch Watch()
    {
        var config = DaorisHome.Entry(_home, HomeEntry.DriverConfig);
        File.WriteAllText(config, """{ "drivable": [], "holds": [], "adapter": "stub", "cap": 3, "pollSeconds": 1 }""");
        return new DriverWatch(
            _ledger.Client(), config, _home, new SessionProcesses(), sync: null,
            harnesses: new HarnessRoster(ProviderRegistry.Built(), DaorisHome.Entry(_home, HomeEntry.Harnesses)),
            events: _events);
    }

    /// <summary>Begin a run that goes on until <paramref name="end"/>, held and watched as the protocol door does, between turns where <paramref name="between"/> says so.</summary>
    private async Task<SessionWatch.WatchHandle> RunAsync(DriverWatch watch, string session, Func<bool> between, TaskCompletionSource end)
    {
        await watch.Running.StartAsync(open =>
        {
            open();
            return end.Task.ContinueWith(_ => new StartRun("ended", Opened: true), TaskScheduler.Default);
        });
        _held.Add(watch.Running.GoesLive("q-" + session, session));
        var handle = SessionWatch.Of(_events).Watch(session, new WatchedRun(SessionWatch.Quest, "q-" + session, "stub"));
        SessionWatch.Of(_events).Between(session, between);
        return handle;
    }

    [Fact]
    public async Task A_session_that_waits_on_its_own_work_runs_no_turn_and_one_that_works_does()
    {
        var watch = Watch();
        var end = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var checking = true;
        using var first = await RunAsync(watch, "s1", () => checking, end);
        using var second = await RunAsync(watch, "s2", () => false, end);

        Assert.Equal(2, watch.Running.Running);
        Assert.Equal(1, watch.RunsInTurn);

        checking = false;
        Assert.Equal(2, watch.RunsInTurn);

        end.TrySetResult();
    }

    /// <summary>
    /// A session that always has a next turn is between its turns at each turn's end, and the work an update reads is idle then.
    /// </summary>
    [Fact]
    public async Task A_session_that_always_has_a_next_turn_is_idle_at_each_turn_s_end_and_not_while_the_turn_runs()
    {
        var watch = Watch();
        var end = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var between = false;
        using var run = await RunAsync(watch, "s1", () => between, end);

        Assert.Equal(1, watch.RunsInTurn);
        Assert.False(InstallUpdate.Idle(watch.RunsInTurn, turns: 0, steps: 0));

        between = true;
        Assert.Equal(0, watch.RunsInTurn);
        Assert.True(InstallUpdate.Idle(watch.RunsInTurn, turns: 0, steps: 0));
        // Still a run of the loop's: the close that follows stops it and the next start carries it on.
        Assert.Equal(1, watch.Running.Running);

        between = false;
        Assert.Equal(1, watch.RunsInTurn);

        end.TrySetResult();
    }

    /// <summary>A watched session this loop does not hold never lowers the count of a session that is in a turn.</summary>
    [Fact]
    public async Task A_watched_session_this_loop_does_not_hold_never_lowers_the_count_of_one_in_a_turn()
    {
        var watch = Watch();
        var end = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var mine = await RunAsync(watch, "s1", () => false, end);
        using var other = SessionWatch.Of(_events).Watch("s9", new WatchedRun(SessionWatch.Intake, null, "stub"));
        SessionWatch.Of(_events).Between("s9", () => true);

        Assert.Equal(1, watch.Running.Running);
        Assert.Equal(1, watch.RunsInTurn);

        end.TrySetResult();
    }
}
