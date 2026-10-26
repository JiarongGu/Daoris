using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Daoris.Driver;

/// <summary>
/// The time a session's quiet is counted on (SESSWATCH1a, D200, the session-watch design §1.5): one that does not advance while the
/// machine sleeps, so a wake reads no session as quiet, and a slow look or a long <c>PollSeconds</c> loses nothing, since the count is
/// time, not looks.
/// </summary>
/// <remarks>
/// On Windows the machine's unbiased interrupt time (<c>QueryUnbiasedInterruptTime</c>), which stands still in sleep and hibernation.
/// Elsewhere a monotonic clock, which may run on through a sleep, so the watcher does not count a gap between looks longer than three
/// paces and at least two minutes (<see cref="StopsInSleep"/> false). A test hands one it moves.
/// </remarks>
public sealed class QuietClock(Func<TimeSpan> now, bool stopsInSleep)
{
    /// <summary>The machine's: unbiased interrupt time on Windows, a monotonic clock elsewhere.</summary>
    public static QuietClock System { get; } = OperatingSystem.IsWindows()
        ? new QuietClock(Unbiased, stopsInSleep: true)
        : new QuietClock(() => Stopwatch.GetElapsedTime(Started), stopsInSleep: false);

    private static readonly long Started = Stopwatch.GetTimestamp();

    /// <summary>The time now, on this clock: only differences between two readings mean anything.</summary>
    public TimeSpan Now => now();

    /// <summary>Whether this clock stands still while the machine sleeps; where it does not, the watcher's gap rule counts for it.</summary>
    public bool StopsInSleep { get; } = stopsInSleep;

    private static TimeSpan Unbiased() =>
        QueryUnbiasedInterruptTime(out var ticks) ? TimeSpan.FromTicks((long)ticks) : Stopwatch.GetElapsedTime(Started);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryUnbiasedInterruptTime(out ulong unbiasedTime);
}
