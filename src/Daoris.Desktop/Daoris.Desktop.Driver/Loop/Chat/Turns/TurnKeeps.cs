namespace Daoris.Driver;

/// <summary>
/// The conversation's keeps on its record, one at a time under <see cref="TurnState.Keeping"/> (<see cref="ChatTurns"/>): a word
/// kept before it is taken, a waiting word taken back, reworded or raised, the person's press reserved, and a second opinion's send-back
/// reserved and handed. The record's calls run with the gate released; what they found is applied under it.
/// </summary>
/// <param name="steer">The door's next step, as <see cref="TurnTakes"/> takes it: where a raised word goes into the turn.</param>
/// <param name="steerNow">The door's <i>Send now</i>, as <see cref="TurnTakes"/> takes it.</param>
internal sealed class TurnKeeps(
    TurnState state, TurnTakes takes, TurnPump pump, Func<ChatMessage, Task?>? steer, Func<ChatMessage, Task?>? steerNow)
{
    /// <summary>
    /// Keep a word on the conversation's record first, then take it as <see cref="Take"/> or, at <c>now</c>,
    /// <see cref="TakeNowAsync"/> takes it, under the record's id. One keep at a time: two said at once wait in the order the record
    /// kept them. A record that will not keep it, or does not answer twice, leaves it waiting here alone. Null, keeping nothing, once
    /// nothing more is heard (finishing, held for a hand-over's take, or gone).
    /// </summary>
    public async Task<TurnKept?> KeepAsync(ISaidDoor door, ChatMessage message, CancellationToken ct)
    {
        await state.Keeping.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            bool forMove;
            lock (state.Gate)
            {
                // Moved to the replacement opening in its place, the word joins the words moved there, kept nowhere here.
                if (state.Onward is { } onward) return onward(message) ? new TurnKept(TurnReach.TurnEnd, null) : null;
                forMove = state.Held && state.KeepsHeld && !state.CloseHeld;
                if (state.Finishing || state.Gone || (state.Held && !forMove)) return null;
            }

            message = KeepRetry.Keyed(message);
            var said = await KeepRetry.KeepAsync(door, message, ct).ConfigureAwait(false);

            // The record answered the retry settled: its word is held or went already, so nothing is held, queued, steered or taken.
            if (said is { Settled: not null }) return new TurnKept(TurnReach.TurnEnd, null) { Settled = true };

            // The record is no longer its line's head, which the answer names. Never the fallback below: nothing is queued, steered,
            // attempted or written on this door, and the caller says the word at the head.
            if (said is { Head: { } head }) return new TurnKept(TurnReach.TurnEnd, null) { Head = head };

            var id = said is { Kept: true, Word.Id: { } kept } ? kept : null;
            var word = id is null ? message : message with { Id = id, Kept = true };
            // Held for a move, the record would not keep it: nothing is heard, and the shell keeps or holds it as it judges the record.
            if (forMove && id is null) return null;
            if (id is not null)
            {
                lock (state.Gate)
                {
                    state.Door ??= door;
                    // A producer's retry under the key of a word that waits already: taken once.
                    if (!state.Known.Add(id)) return new TurnKept(TurnReach.TurnEnd, id);

                    // The move's, while the hold stands, and the record's for its open once the move ended the conversation.
                    if (forMove && (state.Held || state.Gone || state.Finishing))
                    {
                        if (state.Held && !state.Gone && !state.Finishing) state.HeldBack.Add(word);
                        return new TurnKept(TurnReach.TurnEnd, id);
                    }
                }
            }

            if (word.Level == MessageLevel.Now)
            {
                if (await takes.TakeNowAsync(word).ConfigureAwait(false) is { } now) return new TurnKept(TurnReach.Steered, id) { Now = now };
            }
            else if (takes.Take(word) is { } reach)
            {
                return new TurnKept(reach, id);
            }

            // Nothing more is heard since it is kept: off the record again, which is ending.
            lock (state.Gate) state.TakeOff([word]);
            return null;
        }
        finally
        {
            state.Keeping.Release();
        }
    }

    /// <summary>
    /// Take back, reword, or change the level of one word that still waits: with neither
    /// <paramref name="text"/> nor <paramref name="level"/> it is removed; with words, they replace its words where it waits, its
    /// place and its files kept, under a new id; with a level, it keeps its id and asks to reach the agent then. Raised while a turn
    /// is on the wire whose door takes words at its next step, it goes into that turn as a word said now would, steered past what
    /// waits where it is <c>now</c> and the agent steers. <see cref="QueueEdit.Taken"/> where a turn took it already.
    /// </summary>
    public QueueEdit EditQueued(string id, string? text, MessageLevel? level = null)
    {
        lock (state.Gate)
        {
            return EditedHere(id, text, level, newId: null);
        }
    }

    /// <summary>
    /// <see cref="EditQueued"/>, and a word kept on the record reworded as a word said is kept: the new words on the record
    /// first, under their own id, then in the old one's place, which leaves the record. One a turn took meanwhile is the turn's, and the
    /// new words leave the record again. Where the record will not keep them, they wait here alone.
    /// </summary>
    public async Task<QueueEdit> EditQueuedAsync(string id, string? text, MessageLevel? level = null, CancellationToken ct = default)
    {
        ChatMessage? old;
        ISaidDoor? door;
        lock (state.Gate)
        {
            old = state.WaitingWord(id);
            door = state.Door;
        }

        if (old is not { Kept: true } || text is null || door is null) return EditQueued(id, text, level);

        await state.Keeping.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var said = await KeepRetry.KeepAsync(
                door, KeepRetry.Keyed(old with { Text = text, Level = level ?? old.Level, Key = null, Id = null, Kept = false }), ct).ConfigureAwait(false);

            // The record went on in its line's head meanwhile, so nothing is reworded or queued here; the word kept stays as it
            // is, on the record the open moves its words from.
            if (said is { Head: not null }) return QueueEdit.Taken;

            var kept = said is { Kept: true, Word.Id: { } newId } ? newId : null;
            lock (state.Gate)
            {
                if (state.WaitingWord(id) is null)
                {
                    if (kept is not null) state.TakeOff([new ChatMessage(text, []) { Id = kept, Kept = true }]);
                    return QueueEdit.Taken;
                }

                if (kept is not null) state.Known.Add(kept);
                return EditedHere(id, text, level, kept);
            }
        }
        finally
        {
            state.Keeping.Release();
        }
    }

    /// <summary>
    /// One waiting word taken back, reworded (under <paramref name="newId"/>, the record's, where it kept the new words), or at a new
    /// level. A kept word taken back or reworded leaves the record, so a restart never brings back what the person changed.
    /// Called under the lock.
    /// </summary>
    private QueueEdit EditedHere(string id, string? text, MessageLevel? level, string? newId)
    {
        var withdrawn = text is not null || level is null;
        if (state.Holding is { Theirs: true } holding && holding.Id == id)
        {
            state.Holding = text is null && level is null ? null : Changed(holding, text, level, newId);
            if (withdrawn) state.TakeOff([holding]);
        }
        else
        {
            var at = state.Waiting.FindIndex(message => message.Theirs && message.Id == id);
            if (at < 0) return QueueEdit.Taken;
            var old = state.Waiting[at];
            state.Waiting.RemoveAt(at);
            if (withdrawn) state.TakeOff([old]);
            if (text is not null || level is not null)
            {
                var word = Changed(old, text, level, newId);
                if (!IntoTheTurn(word, at)) state.Waiting.Insert(at, word);
            }
        }

        state.Publish();
        return QueueEdit.Done;
    }

    /// <summary>A waiting word reworded under a new id, or at a new level under its own. Called under the lock.</summary>
    private ChatMessage Changed(ChatMessage message, string? text, MessageLevel? level, string? kept = null) =>
        (text is null ? message : Reworded(message, text, kept)) with { Level = level ?? message.Level };

    /// <summary>A waiting word reworded: under the record's new id where it kept the new words, else under the queue's own.</summary>
    private ChatMessage Reworded(ChatMessage message, string text, string? kept)
    {
        if (kept is not null) return message with { Text = text, Id = kept, Kept = true };
        var id = $"said-{++state.IdsGiven}";
        state.Stamps.Add(id);
        return message with { Text = text, Id = id, Kept = false };
    }

    /// <summary>
    /// A waiting word raised: handed to the turn on the wire where its level and the door allow, as a word said now goes
    /// there; at <c>now</c> steered past what waits where the agent steers, and at its next step only with no urgent word waiting
    /// before it. False where it waits on. Called under the lock, with the word out of the queue.
    /// </summary>
    private bool IntoTheTurn(ChatMessage word, int at)
    {
        if (word.Level == MessageLevel.Ordinary || TurnState.Answer(word) || !state.InFlight || !state.OnWire || state.Stopped || state.StopPending
            || state.Holding is not null || state.Retrying) return false;

        // Without the queue's id, as a turn is sent: the door gives a word handed to the turn its own.
        var stamped = word.Id is { } queued && state.Stamps.Remove(queued) ? queued : null;
        var handed = stamped is null ? word : word with { Id = null };
        if (word.Level == MessageLevel.Now && steerNow?.Invoke(state.Dispatched(handed)) is { } steered)
        {
            state.Steered.Add(steered);
            state.TheirsSteered |= word.Theirs;
            return true;
        }

        if (!state.UrgentWaiting(before: at) && steer?.Invoke(state.Dispatched(handed)) is { } stepped)
        {
            state.Steered.Add(stepped);
            state.OnWay.Add(stepped);
            state.TheirsSteered |= word.Theirs;
            return true;
        }

        if (stamped is not null) state.Stamps.Add(stamped);
        return false;
    }

    /// <summary>
    /// Reserve the conversation for the person's press on its work:
    /// <see cref="PressHold.Held"/> where nothing is taking, opening, held for a take or waiting, and from then on a word is kept and waits,
    /// starting no turn, until <see cref="Pressed"/>. Taken after any word whose keeping is in flight has been kept and reached the queue, so a
    /// word the person sent before the press is never overtaken by it.
    /// </summary>
    /// <remarks>Not <see cref="Hold"/>, under which a word is dropped: the press keeps every word the person says while it lands.</remarks>
    public async Task<PressHold> PressAsync(CancellationToken ct = default)
    {
        await state.Keeping.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            lock (state.Gate)
            {
                if (state.Finishing || state.Gone || state.Held) return PressHold.Ending;
                if (state.Pressing || state.SendingBack || state.Pumping || state.Opening || state.Holding is not null || state.Waiting.Count > 0) return PressHold.Busy;
                state.Pressing = true;
                state.Publish();
                return PressHold.Held;
            }
        }
        finally
        {
            state.Keeping.Release();
        }
    }

    /// <summary>
    /// A second opinion's send-back into this conversation: one operation of the keep, so a word of the person's whose keeping is in flight is kept and queued before anything is
    /// tested. Then, under the turns' lock, the person's turn count (<paramref name="history"/>, plus their words waiting or on the wire) is
    /// compared with the press's: risen, the findings are the person's (<see cref="SendBack.PersonWentOn"/>). Anything else taking, opening,
    /// waiting, held, pressed or limited is activity that is not theirs (<see cref="SendBack.Waiting"/>). Otherwise it is reserved, and
    /// the service's hand (<paramref name="hand"/>) is asked inside the same operation; the word it kept is the reserved turn's, taken at
    /// once ahead of any word said meanwhile, which waits behind it as behind any turn.
    /// </summary>
    /// <remarks>Never at a next step nor by <i>Send now</i>: the findings are an ordinary word of their own turn, never steered.</remarks>
    /// <param name="pressed">The person's turn count the press recorded on the due.</param>
    /// <param name="history">The person's own words in the line's conversation history (<see cref="ChatPresses.TurnsOf"/>), read with the keep held.</param>
    /// <param name="hand">The service's hand: the findings word it kept, as this conversation's message; null where it refused.</param>
    public async Task<SendBack> SendBackAsync(
        int pressed, Func<int> history, Func<CancellationToken, Task<ChatMessage?>> hand, CancellationToken ct = default)
    {
        await state.Keeping.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // A turn put in flight while the history is read may have moved a word of theirs into it: read again, a few times, else the next look does.
            for (var read = 0; ; read++)
            {
                int turn;
                lock (state.Gate)
                {
                    if (state.Finishing || state.Gone || state.Held || state.Onward is not null) return SendBack.Waiting;
                    turn = state.Turn;
                }

                var counted = history();
                lock (state.Gate)
                {
                    if (state.Finishing || state.Gone || state.Held || state.Onward is not null) return SendBack.Waiting;
                    if (state.Turn != turn)
                    {
                        if (read < 3) continue;
                        return SendBack.Waiting;
                    }

                    counted += state.Waiting.Count(message => message.Theirs) + (state.Holding is { Theirs: true } ? 1 : 0)
                               + (state.InFlight && state.Current is { Theirs: true } ? 1 : 0);
                    if (counted > pressed) return SendBack.PersonWentOn;
                    if (state.Pumping || state.Opening || state.Holding is not null || state.Waiting.Count > 0 || state.Pressing || state.Limited || state.SendingBack) return SendBack.Waiting;
                    state.SendingBack = true;
                    break;
                }
            }

            ChatMessage? word = null;
            try
            {
                word = await hand(ct).ConfigureAwait(false);
            }
            finally
            {
                bool start;
                lock (state.Gate)
                {
                    state.SendingBack = false;
                    if (word is not null && !state.Finishing && !state.Gone && !state.Held && state.Onward is null) state.Waiting.Insert(0, word with { Level = MessageLevel.Ordinary });
                    else word = null;
                    start = state.Waiting.Count > 0 && !state.Pumping && !state.Limited && !state.Pressing && !state.Gone && !state.Finishing && !state.CloseHeld && !state.Held;
                    if (start)
                    {
                        state.Pumping = true;
                        state.Drained = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    }
                }

                if (start) _ = pump.PumpAsync();
                lock (state.Gate) state.Publish();
            }

            return word is null ? SendBack.Refused : SendBack.Reserved;
        }
        finally
        {
            state.Keeping.Release();
        }
    }
}
