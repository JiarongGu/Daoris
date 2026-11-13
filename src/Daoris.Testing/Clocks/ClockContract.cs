using Daoris.Ports.Clocks;
using Xunit;

namespace Daoris.Testing;

/// <summary>
/// The cases every clock the application is handed must pass, written once against the platform's <see cref="TimeProvider"/> and the
/// quiet clock. A subclass hands the backend and how time passes on it: the fake moves by hand and the machine's lets the time go by,
/// so the same case holds both to one behaviour. A case never waits itself.
/// </summary>
public abstract class ClockContract
{
    /// <summary>The clock under test, with timestamps that follow its time.</summary>
    protected abstract TimeProvider Backend { get; }

    /// <summary>The quiet clock under test, on the same time as <see cref="Backend"/> where the backend moves by hand.</summary>
    protected abstract QuietClock Quiet { get; }

    /// <summary>Lets <paramref name="by"/> pass on the backend: the fake advances, the machine's waits it out.</summary>
    protected abstract Task PassAsync(TimeSpan by);

    private static readonly TimeSpan Step = TimeSpan.FromMilliseconds(400);

    [Fact]
    public async Task The_time_moves_on_by_what_passes()
    {
        var before = Backend.GetUtcNow();

        await PassAsync(Step);

        Assert.True(Backend.GetUtcNow() - before >= Step / 2);
    }

    [Fact]
    public async Task A_timestamp_measures_what_passed()
    {
        var start = Backend.GetTimestamp();

        await PassAsync(Step);

        Assert.True(Backend.GetElapsedTime(start) >= Step / 2);
    }

    [Fact]
    public async Task A_timer_fires_once_its_time_has_passed()
    {
        var fired = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var timer = Backend.CreateTimer(_ => fired.TrySetResult(), null, TimeSpan.FromMilliseconds(100), Timeout.InfiniteTimeSpan);

        await PassAsync(Step);

        await fired.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task A_timer_disposed_before_its_time_never_fires()
    {
        var fired = 0;
        var timer = Backend.CreateTimer(_ => Interlocked.Increment(ref fired), null, TimeSpan.FromMilliseconds(100), Timeout.InfiniteTimeSpan);
        timer.Dispose();

        await PassAsync(Step);

        Assert.Equal(0, Volatile.Read(ref fired));
    }

    [Fact]
    public async Task A_delay_on_the_clock_ends_once_its_time_has_passed()
    {
        var delay = Task.Delay(TimeSpan.FromMilliseconds(100), Backend);

        await PassAsync(Step);

        await delay.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(delay.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task The_quiet_clock_never_runs_back_and_moves_on_by_what_passes()
    {
        var before = Quiet.Now;

        await PassAsync(Step);

        Assert.True(Quiet.Now - before >= Step / 2);
    }
}
