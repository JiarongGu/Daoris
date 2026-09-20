using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// Live session output, batched for whoever is showing it (D49 §2): subscribes to every line and
/// hands them out a window at a time.
/// </summary>
/// <remarks>
/// <para><b>Batched on a short timer, deliberately.</b> One message per line is the obvious shape and
/// the wrong one: a session can emit thousands in a second — a build log, a test run — and a bridge
/// asked to carry one message each turns watching a session into watching a frozen window. A window's
/// worth of lines in one message is indistinguishable to a reader and bounded work for the shell.</para>
///
/// <para><b>It decides nothing about what a line means.</b> The buffer holds the sequence numbers, so
/// a reader that misses a batch can ask the driver for the gap; this only moves lines, grouped by
/// session because a surface shows one console at a time.</para>
///
/// <para>It lives in the library rather than the shell so the batching is testable without an event
/// bus — the same split <see cref="DriverWatch"/> makes: the mechanics here, what a batch BECOMES
/// with whoever has a door to put it through.</para>
/// </remarks>
public sealed class ConsoleRelay : IDisposable
{
    /// <summary>How long lines accumulate before a batch goes out. Short enough to read as live.</summary>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMilliseconds(120);

    private readonly SessionOutput _output;
    private readonly Func<string, IReadOnlyList<ConsoleLine>, Task> _emit;
    private readonly ConcurrentQueue<(string Session, ConsoleLine Line)> _pending = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _pump;

    /// <param name="emit">What a batch becomes — an IPC event in the shell, a list in a test.</param>
    /// <param name="window">How long to accumulate. The default reads as live to a person.</param>
    public ConsoleRelay(
        SessionOutput output, Func<string, IReadOnlyList<ConsoleLine>, Task> emit, TimeSpan? window = null)
    {
        _output = output;
        _emit = emit;
        _output.Lined += Enqueue;
        _pump = Task.Run(() => RunAsync(window ?? DefaultWindow, _stopping.Token));
    }

    private void Enqueue(string session, ConsoleLine line) => _pending.Enqueue((session, line));

    private async Task RunAsync(TimeSpan window, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(window, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await FlushAsync().ConfigureAwait(false);
        }

        // Whatever arrived during the last window still goes: a session's final lines are the ones
        // most worth seeing, and a shell closing is not a shell crashing.
        await FlushAsync().ConfigureAwait(false);
    }

    /// <summary>Hand out everything queued, grouped by session. Internal so a test need not wait.</summary>
    internal async Task FlushAsync()
    {
        if (_pending.IsEmpty) return;

        var batch = new Dictionary<string, List<ConsoleLine>>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        while (_pending.TryDequeue(out var entry))
        {
            if (!batch.TryGetValue(entry.Session, out var lines))
            {
                lines = [];
                batch[entry.Session] = lines;
                order.Add(entry.Session);
            }

            lines.Add(entry.Line);
        }

        foreach (var session in order)
        {
            try
            {
                await _emit(session, batch[session]).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // A reader that is not listening is not a failure, and a driver that stopped capturing
                // because a window closed would be. These lines are still in the buffer.
            }
        }
    }

    public void Dispose()
    {
        _output.Lined -= Enqueue;
        _stopping.Cancel();
        try
        {
            _pump.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Cancellation surfacing as it should.
        }

        _stopping.Dispose();
    }
}
