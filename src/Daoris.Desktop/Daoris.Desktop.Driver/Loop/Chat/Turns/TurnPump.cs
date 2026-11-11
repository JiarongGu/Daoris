namespace Daoris.Driver;

/// <summary>
/// A conversation's one pump (<see cref="ChatTurns"/>): the next waiting word taken off the queue, the door opened for it, the turn sent,
/// and the turn kept in flight until every word handed to it at its next step is answered; then the next. Its state is
/// <see cref="TurnState"/>'s, read and written under its gate between the door's calls, which run with it released.
/// </summary>
/// <param name="ready">Whether the door can take a turn: false means it never will, and everything waiting is dropped.</param>
/// <param name="take">Send one turn and complete when it ends, calling its second argument the moment the turn is on the wire.</param>
/// <param name="settled">What the words handed to a turn came to, settled before the turn is found to have ended; null settles nothing.</param>
internal sealed class TurnPump(
    TurnState state, TurnStops stops, Func<Task<bool>> ready, Func<ChatMessage, Action, Task> take, Func<Task>? settled)
{
    /// <summary>
    /// Whether the bound holds this helper's answer. A judge that cannot judge holds nothing, as a line whose events do not
    /// read counts nothing.
    /// </summary>
    private async Task<bool> HeldAloneAsync(ChatMessage message)
    {
        Func<ChatMessage, Task<bool>>? alone;
        lock (state.Gate) alone = state.Alone;
        if (message.Helped is null || alone is null) return false;
        try
        {
            return await alone(message).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal async Task PumpAsync()
    {
        while (true)
        {
            ChatMessage message;
            lock (state.Gate)
            {
                // A conversation waiting for its account's limit starts no turn; the wake starts the next.
                if (state.Waiting.Count == 0 || state.Gone || state.Limited || state.CloseHeld || state.Closing?.Invoke() == true)
                {
                    state.Pumping = false;
                    state.Drained.TrySetResult();
                    state.Publish();
                    return;
                }

                message = state.Waiting[0];
                state.Waiting.RemoveAt(0);
                state.Holding = message;
            }

            // 🔴 A stop decided for the turn before is on the wire, or dropped, before this one goes: sent after it, the
            // stop would cut this turn, whose words have left the queue and would be lost. No stop is decided meanwhile, since
            // none is decided with no turn in flight.
            Task stopping;
            lock (state.Gate) stopping = state.Stopping;
            await stopping.ConfigureAwait(false);

            // 🔴 The conversation's one dispatcher judges a helper's answer as its
            // turn comes, once every turn before it is recorded, so two answers queued at once under separate asks never both pass as the
            // second. One the bound holds goes no further: it stays on its record, marked for the person, and the queue goes on.
            if (await HeldAloneAsync(message).ConfigureAwait(false))
            {
                lock (state.Gate)
                {
                    if (ReferenceEquals(state.Holding, message)) state.Holding = null;
                    state.Publish();
                }

                continue;
            }

            var readying = ready();
            if (!readying.IsCompleted)
            {
                // The door is still opening: the message is waiting on it, and says so.
                lock (state.Gate)
                {
                    state.Opening = true;
                    state.Publish();
                }
            }

            bool open;
            try
            {
                open = await readying.ConfigureAwait(false);
            }
            catch (Exception error) when (error is DriverException or IOException or InvalidOperationException
                                              or OperationCanceledException)
            {
                open = false;
            }

            lock (state.Gate)
            {
                state.Opening = false;
                if (state.CloseHeld || state.Closing?.Invoke() == true)
                {
                    state.Pumping = false;
                    state.Drained.TrySetResult();
                    state.Publish();
                    return;
                }
                if (!open)
                {
                    state.Gone = true;
                    state.Holding = null;
                    state.Waiting.Clear();
                    continue;
                }

                // Withdrawn by a stop while the door opened: never sent.
                if (state.Holding is null) continue;
                // As it stands now: reworded while the door opened, and without the queue's id.
                message = state.Holding;
                if (message.Id is { } stamped && state.Stamps.Remove(stamped)) message = message with { Id = null };
                state.Holding = null;
                state.InFlight = true;
                state.Current = message;
                state.TheirsSteered = false;
                state.Turn++;
                state.OnWire = false;
                state.StopPending = false;
                state.Stopped = false;
                state.Publish();
            }

            try
            {
                // A kept word's attempt is marked on its record before its turn is on the wire, and acknowledged there:
                // a word the record does not name marked is not written and waits on its record,
                // said; a go-on carries only the words it names.
                if (await SaidDoor.AcknowledgedAsync(state.Dispatched(message), count => state.Unsent?.Invoke(count)).ConfigureAwait(false) is { } going)
                {
                    message = message with { Text = going.Text };
                    await take(message, () =>
                    {
                        stops.Sent();
                        // A helper's answer comes off the record once its turn is on the wire, and a kept word by its id,
                        // after every take-off before it.
                        message.Delivered?.Invoke();
                        going.Told?.Invoke();
                        lock (state.Gate) state.TakeOff([message]);
                    }).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                // The door says what failed in its own words; the queue outlives one bad turn, or every
                // later message would wait on a pump that is gone.
            }

            // 🔴 The turn lasts until every word handed to it at its next step is answered: the agent
            // answers the prompt before them when it takes them, and works on. Found empty under the lock that ends the
            // turn, so no word is handed to a turn that has already ended.
            bool wentOut;
            bool theirs;
            while (true)
            {
                // What the words handed to this turn came to is settled before it is found to have ended, so one whose
                // own turn failed transient is tried again within it. Read again each time those words have all been answered.
                if (settled is not null)
                {
                    try
                    {
                        await settled().ConfigureAwait(false);
                    }
                    catch (Exception)
                    {
                        // The door says what became of them in its own words; the turn ends all the same.
                    }
                }

                Task[] steered;
                lock (state.Gate)
                {
                    steered = [.. state.Steered];
                    state.Steered.Clear();
                    if (steered.Length == 0)
                    {
                        // The end of a turn that reached the harness is the chat's last move, told
                        // at once: the next message may start straight away and leave nothing else changed
                        // for the page to hear. One that never went out moved nothing.
                        wentOut = state.OnWire;
                        theirs = state.TheirsSteered;
                        if (state.OnWire)
                        {
                            state.LastEnded = DateTimeOffset.UtcNow;
                            state.Publish();
                        }

                        state.InFlight = false;
                        state.Current = null;
                        state.OnWire = false;
                        state.StopPending = false;
                        break;
                    }
                }

                try
                {
                    await Task.WhenAll(steered).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Each word's door says what became of it; the turn ends all the same.
                }
            }

            // 🔴 A send-back's turn has a completion of its own, not the pump's drain. Its ending tip
            // and answers are frozen here, before the next queued word is taken, so a person's turn behind it never moves what it read. One that
            // never went out answered nothing: its word stays on its record.
            if (wentOut && message.Answering is { } answering)
            {
                try
                {
                    await answering(theirs).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // The reading's own caller says what it could not read; the turns go on either way.
                }
            }
        }
    }
}
