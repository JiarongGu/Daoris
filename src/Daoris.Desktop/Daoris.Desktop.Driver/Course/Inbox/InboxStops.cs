namespace Daoris.Driver;

/// <summary>
/// The person's keeping stop of a driven session's turn (<see cref="DrivenInbox"/>, COWORK1a, STEER2a): decided once per turn under
/// <see cref="InboxState"/>'s gate, sent to the turn it was decided for with the gate released, or dropped where the run took another.
/// </summary>
internal sealed class InboxStops(InboxState state)
{
    /// <summary>
    /// The keeping stop (COWORK1a, the design §3.2; <c>CANCEL_TURN</c>, <c>sessions stop --turn</c>): stop the running turn so what
    /// is held goes next, the person's own press. <see cref="TurnStop.Nothing"/> with nothing held, before the session is open,
    /// where nothing can stop a turn and keep the session (the native door's run), or where words go at once (STEER1): a stop
    /// while one is on its way would answer it cancelled while the agent may still act on it (docs/2026-10-03-steer-evidence.md §3).
    /// </summary>
    /// <remarks>A turn is asked to stop once, however often the person presses (STEER2a): the answer still says it was.</remarks>
    public async Task<TurnStop> SendNowAsync()
    {
        Stop? stop;
        lock (state.Gate)
        {
            // COWORK1d: claims that only ride are no reason to stop a turn.
            if (state.Closed || !state.Turns() || state.Interrupt is null || state.Reach == DrivenReach.NextStep) return TurnStop.Nothing;
            stop = StopThisTurn();
        }

        if (stop is not null) await SendStopAsync(stop).ConfigureAwait(false);
        return new TurnStop(Cancelled: true, Withdrawn: []);
    }

    /// <summary>A stop decided for one turn (STEER2a): the door's interrupt, the turn, and what tells the run it is on the wire or dropped.</summary>
    private sealed record Stop(Func<Task> Interrupt, int Turn, TaskCompletionSource Sent);

    /// <summary>
    /// Decide the stop for the turn running now, once per turn; null where it was asked already or nothing can stop a turn. The
    /// run takes no held word until it is on the wire or dropped (<see cref="NextOrClose"/>). Called under the gate.
    /// </summary>
    private Stop? StopThisTurn()
    {
        if (state.Interrupt is not { } interrupt || state.StoppedTurn == state.Turn) return null;
        state.StoppedTurn = state.Turn;
        var sent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        state.Stopping = state.Stopping.IsCompleted ? sent.Task : Task.WhenAll(state.Stopping, sent.Task);
        return new Stop(interrupt, state.Turn, sent);
    }

    /// <summary>
    /// Send a stop to the turn it was decided for, or drop it where the run took another turn or the inbox closed; the run may
    /// then take the next held word, never before. Called outside the gate: the door's interrupt is its own.
    /// </summary>
    private async Task SendStopAsync(Stop stop)
    {
        try
        {
            bool current;
            lock (state.Gate) current = !state.Closed && state.Turn == stop.Turn;
            if (current) await stop.Interrupt().ConfigureAwait(false);
        }
        finally
        {
            stop.Sent.TrySetResult();
        }
    }
}
