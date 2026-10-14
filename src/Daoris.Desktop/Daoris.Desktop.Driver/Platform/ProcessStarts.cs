using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// Process starts, a few at a time and never on the caller's thread (UIFREEZE1).
/// </summary>
/// <remarks>
/// <para><b>Why.</b> .NET starts every process of this one under a single lock, held across <c>CreateProcess</c>, and on a
/// machine whose antivirus hooks process creation that call is slow, slower under load. A start was made synchronously on
/// whichever thread asked, so a sweep's git fan-out parked a thread per git on the lock (51 of 76 in the frozen window's
/// stack), the window's own thread among them.</para>
///
/// <para><b>The gate.</b> A start first takes one of <see cref="Bound"/> places, waiting asynchronously, so the rest of a
/// fan-out queues as tasks rather than as threads; then it runs on the thread pool, so the caller's thread is free from the
/// first wait. Only the start is gated, never the run: a child that runs for minutes holds no place, and nothing waits on
/// it but its own caller.</para>
///
/// <para><b>Four.</b> The lock lets one start through at a time, so more places only park more threads on it; four keep the
/// next starts ready the moment the lock frees, and a start hung in an antivirus scan holds one place, not every start.</para>
/// </remarks>
internal sealed class ProcessStarts(int bound, Func<ProcessStartInfo, Process?> start)
{
    /// <summary>How many starts may be under way at once.</summary>
    internal const int Bound = 4;

    /// <summary>The one gate every start through <see cref="ChildProcess"/> passes, as the lock is one per process.</summary>
    internal static ProcessStarts Shared { get; } = new(Bound, Process.Start);

    private readonly SemaphoreSlim _places = new(bound, bound);

    /// <summary>How many places the gate has, free or taken.</summary>
    internal int Places => bound;

    /// <summary>How many places are free now: the tests' window on the gate.</summary>
    internal int Free => _places.CurrentCount;

    /// <summary>
    /// Start <paramref name="info"/> once a place is free, on the thread pool. Cancelling <paramref name="ct"/> while waiting
    /// for a place throws <see cref="OperationCanceledException"/>; once the start is under way it is seen through, since a
    /// process abandoned as it started would run on unowned. The runtime's refusal to start is thrown as it threw it.
    /// </summary>
    internal async Task<Process?> StartAsync(ProcessStartInfo info, CancellationToken ct)
    {
        await _places.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => start(info), CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _places.Release();
        }
    }
}
