using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// What a session produces, batched per session on a short timer for whoever is showing it — the
/// mechanics <see cref="ConsoleRelay"/> (lines) and <see cref="EventRelay"/> (events, D76) share.
/// </summary>
/// <remarks>
/// <para><b>Batched, deliberately.</b> One message per item is the obvious shape and the wrong one: a
/// session can emit thousands in a second, and a bridge asked to carry one message each turns watching
/// into watching a frozen window. A window's worth in one message reads the same to a person and is
/// bounded work for the shell.</para>
///
/// <para><b>It decides nothing about what an item means</b>, and holds nothing a reader could not ask
/// for again: the sequence numbers are the source's, so a reader that misses a batch asks the source
/// for the gap.</para>
/// </remarks>
/// <typeparam name="T">What is relayed — a console line, or a session event.</typeparam>
public sealed class BatchRelay<T> : IDisposable
{
    private readonly Action<Action<string, T>> _unsubscribe;
    private readonly Func<string, IReadOnlyList<T>, Task> _emit;
    private readonly ConcurrentQueue<(string Session, T Item)> _pending = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly Task _pump;

    /// <param name="subscribe">Attach the handler to the source's event.</param>
    /// <param name="unsubscribe">Detach it, on dispose.</param>
    /// <param name="emit">What a batch becomes — an IPC event in the shell, a list in a test.</param>
    /// <param name="window">How long to accumulate. Short enough to read as live.</param>
    public BatchRelay(
        Action<Action<string, T>> subscribe, Action<Action<string, T>> unsubscribe,
        Func<string, IReadOnlyList<T>, Task> emit, TimeSpan window)
    {
        _unsubscribe = unsubscribe;
        _emit = emit;
        subscribe(Enqueue);
        _pump = Task.Run(() => RunAsync(window, _stopping.Token));
    }

    private void Enqueue(string session, T item) => _pending.Enqueue((session, item));

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

        // Whatever arrived during the last window still goes: a session's final words are the ones
        // most worth seeing, and a shell closing is not a shell crashing.
        await FlushAsync().ConfigureAwait(false);
    }

    /// <summary>Hand out everything queued, grouped by session, in arrival order.</summary>
    public async Task FlushAsync()
    {
        if (_pending.IsEmpty) return;

        var batch = new Dictionary<string, List<T>>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        while (_pending.TryDequeue(out var entry))
        {
            if (!batch.TryGetValue(entry.Session, out var items))
            {
                items = [];
                batch[entry.Session] = items;
                order.Add(entry.Session);
            }

            items.Add(entry.Item);
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
                // because a window closed would be. The source still holds what was said.
            }
        }
    }

    public void Dispose()
    {
        _unsubscribe(Enqueue);
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

/// <summary>
/// A session's events, batched live for whoever is showing its conversation (D76, CONV1) — the
/// console's relay, for the record's events.
/// </summary>
public sealed class EventRelay(
    SessionEvents events, Func<string, IReadOnlyList<SessionEvent>, Task> emit, TimeSpan? window = null)
    : IDisposable
{
    private readonly BatchRelay<SessionEvent> _relay = new(
        handler => events.Evented += handler, handler => events.Evented -= handler,
        emit, window ?? ConsoleRelay.DefaultWindow);

    /// <summary>Hand out everything queued. Internal so a test need not wait.</summary>
    internal Task FlushAsync() => _relay.FlushAsync();

    public void Dispose() => _relay.Dispose();
}
