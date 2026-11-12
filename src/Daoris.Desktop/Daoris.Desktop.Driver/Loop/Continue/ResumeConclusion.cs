namespace Daoris.Driver;

/// <summary>How a resumed run ends: its conclusion, and the account its refused open reads.</summary>
internal sealed class ResumeConclusion(LoopContext context, Driver driver)
{
    private readonly LoopContext _context = context;

    private readonly Driver _driver = driver;

    private readonly ResumeLedger _ledger = new(context);

    private readonly ContinuationLimits _limits = new(context);

    /// <summary>
    /// A resume's open refused with a limit or an access failure, read by the transient retry's reader: its account's policy as a failed
    /// run's conclusion applies it, read from the refusal's own words. A limit cools its account,
    /// by its sentence or, read by its code alone, for the unstated hour; an access failure marks its account as the reader reads it:
    /// refused by a credential's refusal, the adapter's words or its owner's (the reader closes <c>AccountRefused</c>'s missing owner's
    /// fallback), and signed out by a sign-in's words or an access code. The record's own end stays the refused resume's.
    /// </summary>
    private void OpeningAccount(AgentError opening, AgentDoor adapter, HarnessSelection selection, string sessionId)
    {
        var words = opening.Failure.Words is { Length: > 0 } said ? said : opening.By;
        if (opening.Kind == AgentErrorKind.Limit)
        {
            _driver.AccountLimited(
                SessionConclusion.Of("failed", DoorNotes.Failed(words)), adapter, selection, words, sessionId, used: null, byCode: opening.Limit);
            return;
        }

        switch (opening.Access)
        {
            case AccessDisposition.Refused:
                _driver.RefuseAccount(adapter, selection, context.Harnesses.Adapters.OwnerOfAgent(adapter.Id.Value).Value);
                return;
            case AccessDisposition.SignedOutByWords:
                _driver.AccountSignedOut(SessionConclusion.Of("failed", DoorNotes.Failed(words)), adapter, selection, words);
                return;
            default:
                // Read by a code the door declares (`AuthRequiredCode`, an access kind or status): its sign-in is what it says ended.
                _context.Harnesses.SignedOut(adapter.Id.Value, selection.Profile);
                return;
        }
    }

    /// <summary>
    /// The resumed run's conclusion: from the exit code and the quest's own state as any start's, its evidence
    /// counted from the record's own base so the review's range is the whole session's — or, where the door said the
    /// agent would not resume, the record ended with the reason and the words left to be handed on.
    /// </summary>
    /// <remarks>
    /// <para><b>A record on a closed quest</b> ends as its process does: in the state it had before it
    /// went on when it exits cleanly, <c>failed</c> otherwise. Its quest does not move, and it cannot park, holding none.</para>
    ///
    /// <para><b>The words leave the record by their ids</b>, once they went and before the conclusion moves it, so a park the
    /// conclusion makes, which clears what waits, never drops one the session took without keeping it on the ask.</para>
    /// </remarks>

    internal async Task<(StartRun? Run, ContinueReason? FellBack)> ConcludeResumedAsync(
        QuestView quest, PriorSession park, AgentDoor adapter, HarnessSelection selection, ResumeAsk resume,
        string workTree, string transcript, string? before, int? exitCode, AcpUsage? used, string? turnFailed, HarnessEnding ended,
        DateTimeOffset openedAt, CancellationToken ct)
    {
        var service = _context.Service;
        var config = _context.Config;
        var sessionId = park.Session;

        // 🔴 Its own account refused its asked model at the open, before any prompt.
        // The record holds work and the person's words, so it is never ended `choice-not-offered`: it is put back as it ended, which leaves
        // it not running and concludes nothing, so no strike can be read; its count is given back once its process stopped; and the next
        // look's resume judges the reading kept, carrying its words to a new record on another account of the same choice.
        if (ended.NotOffered is not null && _context.Processes.StopMade(sessionId) is null)
        {
            _context.Harnesses.Release(selection.Number);
            var (put, fellBack, _) = await _ledger.PutBackAsync(quest, park).ConfigureAwait(false);
            return (put, fellBack);
        }

        // 🔴 The native door's harness ended on a failed exit before naming its conversation, once more after its one retry as the same
        // resume (HoldAsync tried it): it never opened it, which a crash, a failed sign-in or a flag it
        // rejected ends the same way, so it is `exited`, never `gone`, and the record carries on as any refused resume's does. Read from
        // the structure, never from its words.
        if (resume.Refused is null && adapter.Can.ResumesByRestart && !resume.Named && exitCode is int failed && failed != 0)
        {
            resume.Refused = ContinueWhy.Of(ContinueWhy.Exited);
        }

        var after = await service.FindQuestAsync(quest.Id, ct).ConfigureAwait(false);
        var status = after?.Status ?? "Open";
        // 🔴 Whether its quest had closed, as the look planned the run: never as the run left it, since a resumed run closes
        // its own quest, and read after it an answered park that finished its work would take a closed quest's ending.
        var closed = quest.Status is not ("Open" or "Taken");

        // 🔴 An open refused with a limit or an access failure after a transient one is wrapped as
        // a refused resume, whose fallback below would come first and read no account. The typed reading keeps its account's policy, applied as
        // a failed run's conclusion applies it, before the fallback is chosen; the person's words still go back unprompted.
        if (resume.Refused is not null && _context.Processes.StopMade(sessionId) is null && ended.Transient?.Last is
                { Failure.Where: DoorWhere.Open, Kind: AgentErrorKind.Limit or AgentErrorKind.Access } opening)
        {
            OpeningAccount(opening, adapter, selection, sessionId);
        }

        if (resume.Refused is { } refused)
        {
            // Back to how it ended: an ended record goes on only with words, and these never went. A park ends
            // completed, as an answer it could not take ends it. Where another client holds its
            // conversation it waits for the person (the caller marks its words), so its note says it cannot go on, as a closed quest's.
            var held = closed || refused.Holds;
            // A resume refused parks nothing, so no question is kept for it.
            _driver.Questions(
                sessionId, new QuestionFiles(_context.Home).SettleRun(sessionId, park.Parked ? "completed" : park.State, openedAt, _context.Events));
            await service.AdvanceAsync(
                    sessionId, park.Parked ? "completed" : park.State,
                    held ? Continuations.CannotNote(park, refused)
                        : park.Parked ? Continuations.EndedNote(park, refused)
                        : Continuations.WentNote(park, refused),
                    ct: ct, interrupted: !park.Parked && park.Interrupted, limit: !park.Parked && park.Limit)
                .ConfigureAwait(false);
            service.AccountSaid(_ledger.Took(park, adapter.Id.Value, refused));
            return (null, refused);
        }

        if (used is not null)
        {
            _context.Usage?.Record(new UsageEntry(sessionId, quest.To, adapter.Id.Value, selection.Profile, used.Used, used.Size, DateTimeOffset.UtcNow));
        }

        // Another agent's findings it took are answered as its turn ends, read before the record moves,
        // with each fix's commit read from its tree as it stands now; one it left unanswered is unresolved.
        await _driver.Deliverer.OpinionsAnsweredAsync(resume, sessionId, workTree, ct).ConfigureAwait(false);

        // A stop made of it is its end, a pause's naming the pause, as a first run's is (one reading).
        var stop = _context.Processes.StopMade(sessionId);
        // A clean end holding its quest, on its agent's own open helper ask and no question of the person's, waits on that
        // helper, as a first run's does. A closed quest's session holds none, and never waits.
        var awaitsHelper = stop is null && exitCode == 0 && !closed && status == "Taken"
                           && await _driver.WaitsOnOwnHelperAsync(sessionId, quest, openedAt, ct).ConfigureAwait(false);
        var awaitsCheck = stop is null && exitCode == 0 && !closed && status == "Taken" && !awaitsHelper
                          && await _driver.WaitsOnOwnCheckAsync(sessionId, openedAt, ct).ConfigureAwait(false);
        var conclusion = stop is not null
            ? stop.Conclusion
            : exitCode is int code
                // As any start's where its quest is open or taken as the look planned it, so a park whose run closes its
                // quest ends completed; a closed quest's session ends as its process does. It carried on a take this
                // machine already held, so ending with it still taken is a park, not a stand-down.
                ? Observation.Resumed(
                    code, park.State, quest.Status, status, quest.Awaits, after?.Awaits, turnFailed,
                    took: !closed && status == "Taken" && await service.TookAsync(sessionId, ct).ConfigureAwait(false),
                    // A refused turn quotes its provider's words whether or not its quest had closed.
                    lastWords: closed && !ended.ProviderRefused ? null : SessionLastWords.ParkedWords(_context.Events, sessionId, transcript),
                    // The native door's harness ended its own background work with its turn.
                    backgroundCut: ended.BackgroundCut,
                    // A closure graph's submission is its done, as at a first run's end.
                    submitted: !closed && QuestResults.Submitted(after),
                    awaitsHelper: awaitsHelper, awaitsCheck: awaitsCheck,
                    // Its provider refused its turn, a failed run and never a park, as at a first run's end.
                    providerRefused: ended.ProviderRefused,
                    reopenedFrom: Planner.ReopenedFrom(quest, after))
                // A done this run reached outranks the timeout; a quest closed before it went on is not this run's.
                : _driver.Watch.EndedMinutes(sessionId) is { } quietMinutes
                    ? Observation.AfterQuiet(quietMinutes, quest.Status, status)
                    : Observation.AfterTimeout(config.TestBound ?? 0, quest.Status, status);
        // A park whose turn its agent cut asked the person nothing, and says so, as a first run's does.
        if (stop is null) conclusion = Observation.Cut(conclusion, ended.Cut);

        // A run Daoris ended at its turn's end for a changed setup is neither a park nor a failure, and goes on again.
        var swappedOn = stop is null && ended.SetupChanged is not null;
        if (swappedOn) conclusion = Observation.SetupChanged(conclusion, ended.SetupChanged!);

        // Past its transient bound a failed end is `ended.transient`, no strike and no cool-off, its quest held
        // for the person; Daoris's go-on comes off by its key where its take-off did not land, as at a first run's end.
        if (stop is null && ended.Transient?.Episode is { Exhausted: true } exhausted && conclusion.State == "failed")
        {
            conclusion = Observation.Transient(exhausted);
        }

        var episode = ended.Transient?.Opened == true;
        if (episode) await TransientRetries.GoOnOffAsync(service, sessionId, ct).ConfigureAwait(false);

        // 🔴 Nothing prompted: an open that failed past its bound sent none of
        // the resume's words. Daoris's go-on comes off by its id whatever `words` reads below; the person's carried words stay on the record
        // unmarked, and the hold's word rule holds them, since they were said before this end, until a word of theirs said after it.
        if (stop is null && !resume.Prompted && ended.Transient?.Episode is { Exhausted: true, AtOpen: true })
        {
            await _limits.TakeLimitWordsOffAsync(sessionId, LimitResume.WordIds(resume.Words), ct).ConfigureAwait(false);
        }

        conclusion = _driver.AccountRefused(conclusion, adapter, selection, ended);
        conclusion = _driver.AccountSignedOut(conclusion, adapter, selection, turnFailed, ended);
        // A pipe go-on run whose door refused its asked model on a silent turn keeps the account's reading and
        // ends with its line, no strike; its words wait for the next look, whose resume judges the reading and carries them elsewhere.
        conclusion = _driver.ModelRefused(conclusion, adapter.Id.Value, selection, park.Asked?.NamedModel, ended, sessionId);
        (conclusion, var limited) = _driver.AccountLimited(
            conclusion, adapter, selection, turnFailed, sessionId, used,
            byCode: ended.Transient?.Last is { Kind: AgentErrorKind.Limit, Limit: { } coded } ? coded : null);

        // 🔴 After an episode the resume's words went on the wire in this run, so they are
        // never sent again, whatever kind ends it: taken, or unacknowledged where its first prompt met the error before any update of its
        // turn, said then by the `lost` note. A failed turn's and a limit's words are handed only while still held, which a resume's never are.
        if (episode && stop is null && resume.Prompted && ended.Transient!.OpeningUnacknowledged && resume.Ids.Count > 0)
        {
            _context.Events.Keep(
                sessionId,
                new SessionEvent { Kind = SessionEventKind.Note, Text = ChatTurns.LostLine, Words = [.. resume.Ids], Code = SessionEventCodes.Lost },
                say: null);
        }

        // 🔴 Words a failed turn carried were never answered. They stay waiting on the record, unmarked, as a cooling
        // account's do, and its note and conversation say why; failing a second time running, they wait for the
        // person (FailedTurnWords). Every other run took them. A limited end's always wait.
        var words = stop is null && resume.Prompted && resume.Ids.Count > 0 && !episode
            ? FailedTurnWords.Judge(conclusion, turnFailed, ended, park, limited)
            : WordsAfter.Taken;
        // Daoris's limit word comes off at every conclusion, so it never reaches a
        // later attempt, a fallback or a new part; the rule that the words wait is for the person's, and with none of theirs nothing waits.
        var limitWords = words == WordsAfter.Taken ? [] : LimitResume.WordIds(resume.Words);
        await _limits.TakeLimitWordsOffAsync(sessionId, limitWords, ct).ConfigureAwait(false);
        IReadOnlyList<string> waiting = [.. resume.Ids.Except(limitWords, StringComparer.Ordinal)];
        if (waiting.Count == 0 && limitWords.Count > 0) words = WordsAfter.Taken;
        else if (FailedTurnWords.Line(words) is { } waits)
        {
            conclusion = conclusion.Then("\n\n", waits);
            if (words == WordsAfter.Held) _context.Marks.Mark(sessionId, waiting, GoOnMarks.FailedTwice, DateTimeOffset.UtcNow);
            _context.Events.Keep(
                sessionId,
                new SessionEvent { Kind = SessionEventKind.Note, Text = waits.Note, Parts = waits.Parts, Words = waiting.Count > 0 ? waiting : null },
                say: null);
        }
        else if (resume.Prompted && resume.Ids.Count > 0)
        {
            // The words went: off the record by their ids, before the conclusion moves it.
            var (taken, message) = await service.TakenAsync(sessionId, resume.Ids, by: null, ct).ConfigureAwait(false);
            if (!taken)
            {
                var untaken = WordsNotes.NotTaken(message);
                _context.Events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = untaken.Note, Parts = untaken.Parts }, say: null);
            }

            // Its own account carried them, so a choice of a new session made while it cooled is spent with them.
            _context.NewSessions.Clear(sessionId);
        }

        var evidence = await WorkingTree.CommitsSinceAsync(workTree, park.BaseCommit ?? before, ct).ConfigureAwait(false);
        // Its question kept only for the park this resumed run ends in, asked in its last turn.
        _driver.Questions(sessionId, new QuestionFiles(_context.Home).SettleRun(sessionId, conclusion.State, openedAt, _context.Events));
        await service.AdvanceAsync(
                sessionId, conclusion.State, note: conclusion.Note, evidence: evidence, ct: ct, limit: limited, parts: conclusion.Parts)
            .ConfigureAwait(false);
        service.AccountSaid(_ledger.Took(park, adapter.Id.Value, why: null));

        // A set-up step that went on with the person's not yet says a new set-up, posted with
        // the commit its tree holds now, its correction among it.
        await ReviewSetUps.PostAsync(service, config, _context.Events, after, sessionId, workTree, quest.Workspace, openedAt, ct, _driver.Reviews)
            .ConfigureAwait(false);

        await _driver.ConcludedForOpinionAsync(sessionId, quest, QuestResults.Status(after) ?? status, conclusion.State, workTree, quest.Workspace, ct)
            .ConfigureAwait(false);

        return (new StartRun(
            $"{conclusion.State}  session {sessionId} (#{quest.Id} → {quest.To}) [resumed its conversation]: {conclusion.Note}",
            true,
            // A session waiting on its own helper or check is no ending, and keeps its tree, as a park does.
            SessionStates.IsParked(conclusion.State) || swappedOn || HelperWaits.Ended(conclusion.Parts) || CheckWaits.Ended(conclusion.Parts)
                ? null
                : new SessionEnded(
                    sessionId, quest.To, conclusion.State,
                    ByPerson: stop is { ByPerson: true }, conclusion.Note,
                    Quest: quest.Id, Adapter: adapter.Id.Value, Account: selection.Profile) { Limit = limited, Reason = SessionEnded.ReasonOf(conclusion.Parts) }), null);
    }
}
