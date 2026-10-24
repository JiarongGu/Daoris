namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A clock that stands still until the test moves it (TESTCLOCK2, the modules tests' RPC1c clock): a timer it made fires only once
/// <see cref="Advance"/> passes its time, so a plugin's grace is the test's to spend, never the machine's load. <see cref="Timed"/>
/// says a timer was made, so the test moves the clock only once the code is waiting on it.
/// </summary>
internal sealed class MovedClock : TimeProvider
{
    private readonly Lock _gate = new();
    private readonly List<MovedTimer> _timers = [];
    private readonly TaskCompletionSource _timed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DateTimeOffset _now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    /// <summary>Done once the code has made its first timer on this clock.</summary>
    public Task Timed => _timed.Task;

    /// <summary>Whether a timer made on this clock is due and has not fired (AGENTERRORS1a): the code waits on it now.</summary>
    public bool Waiting
    {
        get
        {
            lock (_gate) return _timers.Any(timer => timer.Due is not null);
        }
    }

    /// <summary>
    /// When each timer made on this clock is due, earliest first, those not fired (AGENTERRORS1, the tick tests' determinism): what
    /// the code waits on, so a test moves the clock to exactly the moment a wait it can name ends, never by a guess of its own.
    /// </summary>
    public IReadOnlyList<DateTimeOffset> Pending
    {
        get
        {
            lock (_gate) return [.. _timers.Select(timer => timer.Due).OfType<DateTimeOffset>().Order()];
        }
    }

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate) return _now;
    }

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new MovedTimer(this, callback, state);
        timer.Change(dueTime, period);
        _timed.TrySetResult();
        return timer;
    }

    /// <summary>Moves the clock on by <paramref name="by"/>, firing each timer whose time it reached, once.</summary>
    public void Advance(TimeSpan by)
    {
        List<MovedTimer> due;
        lock (_gate)
        {
            _now += by;
            due = [.. _timers.Where(timer => timer.Due is { } at && at <= _now)];
            foreach (var timer in due) timer.Due = null;
        }

        foreach (var timer in due) timer.Fire();
    }

    private sealed class MovedTimer(MovedClock clock, TimerCallback callback, object? state) : ITimer
    {
        public DateTimeOffset? Due { get; set; }

        public bool Change(TimeSpan dueTime, TimeSpan period)
        {
            lock (clock._gate)
            {
                Due = dueTime == Timeout.InfiniteTimeSpan ? null : clock._now + dueTime;
                if (!clock._timers.Contains(this)) clock._timers.Add(this);
            }

            return true;
        }

        public void Fire() => callback(state);

        public void Dispose()
        {
            lock (clock._gate) clock._timers.Remove(this);
        }

        public ValueTask DisposeAsync()
        {
            Dispose();
            return ValueTask.CompletedTask;
        }
    }
}
