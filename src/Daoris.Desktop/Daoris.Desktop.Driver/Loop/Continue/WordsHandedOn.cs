namespace Daoris.Driver;

/// <summary>The words a session could not go on with, handed to a new one or left with the person.</summary>
internal sealed class WordsHandedOn(LoopContext context, Driver driver)
{
    private readonly LoopContext _context = context;

    private readonly Driver _driver = driver;

    /// <summary>
    /// Where the words cannot go on in their session and nothing carries them on by itself: a closed
    /// quest's session, or a never. The record stays as it ended, its words waiting as said; they are marked by their ids so
    /// a later look leaves them there (<see cref="GoOnMarks"/>), and the session's conversation says why, where the person
    /// wrote them. A new conversation with them is the person's press, never the driver's.
    /// </summary>
    internal StartRun CannotGoOn(QuestView quest, PriorSession record, ContinueReason why)
    {
        var words = record.Waiting.Select(word => word.Id).OfType<string>().ToList();
        _context.Marks.Mark(record.Session, words, why, DateTimeOffset.UtcNow);
        // Another agent's findings among them go to the person instead, unanswered.
        _driver.Deliverer.FindingsToPerson(record, why);
        // A choice of a new session is spent with the words it named: words said later wait for the account again.
        _context.NewSessions.Clear(record.Session);
        // The words' ids and the code beside the line, so the page says why in its own words.
        var note = Continuations.Cannot(words, why);
        _context.Output?.Append(record.Session, note.Text!);
        _context.Events.Keep(record.Session, note, say: null);
        return new StartRun(
            $"cannot  session {record.Session} (#{quest.Id} → {quest.To}): it cannot go on in this session, because {why.Said}.",
            false);
    }

    /// <summary>
    /// A closed quest's session whose own conversation is proven lost goes on by itself as a conversation in its tree:
    /// handed to the shell's chat runner, which opens the new part, hands it the brief and the person's words and takes
    /// them off the record, naming it. Another agent's findings or answer among the words stay, marked, and go to the person,
    /// since the conversation is handed only the person's. Null where nothing went on there, and the session then waits for the person.
    /// </summary>
    internal async Task<StartRun?> GoesOnLostAsync(
        Func<string, ContinueReason, CancellationToken, Task<StartedFrom>> goesOn, QuestView quest, PriorSession record, ContinueReason why,
        CancellationToken ct)
    {
        // The reason `account`: Daoris's own words that state a fact go with the person's; a lost conversation's part is handed
        // the person's alone.
        var switching = why.Code == ContinueWhy.Account;
        bool Carried(SaidWordView word) => switching ? DaorisWords.NewPartCarries(word) : word.Persons;
        if (!record.Waiting.Any(Carried)) return null;
        StartedFrom moved;
        try
        {
            moved = await goesOn(record.Session, why, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or IOException or System.Text.Json.JsonException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return null;
        }

        if (moved is not { SessionId: not null, Sent: true }) return null;

        // A choice of a new session is spent with the words it named, which went now.
        _context.NewSessions.Clear(record.Session);
        if (record.Waiting.Any(word => !Carried(word)))
        {
            _context.Marks.Mark(record.Session, record.Waiting.Where(word => !Carried(word)).Select(word => word.Id).OfType<string>(), why, DateTimeOffset.UtcNow);
            _driver.Deliverer.FindingsToPerson(record, why);
        }

        return new StartRun($"moved   session {record.Session} (#{quest.Id} → {quest.To}): {moved.Message}", false);
    }

    /// <summary>
    /// The words a session could not go on with went to a new one: taken off the record they were said to
    /// by their ids, naming the session that took them, so the service keeps those said after it ended on the ask as
    /// <c>reopened</c>; and, for a record that had ended, said in its conversation where the person wrote them. A park's
    /// answer is said beneath its question. A refusal costs a line, never the start.
    /// </summary>
    /// <param name="resumed">
    /// A resume's: the session opened anyway, so Daoris's own words go on in it too (<see cref="HandedToResume"/>).
    /// </param>
    /// <param name="switched">
    /// A carry-on the roster's switch made off a cooling own account: Daoris's own words that state a fact go on in it too
    /// (<see cref="DaorisWords.NewPartCarries"/>), as the planner goes on with their record already.
    /// </param>
    /// <param name="opened">
    /// The new session's keyed open, where it moved the words onto it in its own step: they are delivered from there
    /// (<see cref="HandedMovedAsync"/>), never taken off <paramref name="record"/> by the ids it is read with. Null for an open that moved
    /// nothing, against a host whose <c>opens</c> mark is below 2 or that keeps none, which takes them off the record as before.
    /// </param>
    /// <param name="delivery">
    /// The carry-on's move record, where it keeps one and its fence is written: told the moved words it hands and the take of
    /// them, which the door's write of the prompt carrying them makes after it writes their <c>sent</c>; the move then closes
    /// <c>delivered</c>. Null for a start that keeps none, a hand-over's among them, and the take is made here, before the spawn.
    /// </param>
    /// <returns>The words the open moved and the new session is handed, as it holds them; null where they were taken off the record as before.</returns>
    internal async Task<IReadOnlyList<SaidWordView>?> HandedOnAsync(
        PriorSession record, string to, ContinueReason why, CancellationToken ct, bool resumed = false, bool switched = false,
        DestinationOpen? opened = null, CarryOnRecord? delivery = null)
    {
        if (MovedWords.Moving(opened)) return await HandedMovedAsync(record, to, why, opened!, ct, resumed, switched, delivery).ConfigureAwait(false);
        if (!record.WordsWaiting) return null;

        var output = _context.Output;
        var handed = Hands(resumed, switched);
        StaysWithPerson(record, why, handed);

        var ids = record.Waiting.Where(handed).Select(word => word.Id).OfType<string>().ToList();
        if (!record.Parked)
        {
            // The words' ids, the session and the code beside the line: the page links where they went.
            var note = Continuations.Went(ids, to, why);
            output?.Append(record.Session, note.Text!);
            _context.Events.Keep(record.Session, note, say: null);
        }

        if (ids.Count == 0) return null;
        try
        {
            var (taken, message) = await _context.Service.TakenAsync(record.Session, ids, by: to, ct).ConfigureAwait(false);
            if (!taken) output?.Append(to, $"— the words handed to this session could not be taken off session `{record.Session}`: {message}");
        }
        catch (Exception error) when (error is HttpRequestException or DriverException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            output?.Append(to, $"— the words handed to this session could not be taken off session `{record.Session}`: {error.Message}");
        }

        return null;
    }

    /// <summary>
    /// Which of a record's waiting words a new session is handed: the person's; a resume's, Daoris's too
    /// (<see cref="HandedToResume"/>); a carry-on the roster's switch made, Daoris's that state a fact with them
    /// (<see cref="DaorisWords.NewPartCarries"/>); and in each, a helper's answer it never took (<see cref="HelperAnswersCarried"/>). What
    /// the new session's keyed open names to move, and what it takes off the old record where the open moved nothing.
    /// </summary>
    internal static Func<SaidWordView, bool> Hands(bool resumed, bool switched)
    {
        Func<SaidWordView, bool> own = switched ? DaorisWords.NewPartCarries : resumed ? DriverContinuations.HandedToResume : word => word.Persons;
        return word => own(word) || HelperAnswersCarried.Is(word);
    }

    /// <summary>
    /// The words a new session is not handed stay on the record they were said to: another agent's findings would reach
    /// it as the person's, which would make them requirements, so they are marked as words that cannot go on there and go to the person
    /// instead. So do Daoris's own words: a message of Daoris's never starts a new session, bar
    /// a resume's, which starts whatever they say. And a choice of a new session is spent with the words it named.
    /// </summary>
    private void StaysWithPerson(PriorSession record, ContinueReason why, Func<SaidWordView, bool> handed)
    {
        _context.NewSessions.Clear(record.Session);
        if (!record.Waiting.Any(word => !handed(word))) return;
        _context.Marks.Mark(record.Session, record.Waiting.Where(word => !handed(word)).Select(word => word.Id).OfType<string>(), why, DateTimeOffset.UtcNow);
        _driver.Deliverer.FindingsToPerson(record, why);
    }

    /// <summary>
    /// The words a new session's keyed open moved onto it, delivered from there: read
    /// on the new session by their ids in the transfer's order, each word's files copied first from the record they left into its folder;
    /// each shown there by its own <c>User</c> event under its id, before its run is written, the display's and never delivery's
    /// evidence; then taken off it by those ids, so the service keeps the person's that reopened the work on the ask, said to the new session,
    /// where its instruction reads them, exactly as a take of the old record's words naming it kept them, and no dispatcher of the new session's
    /// hands them again. The old record says where they went by the ids it held. A word the open did not move, another agent's claim or Daoris's
    /// own, stays there with the person as before. Where the new session's record does not read, nothing is handed or taken and the moved words
    /// wait on it, for its own go-on.
    /// </summary>
    private async Task<IReadOnlyList<SaidWordView>> HandedMovedAsync(
        PriorSession record, string to, ContinueReason why, DestinationOpen opened, CancellationToken ct, bool resumed, bool switched,
        CarryOnRecord? delivery)
    {
        var output = _context.Output;
        StaysWithPerson(record, why, Hands(resumed, switched));
        var moved = await MovedWords.OnAsync(_context.Service, _context.Home, record.Session, to, opened.Moved, ct).ConfigureAwait(false);
        if (moved is null)
        {
            output?.Append(to, "— the words moved to this session could not be read here, so they wait on it.");
            // The prompt carries none of them, so none is `sent`: the move ends by its phase now, as before the door's write.
            if (delivery is not null) await delivery.EndAsync().ConfigureAwait(false);
            return [];
        }

        // Each moved word is marked attempted on the new
        // part at the fence, before the door writes it, so a loop killed from here on never lets the part's own go-on send it again; the come-up
        // reads it attempted and settles it uncertain. The mark is the hand's, not the write's: a kill after it and before the write leaves a word
        // never written read uncertain too, the conservative side, as a live move's is. A word the mark does not name is not handed and waits on
        // the part for its own go-on. A start keeping no record takes the words before its instruction, so it marks none.
        if (delivery is not null) moved = await moved.AttemptedAsync(_context.Service).ConfigureAwait(false);

        // A word the instruction hands by itself under its id (the target's `Handed`: a resume's words, the switch's Daoris words) its opening
        // shows there already, after the instruction (HarnessCapture.Opening); every other moved word is shown here, once. The person's words of
        // any other carry-on reach the instruction through the ask instead.
        Func<SaidWordView, bool> instructed = resumed ? DriverContinuations.HandedToResume : word => switched && word.Daoris && DaorisWords.NewPartCarries(word);
        foreach (var word in moved.Words.Where(word => !instructed(word))) _context.Events.Keep(to, MovedWords.Of(word), say: null);
        if (!record.Parked && moved.Left.Count > 0)
        {
            // The words' ids, the session and the code beside the line: the page links where they went.
            var note = Continuations.Went(moved.Left, to, why);
            output?.Append(record.Session, note.Text!);
            _context.Events.Keep(record.Session, note, say: null);
        }

        if (delivery is null)
        {
            if (!await moved.TakeAsync(_context.Service, ct).ConfigureAwait(false))
            {
                output?.Append(to, "— the words moved to this session could not be taken off it, so they still wait on it.");
            }
        }
        else
        {
            // The take waits for the door's write of the prompt that carries the words, which is where each has `sent`;
            // a run that never writes it ends the record by its phase and the words wait on the new part, none taken.
            delivery.Handed(
                [.. opened.Moved.Where(word => moved.Ids.Contains(word.Id, StringComparer.Ordinal)).Select(word => word.Transfer)],
                async () =>
                {
                    if (!await moved.TakeAsync(_context.Service, CancellationToken.None).ConfigureAwait(false))
                    {
                        output?.Append(to, "— the words moved to this session could not be taken off it, so they still wait on it.");
                    }
                });
        }

        return moved.Words;
    }
}
