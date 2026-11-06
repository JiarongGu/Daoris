namespace Daoris.Driver;

/// <summary>The native door's go-on: what the person said while a run worked, carried on through the run's own conversation.</summary>
internal sealed class NativeGoOn(LoopContext context)
{
    private readonly LoopContext _context = context;

    /// <summary>
    /// Go on with what the person said to a native run while it worked (MSG1b, D137 §2.1): each time a run ends with words
    /// held, its own conversation is resumed with all of them as one prompt through the run's go-on, under the same record,
    /// which stays working; the record concludes only when a run ends with nothing held. A stop, a timeout, or no conversation
    /// id kept ends it, and any words still held are said, never dropped without a trace. The run holds each process it starts
    /// until the record has concluded; what this answers is the last one's end.
    /// </summary>
    /// <param name="ended">The end of the run's process before this, the first's or its retry's.</param>
    /// <param name="left">Where the words a failed run carried or left held are handed back to wait (TURNFAIL2).</param>
    /// <param name="own">Where the harness's own words are kept (AGT3c): each run begins it again, so it says the last run's ending.</param>
    /// <param name="setupChanged">
    /// Asked before the held words go on, whether the setup the record's start ran under changed since (ACCESSSWAP1b, D183's note): each
    /// run here goes on under the start's spawn, so where it did, the words are handed back to <paramref name="left"/> and the record
    /// concludes, its next look going on under the setup standing then. Null never asks.
    /// </param>
    /// <param name="carriesOn">
    /// The look at the quest and its tree a run's end takes (LANDBACK1 part 3, <see cref="SessionWaits.CarriesOn"/>): where nothing is held, a
    /// closed quest with an uncommitted tree resumes the conversation once more in Daoris's own words. Null never carries on.
    /// </param>
    internal async Task<AgentRunEnd> GoOnAsync(
        TransportRun run, DrivenInbox held, AgentDoor adapter, string sessionId, AgentRunEnd ended, ResumeAsk? resume,
        List<ChatMessage> left, CancellationToken ct, HarnessWords? own = null, Func<bool>? setupChanged = null,
        Func<CancellationToken, Task<TurnCarry>>? carriesOn = null)
    {
        var processes = _context.Processes;
        IReadOnlyList<ChatMessage> carried = [];
        var carriedOn = 0;
        while (ended.Exit is not null && !processes.WasStopRequested(sessionId))
        {
            // TURNFAIL2: a run whose turn the harness said failed is not gone on from: the words it carried and those held wait,
            // handed back for the record's say door (WordsLeft), as the protocol door's do (D137's TURNFAIL1 note).
            if (NativeWords.Left(own, ended.Exit, held, carried) is { } waiting)
            {
                left.AddRange(waiting);
                return ended;
            }

            if (held.TakeAllOrClose() is not { Count: > 0 } taken)
            {
                // LANDBACK1 part 3: nothing held, so the turn's end is here. A clean exit whose quest is closed with its tree uncommitted
                // is carried on in Daoris's own words, bounded by the look's own limit; a failed exit is not (Left read it above).
                if (carriesOn is null || ended.Exit != 0) break;
                var ran = await CarryOnNativeAsync(run, held, carriesOn, sessionId, carriedOn, resume, ended, ct).ConfigureAwait(false);
                if (ran is not { } carry) break;
                carriedOn++;
                ended = carry;
                continue;
            }

            // ACCESSSWAP1b: its setup changed while the run worked, so the words go back unjudged, to go on under the new one at the next
            // look rather than in a run spawned under the start's; the look judges Daoris's words on the record before its go-on.
            if (setupChanged?.Invoke() == true)
            {
                left.AddRange([.. taken, .. held.Close()]);
                return ended;
            }

            // COWORK1b (the message-urgency design §4.3): Daoris's words held for the run's end are judged again here, by their key; one
            // whose fact a later submission answered is dropped, said so, and never goes late. None left, the next take closes.
            var words = await DaorisWords.GoingAsync(
                taken, note => _context.Events.Keep(sessionId, note, say: null), ct).ConfigureAwait(false);
            // COWORK1c: a word dropped is settled without the wire, so off its record too.
            Went([.. taken.Except(words)]);
            if (words.Count == 0) continue;
            carried = words;
            // The id its harness named, kept as each run's output ended (ANSWER1a); never read from the account's home.
            if ((_context.Conversations.Read(sessionId)?.Conversation ?? resume?.Conversation) is not { } conversation)
            {
                Went(words);
                Untaken(sessionId, held.Close().Count + words.Count, UnreachedWhy.Unkept);
                return ended;
            }

            // COWORK1c (the message-urgency design §6): each kept word marked attempted on its record before the run that carries it, and
            // carried only once the record names it marked (FOUNDATION1 row p, the design §3.4 rule 5): a word it does not name is left
            // waiting on its record, said, and the run carries the words it does, or none starts.
            var acknowledged = new List<ChatMessage>();
            foreach (var word in words)
            {
                if (word.Attempting is not { } attempting
                    || word.Id is not { } wordId
                    || (await attempting().ConfigureAwait(false)).Contains(wordId, StringComparer.Ordinal))
                {
                    acknowledged.Add(word);
                }
            }

            if (acknowledged.Count < words.Count)
            {
                var unsent = words.Count - acknowledged.Count;
                var note = WordsNotes.Unreached(unsent, "were not sent", UnreachedWhy.Unmarked);
                _context.Events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = note.Note, Parts = note.Parts.Count > 0 ? note.Parts : null }, say: null);
                words = acknowledged;
                carried = words;
                if (words.Count == 0) continue;
            }

            ended = await GoneOnAsync(
                    run, held, sessionId, conversation, words, NativeWords.Prompt(words), NativeWords.Opening(adapter.Id.Value, words), ended, ct)
                .ConfigureAwait(false);
        }

        // Words held when it could go on no more: a stop, a timeout. Said, never dropped without a trace.
        if (held.Close() is { Count: > 0 } unreached) Untaken(sessionId, unreached.Count, UnreachedWhy.EndedFirst);
        return ended;
    }

    /// <summary>
    /// The carry-on a native run's end owes (LANDBACK1 part 3): its harness ends the session's own work with its turn, so where the
    /// quest stands closed with the tree uncommitted the conversation is resumed once more with Daoris's own words as the run's
    /// prompt, as held words are, and the look is taken again at its end. Null where nothing is owed: the look is clean or unread, the
    /// session was stopped, no conversation was kept, or the bound was met, which is said once. No inbox, no move record and no card
    /// takes part; Daoris commits nothing (D164).
    /// </summary>
    private async Task<AgentRunEnd?> CarryOnNativeAsync(
        TransportRun run, DrivenInbox held, Func<CancellationToken, Task<TurnCarry>> carriesOn, string sessionId, int carried, ResumeAsk? resume,
        AgentRunEnd ended, CancellationToken ct)
    {
        TurnCarry look;
        try
        {
            look = await carriesOn(ct).ConfigureAwait(false);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            // A look that cannot be taken owes nothing: the end is as it always was.
            return null;
        }

        if (look is not { Dirty: true } || look.Holds?.Invoke() == false || _context.Processes.WasStopRequested(sessionId)) return null;
        if (carried >= look.Limit)
        {
            KeepNote(sessionId, BackgroundNotes.CarryBound());
            return null;
        }

        if ((_context.Conversations.Read(sessionId)?.Conversation ?? resume?.Conversation) is not { } conversation) return null;
        var note = BackgroundNotes.CarriedOn(look.Paths);
        return await GoneOnAsync(run, held, sessionId, conversation, [], look.Words, NativeWords.Opening(note), ended, ct).ConfigureAwait(false);
    }

    /// <summary>The driver's own sentence by its code, on the console and in the record.</summary>
    private void KeepNote(string sessionId, Noted noted)
    {
        _context.Output?.Append(sessionId, noted.Note);
        _context.Events.Keep(
            sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = noted.Note, Parts = noted.Parts.Count > 0 ? noted.Parts : null }, say: null);
    }

    /// <summary>
    /// One go-on of a native conversation through the run, the held words' and a carry-on's alike: its process through the start gate
    /// (PROCSTART2) and the launcher (TERMTRUST1a), waiting for its place on the loop's token (PARALLEL1f). A driver closing while it
    /// waits starts no process, so the words it took are said never to have reached the session, as a stop's are, and the close ends the
    /// record as it ends any run it cuts off (D104). A go-on whose door reported no usage keeps the last one's.
    /// </summary>
    /// <param name="words">The held words this go-on carries, none for a carry-on, whose <paramref name="prompt"/> is Daoris's own.</param>
    /// <param name="opening">What the record opens the go-on with: the words under their ids, or the carry-on's note.</param>
    private async Task<AgentRunEnd> GoneOnAsync(
        TransportRun run, DrivenInbox held, string sessionId, string conversation, IReadOnlyList<ChatMessage> words, string prompt,
        IReadOnlyList<SessionEvent> opening, AgentRunEnd before, CancellationToken ct)
    {
        var up = false;
        AgentRunEnd ended;
        try
        {
            // The words are the run's input, written to its next process as that process's one prompt (D243): their receipt `sent` is the
            // write's return, and they leave the record then (COWORK1c, the design §7).
            run.Input.Hold(new DoorWord([.. words.Select(word => word.Id).OfType<string>()], prompt, opening, Written: () =>
            {
                up = true;
                foreach (var word in words)
                {
                    if (word.Id is { } id) _context.Events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Receipt, Id = id, Receipt = Receipts.Sent }, say: null);
                }

                Went(words);
            }));
            await run.ResumeAsync(conversation, ct).ConfigureAwait(false);
            ended = await run.EndAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested && !up)
        {
            if (words.Count > 0)
            {
                Went(words);
                Untaken(sessionId, held.Close().Count + words.Count, UnreachedWhy.EndedFirst);
            }

            throw;
        }

        return ended.Usage is null ? ended with { Usage = before.Usage } : ended;
    }

    /// <summary>The driver's line in the record for words the person said that never reached the session, and why.</summary>
    private void Untaken(string sessionId, int count, string why)
    {
        var line = WordsNotes.Unreached(count, "while it worked never reached it", why);
        _context.Output?.Append(sessionId, line.Note);
        _context.Events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts }, say: null);
    }

    /// <summary>Words the run carried, or settled without it (COWORK1c): each kept one leaves its record through its dispatcher.</summary>
    private static void Went(IEnumerable<ChatMessage> words)
    {
        foreach (var word in words) word.Went?.Invoke();
    }
}
