using System.Diagnostics;
using System.Runtime.InteropServices;
using Daoris.Ports.Clocks;

namespace Daoris.Adapters.Clock;

/// <summary>
/// What a root hands the application for time: the platform's own provider, and the reading quiet is counted on. The port is the
/// platform's <see cref="TimeProvider"/>; the quiet clock crosses as a value of <c>Daoris.Ports</c>.
/// </summary>
public static class MachineClock
{
    private static readonly long Started = Stopwatch.GetTimestamp();

    /// <summary>The machine's clock and timers.</summary>
    public static TimeProvider Time => TimeProvider.System;

    /// <summary>
    /// The machine's quiet clock: on Windows its unbiased interrupt time (<c>QueryUnbiasedInterruptTime</c>), which stands still in
    /// sleep and hibernation; elsewhere a monotonic clock, which may run on through a sleep.
    /// </summary>
    public static QuietClock Quiet { get; } = OperatingSystem.IsWindows()
        ? new QuietClock(Unbiased, stopsInSleep: true)
        : new QuietClock(() => Stopwatch.GetElapsedTime(Started), stopsInSleep: false);

    private static TimeSpan Unbiased() =>
        QueryUnbiasedInterruptTime(out var ticks) ? TimeSpan.FromTicks((long)ticks) : Stopwatch.GetElapsedTime(Started);

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryUnbiasedInterruptTime(out ulong unbiasedTime);
}
