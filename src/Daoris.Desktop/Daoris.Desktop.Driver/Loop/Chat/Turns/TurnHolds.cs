namespace Daoris.Driver;

/// <summary>
/// The holds on a conversation's turns (<see cref="ChatTurns"/>): a hand-over's take and the shell's close, a limit's wait and its wake, the
/// person's press answered, a refused open's words moved out, a transient episode's wait and its try, and the turns finishing or gone. Each
/// is set and released under <see cref="TurnState"/>'s gate; the pump a release starts runs with it released.
/// </summary>
internal sealed class TurnHolds(TurnState state, TurnPump pump)
{
    /// <summary>
    /// Hold the turns for a hand-over's take, never mid-turn: true where no turn runs and no
    /// word waits, and from then on a word takes nothing (<see cref="Take"/> is null) until <see cref="Release"/>. False where a
    /// turn runs or a word waits, whose turn ends first; or where the conversation is finishing or gone.
    /// </summary>
    /// <remarks>
    /// Asked and set under the turns' own lock, so a word said as a move begins either started its turn first, and the move waits
    /// for that turn's end, or finds the turns held. Checked first and held after, a word between the two started a turn the take's
    /// end then cut.
    /// </remarks>
    /// <param name="keeps">
    /// The host's open moves the old part's words: a word said while held is kept on the record
    /// and waits behind the hold, starting no turn, as under a limit's wait and a press, where without it a word is neither kept nor taken.
    /// </param>
    public bool Hold(bool keeps = false)
    {
        lock (state.Gate)
        {
            if (state.Pumping || state.Holding is not null || state.Waiting.Count > 0 || state.Finishing || state.Gone || state.Held) return false;
            state.Held = true;
            state.KeepsHeld = keeps;
            return true;
        }
    }

    /// <summary>Hold even a running turn at the shell's closing boundary; nothing queued starts after it.</summary>
    public bool HoldForClose()
    {
        lock (state.Gate)
        {
            var cut = state.Pumping || state.Holding is not null || state.Waiting.Count > 0;
            state.Held = true;
            state.CloseHeld = true;
            return cut;
        }
    }

    /// <summary>
    /// Take words again after <see cref="Hold"/>: the take did not end the conversation, or it has ended and nothing listens. A word
    /// kept while held is the next turn where the conversation goes on, and stays on its record for the move's open where it ended.
    /// </summary>
    public void Release()
    {
        bool start;
        lock (state.Gate)
        {
            state.Held = false;
            state.KeepsHeld = false;
            if (!state.Gone && !state.Finishing) state.Waiting.AddRange(state.HeldBack);
            state.HeldBack.Clear();
            start = state.Waiting.Count > 0 && !state.Pumping && !state.Limited && !state.Pressing && !state.SendingBack && !state.Gone && !state.Finishing && !state.CloseHeld;
            if (start)
            {
                state.Pumping = true;
                state.Drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        if (start) _ = pump.PumpAsync();
        lock (state.Gate) state.Publish();
    }

    /// <summary>
    /// Wait for the account's limit to reset: from now on a word is kept as any is and waits, shown queued,
    /// but starts no turn, since the account would refuse it. False where the conversation is finishing, held for a take, or gone.
    /// </summary>
    public bool WaitForLimit()
    {
        lock (state.Gate)
        {
            if (state.Finishing || state.Gone || state.Held) return false;
            state.Limited = true;
            return true;
        }
    }

    /// <summary>
    /// The wake: every word that waited, handed to <paramref name="compose"/> in the order said, becomes one
    /// turn, the next at once, and the turns take words again. Under the turns' own lock, so a word said as it wakes either waited and is
    /// among them or comes after it. False, composing nothing, where it is not waiting, or is finishing, held or gone.
    /// </summary>
    /// <param name="compose">The one turn the waiting words become; called under the lock, so it must not call back into the turns.</param>
    public bool WakeFromLimit(Func<IReadOnlyList<ChatMessage>, ChatMessage> compose)
    {
        bool start;
        lock (state.Gate)
        {
            if (!state.Limited || state.Finishing || state.Gone || state.Held) return false;
            List<ChatMessage> waited = [.. state.Waiting.Select(state.Unstamped)];
            state.Waiting.Clear();
            state.Waiting.Add(compose(waited));
            state.Limited = false;
            start = !state.Pumping && !state.Pressing && !state.SendingBack;
            if (start)
            {
                state.Pumping = true;
                state.Drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        if (start) _ = pump.PumpAsync();
        lock (state.Gate) state.Publish();
        return true;
    }

    /// <summary>
    /// Hold the turns for a move while they wait for the limit (as <see cref="Hold"/> holds them for a take): the words
    /// that waited are handed out, in the order said, for the move to carry, and nothing more is taken until <see cref="Release"/>.
    /// Null where it is not waiting, a turn runs, or it is finishing, held or gone.
    /// </summary>
    public IReadOnlyList<ChatMessage>? HoldLimited(bool keeps = false)
    {
        lock (state.Gate)
        {
            if (!state.Limited || state.Pumping || state.Holding is not null || state.Finishing || state.Gone || state.Held) return null;
            List<ChatMessage> waited = [.. state.Waiting.Select(state.Unstamped)];
            state.Waiting.Clear();
            state.Limited = false;
            state.Held = true;
            state.KeepsHeld = keeps;
            state.Publish();
            return waited;
        }
    }

    /// <summary>
    /// The person's press answered, landed or refused: the conversation is let go, and a word that waited for it starts at once.
    /// Nothing where it is not held.
    /// </summary>
    public void Pressed()
    {
        bool start;
        lock (state.Gate)
        {
            if (!state.Pressing) return;
            state.Pressing = false;
            start = state.Waiting.Count > 0 && !state.Pumping && !state.Limited && !state.SendingBack && !state.Gone && !state.CloseHeld;
            if (start)
            {
                state.Pumping = true;
                state.Drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        if (start) _ = pump.PumpAsync();
        lock (state.Gate) state.Publish();
    }

    /// <summary>
    /// Its open is refused before any prompt and a replacement opens in its place: every word it holds, the
    /// one waiting on the open first and then the rest in the order said, is handed out, and from now on a word said to it is handed to
    /// <paramref name="onward"/> instead, under this same lock, so each reaches the replacement once. Nothing is sent here again.
    /// </summary>
    /// <param name="onward">Takes a word said meanwhile: false where it no longer takes words, and the word is not taken.</param>
    public IReadOnlyList<ChatMessage> MoveOut(Func<ChatMessage, bool> onward)
    {
        lock (state.Gate)
        {
            List<ChatMessage> moved = [.. (state.Holding is null ? [] : new[] { state.Holding }).Concat(state.Waiting).Select(state.Unstamped)];
            state.Holding = null;
            state.Waiting.Clear();
            state.Onward = onward;
            state.Publish();
            return moved;
        }
    }

    /// <summary>
    /// The one closing turn a hand-over's take asks of the outgoing agent while its turns are held: taken as a turn of its own though held, every other word still taking nothing, and what completes once that turn has
    /// ended. Null where the turns are not held by <see cref="Hold"/>, or something runs or waits, or the conversation is finishing or gone.
    /// </summary>
    public Task? TakeHeld(ChatMessage message)
    {
        Task drained;
        lock (state.Gate)
        {
            if (!state.Held || state.CloseHeld || state.Closing?.Invoke() == true || state.Pumping || state.Holding is not null || state.Waiting.Count > 0 || state.Finishing || state.Gone) return null;
            state.Waiting.Add(state.Stamped(message));
            state.Pumping = true;
            state.Drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            drained = state.Drained.Task;
        }

        _ = pump.PumpAsync();
        lock (state.Gate) state.Publish();
        return drained;
    }

    /// <summary>
    /// A transient episode settles its wire (from its first failure and each try's): every word waits from now, handed to no
    /// turn alongside the one that failed, so the turns the episode settles are only those already written.
    /// </summary>
    internal void Settling()
    {
        lock (state.Gate) state.Retrying = true;
    }

    /// <summary>
    /// A transient episode's wait begins: every word waits, and the wait ends once a word of the person's does, at once
    /// where one waits already. A word said meanwhile is kept on its record and shown queued, as any word is.
    /// </summary>
    internal Task Waiting()
    {
        lock (state.Gate)
        {
            state.Retrying = true;
            state.Woke = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            if (state.Waiting.Any(TurnState.Carries)) state.Woke.TrySetResult();
            return state.Woke.Task;
        }
    }

    /// <summary>The episode's wait is over, a try is on the wire (or the episode ended): a word said from now goes as it did.</summary>
    internal void Waited()
    {
        lock (state.Gate) state.Retrying = false;
    }

    /// <summary>
    /// Every word of the person's that waits, handed to a transient try in the order said: off the queue
    /// the page shows, each dispatched as its door dispatches one, marked attempted before the wire and taken off the record once written.
    /// Their turn counts as the person's, as a word handed to a turn at its next step does.
    /// </summary>
    internal IReadOnlyList<ChatMessage> TakeHeld()
    {
        lock (state.Gate)
        {
            List<ChatMessage> taken = [];
            for (var at = 0; at < state.Waiting.Count;)
            {
                if (!TurnState.Carries(state.Waiting[at]))
                {
                    at++;
                    continue;
                }

                taken.Add(state.Dispatched(state.Unstamped(state.Waiting[at])));
                state.Waiting.RemoveAt(at);
            }

            if (taken.Count > 0)
            {
                state.TheirsSteered = true;
                state.Publish();
            }

            return taken;
        }
    }

    /// <summary>
    /// Take no more, and complete once every turn already asked for has ended — what finishing waits on
    /// before it ends the input, on either door — and every kept word that left is off the record.
    /// </summary>
    public Task FinishAsync()
    {
        Task drained;
        lock (state.Gate)
        {
            state.Finishing = true;
            drained = state.Drained.Task;
        }

        return TakenOffAfterAsync(drained);

        async Task TakenOffAfterAsync(Task turns)
        {
            await turns.ConfigureAwait(false);
            await state.TakingOff.ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The harness has gone: nothing waiting will be sent, and nothing more is taken. Words kept on the record stay there for the
    /// conversation to go on with (nothing undelivered vanishes), unless <paramref name="personStopped"/>: the person's stop
    /// ends them with the conversation, rather than start it again.
    /// </summary>
    public void Gone(bool personStopped = false)
    {
        lock (state.Gate)
        {
            state.Gone = true;
            if (personStopped) state.TakeOff([.. (state.Holding is null ? [] : new[] { state.Holding }).Concat(state.Waiting).Where(message => !message.OnRecord)]);
            state.Holding = null;
            state.Waiting.Clear();
            // Words kept behind a move's hold stay on the record for its open.
            state.HeldBack.Clear();
            state.Publish();
        }
    }
}
