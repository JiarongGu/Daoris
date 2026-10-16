namespace Daoris.Driver;

/// <summary>
/// The person's words continue their session (ANSWER1a, D131; MSG1b, D137 §2.2): the record they wait on goes on itself,
/// a park moving <c>awaiting-person</c> → <c>working</c> and an ended record taking the ledger's one move out of an ended
/// state, its harness's own conversation resumed with the words as its next prompt, where the account, the adapter and the
/// tree are the same and the door can. Otherwise the words are handed on by <see cref="RunAsync"/>, saying why.
/// </summary>
/// <remarks>
/// <para><b>One record for one harness conversation</b> (D131 §3). A resume reopens the record the words were said to, so
/// the page, the log, the review and a teammate see one session going on; a fallback is a new record because it is a new
/// conversation.</para>
///
/// <para><b>The wire may still refuse</b>: an agent that offers no resume, one that no longer has the conversation, one
/// whose other client holds it, or a native door whose harness ended before naming it. The resumed run then ends with the
/// reason, nothing done, and the words are handed on in the same look.</para>
/// </remarks>
/// <param name="driver">
/// The driver whose run machinery a resumed run goes through (PART10n, D166): the spawn's preparation, the rules handed, the hold
/// of the process and the account sayings, as <see cref="DriverOpinions"/> does. The three stores it keeps (the harness's own
/// conversations, the marks of words that could not go on and the choices of a new session) live in the context, where the
/// front reads them too.
/// </param>
internal sealed class DriverContinuations(LoopContext context, Driver driver)
{
    private readonly LoopContext _context = context;

    private readonly Driver _driver = driver;

    /// <summary>
    /// The record a start goes on in with the person's words, where its resume asks for that record's own account (MSG1g, D137
    /// §2.2): words the planner took up, on the adapter the record ran on. Null for every other start, and for a record that ran
    /// on another adapter, whose conversation cannot go on here whichever account runs (D131 §2's <c>adapter</c>), so its words
    /// are carried on by the walk's pick as before.
    /// </summary>
    internal PriorSession? ResumesOwn(Consideration start, string agent)
    {
        if (!start.GoesOn || start.Resumes is not { WordsWaiting: true } words || start.Quest.Awaits is { Length: > 0 }) return null;
        var ranOn = words.Adapter ?? _context.Conversations.Read(words.Session)?.Adapter;
        return ranOn is not { Length: > 0 } || string.Equals(ranOn, agent, StringComparison.OrdinalIgnoreCase) ? words : null;
    }

    /// <summary>
    /// Why the words wait (MSG1g): the account, its reset and that its conversation is there, then the door out of the wait. A
    /// taken or open quest's session goes on in a new session at the person's choice; a closed quest's has nothing to carry it
    /// on by itself, so its door is a conversation with the words (D137 §2.2). Machine-local: it names the account.
    /// </summary>
    internal static string WaitsFor(QuestView quest, PriorSession record, HarnessSelection held) =>
        $"{held.Refusal} {DoorOf(quest, record).Note}";

    /// <summary>The door out of the wait <see cref="WaitsFor"/> says, by code (CONVNOTE2g).</summary>
    internal static Noted DoorOf(QuestView quest, PriorSession record) =>
        quest.Status is "Open" or "Taken" ? ResumeWords.NewSessionDoorOf(record.Session) : ResumeWords.ChatDoorOf(record.Session);

    /// <summary>
    /// The record's conversation says why its words wait (MSG1g, D143 point 1), once for each wait: a look that finds it saying
    /// the same as its last event writes nothing more, and a word said since is answered again. The person wrote to it, so it
    /// stays in view while it waits (D137 §2.3). The words stay waiting and unmarked: the reset lets them go on. Its line is
    /// <see cref="ResumeWords.NotYetOn"/>'s, by code (CONVNOTE2g); the last event is compared by its English, as it always was.
    /// </summary>
    internal void HeldForAccount(PriorSession record, Noted line)
    {
        try
        {
            if (_context.Events.Page(record.Session, limit: 1).Events is [{ Kind: SessionEventKind.Note, Text: var last }] && last == line.Note) return;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            // A record that does not read is said again: one line more, never none.
        }

        if (!record.Parked) new SessionArchive(_context.Home).Unarchive([record.Session]);
        _context.Output?.Append(record.Session, line.Note);
        _context.Events.Keep(
            record.Session,
            new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts.Count > 0 ? line.Parts : null },
            say: null);
    }

    /// <summary>
    /// Continue the record the person's words wait on, or end it saying why (ANSWER1a, D131 §1–§2; MSG1b, D137 §2.2).
    /// </summary>
    /// <returns>
    /// The resumed run, whatever it came to; or null and why not, a park ended where it was still parked, for
    /// <see cref="RunAsync"/> to hand the words on; and whether the words are already in the record, as a resume that was
    /// tried puts them.
    /// </returns>
    /// <param name="account">
    /// Why the record's own account could not carry the words, where its resume asked for it (MSG1g): the walk's pick carries
    /// them, and the reason says why. Null where its own account runs it, or nobody asked.
    /// </param>
    /// <param name="inSync">
    /// What the start did with the tree against the line before the words go on (LANDSYNC2, D87's 2026-10-15 amendment point 7), said
    /// after them: the conversation was handed no instruction it could read it in. Null for nothing to tell.
    /// </param>
    internal async Task<(StartRun? Run, ContinueReason? FellBack, bool AnswerKept)> ContinueAsync(
        Consideration start, PriorSession park, HarnessSelection selection, IReadOnlyList<RepoView> registry,
        TreeLock? starting, Action onOpened, CancellationToken ct, string agent, ContinueReason? account = null, string? inSync = null)
    {
        var service = _context.Service;
        var config = _context.Config;
        var home = _context.Home;
        var output = _context.Output;
        var quest = start.Quest;
        ISessionAdapter? adapter = null;
        // The agent this start runs on: the machine's, or the one a hand-over named for the quest (MIXAGENT1a-ii), passed in since
        // starts run side by side and a field would be every start's.
        try { adapter = _context.Adapters.Resolve(agent); } catch (DriverException) { }

        // The person wrote to a record that had ended, so it stays in view whatever comes of it (D137 §2.3): its archive mark
        // goes, and a mark of words it could not take before is past, since these are taken up now.
        if (!park.Parked && park.WordsWaiting)
        {
            new SessionArchive(home).Unarchive([park.Session]);
            _context.Marks.Clear(park.Session);
        }

        var kept = _context.Conversations.Read(park.Session);
        var why = adapter is null
            ? ContinueWhy.Of(ContinueWhy.Refused)
            : Continuations.Judge(park, adapter.Name, adapter.Resumes, selection.Profile, kept, account);
        if (why is not null || adapter is null || kept is null)
        {
            var reason = why ?? ContinueWhy.Of(ContinueWhy.Refused);
            if (await EndParkAsync(park, reason, agent, ct).ConfigureAwait(false) is { } refused)
            {
                return (new StartRun($"held  #{quest.Id} → {quest.To}: {refused}", false, null, refused), null, false);
            }

            return (null, reason, false);
        }

        // The park holds its tree in the ledger the whole time (D51), so a replay already sees it in use (LEFT2). An ended
        // record holds it again only once the ledger moves it to working, so its starting hold is kept until then (MSG1b).
        if (park.Parked) starting?.Dispose();

        var sessionId = park.Session;
        var workTree = park.Tree!;
        var before = await WorkingTree.HeadAsync(workTree, ct).ConfigureAwait(false);
        var across = AcrossRules.Reach(config, registry, quest.To, start.Workspace);
        var target = Driver.WithLanguage(SessionTarget.ForQuest(quest, workTree, service.BaseUrl), config, start.Workspace) with
        {
            ReadsAcross = across.Reads,
            WritesAcross = across.Writes,
            Session = sessionId,
        };
        // Another agent's findings among the words (XAGENT1e, D155 point 7): said as that agent's, and answered with the one
        // tool they need, which the person's rules may not name.
        var findings = park.Findings.Count > 0;
        // A helper's answer (MIXAGENT2a-ii) is another agent's claim too: said as its, never as the person's.
        var helper = park.Helpers.Count > 0;
        var persons = park.Waiting.Any(word => word.Persons);
        var transcript = Path.Combine(home, "sessions", $"{sessionId}.log");

        // 🔴 SAIDBACK1e (D137 §2.4): the ledger's move to working comes before the words are read and the run is spawned, so a
        // word taken back between the look and the move never goes (Continuations.HandedAfterMove). A park's note is replaced
        // while it works; an ended record's keeps what ended it and says it goes on (MSG1b), with another agent's findings where
        // those are all it goes on with (XAGENT1e), never "your words".
        var workingNote = park.Parked ? Continuations.WorkingNoted
            : findings && !persons ? Continuations.GoingOnWithFindingsNoted
            : helper && !persons ? Continuations.GoingOnWithHelperNoted
            : Continuations.GoingOnNoted;
        try
        {
            await service.AdvanceAsync(sessionId, "working", workingNote, transcript: transcript, ct: ct).ConfigureAwait(false);
        }
        catch (DriverException refused)
        {
            starting?.Dispose();
            // Every word taken back before it moved: an ended record with none waiting does not move (MSG1a), and nothing holds it.
            if (await ReadAgainAsync(service, sessionId, ct).ConfigureAwait(false) is { Running: false, Waiting.Count: 0 })
            {
                return (TakenBackFirst(quest, sessionId), null, false);
            }

            // The person stopped or answered it, or another run took it up, between the look and here: the next look reads it as
            // it now is.
            return (new StartRun($"held  #{quest.Id} → {quest.To}: {refused.Message}", false, null, refused.Message), null, false);
        }

        // The ledger holds its tree again (LEFT2).
        starting?.Dispose();

        ResumeAsk? resume = null;
        // Whether its process started: a failure before then started no run, so the record goes back with its words (SAIDBACK1e).
        var spawned = false;
        // The stop made of it before a close cut its conclusion off, told as its run lets go (FLAKE3).
        SessionStop? stoppedFirst = null;
        try
        {
            // Every word waiting once it moved, as the move left them (SAIDBACK1d): none can be taken back from a record that runs.
            var words = Continuations.HandedAfterMove(park.Waiting, await ReadAgainAsync(service, sessionId, ct).ConfigureAwait(false));
            if (words.Count == 0) return await PutBackAsync(quest, park, agent, why: null).ConfigureAwait(false);

            // Each word's files where they were kept as it was said (MSG1d3, D137 §2.4), handed with the words; what its start did with
            // its tree against the line first after them (LANDSYNC2), since that is where its next commit goes.
            var after = await ResumedAfterAsync(service, quest, sessionId, target.Language, ct).ConfigureAwait(false);
            resume = new ResumeAsk(
                kept.Conversation, words,
                Continuations.OpeningNoted(adapter.Name, selection.Version, park.HarnessVersion, answer: park.Parked, findings, persons, helper),
                Continuations.AfterWords(inSync, after),
                files: word => ChatFiles.Kept(home, sessionId, word.Files));

            // The protocol door resumes on its wire, from the same spawn a start has; the native door is run on the
            // conversation it kept.
            var prepared = adapter.Wire == SessionWire.Acp
                ? null
                : adapter.PrepareResume(target, config.Commands.GetValueOrDefault(adapter.Name), kept.Conversation, resume.Prompt);
            var (info, harnessNotice) = _driver.Prepare(adapter, target, selection, prepared);
            // A set-up step going on with the person's words shows it again in its own tab (REVIEWENV1d, design §3.4): opened
            // again where the person closed it, and brought forward. Said, never held, where it will not open: the words go on.
            var tabNotice = quest.SetUpIn is not null && _driver.Reviews is { } desk
                            && ReviewSetUps.Environment(config, quest, start.Workspace) is { } shownIn
                            && await desk.OpenForStepAsync(quest, shownIn, ct).ConfigureAwait(false) is { } unopened
                ? $"— {unopened}"
                : null;
            var (servers, browserNotice, drivesBrowser) = await InAppBrowserServers.HandAsync(_context.Servers, _context.Browser, ct).ConfigureAwait(false);
            browserNotice = Driver.JoinNotices(browserNotice, tabNotice);
            _context.Hooks?.Log.Served(_context.Catalog, sessionId, servers);
            var handed = SpawnServers.Hand(adapter, info, home, sessionId, servers);
            var rules = _driver.HandRules(
                adapter, info, sessionId, start.Workspace, quest.To, workTree, target.AttachmentsDirectory, across: across,
                said: SaidFilesFolder(home, sessionId, resume), job: findings ? AnswersFindings : null);

            onOpened();
            var openedAt = DateTimeOffset.UtcNow;
            _context.Runs.Live[quest.Id] = sessionId;
            using var live = new Disposer(() => _context.Runs.Live.TryRemove(quest.Id, out var _));

            var (run, refusedWhy) = await _driver.HoldAsync(
                adapter, info, target, sessionId, transcript, workTree, Driver.JoinNotices(harnessNotice, browserNotice), rules, handed,
                refusesInput: Driver.TakesNoMessages(quest),
                ct: ct,
                preamble: browserNotice,
                handedServers: servers,
                drivesBrowser: drivesBrowser,
                // A resumed run is on the account it parked on, so what it says about that account's windows is kept
                // as any run's is (TOOL6c with ANSWER1a).
                said: _driver.Said(adapter, selection, sessionId),
                resume: resume,
                goOn: GoOnWith(adapter, target, selection, rules.File, handed),
                // BGWAIT1b: a resumed run waits on its own background work as a start does, by the same judgement; where its
                // quest was closed as the look planned it, never (D83, D105 §3, D137 §2.3).
                waitsOnBackground: Driver.WaitsOnBackground(service, quest, sessionId, resumed: true),
                stopMade: stop => stoppedFirst = stop,
                movedFirst: true,
                spawned: () => spawned = true,
                conclude: (exitCode, used, turnFailed, ended) =>
                    ConcludeResumedAsync(quest, park, adapter, selection, resume, workTree, transcript, before, exitCode, used, turnFailed, ended,
                        openedAt, ct))
                .ConfigureAwait(false);
            return (run, refusedWhy, resume.Spoken);
        }
        catch (Exception error) when (!spawned && !(error is OperationCanceledException && ct.IsCancellationRequested))
        {
            // 🔴 No run started (SAIDBACK1e): the record the move took up goes back with its words, never `failed` with them, and
            // they are handed on as when its conversation could not be resumed, as a failed spawn handed them on before the move.
            return await PutBackAsync(quest, park, agent, ContinueWhy.Of(ContinueWhy.Refused)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The driver is closing under it (D104): an interrupted take, carried on at the next start. Unless a stop came first,
            // whose record it is, never interrupted, as a first run's close reads it (FLAKE3).
            var (closed, interrupted) = Observation.Closed(stoppedFirst);
            try
            {
                await service.AdvanceAsync(
                    sessionId, closed.State, closed.AsNoted(), ct: CancellationToken.None, interrupted: interrupted).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort by construction: the host may already be gone on the same shutdown.
            }

            if (stoppedFirst is { } first)
            {
                return (new StartRun(
                    $"{closed.State}  session {sessionId} (#{quest.Id} → {quest.To}) [resumed its conversation]: {closed.Note}",
                    true,
                    new SessionEnded(sessionId, quest.To, closed.State, ByPerson: first.ByPerson, closed.Note, Quest: quest.Id, Adapter: agent)),
                    null, resume?.Spoken ?? false);
            }

            return (new StartRun(
                $"stopped  session {sessionId} (#{quest.Id} → {quest.To}): the driver was stopped.",
                true,
                new SessionEnded(sessionId, quest.To, "stopped", ByPerson: true, Quest: quest.Id, Adapter: agent)), null, resume?.Spoken ?? false);
        }
        catch (Exception error)
        {
            // Failed once its process started: a failure, which the strikes bound. Where the ledger refuses that move, the record
            // moved on first; a park is ended as a fallback ends it where the ledger takes that, and its words handed on.
            try
            {
                await service.AdvanceAsync(sessionId, "failed", Observation.Failure(error.Message), ct: CancellationToken.None).ConfigureAwait(false);
            }
            catch (DriverException)
            {
                var reason = ContinueWhy.Of(ContinueWhy.Refused);
                if (await EndParkAsync(park, reason, agent, CancellationToken.None).ConfigureAwait(false) is null)
                {
                    return (null, reason, resume?.Spoken ?? false);
                }
            }
            catch (Exception)
            {
                // The first failure is the report; a host that is gone here must not turn it into a second throw.
            }

            return (new StartRun(
                $"failed  session {sessionId} (#{quest.Id} → {quest.To}): {error.Message}",
                true,
                new SessionEnded(sessionId, quest.To, "failed", ByPerson: false, error.Message, Quest: quest.Id, Adapter: agent)), null, resume?.Spoken ?? false);
        }
        finally
        {
            output?.Close(sessionId);
            // A resume that was tried lets the starting hold go however it ended, so a carry-on takes it again (LEFT2).
            starting?.Dispose();
        }
    }

    /// <summary>
    /// What a resumed run handed another agent's findings is given beside the person's rules (XAGENT1e, D155 point 7): the
    /// connector's <c>opinion_answer</c>, which no default names. Without it the harness would ask, and every ask is refused
    /// (D52), so the session could check every finding and answer none. A deny the person wrote still wins (D72).
    /// </summary>
    internal static readonly IReadOnlyList<string> AnswersFindings = [$"mcp__{KnowledgeConnector.ServerName}__opinion_answer"];

    /// <summary>
    /// What a resumed run's turn carries after the person's words: the answers to the go-aheads its session asked (KNOWUSE1a,
    /// D135 §2), read from its quest's ask as the run takes the record up, since its conversation was handed the ask's at its
    /// start and is told the answers after the words; then the work's session language where one is set (LANG1c), since it
    /// may have changed since the conversation was handed it. Unread, or on no ask, nothing of the go-aheads; none set, and
    /// no language line.
    /// </summary>
    /// <remarks>
    /// <b>A park its go-aheads' answers woke</b> (GOAHEAD2) holds the blank answer the service keeps for one, and its turn is
    /// that, then this: each go-ahead it asked, which, approved or refused, and the person's words, quoted as theirs.
    /// </remarks>
    internal static async Task<string> ResumedAfterAsync(
        ServiceClient service, QuestView quest, string session, SessionLanguage? language, CancellationToken ct)
    {
        var asked = await AskWords.ReadAsync(service, quest.From, ct).ConfigureAwait(false);
        return SessionLanguageText.Resumed(GoAheadsText.Resumed("", asked, session).TrimStart(), language);
    }

    /// <summary>
    /// The folder a resumed run is handed a read of for the files said with its words (MSG1d3, D137 §2.4): the session's own
    /// files folder, outside its tree, where any word it goes on with carries a kept file; null where none does. A read of
    /// exactly that folder, INT4j's rule, as a conversation is handed its own (CONV4c): every other read there would be
    /// asked, and every ask is refused (D52).
    /// </summary>
    internal static string? SaidFilesFolder(string home, string sessionId, ResumeAsk resume) =>
        resume.Files.Count > 0 ? ChatFiles.Folder(home, sessionId) : null;

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
    private async Task<(StartRun? Run, ContinueReason? FellBack)> ConcludeResumedAsync(
        QuestView quest, PriorSession park, ISessionAdapter adapter, HarnessSelection selection, ResumeAsk resume,
        string workTree, string transcript, string? before, int? exitCode, AcpUsage? used, string? turnFailed, HarnessEnding ended,
        DateTimeOffset openedAt, CancellationToken ct)
    {
        var service = _context.Service;
        var config = _context.Config;
        var sessionId = park.Session;

        // 🔴 The native door's harness ended on a failed exit before naming its conversation: it never opened it, which is
        // how a conversation it no longer has ends there. Read from the structure, never from its words.
        if (resume.Refused is null && adapter.Wire == SessionWire.Pipe && !resume.Named && exitCode is int failed && failed != 0)
        {
            resume.Refused = ContinueWhy.Of(ContinueWhy.Gone);
        }

        var after = await service.FindQuestAsync(quest.Id, ct).ConfigureAwait(false);
        var status = after?.Status ?? "Open";
        // 🔴 Whether its quest had closed, as the look planned the run: never as the run left it, since a resumed run closes
        // its own quest, and read after it an answered park that finished its work took a closed quest's ending (MSG1b).
        var closed = quest.Status is not ("Open" or "Taken");

        if (resume.Refused is { } refused)
        {
            // Back to how it ended (MSG1b): an ended record goes on only with words, and these never went. A park ends
            // completed, as an answer it could not take always ended it (D131 §2).
            await service.AdvanceAsync(
                    sessionId, park.Parked ? "completed" : park.State,
                    park.Parked ? Continuations.EndedNote(park, refused)
                        : closed ? Continuations.CannotNote(park, refused)
                        : Continuations.WentNote(park, refused),
                    ct: ct, interrupted: !park.Parked && park.Interrupted, limit: !park.Parked && park.Limit)
                .ConfigureAwait(false);
            service.AccountSaid(Took(park, adapter.Name, refused));
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
        var conclusion = stop is not null
            ? stop.Conclusion
            : exitCode is int code
                // As any start's where its quest was open or taken as the look planned it, so a park whose run closes its
                // quest ends completed; a closed quest's session ends as its process does (MSG1b). It carried on a take this
                // machine already held, so ending with it still taken is a park, not a stand-down.
                ? Observation.Resumed(
                    code, park.State, quest.Status, status, quest.Awaits, after?.Awaits, turnFailed,
                    took: !closed && status == "Taken" && await service.TookAsync(sessionId, ct).ConfigureAwait(false),
                    lastWords: closed ? null : SessionLastWords.ParkedWords(_context.Events, sessionId, transcript))
                : SessionConclusion.Of("failed", Observation.TimedOut(config.TimeoutMinutes));

        conclusion = _driver.AccountRefused(conclusion, adapter, selection, ended);
        conclusion = _driver.AccountSignedOut(conclusion, adapter, selection, turnFailed);
        (conclusion, var limited) = _driver.AccountLimited(conclusion, adapter, selection, turnFailed, sessionId, used);

        // 🔴 TURNFAIL1: words a failed turn carried were never answered. They stay waiting on the record, unmarked, as a cooling
        // account's do (D137 §2.2), and its note and conversation say why; failing a second time running, they wait for the
        // person (FailedTurnWords). Every other run took them.
        var words = stop is null && resume.Prompted && resume.Ids.Count > 0 ? FailedTurnWords.Judge(conclusion, turnFailed, ended, park) :WordsAfter.Taken;
        if (FailedTurnWords.Line(words) is { } waits)
        {
            conclusion = conclusion.Then("\n\n", waits);
            if (words == WordsAfter.Held) _context.Marks.Mark(sessionId, resume.Ids, GoOnMarks.FailedTwice, DateTimeOffset.UtcNow);
            _context.Events.Keep(
                sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = waits.Note, Parts = waits.Parts, Words = resume.Ids }, say: null);
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
        await service.AdvanceAsync(
                sessionId, conclusion.State, note: conclusion.Note, evidence: evidence, ct: ct, limit: limited, parts: conclusion.Parts)
            .ConfigureAwait(false);
        service.AccountSaid(Took(park, adapter.Name, why: null));

        // REVIEWENV1c (design §2.6, §3.4): a set-up step that went on with the person's not yet says a new set-up, posted with
        // the commit its tree holds now, its correction among it.
        await ReviewSetUps.PostAsync(service, config, _context.Events, after, sessionId, workTree, quest.Workspace, openedAt, ct, _driver.Reviews)
            .ConfigureAwait(false);

        // LAND2b: as a first run's ending, so a resumed session that closes its quest done is due too. One that went on after
        // its landing moves its own branch on at the next look (LAND2c).
        await _driver.ConcludedForLandingAsync(sessionId, quest, status, conclusion.State, workTree, quest.Workspace, ct).ConfigureAwait(false);

        return (new StartRun(
            $"{conclusion.State}  session {sessionId} (#{quest.Id} → {quest.To}) [resumed its conversation]: {conclusion.Note}",
            true,
            SessionStates.IsParked(conclusion.State)
                ? null
                : new SessionEnded(
                    sessionId, quest.To, conclusion.State,
                    ByPerson: stop is { ByPerson: true }, conclusion.Note,
                    Quest: quest.Id, Adapter: adapter.Name, Account: selection.Profile)), null);
    }

    /// <summary>
    /// The record the move to working took up goes back where no run of it started (SAIDBACK1e, D137 §2.4), its words waiting on
    /// it again, as <see cref="Continuations.PutBack"/> says. A run that could not start is a resume refused (<paramref name="why"/>):
    /// the machine log says so, and the caller hands the words on as any fallback does, or marks them where nothing carries them
    /// on. Every word taken back first (no reason): nothing is sent and nothing holds it. Where the ledger will not move it back,
    /// its refusal holds the start, and the next look reads the record as it now is.
    /// </summary>
    private async Task<(StartRun? Run, ContinueReason? FellBack, bool AnswerKept)> PutBackAsync(
        QuestView quest, PriorSession park, string agent, ContinueReason? why)
    {
        var (state, note, interrupted, limit) = Continuations.PutBack(park, why);
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

        if (why is null) return (TakenBackFirst(quest, park.Session), null, false);
        _context.Service.AccountSaid(Took(park, agent, why));
        return (null, why, false);
    }

    /// <summary>
    /// The record a fallback goes on from, its words as they stand now (SAIDBACK1f, <see cref="Continuations.HandedOn"/>): the
    /// look's words may have been taken back since, and one taken back must never reach an agent.
    /// </summary>
    internal async Task<PriorSession> HandedOnRecordAsync(PriorSession look, CancellationToken ct) =>
        look.Parked ? look : Continuations.HandedOn(look, await ReadAgainAsync(_context.Service, look.Session, ct).ConfigureAwait(false));

    /// <summary>The look's line where every word waiting on the record was taken back before it went on (SAIDBACK1d, SAIDBACK1e).</summary>
    internal static StartRun TakenBackFirst(QuestView quest, string session) => new(
        $"held  #{quest.Id} → {quest.To}: every word waiting on session {session} was taken back before it went on; nothing is sent.",
        false);

    /// <summary>The record as it stands now, or null where it cannot be read: the look's words are then what there is (SAIDBACK1d).</summary>
    private static async Task<PriorSession?> ReadAgainAsync(ServiceClient service, string sessionId, CancellationToken ct)
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
    /// End the park a fallback leaves behind, keeping what it asked and saying why (D131 §2), and write the line. A record
    /// that had already ended moves no further: its words wait on it until they are handed on (MSG1b). Null when done; else
    /// the service's refusal.
    /// </summary>
    private async Task<string?> EndParkAsync(PriorSession park, ContinueReason why, string agent, CancellationToken ct)
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
    /// Where the words cannot go on in their session and nothing carries them on by itself (MSG1b, D137 §2.2): a closed
    /// quest's session, or a never. The record stays as it ended, its words waiting as said; they are marked by their ids so
    /// a later look leaves them there (<see cref="GoOnMarks"/>), and the session's conversation says why, where the person
    /// wrote them. A new conversation with them is the person's press, never the driver's (MSG1f).
    /// </summary>
    internal StartRun CannotGoOn(QuestView quest, PriorSession record, ContinueReason why)
    {
        var words = record.Waiting.Select(word => word.Id).OfType<string>().ToList();
        _context.Marks.Mark(record.Session, words, why, DateTimeOffset.UtcNow);
        // Another agent's findings among them go to the person instead, unanswered (XAGENT1e, design §6.7).
        _driver.Deliverer.FindingsToPerson(record, why);
        // A choice of a new session is spent with the words it named (MSG1g): words said later wait for the account again.
        _context.NewSessions.Clear(record.Session);
        // The words' ids and the code beside the line (MSG1d), so the page says why in its own words.
        var note = Continuations.Cannot(words, why);
        _context.Output?.Append(record.Session, note.Text!);
        _context.Events.Keep(record.Session, note, say: null);
        return new StartRun(
            $"cannot  session {record.Session} (#{quest.Id} → {quest.To}): it cannot go on in this session, because {why.Said}.",
            false);
    }

    /// <summary>
    /// The words a session could not go on with went to a new one (MSG1b, D137 §2.2): taken off the record they were said to
    /// by their ids, naming the session that took them, so the service keeps those said after it ended on the ask as
    /// <c>reopened</c>; and, for a record that had ended, said in its conversation where the person wrote them. A park's
    /// answer is said beneath its question as it always was. A refusal costs a line, never the start.
    /// </summary>
    internal async Task HandedOnAsync(PriorSession record, string to, ContinueReason why, CancellationToken ct)
    {
        if (!record.WordsWaiting) return;

        var output = _context.Output;
        // A choice of a new session is spent with the words it named, which went now (MSG1g).
        _context.NewSessions.Clear(record.Session);
        // 🔴 Only the person's words go on in a new session (XAGENT1e, design §6.7): another agent's findings would reach it as the
        // person's, which DRIFT1 would make requirements. They stay on the record, marked as words that cannot go on there, and
        // go to the person instead.
        if (record.Findings.Count > 0 || record.Helpers.Count > 0)
        {
            _context.Marks.Mark(record.Session, record.Waiting.Where(word => !word.Persons).Select(word => word.Id).OfType<string>(), why, DateTimeOffset.UtcNow);
            _driver.Deliverer.FindingsToPerson(record, why);
        }

        var ids = record.Waiting.Where(word => word.Persons).Select(word => word.Id).OfType<string>().ToList();
        if (!record.Parked)
        {
            // The words' ids, the session and the code beside the line (MSG1d, D137 §3.1): the page links where they went.
            var note = Continuations.Went(ids, to, why);
            output?.Append(record.Session, note.Text!);
            _context.Events.Keep(record.Session, note, say: null);
        }

        if (ids.Count == 0) return;
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
    }

    /// <summary>
    /// The machine log's line for words taken up (D94 §4): a park's answer is <c>session.answered</c> (D131 §2), and words to
    /// a record that had ended are <c>session.reopened</c> (D137 §3.3), from the state it ended in, naming the door the first
    /// of them was said at, as the record showed them (MSG1d).
    /// </summary>
    private AccountLine Took(PriorSession record, string adapter, ContinueReason? why) =>
        record.Parked || !record.WordsWaiting
            ? Continuations.Answered(record.Session, adapter, why)
            : Continuations.Reopened(
                record.Session, adapter, record.State, why,
                _context.Events.DoorOf(record.Session, [.. record.Waiting.Select(word => word.Id).OfType<string>()]));

    /// <summary>
    /// How a native run goes on with the words the person said while it ran (MSG1b, D137 §2.1): its own conversation
    /// resumed with them, with the environment, the rules and the servers a start is handed. Null on a door that cannot
    /// resume, whose words wait for nothing: it opens no inbox.
    /// </summary>
    internal Func<string, string, System.Diagnostics.ProcessStartInfo?>? GoOnWith(
        ISessionAdapter adapter, SessionTarget target, HarnessSelection selection, string? settings, string? servers) =>
        adapter.Wire == SessionWire.Pipe && adapter.Resumes
            ? (conversation, words) =>
                GoOnStart(adapter, target, _context.Config.Commands.GetValueOrDefault(adapter.Name), conversation, words, settings, servers) is { } next
                    ? _driver.Prepare(adapter, target, selection, next).Info
                    : null
            : null;

    /// <summary>
    /// The native run that goes on with held words (MSG1b, D137 §2.1): the adapter's own resume of the kept conversation,
    /// the words as its prompt, then the settings and servers files the run before it was handed, so it runs under the same
    /// rules. Null on an adapter that cannot resume.
    /// </summary>
    internal static System.Diagnostics.ProcessStartInfo? GoOnStart(
        ISessionAdapter adapter, SessionTarget target, IReadOnlyList<string>? command, string conversation, string words,
        string? settings, string? servers)
    {
        var info = adapter.PrepareResume(target, command, conversation, words);
        if (info is null) return null;
        if (settings is not null) adapter.HandSettings(info, settings);
        if (servers is not null) adapter.HandServers(info, servers);
        return info;
    }

    /// <summary>
    /// Go on with what the person said to a native run while it worked (MSG1b, D137 §2.1): each time a run ends with words
    /// held, its own conversation is resumed with all of them as one prompt, under the same record, which stays working; the
    /// record concludes only when a run ends with nothing held. A stop, a timeout, or no conversation id kept ends it, and
    /// any words still held are said, never dropped without a trace.
    /// </summary>
    /// <param name="keep">Where each run's process, its tracking and its reaper are kept, until the record has concluded.</param>
    /// <param name="left">Where the words a failed run carried or left held are handed back to wait (TURNFAIL2).</param>
    /// <param name="own">Where the harness's own words are kept (AGT3c): each run begins it again, so it says the last run's ending.</param>
    internal async Task<(int? ExitCode, AcpUsage? Used)> GoOnAsync(
        DrivenInbox held, Func<string, string, System.Diagnostics.ProcessStartInfo?> goOn, ISessionAdapter adapter,
        string sessionId, string transcript, string? refusesInput, bool drivesBrowser, int? exitCode, AcpUsage? used,
        ResumeAsk? resume, Action<System.Text.Json.JsonElement>? said, List<IDisposable> keep, List<ChatMessage> left,
        CancellationToken ct, HarnessWords? own = null)
    {
        var processes = _context.Processes;
        IReadOnlyList<ChatMessage> carried = [];
        while (exitCode is not null && !processes.WasStopRequested(sessionId))
        {
            // TURNFAIL2: a run whose turn the harness said failed is not gone on from: the words it carried and those held wait,
            // handed back for the record's say door (WordsLeft), as the protocol door's do (D137's TURNFAIL1 note).
            if (NativeWords.Left(own, exitCode, held, carried) is { } waiting)
            {
                left.AddRange(waiting);
                return (exitCode, used);
            }

            if (held.TakeAllOrClose() is not { Count: > 0 } words) break;
            carried = words;
            // The id its harness named, kept as each run's output ended (ANSWER1a); never read from the account's home.
            var conversation = _context.Conversations.Read(sessionId)?.Conversation ?? resume?.Conversation;
            var info = conversation is null ? null : goOn(conversation, NativeWords.Prompt(words));
            if (info is null)
            {
                Untaken(sessionId, held.Close().Count + words.Count, UnreachedWhy.Unkept);
                return (exitCode, used);
            }

            // Through the start gate (PROCSTART2), as the first run's start is.
            var process = await ProcessStarts.Shared.StartAsync(info, CancellationToken.None).ConfigureAwait(false)
                ?? throw new DriverException($"the {adapter.Name} adapter's process did not start");
            // As the first run's: the process, tracked so a stop reaches it, and a reaper that ends it while still tracked.
            keep.Add(process);
            keep.Add(processes.Track(sessionId, process, refusesInput, drivesBrowser));
            keep.Add(new Disposer(() => SessionProcesses.EndIfRunning(process)));

            var capture = adapter.StructuredOutput() is { } mapper
                ? _driver.KeepingAsync(mapper, HarnessCapture.CaptureStructuredAsync(
                    process.StandardOutput, process.StandardError, transcript, sessionId, _context.Output, _context.Events, mapper,
                    prompt: null, ct, opened: NativeWords.Opening(adapter.Name, words), append: true, said: said, own: own),
                    sessionId, adapter.Name, resume: null)
                : null;
            exitCode = await _driver.WaitAsync(process, ct).ConfigureAwait(false);
            if (capture is not null && await capture.ConfigureAwait(false) is { } reported) used = reported;
        }

        // Words held when it could go on no more: a stop, a timeout. Said, never dropped without a trace.
        if (held.Close() is { Count: > 0 } unreached) Untaken(sessionId, unreached.Count, UnreachedWhy.EndedFirst);
        return (exitCode, used);
    }

    /// <summary>The driver's line in the record for words the person said that never reached the session, and why.</summary>
    private void Untaken(string sessionId, int count, string why)
    {
        var line = WordsNotes.Unreached(count, "while it worked never reached it", why);
        _context.Output?.Append(sessionId, line.Note);
        _context.Events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = line.Note, Parts = line.Parts }, say: null);
    }
}

/// <summary>
/// The words a native run goes on with (MSG1b, D137 §2.1): what the person said while it worked, joined into the one
/// argument the native door takes, and the record's opening for the run that takes them.
/// </summary>
internal static class NativeWords
{
    /// <summary>
    /// Every word in the order said, joined by a blank line (D137 §2.2), each with where its files are kept, as a
    /// conversation's message names them on the native door.
    /// </summary>
    public static string Prompt(IReadOnlyList<ChatMessage> words) =>
        string.Join("\n\n", words.Select(word => word.Prompt + ChatFiles.PathLines(word.Files)));

    /// <summary>
    /// The words a native run leaves waiting when its turn failed (TURNFAIL2): what the harness said failed, its <c>result</c>'s
    /// reason (<see cref="HarnessWords.Failed"/>), never its exit alone, which ends a run no word was lost to. The words the run
    /// carried and those held behind it, in the order said, with the inbox closed so a word said as it winds up is refused for
    /// the record's door to keep. Null where the run did not fail: it goes on with what is held.
    /// </summary>
    public static IReadOnlyList<ChatMessage>? Left(HarnessWords? own, int? exit, DrivenInbox held, IReadOnlyList<ChatMessage> carried) =>
        own?.Ended(exit).Failure is { Length: > 0 } ? [.. carried, .. held.Close()] : null;

    /// <summary>
    /// The run's opening in the record: the driver's line, then the same words again under the ids they were shown with
    /// while they waited, now that the session took them (D137 §3.1).
    /// </summary>
    public static IReadOnlyList<SessionEvent> Opening(string adapter, IReadOnlyList<ChatMessage> words) =>
    [
        new SessionEvent
        {
            Kind = SessionEventKind.Note,
            Text = $"— what you said while it worked is the next turn of its own conversation, resumed on `{adapter}`.",
            Parts = [NoteCodes.ResumedNative.Part(
                $"— what you said while it worked is the next turn of its own conversation, resumed on `{adapter}`.", ("adapter", adapter))],
        },
        .. words.Select(ChatMessage.Words),
    ];
}
