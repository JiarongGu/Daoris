namespace Daoris.Ports.Clocks;

/// <summary>
/// The time a session's quiet is counted on: one that does not advance while the machine sleeps, so a wake reads no session as quiet,
/// and a slow look or a long poll loses nothing, since the count is time, not looks. The reading is a delegate the root takes from
/// the clock adapter; a test hands one it moves.
/// </summary>
/// <remarks>
/// Where the clock keeps running through a sleep (<see cref="StopsInSleep"/> false), the watcher does not count a gap between looks
/// longer than three paces and at least two minutes.
/// </remarks>
public sealed class QuietClock(Func<TimeSpan> now, bool stopsInSleep)
{
    /// <summary>The time now, on this clock: only differences between two readings mean anything.</summary>
    public TimeSpan Now => now();

    /// <summary>Whether this clock stands still while the machine sleeps; where it does not, the watcher's gap rule counts for it.</summary>
    public bool StopsInSleep { get; } = stopsInSleep;
}
