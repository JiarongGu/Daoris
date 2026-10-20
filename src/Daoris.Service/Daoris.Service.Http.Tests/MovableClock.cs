namespace Daoris.Service.Http.Tests;

/// <summary>
/// A clock a test moves, so two minutes pass without waiting them (PERSONDOOR1b). Its timestamp moves with it, and a timer it made
/// fires only once <see cref="Move"/> passes its time (TESTCLOCK3), so a host handed it holds a change stream's wait until the test
/// moves it: a wait it never moves is answered only by a move, a cursor or a slot.
/// </summary>
public sealed class MovableClock : TimeProvider
{
    private readonly Lock _gate = new();
    private readonly List<MovedTimer> _timers = [];
    private DateTimeOffset _now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    public DateTimeOffset Now
    {
        get
        {
            lock (_gate) return _now;
        }
    }

    /// <summary>Moves the clock on by <paramref name="by"/>, firing each timer whose time it reached, once.</summary>
    public void Move(TimeSpan by)
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

    public override DateTimeOffset GetUtcNow() => Now;

    public override long TimestampFrequency => TimeSpan.TicksPerSecond;

    public override long GetTimestamp() => Now.UtcTicks;

    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new MovedTimer(this, callback, state);
        timer.Change(dueTime, period);
        return timer;
    }

    private sealed class MovedTimer(MovableClock clock, TimerCallback callback, object? state) : ITimer
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
