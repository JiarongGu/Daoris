namespace Daoris.Driver;

/// <summary>
/// A driven session's keeps on its record, one at a time under <see cref="InboxState.Keeping"/> (<see cref="DrivenInbox"/>): a
/// word kept before it is held, or held here alone where the record cannot keep it, and a held word taken back, reworded or raised. The
/// record's calls run with the gate released; what they found is applied under it. A word the record cannot keep is held here alone
/// by <see cref="DrivenInbox.KeepOrHoldAsync"/>, over this and the holds.
/// </summary>
internal sealed class InboxKeeps(InboxState state)
{
    /// <summary>
    /// Take back, reword, or change the level of one held word: with neither <paramref name="text"/> nor
    /// <paramref name="level"/> it is removed; with words, they replace its words where it is held, its place and its files kept,
    /// under a new id the record is told as said; with a level, it asks to reach the session then, under the same id, told again
    /// with where it reaches now. Raised where the door takes words at its next step, it goes there at once, and at <c>now</c> where
    /// the agent steers, steered past what waits. Only a word still held (<see cref="QueueEdit.Taken"/>
    /// otherwise): one sent to the turn on the wire, or taken for the next, is the session's. The record, which showed it waiting
    /// once the door is known, settles a reworded or removed word's old id as withdrawn.
    /// </summary>
    public QueueEdit Edit(string id, string? text, MessageLevel? level = null)
    {
        lock (state.Gate)
        {
            var at = state.HeldAt(id);
            if (at < 0) return QueueEdit.Taken;
            return Edited(at, text, level, text is null ? null : $"said-{++state.IdsGiven}", kept: false);
        }
    }

    /// <summary>
    /// <see cref="Edit"/>, and a word kept on the record reworded as a word said is kept: the new words on the record first,
    /// under their own id, then in the old one's place, which leaves the record. One taken while the new words are kept is the
    /// session's, and the new words leave the record again. Where the record will not keep them, they are held in this process alone.
    /// </summary>
    public async Task<QueueEdit> EditAsync(string id, string? text, MessageLevel? level = null, CancellationToken ct = default)
    {
        ISaidDoor? door;
        lock (state.Gate)
        {
            var at = state.HeldAt(id);
            door = state.Door;
            if (at < 0 || text is null || !state.Held[at].Kept || door is null) return Edit(id, text, level);
        }

        await state.Keeping.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ChatMessage old;
            lock (state.Gate)
            {
                var at = state.HeldAt(id);
                if (at < 0) return QueueEdit.Taken;
                old = state.Held[at];
            }

            SayAnswer said;
            try
            {
                said = await door.KeepAsync(old with { Text = text, Level = level ?? old.Level, Key = null, Id = null, Kept = false }, ct)
                    .ConfigureAwait(false);
            }
            catch (Exception error) when (error is HttpRequestException or DriverException or ObjectDisposedException
                                              or System.Text.Json.JsonException
                                              || (error is OperationCanceledException && !ct.IsCancellationRequested))
            {
                said = new SayAnswer(false, error.Message);
            }

            // The record went on in its line's head meanwhile, so nothing is reworded or held here; the word kept stays as
            // it is, on the record the open moves its words from.
            if (said.Head is not null) return QueueEdit.Taken;

            lock (state.Gate)
            {
                var at = state.HeldAt(id);
                var kept = said is { Kept: true, Word.Id: { } newId } ? newId : null;
                if (at < 0)
                {
                    if (kept is not null) state.TakeOff([new ChatMessage(text, []) { Id = kept, Kept = true }]);
                    return QueueEdit.Taken;
                }

                if (kept is not null) state.Known.Add(kept);
                return Edited(at, text, level, kept ?? $"said-{++state.IdsGiven}", kept: kept is not null);
            }
        }
        finally
        {
            state.Keeping.Release();
        }
    }

    /// <summary>
    /// The held word at <paramref name="at"/> taken back, reworded under <paramref name="newId"/>, or at a new level. A kept word
    /// taken back or reworded leaves the record, so a restart never brings back what the person changed. Called under the gate.
    /// </summary>
    private QueueEdit Edited(int at, string? text, MessageLevel? level, string? newId, bool kept)
    {
        var old = state.Held[at];
        // A word said before the door is known is not yet told: the record has nothing of it to settle.
        var told = state.Reach is not null;
        ChatMessage? word = text is null && level is null
            ? null
            : old with
            {
                Text = text ?? old.Text, Id = text is null ? old.Id : newId, Level = level ?? old.Level, Kept = text is null ? old.Kept : kept,
            };
        state.Held.RemoveAt(at);
        if (word is null || text is not null) state.TakeOff([old]);

        if (told && (word is null || text is not null))
        {
            try
            {
                state.Withdrawn?.Invoke(old);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                // The record misses it; the queue has moved either way.
            }
        }

        if (word is not null)
        {
            if (word.Level == MessageLevel.Now && state.Reach == DrivenReach.NextStep && state.Flowing && state.SteerWith is { } steer)
            {
                state.Tell(word, DrivenReach.Steered);
                state.Steer(steer, word);
            }
            else if (state.GoesAtNextStep(word))
            {
                state.Tell(word, DrivenReach.NextStep);
                state.Send(word);
            }
            else
            {
                state.Held.Insert(at, word);
                if (told) state.Tell(word, state.ReachOf(word.Level));
            }
        }

        state.Publish();
        return QueueEdit.Done;
    }

    /// <summary>
    /// Keep a word for the run on the session's record first, then take it as <see cref="Hold"/> or, at <c>now</c>,
    /// <see cref="HoldNowAsync"/> takes it, under the record's id. One keep
    /// at a time: two said at once are held in the order the record kept them, whichever write answered first. A keep on
    /// its way holds the inbox open, so a word said as the turn ends is taken by this run. A word the record answers by a key it
    /// already holds is held once.
    /// </summary>
    /// <remarks>
    /// <para><b>Kept before told.</b> The record keeps the word, then the inbox tells it with where it reaches the session; a word the
    /// record refused is never held, and its caller says so.</para>
    ///
    /// <para><b>Closed</b> (<see cref="InboxKeep.Closed"/>), before the keep or while it is on its way: the run takes no more words,
    /// so nothing stays on the record, which is ending, and the caller holds the word as words said while a session winds up are held.</para>
    /// </remarks>
    /// <exception cref="HttpRequestException">The record did not answer: nothing holds the word.</exception>
    public async Task<InboxKept> KeepAsync(ISaidDoor door, ChatMessage message, CancellationToken ct)
    {
        lock (state.Gate)
        {
            if (state.Closed) return InboxKept.Closed;
            if (state.KeepsOnWay++ == 0) state.KeepsLanded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        try
        {
            await state.Keeping.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                lock (state.Gate)
                {
                    if (state.Closed) return InboxKept.Closed;
                }

                var said = await door.KeepAsync(message, ct).ConfigureAwait(false);
                // The record answered the retry settled, so its word is held or went already:
                // answered as a retry of a word held, with nothing held, sent, steered, attempted or taken, and no word to read an id from.
                if (said.Settled is not null) return new InboxKept(InboxKeep.Held, said);
                // The record is no longer its line's head, which the answer names. Nothing is held,
                // sent, steered, attempted or taken here, and KeepOrHoldAsync never holds it here instead: its caller says it at the head.
                if (said.Head is not null) return new InboxKept(InboxKeep.Continued, said);
                if (!said.Kept || said.Word?.Id is not { } id) return new InboxKept(InboxKeep.Refused, said);

                lock (state.Gate)
                {
                    state.Door ??= door;
                    var kept = message with { Id = id, Kept = true };
                    if (state.Closed)
                    {
                        // 🔴 Never left on a record that is ending: off it again, and its caller holds it as words said
                        // while a session winds up are held, until the record ends.
                        state.TakeOff([kept]);
                        return InboxKept.Closed;
                    }

                    var reach = state.Reach is null ? (DrivenReach?)null : state.ReachOf(kept.Level);
                    // A producer's retry under the key of a word held already: held once.
                    if (!state.Known.Add(id)) return new InboxKept(InboxKeep.Held, said) { Reach = reach };
                    if (kept.Level == MessageLevel.Now) return new InboxKept(InboxKeep.Held, said) { Reach = state.Reach, Now = state.NowHeld(kept) };

                    state.HoldHere(kept);
                    return new InboxKept(InboxKeep.Held, said) { Reach = reach };
                }
            }
            finally
            {
                state.Keeping.Release();
            }
        }
        finally
        {
            TaskCompletionSource? landed = null;
            lock (state.Gate)
            {
                if (--state.KeepsOnWay == 0) landed = state.KeepsLanded;
            }

            landed?.TrySetResult();
        }
    }
}
