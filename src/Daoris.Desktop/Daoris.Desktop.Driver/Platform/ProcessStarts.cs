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
///
/// <para><b>Every start of the desktop's</b> passes it (PROCSTART2): git's, a session's, a harness action's, a plugin's, the
/// host's, Edge's, and the file manager the window kit's launcher opens (<see cref="RunAsync(Action, CancellationToken)"/>).
/// <c>ProcessStartsCensusTests</c> names the few that stay outside, each with why.</para>
/// </remarks>
public sealed class ProcessStarts
{
    /// <summary>How many starts may be under way at once.</summary>
    public const int Bound = 4;

    /// <summary>The one gate every start of the desktop's passes, as the lock is one per process.</summary>
    public static ProcessStarts Shared { get; } = new(Bound, Process.Start);

    // The stand-in a test's flow makes its starts with (Within); null in production.
    private static readonly AsyncLocal<Func<ProcessStartInfo, Process?>?> s_within = new();

    private readonly int _bound;
    private readonly Func<ProcessStartInfo, Process?> _start;
    private readonly SemaphoreSlim _places;

    /// <summary>A gate of <paramref name="bound"/> places over <paramref name="start"/>: the shared one's, or a test's.</summary>
    internal ProcessStarts(int bound, Func<ProcessStartInfo, Process?> start)
    {
        _bound = bound;
        _start = start;
        _places = new SemaphoreSlim(bound, bound);
    }

    /// <summary>How many places the gate has, free or taken.</summary>
    internal int Places => _bound;

    /// <summary>How many places are free now: the tests' window on the gate.</summary>
    internal int Free => _places.CurrentCount;

    /// <summary>
    /// Start <paramref name="info"/> once a place is free, on the thread pool. Cancelling <paramref name="ct"/> while waiting
    /// for a place throws <see cref="OperationCanceledException"/>; once the start is under way it is seen through, since a
    /// process abandoned as it started would run on unowned. The runtime's refusal to start is thrown as it threw it.
    /// </summary>
    public Task<Process?> StartAsync(ProcessStartInfo info, CancellationToken ct)
    {
        var start = s_within.Value ?? _start;
        return RunAsync(() => start(info), ct);
    }

    /// <summary>
    /// Run <paramref name="start"/>, a start the runtime's <see cref="Process"/> is not handed directly (the window kit's
    /// launcher opening the file manager), as <see cref="StartAsync"/> runs one: once a place is free, on the thread pool.
    /// </summary>
    public Task RunAsync(Action start, CancellationToken ct) => RunAsync<object?>(() =>
    {
        start();
        return null;
    }, ct);

    private async Task<T> RunAsync<T>(Func<T> start, CancellationToken ct)
    {
        await _places.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            return await Task.Run(start, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            _places.Release();
        }
    }

    /// <summary>
    /// The test seam (PROCSTART2): until disposed, every start this flow makes through a gate is made by
    /// <paramref name="start"/> instead of the runtime, still on the thread pool and inside its place, so a test sees where a
    /// site's start was made and answers it without a process. Production opens none.
    /// </summary>
    public static IDisposable Within(Func<ProcessStartInfo, Process?> start)
    {
        var before = s_within.Value;
        s_within.Value = start;
        return new Ending(before);
    }

    private sealed class Ending(Func<ProcessStartInfo, Process?>? before) : IDisposable
    {
        public void Dispose() => s_within.Value = before;
    }
}
