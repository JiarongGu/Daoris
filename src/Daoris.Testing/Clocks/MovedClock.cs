namespace Daoris.Testing;

/// <summary>
/// A clock that stands still until the test moves it: its now moves only by <see cref="Advance"/>, and a timer it made fires only once
/// <see cref="Advance"/> passes its time, so a wait read on it is the test's to spend, never the machine's load. <see cref="Timed"/>
/// says a timer exists, so the test moves the clock only once the code is waiting on it.
/// </summary>
public sealed class MovedClock : TimeProvider
{
    private readonly Lock _gate = new();
    private readonly List<MovedTimer> _timers = [];
    private readonly TaskCompletionSource _timed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private DateTimeOffset _now = new(2026, 10, 20, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Whether the timestamps, and so the elapsed times read from them, move with the clock. Off, they are the machine's, which a
    /// test that measures a real span relies on.
    /// </summary>
    public bool VirtualTimestamp { get; init; }

    /// <summary>Done once the code has made its first timer on this clock.</summary>
    public Task Timed => _timed.Task;

    /// <summary>Whether a timer made on this clock is due and has not fired: the code waits on it.</summary>
    public bool Waiting
    {
        get
        {
            lock (_gate) return _timers.Any(timer => timer.Due is not null);
        }
    }

    /// <summary>
    /// When each timer made on this clock is due, earliest first, those not fired: what the code waits on, so a test moves the clock
    /// to exactly the moment a wait it can name ends, never by a guess of its own.
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

    public override long TimestampFrequency => VirtualTimestamp ? TimeSpan.TicksPerSecond : base.TimestampFrequency;

    public override long GetTimestamp() => VirtualTimestamp ? GetUtcNow().UtcTicks : base.GetTimestamp();

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
