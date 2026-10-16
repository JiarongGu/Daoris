namespace Daoris.Desktop;

/// <summary>
/// One ask under way per read and its payload (SWEEPFLIGHT1, UIFREEZE1): a caller asking while the same ask runs joins it
/// and is answered what it answers, rather than starting another.
/// </summary>
/// <remarks>
/// <para><b>Why.</b> The frozen window's stack held 22 clean-up lists at once, each a whole git fan-out on the machine's one
/// process-start lock: every window, and every change the page heard, asked the list again while the last was still
/// running, and each new ask made the ones before it slower.</para>
///
/// <para><b>Kept for no one past its end.</b> A flight is dropped from the table before its callers are answered, so an ask
/// made after an answer always starts afresh: this shares the work under way, it never caches an answer.</para>
///
/// <para><b>Stopped only when no one waits.</b> The ask runs under a token of its own. A caller that stops waiting leaves
/// it; the last to leave stops it, and the next ask starts afresh.</para>
///
/// <para><b>Overtaken by a change.</b> <see cref="Forget"/> is called as a press answers (<see cref="DriverModule"/>): the
/// page asks again because of the change, and an ask begun before it would answer as if it had not happened. The flights
/// under way still answer the callers that joined them.</para>
/// </remarks>
public sealed class RouteFlights
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Flight> _under = new(StringComparer.Ordinal);

    /// <summary>
    /// Join the flight under way for <paramref name="key"/>, or start one with <paramref name="ask"/>, which runs on this
    /// caller's thread until its first wait, as a route always has. Cancelling <paramref name="ct"/> stops this caller
    /// waiting, and the flight with it only when no other caller waits.
    /// </summary>
    public async Task<object?> JoinAsync(string key, Func<CancellationToken, Task<object?>> ask, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        Flight flight;
        bool first;
        lock (_gate)
        {
            first = !_under.TryGetValue(key, out flight!);
            if (first) _under[key] = flight = new Flight();
            flight.Waiting++;
        }

        if (first) _ = FlyAsync(key, flight, ask);
        try
        {
            return await flight.Done.Task.WaitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Left here, not in a callback on the token: the wait's own callback runs first and may end this method, and with
            // it a registration whose callback had not run yet.
            Leave(key, flight);
            throw;
        }
    }

    /// <summary>Join none of the flights under way: the next ask of each starts afresh. They still answer their callers.</summary>
    public void Forget()
    {
        lock (_gate) _under.Clear();
    }

    private async Task FlyAsync(string key, Flight flight, Func<CancellationToken, Task<object?>> ask)
    {
        try
        {
            var answer = await ask(flight.Stop.Token).ConfigureAwait(false);
            Land(key, flight);
            flight.Done.TrySetResult(answer);
        }
        catch (OperationCanceledException) when (flight.Stop.IsCancellationRequested)
        {
            Land(key, flight);
            flight.Done.TrySetCanceled(flight.Stop.Token);
        }
        catch (Exception error)
        {
            Land(key, flight);
            flight.Done.TrySetException(error);
        }
    }

    // Out of the table before anyone is answered, so no ask made after an answer joins it.
    private void Land(string key, Flight flight)
    {
        lock (_gate)
        {
            if (_under.TryGetValue(key, out var under) && ReferenceEquals(under, flight)) _under.Remove(key);
        }
    }

    private void Leave(string key, Flight flight)
    {
        lock (_gate)
        {
            if (--flight.Waiting > 0) return;
            if (_under.TryGetValue(key, out var under) && ReferenceEquals(under, flight)) _under.Remove(key);
        }

        // Not on the canceller's thread: the ask's own callbacks run off it.
        _ = flight.Stop.CancelAsync();
    }

    /// <summary>
    /// One ask under way: its answer, its own stop, and how many callers wait for it. The stop is never disposed, since a
    /// caller's cancellation may reach it after the answer.
    /// </summary>
    private sealed class Flight
    {
        public readonly TaskCompletionSource<object?> Done = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public readonly CancellationTokenSource Stop = new();

        public int Waiting;
    }
}
