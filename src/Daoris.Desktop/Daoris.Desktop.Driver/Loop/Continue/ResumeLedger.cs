namespace Daoris.Driver;

/// <summary>The ledger's side of a resume: the held saying, the record put back, not handed or unstarted, the park ended, and the machine log's line for words taken up.</summary>
internal sealed class ResumeLedger(LoopContext context)
{
    private readonly LoopContext _context = context;

    /// <summary>
    /// The record's conversation says why its words wait, once for each wait: a look that finds it saying
    /// the same as its last event writes nothing more, and a word said since is answered again. The person wrote to it, so it
    /// stays in view while it waits. The words stay waiting and unmarked: the reset lets them go on. Its line is
    /// <see cref="ResumeWords.NotYetOn"/>'s, by code; the last event is compared by its English. A start
    /// held because the record's tree is gone and cannot be made again says so the same way.
    /// </summary>
    internal void HeldSaying(PriorSession record, Noted line)
    {
        try
        {
            if (_context.Events.Page(record.Session, limit: 1).Events is [{ Kind: SessionEventKind.Note, Text: var last }] && last == line.Note) return;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            // A record that does not read is said again: one line more, never none.
        }

        if (!record.Parked) new ConversationArchive(_context.Home).Show(record);
        _context.Output?.Append(record.Session, line.Note);
        _context.Events.Keep(
            record.Session,
            new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts.Count > 0 ? line.Parts : null },
            say: null);
    }

    /// <summary>
    /// The record the move to working took up goes back where no run of it started and nothing went, as
    /// <see cref="Continuations.PutBack"/> says: every word taken back first, or the driver closed first. Nothing is
    /// sent and nothing holds it. Where the ledger will not move it back, its refusal holds the start, and the next look reads the
    /// record as it now is. A run that could not start is <see cref="UnstartedAsync"/>'s.
    /// </summary>
    /// <param name="unsent">
    /// The look's line: every word taken back first, unless the driver closed before its run started
    /// (<see cref="ClosedFirst"/>), its words still waiting.
    /// </param>
    internal async Task<(StartRun? Run, ContinueReason? FellBack, bool AnswerKept)> PutBackAsync(
        QuestView quest, PriorSession park, StartRun? unsent = null)
    {
        var (state, note, interrupted, limit) = Continuations.PutBack(park);
        try
        {
            // Never cancelled: a record left working with no run is the orphan sweep's to end, its words with it.
            await _context.Service.AdvanceAsync(park.Session, state, note, ct: CancellationToken.None, interrupted: interrupted, limit: limit)
                .ConfigureAwait(false);
        }
        catch (Exception refused) when (refused is DriverException or HttpRequestException)
        {
            return (new StartRun(
                $"held  #{quest.Id} → {quest.To}: session {park.Session} could not be put back as it was: {refused.Message}",
                false, null, refused.Message), null, false);
        }

        return (unsent ?? TakenBackFirst(quest, park.Session), null, false);
    }

    /// <summary>
    /// An admitted supervisor's word that is not handed: the decision moves to
    /// <c>not-handed</c>, the park is restored by the ledger's <c>RestoreParkAsync</c> keeping every word but that one (never
    /// <see cref="PutBackAsync"/>, whose move to a park clears them), and its question is written back from the decision's snapshot with
    /// the same id, since the run's opening may have cleared it. Where the restore fails the record is left working with no run, for the
    /// orphan sweep, and the word, no longer handable, is taken back by the first look after.
    /// </summary>
    internal async Task<(StartRun? Run, ContinueReason? FellBack, bool AnswerKept)> NotHandedAsync(
        QuestView quest, PriorSession park, SupervisorTicket ticket, string why)
    {
        var question = _context.Supervisor.LiveOf(park.Session).FirstOrDefault(each => each.Id == ticket.Decision)?.Question;
        await _context.Supervisor.NotHandedAsync(ticket, why, DateTimeOffset.UtcNow, CancellationToken.None).ConfigureAwait(false);
        try
        {
            // Never cancelled: a record left working with no run is the orphan sweep's to end.
            await _context.Service.RestoreParkAsync(
                park.Session, [ticket.Word], park.Note is { } note ? new Noted(note, park.NoteParts ?? []) : null, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception refused) when (refused is DriverException or HttpRequestException)
        {
            return (new StartRun(
                $"held  #{quest.Id} → {quest.To}: the supervisor's answer was not handed, and session {park.Session} could not be restored: {refused.Message}",
                false, null, refused.Message), null, false);
        }

        if (question is not null) new QuestionFiles(_context.Home).Restore(question);
        const string said = "the supervisor's answer was not handed: the park waits on the owner again, with its other words.";
        return (new StartRun($"held  #{quest.Id} → {quest.To}: session {park.Session}: {said}", false, null, said), null, false);
    }

    /// <summary>
    /// A resumed run that could not start: a start failure, never a move.
    /// The record the move to working took up ends <c>failed</c> with the spawn's words, as <see cref="Continuations.Unstarted"/> says,
    /// so the strikes count it as they count a first start's failed spawn, and the words wait on it to go again; failing twice
    /// running they are marked and wait for the person, as a failed turn's are. Its conversation says so beside them, by
    /// their ids. No new session opens and nothing is handed on.
    /// </summary>
    /// <param name="ids">The ids of the words that are to go, said beside the line.</param>
    internal async Task<(StartRun? Run, ContinueReason? FellBack, bool AnswerKept)> UnstartedAsync(
        QuestView quest, PriorSession park, string agent, string failure, IReadOnlyList<string> ids)
    {
        var (note, words) = Continuations.Unstarted(park, failure);
        try
        {
            // Never cancelled: a record left working with no run is the orphan sweep's to end, its words with it.
            await _context.Service.AdvanceAsync(park.Session, "failed", note, ct: CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception refused) when (refused is DriverException or HttpRequestException)
        {
            return (new StartRun(
                $"held  #{quest.Id} → {quest.To}: session {park.Session} could not start, and its record would not end failed: {refused.Message}",
                false, null, refused.Message), null, false);
        }

        if (words == WordsAfter.Held) _context.Marks.Mark(park.Session, ids, GoOnMarks.FailedTwice, DateTimeOffset.UtcNow);
        if (FailedTurnWords.Line(words) is { } waits)
        {
            _context.Output?.Append(park.Session, $"— its run could not start: {failure}");
            _context.Events.Keep(
                park.Session,
                new SessionEvent { Kind = SessionEventKind.Note, Text = waits.Note, Parts = waits.Parts, Words = ids.Count > 0 ? ids : null },
                say: null);
        }

        return (new StartRun(
            $"failed  session {park.Session} (#{quest.Id} → {quest.To}): its run could not start: {failure}",
            false,
            new SessionEnded(park.Session, quest.To, "failed", ByPerson: false, failure, Quest: quest.Id, Adapter: agent)), null, false);
    }

    /// <summary>The look's line where every word waiting on the record is taken back before it goes on.</summary>
    internal static StartRun TakenBackFirst(QuestView quest, string session) => new(
        $"held  #{quest.Id} → {quest.To}: every word waiting on session {session} was taken back before it went on; nothing is sent.",
        false);

    /// <summary>
    /// The look's line where the driver closed before a record's run started: put back as it ended, its words waiting on
    /// it for the next start, which goes on with them.
    /// </summary>
    internal static StartRun ClosedFirst(QuestView quest, string session) => new(
        $"held  #{quest.Id} → {quest.To}: the driver closed before session {session}'s run started; it is put back as it ended, "
        + "its words waiting for the next start.",
        false);

    /// <summary>The record as it stands now, or null where it cannot be read: the look's words are then what there is.</summary>
    internal static async Task<PriorSession?> ReadAgainAsync(ServiceClient service, string sessionId, CancellationToken ct)
    {
        try
        {
            return await service.RecordAsync(sessionId, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException or System.Text.Json.JsonException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            return null;
        }
    }

    /// <summary>
    /// End the park a fallback leaves behind, keeping what it asked and saying why, and write the line. A record
    /// that has already ended moves no further: its words wait on it until they are handed on. Null when done; else
    /// the service's refusal.
    /// </summary>
    internal async Task<string?> EndParkAsync(PriorSession park, ContinueReason why, string agent, CancellationToken ct)
    {
        var service = _context.Service;
        if (park.AnsweredPark)
        {
            try
            {
                await service.AdvanceAsync(park.Session, "completed", Continuations.EndedNote(park, why), ct: ct).ConfigureAwait(false);
            }
            catch (DriverException refused)
            {
                // The person stopped or answered it again between the look and here: the next look reads it as it now is.
                return refused.Message;
            }
        }

        service.AccountSaid(Took(park, agent, why));
        return null;
    }

    /// <summary>
    /// The machine log's line for words taken up: a park's answer is <c>session.answered</c>, and words to
    /// a record that has ended are <c>session.reopened</c>, from the state it ended in, naming the door the first
    /// of them is said at, as the record showed them.
    /// </summary>
    internal AccountLine Took(PriorSession record, string adapter, ContinueReason? why) =>
        record.Parked || !record.WordsWaiting
            ? Continuations.Answered(record.Session, adapter, why)
            : Continuations.Reopened(
                record.Session, adapter, record.State, why,
                _context.Events.DoorOf(record.Session, [.. record.Waiting.Select(word => word.Id).OfType<string>()]));
}
