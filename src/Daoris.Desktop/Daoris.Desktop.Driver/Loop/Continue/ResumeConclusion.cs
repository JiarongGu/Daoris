namespace Daoris.Driver;

/// <summary>How a resumed run ends: its conclusion, and the account its refused open reads.</summary>
internal sealed class ResumeConclusion(LoopContext context, Driver driver)
{
    private readonly LoopContext _context = context;

    private readonly Driver _driver = driver;

    private readonly ResumeLedger _ledger = new(context);

    private readonly ContinuationLimits _limits = new(context);

    /// <summary>
    /// A resume's open refused with a limit or an access failure, read by the transient retry's reader (Codex's owed read of D210, finding
    /// 6): its account's policy as a failed run's conclusion applies it, read from the refusal's own words. A limit cools its account,
    /// by its sentence or, read by its code alone, for D125's unstated hour; an access failure marks its account as the reader read it:
    /// refused by a credential's refusal, the adapter's words or its owner's (the reader closes <c>AccountRefused</c>'s missing owner's
    /// fallback, the design §1.2), and signed out by a sign-in's words or an access code. The record's own end stays the refused resume's.
    /// </summary>
    private void OpeningAccount(AgentError opening, ISessionAdapter adapter, HarnessSelection selection, string sessionId)
    {
        var words = opening.Failure.Words is { Length: > 0 } said ? said : opening.By;
        if (opening.Kind == AgentErrorKind.Limit)
        {
            _driver.AccountLimited(
                SessionConclusion.Of("failed", DoorNotes.Failed(words)), adapter, selection, words, sessionId, used: null, byCode: opening.Limit);
            return;
        }

        if (opening.By.EndsWith(":words:refused", StringComparison.Ordinal))
        {
            _driver.RefuseAccount(adapter, selection, context.Harnesses.Adapters.OwnerOfAgent(adapter.Name).Value);
            return;
        }

        if (opening.By.EndsWith(":words:sign-in", StringComparison.Ordinal))
        {
            _driver.AccountSignedOut(SessionConclusion.Of("failed", DoorNotes.Failed(words)), adapter, selection, words);
            return;
        }

        // Read by a code the door declares (`AuthRequiredCode`, an access kind or status): its sign-in is what it says ended.
        _context.Harnesses.SignedOut(adapter.Name, selection.Profile);
    }

    /// <summary>
    /// The resumed run's conclusion: from the exit code and the quest's own state (D46 §4) as any start's, its evidence
    /// counted from the record's own base so the review's range is the whole session's — or, where the door said the
    /// agent would not resume, the record ended with the reason and the words left to be handed on.
    /// </summary>
    /// <remarks>
    /// <para><b>A record on a closed quest</b> (MSG1b, D137 §2.3) ends as its process does: in the state it had before it
    /// went on when it exits cleanly, <c>failed</c> otherwise. Its quest does not move, and it cannot park, holding none.</para>
    ///
    /// <para><b>The words leave the record by their ids</b>, once they went and before the conclusion moves it, so a park the
    /// conclusion makes, which clears what waits, never drops one the session took without keeping it on the ask.</para>
    /// </remarks>

    internal async Task<(StartRun? Run, ContinueReason? FellBack)> ConcludeResumedAsync(
        QuestView quest, PriorSession park, ISessionAdapter adapter, HarnessSelection selection, ResumeAsk resume,
        string workTree, string transcript, string? before, int? exitCode, AcpUsage? used, string? turnFailed, HarnessEnding ended,
        DateTimeOffset openedAt, CancellationToken ct)
    {
        var service = _context.Service;
        var config = _context.Config;
        var sessionId = park.Session;

        // 🔴 MODELROLE1c2 (D205 §3.5(a), round 4, Opus §2.2 and N4): its own account refused its asked model at the open, before any prompt.
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
        // resume (SESSCTX1c, D189's amendment; HoldAsync tried it): it never opened it, which a crash, a failed sign-in or a flag it
        // rejected ends the same way, so it is `exited`, never `gone`, and the record carries on as any refused resume's does. Read from
        // the structure, never from its words.
        if (resume.Refused is null && adapter.Abilities().ResumesByRestart && !resume.Named && exitCode is int failed && failed != 0)
        {
            resume.Refused = ContinueWhy.Of(ContinueWhy.Exited);
        }

        var after = await service.FindQuestAsync(quest.Id, ct).ConfigureAwait(false);
        var status = after?.Status ?? "Open";
        // 🔴 Whether its quest had closed, as the look planned the run: never as the run left it, since a resumed run closes
        // its own quest, and read after it an answered park that finished its work took a closed quest's ending (MSG1b).
        var closed = quest.Status is not ("Open" or "Taken");

        // 🔴 Codex's owed read of D210, finding 6: an open refused with a limit or an access failure after a transient one was wrapped as
        // a refused resume, whose fallback below came first and read no account. The typed reading keeps its account's policy, applied as
        // a failed run's conclusion applies it, before the fallback is chosen; the person's words still go back unprompted.
        if (resume.Refused is not null && _context.Processes.StopMade(sessionId) is null && ended.Transient?.Last is
                { Failure.Where: DoorWhere.Open, Kind: AgentErrorKind.Limit or AgentErrorKind.Access } opening)
        {
            OpeningAccount(opening, adapter, selection, sessionId);
        }

        if (resume.Refused is { } refused)
        {
            // Back to how it ended (MSG1b): an ended record goes on only with words, and these never went. A park ends
            // completed, as an answer it could not take always ended it (D131 §2). SESSCTX1c: where another client holds its
            // conversation it waits for the person (the caller marks its words), so its note says it cannot go on, as a closed quest's.
            var held = closed || refused.Holds;
            // PARKVIEW1: a resume refused parks nothing, so no question is kept for it.
            _driver.Questions(
                sessionId, new QuestionFiles(_context.Home).SettleRun(sessionId, park.Parked ? "completed" : park.State, openedAt, _context.Events));
            await service.AdvanceAsync(
                    sessionId, park.Parked ? "completed" : park.State,
                    held ? Continuations.CannotNote(park, refused)
                        : park.Parked ? Continuations.EndedNote(park, refused)
                        : Continuations.WentNote(park, refused),
                    ct: ct, interrupted: !park.Parked && park.Interrupted, limit: !park.Parked && park.Limit)
                .ConfigureAwait(false);
            service.AccountSaid(_ledger.Took(park, adapter.Name, refused));
            return (null, refused);
        }

        if (used is not null)
        {
            _context.Usage?.Record(new UsageEntry(sessionId, quest.To, adapter.Name, selection.Profile, used.Used, used.Size, DateTimeOffset.UtcNow));
        }

        // XAGENT1e (design §6.4): another agent's findings it took are answered as its turn ends, read before the record moves,
        // with each fix's commit read from its tree as it stands now; one it left unanswered is unresolved.
        await _driver.Deliverer.OpinionsAnsweredAsync(resume, sessionId, workTree, ct).ConfigureAwait(false);

        // A stop made of it is its end, a pause's naming the pause (PAUSE1b, design §4.1), as a first run's is (FLAKE3: one reading).
        var stop = _context.Processes.StopMade(sessionId);
        // HELPERWAIT1: a clean end holding its quest, on its agent's own open helper ask and no question of the person's, waits on that
        // helper, as a first run's does. A closed quest's session holds none, and never waits.
        var awaitsHelper = stop is null && exitCode == 0 && !closed && status == "Taken"
                           && await _driver.WaitsOnOwnHelperAsync(sessionId, quest, openedAt, ct).ConfigureAwait(false);
        var conclusion = stop is not null
            ? stop.Conclusion
            : exitCode is int code
                // As any start's where its quest was open or taken as the look planned it, so a park whose run closes its
                // quest ends completed; a closed quest's session ends as its process does (MSG1b). It carried on a take this
                // machine already held, so ending with it still taken is a park, not a stand-down.
                ? Observation.Resumed(
                    code, park.State, quest.Status, status, quest.Awaits, after?.Awaits, turnFailed,
                    took: !closed && status == "Taken" && await service.TookAsync(sessionId, ct).ConfigureAwait(false),
                    // APIREFUSAL1: a refused turn quotes its provider's words whether or not its quest had closed.
                    lastWords: closed && !ended.ProviderRefused ? null : SessionLastWords.ParkedWords(_context.Events, sessionId, transcript),
                    // BGWAIT1c: the native door's harness ended its own background work with its turn.
                    backgroundCut: ended.BackgroundCut,
                    // FABRIC-CLOSE1h5: a closure graph's submission is its done, as at a first run's end.
                    submitted: !closed && QuestResults.Submitted(after),
                    awaitsHelper: awaitsHelper,
                    // APIREFUSAL1: its provider refused its turn, a failed run and never a park, as at a first run's end.
                    providerRefused: ended.ProviderRefused)
                // BGWAIT2: a done this run reached outranks the timeout; a quest closed before it went on is not this run's.
                : _driver.Watch.EndedMinutes(sessionId) is { } quietMinutes
                    ? Observation.AfterQuiet(quietMinutes, quest.Status, status)
                    : Observation.AfterTimeout(config.TestBound ?? 0, quest.Status, status);
        // CODEXPERM1: a park whose turn its agent cut asked the person nothing, and says so, as a first run's does.
        if (stop is null) conclusion = Observation.Cut(conclusion, ended.Cut);

        // ACCESSSWAP1b: a run Daoris ended at its turn's end for a changed setup is neither a park nor a failure, and goes on again.
        var swappedOn = stop is null && ended.SetupChanged is not null;
        if (swappedOn) conclusion = Observation.SetupChanged(conclusion, ended.SetupChanged!);

        // AGENTERRORS1a (D210 §5.1): past its transient bound a failed end is `ended.transient`, no strike and no cool-off, its quest held
        // for the person; Daoris's go-on comes off by its key where its take-off did not land, as at a first run's end (R3-O5).
        if (stop is null && ended.Transient?.Episode is { Exhausted: true } exhausted && conclusion.State == "failed")
        {
            conclusion = Observation.Transient(exhausted);
        }

        var episode = ended.Transient?.Opened == true;
        if (episode) await TransientRetries.GoOnOffAsync(service, sessionId, ct).ConfigureAwait(false);

        // 🔴 AGENTERRORS1a2, nothing prompted (the design §5.1, §5.6; round 3's read, R3-O9): an open that failed past its bound sent none of
        // the resume's words. Daoris's go-on comes off by its id whatever `words` reads below; the person's carried words stay on the record
        // unmarked, and the hold's word rule holds them, since they were said before this end, until a word of theirs said after it.
        if (stop is null && !resume.Prompted && ended.Transient?.Episode is { Exhausted: true, AtOpen: true })
        {
            await _limits.TakeLimitWordsOffAsync(sessionId, LimitResume.WordIds(resume.Words), ct).ConfigureAwait(false);
        }

        conclusion = _driver.AccountRefused(conclusion, adapter, selection, ended);
        conclusion = _driver.AccountSignedOut(conclusion, adapter, selection, turnFailed, ended);
        // MODELROLE1c3 (D205 §3.5(a)): a pipe go-on run whose door refused its asked model on a silent turn keeps the account's reading and
        // ends with its line, no strike; its words wait for the next look, whose resume judges the reading and carries them elsewhere.
        conclusion = _driver.ModelRefused(conclusion, adapter.Name, selection, park.Asked?.NamedModel, ended, sessionId);
        (conclusion, var limited) = _driver.AccountLimited(
            conclusion, adapter, selection, turnFailed, sessionId, used,
            byCode: ended.Transient?.Last is { Kind: AgentErrorKind.Limit, Limit: { } coded } ? coded : null);

        // 🔴 AGENTERRORS1a (the design §5.1, round 3, R2-C2): after an episode the resume's words went on the wire in this run, so they are
        // never sent again, whatever kind ends it: taken, or unacknowledged where its first prompt met the error before any update of its
        // turn, said then by COWORK1c's `lost` note. TURNFAIL1 and D206 are handed only words still held, which a resume's never are.
        if (episode && stop is null && resume.Prompted && ended.Transient!.OpeningUnacknowledged && resume.Ids.Count > 0)
        {
            _context.Events.Keep(
                sessionId,
                new SessionEvent { Kind = SessionEventKind.Note, Text = ChatTurns.LostLine, Words = [.. resume.Ids], Code = SessionEventCodes.Lost },
                say: null);
        }

        // 🔴 TURNFAIL1: words a failed turn carried were never answered. They stay waiting on the record, unmarked, as a cooling
        // account's do (D137 §2.2), and its note and conversation say why; failing a second time running, they wait for the
        // person (FailedTurnWords). Every other run took them. LIMITRESUME1b: a limited end's always wait (D137's note as D206 amends it).
        var words = stop is null && resume.Prompted && resume.Ids.Count > 0 && !episode
            ? FailedTurnWords.Judge(conclusion, turnFailed, ended, park, limited)
            : WordsAfter.Taken;
        // LIMITRESUME1b (the design §3.3, round 3's B8 item 3): Daoris's limit word comes off at every conclusion, so it never reaches a
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
            // The words went: off the record by their ids, before the conclusion moves it (MSG1a's taken door).
            var (taken, message) = await service.TakenAsync(sessionId, resume.Ids, by: null, ct).ConfigureAwait(false);
            if (!taken)
            {
                var untaken = WordsNotes.NotTaken(message);
                _context.Events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = untaken.Note, Parts = untaken.Parts }, say: null);
            }

            // Its own account carried them, so a choice of a new session made while it cooled is spent with them (MSG1g).
            _context.NewSessions.Clear(sessionId);
        }

        var evidence = await WorkingTree.CommitsSinceAsync(workTree, park.BaseCommit ?? before, ct).ConfigureAwait(false);
        // PARKVIEW1 (the park's ask design §2): its question kept only for the park this resumed run ends in, asked in its last turn.
        _driver.Questions(sessionId, new QuestionFiles(_context.Home).SettleRun(sessionId, conclusion.State, openedAt, _context.Events));
        await service.AdvanceAsync(
                sessionId, conclusion.State, note: conclusion.Note, evidence: evidence, ct: ct, limit: limited, parts: conclusion.Parts)
            .ConfigureAwait(false);
        service.AccountSaid(_ledger.Took(park, adapter.Name, why: null));

        // REVIEWENV1c (design §2.6, §3.4): a set-up step that went on with the person's not yet says a new set-up, posted with
        // the commit its tree holds now, its correction among it.
        await ReviewSetUps.PostAsync(service, config, _context.Events, after, sessionId, workTree, quest.Workspace, openedAt, ct, _driver.Reviews)
            .ConfigureAwait(false);

        await _driver.ConcludedForOpinionAsync(sessionId, quest, QuestResults.Status(after) ?? status, conclusion.State, workTree, quest.Workspace, ct)
            .ConfigureAwait(false);

        return (new StartRun(
            $"{conclusion.State}  session {sessionId} (#{quest.Id} → {quest.To}) [resumed its conversation]: {conclusion.Note}",
            true,
            // HELPERWAIT1: a session waiting on its own helper is no ending, and keeps its tree (the caller's TREELOCK1), as a park does.
            SessionStates.IsParked(conclusion.State) || swappedOn || HelperWaits.Ended(conclusion.Parts)
                ? null
                : new SessionEnded(
                    sessionId, quest.To, conclusion.State,
                    ByPerson: stop is { ByPerson: true }, conclusion.Note,
                    Quest: quest.Id, Adapter: adapter.Name, Account: selection.Profile) { Limit = limited, Reason = SessionEnded.ReasonOf(conclusion.Parts) }), null);
    }
}
