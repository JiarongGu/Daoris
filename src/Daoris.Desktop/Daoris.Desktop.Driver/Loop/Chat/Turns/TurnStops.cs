namespace Daoris.Driver;

/// <summary>
/// The person's stops of a conversation's turn (<see cref="ChatTurns"/>): what waits withdrawn or kept, the turn in
/// flight marked stopped, and the stop decided for that one turn and sent once it is on the wire, or dropped where it ended. Its state is
/// <see cref="TurnState"/>'s; the door's interrupt is called with the gate released.
/// </summary>
/// <param name="interrupt">The door's own way of stopping the turn in flight.</param>
internal sealed class TurnStops(TurnState state, Func<Task> interrupt)
{
    /// <summary>
    /// Stop the turn in flight and keep what waits (<c>CANCEL_TURN</c> with <c>keep</c>): nothing is withdrawn, and the
    /// words waiting are the next turn once it ends, where <see cref="StopAsync"/> hands them back. A stop already
    /// asked is not asked again. <see cref="TurnStop.Nothing"/> when no turn is in flight.
    /// </summary>
    public async Task<TurnStop> StopKeepingAsync()
    {
        Stop? stop;
        lock (state.Gate)
        {
            if (!state.InFlight) return TurnStop.Nothing;
            stop = StopKeeping();
            state.Publish();
        }

        if (stop is not null) await SendStopAsync(stop).ConfigureAwait(false);
        return new TurnStop(Cancelled: true, Withdrawn: []);
    }

    /// <summary>
    /// Mark the turn in flight stopped, keeping what waits: the stop to send now, or null. Taken but not yet on the wire, the
    /// interrupt goes the moment it is (see <c>take</c>); a stop already asked is not asked again. Called under the lock.
    /// </summary>
    private Stop? StopKeeping()
    {
        var asked = state.Stopped || state.StopPending;
        state.Stopped = true;
        if (!state.OnWire) state.StopPending = true;
        return state.OnWire && !asked ? Decide() : null;
    }

    /// <summary>A stop decided for one turn, and what tells the next turn it is on the wire or dropped.</summary>
    private sealed record Stop(int Turn, TaskCompletionSource Sent);

    /// <summary>Decide a stop for the turn in flight, which the next turn waits for (<see cref="PumpAsync"/>). Called under the lock.</summary>
    private Stop Decide()
    {
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.Stopping = state.Stopping.IsCompleted ? sent.Task : Task.WhenAll(state.Stopping, sent.Task);
        return new Stop(state.Turn, sent);
    }

    /// <summary>
    /// Send a stop to the turn it is decided for, or drop it where that turn has ended; either way the next turn may
    /// then go, never before. Called outside the lock: the door's interrupt is its own.
    /// </summary>
    private async Task SendStopAsync(Stop stop)
    {
        try
        {
            bool current;
            lock (state.Gate) current = state.InFlight && state.Turn == stop.Turn;
            if (current) await interrupt().ConfigureAwait(false);
        }
        finally
        {
            stop.Sent.TrySetResult();
        }
    }

    /// <summary>
    /// Withdraw what is waiting, then stop the turn in flight. <see cref="TurnStop.Nothing"/> when there
    /// is neither.
    /// </summary>
    public async Task<TurnStop> StopAsync()
    {
        List<ChatMessage> withdrawn = [];
        bool cancelled;
        Stop? stop;
        lock (state.Gate)
        {
            if (state.Holding is not null)
            {
                withdrawn.Add(state.Holding);
                state.Holding = null;
            }

            withdrawn.AddRange(state.Waiting);
            state.Waiting.Clear();
            // The person's words handed back to them, so off the record too; a helper's answer stays there (below).
            state.TakeOff([.. withdrawn.Where(message => !message.OnRecord)]);
            cancelled = state.InFlight;
            if (state.InFlight) state.Stopped = true;
            // Taken but not yet on the wire: the interrupt goes the moment it is (see `take`). A word said after the stop waits
            // for it, so it too is sent for this turn alone.
            stop = state.InFlight && state.OnWire ? Decide() : null;
            if (state.InFlight && !state.OnWire) state.StopPending = true;
            state.Publish();
        }

        if (stop is not null)
        {
            try
            {
                await SendStopAsync(stop).ConfigureAwait(false);
            }
            catch (Exception error) when (error is IOException or ObjectDisposedException or InvalidOperationException)
            {
                // The agent is gone and its turn with it; the words already withdrawn still go back to the person.
            }
        }

        // The words a conversation goes on with are not handed back: the page never listed them, and they stay on
        // the record, which takes them off only once they go. Nor is a helper's answer: it is no word of the
        // person's to put back in the composer, and it stays on the record for a later turn. Nor is Daoris's own word:
        // the stop takes it with the turn.
        withdrawn.RemoveAll(message => !message.Theirs);
        return !cancelled && withdrawn.Count == 0 ? TurnStop.Nothing : new TurnStop(cancelled, withdrawn);
    }

    /// <summary>The turn is on the wire — and a stop that arrives while it is on its way goes now.</summary>
    internal void Sent()
    {
        Stop? late;
        lock (state.Gate)
        {
            state.OnWire = true;
            late = state.StopPending ? Decide() : null;
            state.StopPending = false;
        }

        if (late is not null) _ = SendStopAsync(late);
    }
}
