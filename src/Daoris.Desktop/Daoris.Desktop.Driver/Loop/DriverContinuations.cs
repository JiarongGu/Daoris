namespace Daoris.Driver;

/// <summary>
/// The person's words continue their session: the record they wait on goes on itself,
/// a park moving <c>awaiting-person</c> → <c>working</c> and an ended record taking the ledger's one move out of an ended
/// state, its harness's own conversation resumed with the words as its next prompt, where the account, the adapter and the
/// tree are the same and the door can. Otherwise the words are handed on by <see cref="RunAsync"/>, saying why.
/// </summary>
/// <remarks>
/// <para><b>One record for one harness conversation</b>. A resume reopens the record the words were said to, so
/// the page, the log, the review and a teammate see one session going on; a fallback is a new record because it is a new
/// conversation.</para>
///
/// <para><b>The wire may still refuse</b>: an agent that offers no resume, one that no longer has the conversation, one
/// whose other client holds it, or a native door whose harness ended before naming it. The resumed run then ends with the
/// reason, nothing done, and the words are handed on in the same look.</para>
/// </remarks>
/// <param name="driver">
/// The driver whose run machinery a resumed run goes through: the spawn's preparation, the rules handed, the hold
/// of the process and the account sayings, as <see cref="DriverOpinions"/> does. The three stores it keeps (the harness's own
/// conversations, the marks of words that could not go on and the choices of a new session) live in the context, where the
/// front reads them too.
/// </param>
internal sealed class DriverContinuations(LoopContext context, Driver driver)
{
    private readonly LoopContext _context = context;

    private readonly Driver _driver = driver;

    private readonly ContinuationLimits _limits = new(context);

    private readonly ResumeLedger _ledger = new(context);

    private readonly ResumeConclusion _conclusion = new(context, driver);

    private readonly WordsHandedOn _handed = new(context, driver);

    private readonly NativeGoOn _native = new(context);

    internal PriorSession? ResumesOwn(Consideration start, string agent) => _limits.ResumesOwn(start, agent);

    internal bool LimitResumes(PriorSession limited, string agent) => _limits.LimitResumes(limited, agent);

    internal Task<(PriorSession Record, string Word)?> KeepLimitWordAsync(PriorSession limited, CancellationToken ct) =>
        _limits.KeepLimitWordAsync(limited, ct);

    internal Task TakeLimitWordsOffAsync(string session, IReadOnlyList<string> ids, CancellationToken ct) =>
        _limits.TakeLimitWordsOffAsync(session, ids, ct);

    internal void HeldSaying(PriorSession record, Noted line) => _ledger.HeldSaying(record, line);

    private Task<(StartRun? Run, ContinueReason? FellBack, bool AnswerKept)> PutBackAsync(
        QuestView quest, PriorSession park, StartRun? unsent = null) => _ledger.PutBackAsync(quest, park, unsent);

    private Task<(StartRun? Run, ContinueReason? FellBack, bool AnswerKept)> NotHandedAsync(
        QuestView quest, PriorSession park, SupervisorTicket ticket, string why) => _ledger.NotHandedAsync(quest, park, ticket, why);

    private Task<(StartRun? Run, ContinueReason? FellBack, bool AnswerKept)> UnstartedAsync(
        QuestView quest, PriorSession park, string agent, string failure, IReadOnlyList<string> ids) =>
        _ledger.UnstartedAsync(quest, park, agent, failure, ids);

    private Task<string?> EndParkAsync(PriorSession park, ContinueReason why, string agent, CancellationToken ct) =>
        _ledger.EndParkAsync(park, why, agent, ct);

    internal static StartRun TakenBackFirst(QuestView quest, string session) => ResumeLedger.TakenBackFirst(quest, session);

    internal static StartRun ClosedFirst(QuestView quest, string session) => ResumeLedger.ClosedFirst(quest, session);

    private static Task<PriorSession?> ReadAgainAsync(ServiceClient service, string sessionId, CancellationToken ct) =>
        ResumeLedger.ReadAgainAsync(service, sessionId, ct);

    internal StartRun CannotGoOn(QuestView quest, PriorSession record, ContinueReason why) => _handed.CannotGoOn(quest, record, why);

    internal Task<StartRun?> GoesOnLostAsync(
        Func<string, ContinueReason, CancellationToken, Task<StartedFrom>> goesOn, QuestView quest, PriorSession record, ContinueReason why,
        CancellationToken ct) => _handed.GoesOnLostAsync(goesOn, quest, record, why, ct);

    internal Task<IReadOnlyList<SaidWordView>?> HandedOnAsync(
        PriorSession record, string to, ContinueReason why, CancellationToken ct, bool resumed = false, bool switched = false,
        DestinationOpen? opened = null, CarryOnRecord? delivery = null) =>
        _handed.HandedOnAsync(record, to, why, ct, resumed, switched, opened, delivery);

    internal static Func<SaidWordView, bool> Hands(bool resumed, bool switched) => WordsHandedOn.Hands(resumed, switched);

    internal Task<AgentRunEnd> GoOnAsync(
        AgentRun run, DrivenInbox held, AgentDoor adapter, string sessionId, AgentRunEnd ended, ResumeAsk? resume,
        List<ChatMessage> left, CancellationToken ct, HarnessWords? own = null, Func<bool>? setupChanged = null,
        Func<CancellationToken, Task<TurnCarry>>? carriesOn = null) =>
        _native.GoOnAsync(run, held, adapter, sessionId, ended, resume, left, ct, own, setupChanged, carriesOn);

    /// <summary>
    /// Why the words wait: the account, its reset and that its conversation is there, then the door out of the wait. A
    /// taken or open quest's session goes on in a new session at the person's choice; a closed quest's has nothing to carry it
    /// on by itself, so its door is a conversation with the words. Machine-local: it names the account.
    /// </summary>
    internal static string WaitsFor(QuestView quest, PriorSession record, HarnessSelection held) =>
        $"{held.Refusal} {DoorOf(quest, record).Note}";

    /// <summary>The door out of the wait <see cref="WaitsFor"/> says, by code.</summary>
    internal static Noted DoorOf(QuestView quest, PriorSession record) =>
        quest.Status is "Open" or "Taken" ? ResumeWords.NewSessionDoorOf(record.Session) : ResumeWords.ChatDoorOf(record.Session);

    /// <summary>
    /// Continue the record the person's words wait on, or end it saying why.
    /// </summary>
    /// <returns>
    /// The resumed run, whatever it came to; or null and why not, a park ended while it still stood parked, for
    /// <see cref="RunAsync"/> to hand the words on; and whether the words are already in the record, as a resume that has been
    /// tried puts them.
    /// </returns>
    /// <param name="account">
    /// Why the record's own account could not carry the words, where its resume asked for it: the walk's pick carries
    /// them, and the reason says why. Null where its own account runs it, or nobody asked.
    /// </param>
    /// <param name="inSync">
    /// What the start did with the tree against the line before the words go on, said
    /// after them: the conversation holds no instruction it could read it in. Null for nothing to tell.
    /// </param>
    /// <param name="permissions">
    /// The mode its repository's permission prompts name on its harness now, which it goes on under, saying
    /// the change from the setup its record started under; null keeps the adapter's own.
    /// </param>
    /// <param name="treeStands">
    /// Whether the start found the record's tree there as git lists it: false where it is not, whatever folder stands at its
    /// place, so the words never go on in one. True leaves the judgement to the disk, as for a tree this home did not open.
    /// </param>
    internal async Task<(StartRun? Run, ContinueReason? FellBack, bool AnswerKept)> ContinueAsync(
        Consideration start, PriorSession park, HarnessSelection selection, IReadOnlyList<RepoView> registry,
        TreeLock? starting, Action onOpened, CancellationToken ct, string agent, ContinueReason? account = null, string? inSync = null,
        DrivenAccess? permissions = null, bool treeStands = true)
    {
        var service = _context.Service;
        var config = _context.Config;
        var home = _context.Home;
        var output = _context.Output;
        var quest = start.Quest;
        AgentDoor? adapter = null;
        // The agent this start runs on: its record's asked choice's, or the one a hand-over named for
        // the quest, passed in since starts run side by side and a field would be every start's.
        try { adapter = _context.Adapters.Door(agent); } catch (DriverException) { }

        // The person wrote to a record that had ended, so it stays in view whatever comes of it: its archive mark
        // goes, and a mark of words it could not take before is past, since these are taken up now.
        if (!park.Parked && park.WordsWaiting)
        {
            new SessionArchive(home).Unarchive([park.Session]);
            _context.Marks.Clear(park.Session);
        }

        // Whether the words go on in the record's own conversation is the course's answer, read over the judge's reason.
        var kept = _context.Conversations.Read(park.Session);
        var why = adapter is null
            ? null
            : Continuations.Judge(
                tree => treeStands && ConversationTrees.Stands(tree), park, adapter.Id.Value, adapter.Can.Resumes, selection.Profile, kept, account);
        var course = DrivenCourse.GoOn(
            park, quest.Status, why?.Code ?? (adapter is not null && kept is null ? ContinueWhy.Unkept : null), agentGone: adapter is null);
        _driver.Decided?.Invoke(course);

        // 🔴 An adapter that is gone proves nothing of the conversation, and only the person can choose
        // the agent it goes on with, so the record waits as it is, a park still parked, its words unmarked, saying so with the hand-over's
        // offer. Never a park ended for a carry-on that could not start on that agent either.
        if (adapter is null)
        {
            var gone = Continuations.AgentGone(agent, park.Session);
            HeldSaying(park, gone);
            return (new StartRun($"held  #{quest.Id} → {quest.To}: {gone.Note}", false, null, gone.Note), null, false);
        }

        if (course is not LineCourse.Resume || kept is null)
        {
            var reason = why ?? ContinueWhy.Of(ContinueWhy.Unkept);
            if (await EndParkAsync(park, reason, agent, ct).ConfigureAwait(false) is { } refused)
            {
                return (new StartRun($"held  #{quest.Id} → {quest.To}: {refused}", false, null, refused), null, false);
            }

            return (null, reason, false);
        }

        // The park holds its tree in the ledger the whole time, so a replay already sees it in use. An ended
        // record holds it again only once the ledger moves it to working, so its starting hold is kept until then.
        if (park.Parked) starting?.Dispose();

        var sessionId = park.Session;
        var workTree = park.Tree!;
        var before = await WorkingTree.HeadAsync(workTree, ct).ConfigureAwait(false);
        var across = AcrossRules.Reach(config, registry, quest.To, start.Workspace);
        var target = StartTargets.WithLanguage(SessionTarget.ForQuest(quest, workTree, service.BaseUrl), config, start.Workspace) with
        {
            Asked = park.Asked ?? ActorAsked.Before(adapter.Id.Value),
            ReadsAcross = across.Reads,
            WritesAcross = across.Writes,
            Session = sessionId,
            // It goes on under the permission prompts standing now, on either door and in each run after.
            Access = permissions,
        };
        // Another agent's findings among the words: said as that agent's, and answered with the one
        // tool they need, which the person's rules may not name.
        var findings = park.Findings.Count > 0;
        // A helper's answer is another agent's claim too: said as its, never as the person's.
        var helper = park.Helpers.Count > 0;
        var persons = park.Waiting.Any(word => word.Persons);
        // Daoris's own words: a reopen told, a step's go-on, said as Daoris's, never as the person's.
        var daoris = park.Waiting.Any(word => word.Daoris);
        var transcript = DaorisHome.Entry(home, HomeEntry.Sessions, $"{sessionId}.log");

        // 🔴 The ledger's move to working comes before the words are read and the run is spawned, so a
        // word taken back between the look and the move never goes (Continuations.HandedAfterMove). A park's note is replaced
        // while it works; an ended record's keeps what ended it and says it goes on, with another agent's findings where
        // those are all it goes on with, never "your words".
        // A park's note follows who answered, never "your answer" where none of the owner's
        // words wait.
        var workingNote = park.Parked
            ? persons || Continuations.AuthorsOf(park.Waiting) is not { Count: > 0 } others ? Continuations.WorkingNoted
                : Continuations.WorkingWithOthersNoted(others)
            : findings && !persons ? Continuations.GoingOnWithFindingsNoted
            : helper && !persons ? Continuations.GoingOnWithHelperNoted
            : daoris && !persons ? Continuations.GoingOnWithDaorisNoted
            : Continuations.GoingOnNoted;
        // 🔴 The move is admitted under the session's decision lock. A held decision
        // moves nothing; a released one's word is named in `expect` with the record required still parked, and the move draws a
        // single-use ticket; every other move names none, so a supervisor's word the look did not admit refuses it (`words-changed`).
        SupervisorTicket? ticket;
        try
        {
            var (moved, admitted) = await _context.Supervisor.AdmitAsync(
                sessionId,
                async expect =>
                {
                    await service.AdvanceAsync(
                        sessionId, "working", workingNote, transcript: transcript, ct: ct,
                        expected: expect.Count > 0 ? "awaiting-person" : null, expect: expect).ConfigureAwait(false);
                    return true;
                },
                Guid.NewGuid().ToString("N")[..12], DateTimeOffset.UtcNow, ct).ConfigureAwait(false);
            if (!moved)
            {
                starting?.Dispose();
                const string held = "the supervisor's answer on it is held for the owner, or no longer stands: the next look plans it again.";
                return (new StartRun($"held  #{quest.Id} → {quest.To}: {held}", false, null, held), null, false);
            }

            ticket = admitted;
        }
        catch (DriverException refused)
        {
            starting?.Dispose();
            // Every word taken back before it moved: an ended record with none waiting does not move, and nothing holds it.
            if (await ReadAgainAsync(service, sessionId, ct).ConfigureAwait(false) is { Running: false, Waiting.Count: 0 })
            {
                return (TakenBackFirst(quest, sessionId), null, false);
            }

            // The person stopped or answered it, or another run took it up, between the look and here: the next look reads it as
            // it now is.
            return (new StartRun($"held  #{quest.Id} → {quest.To}: {refused.Message}", false, null, refused.Message), null, false);
        }

        // Its process's token and `start` as its move returns, before the tree lock and the start gate; let go wherever
        // no process is spawned under it.
        var proc = ChildLineage.Opened(home, sessionId);
        // Whether its process started: a failure before then started no run, so the record goes back with its words,
        // and so does the driver's close before then.
        var spawned = false;
        // Its tree locked in its name again while it goes on, a park's kept from its run before, asked before
        // the starting hold goes, as a start's is. Never a reason to hold it.
        var locksTree = _context.Trees.Holds(workTree);
        try
        {
            if (locksTree && await SessionLocks.HoldAsync(workTree, sessionId, ct).ConfigureAwait(false) is { } unlocked)
            {
                _driver.Log?.Warn("tree.unlocked", ("session", sessionId), ("why", unlocked));
            }

            // The ledger holds its tree again.
            starting?.Dispose();

            // Under permission prompts changed since it started it says so first, with the mode it got.
            // Its record keeps the setup it opened under.
            if (permissions is not null && DrivenAccess.GoingOnNote(park, permissions) is { } changed)
            {
                _context.Events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = changed }, say: null);
            }

            // Its record names the setup this run goes on under, before anything is spawned. A refusal is said on its console; the run goes on, since the setup it runs
            // under is already set.
            if (permissions is not null)
            {
                try
                {
                    await DrivenAccess.KeepAsync(
                        service, park, permissions, AccessRules.Resolve(config, quest.To, start.Workspace), ct).ConfigureAwait(false);
                }
                catch (DriverException refused)
                {
                    _context.Events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = refused.Message }, say: null);
                }
            }
        }
        catch
        {
            LineageTokens.Current.Unspawned(proc);
            throw;
        }

        ResumeAsk? resume = null;
        // The stop made of it before a close cut its conclusion off, told as its run lets go.
        SessionStop? stoppedFirst = null;
        // Whether its record still holds its tree as the run ends.
        var holdsTree = false;
        try
        {
            // Every word waiting once it moved, as the move left them: none can be taken back from a record that runs.
            var again = await ReadAgainAsync(service, sessionId, ct).ConfigureAwait(false);
            // A supervisor's word is handed only where it is the ticket's; with a ticket, a re-read that
            // failed or no longer holds its word is not-handed, the park restored less it.
            if (ticket is { } admittedFirst && (again is null || !again.Waiting.Any(word => word.Id == admittedFirst.Word)))
            {
                return await NotHandedAsync(quest, park, admittedFirst, again is null ? "unread" : "taken").ConfigureAwait(false);
            }

            var words = Continuations.Ticketed(Continuations.HandedAfterMove(park.Waiting, again), ticket);
            if (words.Count == 0) return await PutBackAsync(quest, park).ConfigureAwait(false);
            // A word pressed as an answer of its park's question carries the mark shown with it, so it
            // is handed as that answer chosen and recorded as one.
            words = Continuations.Ticketed(Continuations.Marked(words, _context.Events.ChoicesOf(sessionId)), ticket);
            // The ticket's word is handed as the paragraph composed from the decision in memory, never its stored text; the ticket spent.
            if (ticket is { } handing)
            {
                if (_context.Supervisor.Hand(handing) is not { } paragraph)
                {
                    return await NotHandedAsync(quest, park, handing, "spent").ConfigureAwait(false);
                }

                words = [.. words.Select(word => word.Id == handing.Word ? word with { Text = paragraph } : word)];
            }

            // Each word's files where they are kept as they are said, handed with the words; what its start did with
            // its tree against the line first after them, since that is where its next commit goes.
            var after = await ResumedAfterAsync(service, sessionId, target.Language, ct).ConfigureAwait(false);
            // Daoris's count for its line, where one moved or stands near what Daoris does on it, last.
            var counted = CountTexts.GoOn((await LineCountsRead.FactsAsync(service, home, sessionId, ct).ConfigureAwait(false)).Counts);
            resume = new ResumeAsk(
                kept.Conversation, words,
                // A helper's answer handed beside the person's at this look is among the words, though not among the look's.
                Continuations.OpeningNoted(
                    adapter.Id.Value, selection.Version, park.DoorVersion, answer: park.Parked, findings, persons,
                    helper || words.Any(word => word.Helper is not null), daoris, words.Any(word => word.Supervisors)),
                // Where its go-aheads changed since it started, told after the rest, since its instruction is not handed again.
                Continuations.AfterWords(
                    Continuations.AfterWords(
                        Continuations.AfterWords(inSync, after),
                        Continuations.GoAheadsChanged(park, AccessRules.Resolve(config, quest.To, start.Workspace))),
                    counted),
                files: word => ChatFiles.Kept(home, sessionId, word.Files));

            // The protocol door resumes on its wire, from the same spawn a start has; the native door is run on the
            // conversation it kept.
            var prepared = !adapter.Can.ResumesByRestart
                ? null
                : _context.Adapters.Resume(adapter, SpawnRequests.Of(target, config.Commands.GetValueOrDefault(adapter.Id.Value)) with { Resume = kept.Conversation });
            var (info, harnessNotice) = _driver.Prepare(adapter, target, selection, prepared);
            // A set-up step going on with the person's words shows it again in its own tab: opened
            // again where the person closed it, and brought forward. Said, never held, where it will not open: the words go on.
            var tabNotice = quest.SetUpIn is not null && _driver.Reviews is { } desk
                            && ReviewSetUps.Environment(config, quest, start.Workspace) is { } shownIn
                            && await desk.OpenForStepAsync(quest, shownIn, ct).ConfigureAwait(false) is { } unopened
                ? $"— {unopened}"
                : null;
            var (servers, browserNotice, drivesBrowser) = await SessionServers.HandAsync(_context.Home, _context.Servers, _context.Browser, _context.Log, ct).ConfigureAwait(false);
            browserNotice = Driver.JoinNotices(browserNotice, tabNotice);
            _context.Hooks?.Log.Served(_context.Catalog, sessionId, servers);
            var connectors = ConnectorPlans.For(sessionId, start.Workspace, servers);
            var handed = adapter.ServerHanding()?.Hand(info, home, connectors);
            // The skills plugins hand sessions, as its first run gets them, gone with this run.
            var (skills, skillPlugins) = SpawnSkills.Hand(_context.Adapters, adapter, info, home, sessionId, _context.Catalog, PluginServerTo.Sessions);
            using var skilled = new Disposer(() => SpawnFiles.RemoveFolder(skills));
            // It goes on under the checks its repository sets now, as it goes on under the prompts
            // standing now: their deny rules handed again where its agent takes Daoris's rules, and kept by its id for `run-heavy` while
            // this run lasts. Its instruction is not handed again, so its words are its first start's.
            // A named workflow's run takes its bound version's, so it is read wherever a run here is bound to one; a
            // graph version's `callMinutes` is kept beside them, as its first start kept it.
            var process = SessionChecks.For(config, quest.To) is null && !WorkflowRunBindings.AnyNamed(home, quest.To)
                ? null
                : await WorkflowProcesses.ReadAsync(
                    home, config, new ServiceReviewWorld(service), quest.Id, quest.To, start.Workspace, ct).ConfigureAwait(false);
            var checks = SessionChecks.Of(config, quest.To, process);
            var checksKept = SessionChecks.Hold(home, sessionId, checks, CallBound.MinutesOf(process));
            using var checksHeld = new Disposer(() => SessionChecks.LetGo(checksKept));
            var rules = _driver.HandRules(
                adapter, info, sessionId, start.Workspace, quest.To, workTree, target.AttachmentsDirectory, across: across,
                said: SaidFilesFolder(home, sessionId, resume), job: findings ? AnswersFindings : null, plugins: skillPlugins,
                heavy: HeavyTold.Hand(workTree, home, quest.To, checks, adapter.Can.TakesSettings));

            onOpened();
            var openedAt = DateTimeOffset.UtcNow;
            // The park's question is over as its record goes on, so a press another window shows it with is refused at once.
            _driver.Questions(sessionId, new QuestionFiles(_context.Home).Clear(sessionId));
            using var live = _context.Runs.GoesLive(quest.Id, sessionId);

            var (run, refusedWhy) = await _driver.Holds.HoldAsync(
                park.Actor ?? "build", adapter, info, target, sessionId, transcript, workTree, Driver.JoinNotices(harnessNotice, browserNotice), rules, handed,
                refusesInput: Driver.TakesNoMessages(quest),
                ct: ct,
                preamble: browserNotice,
                handedPlan: connectors,
                drivesBrowser: drivesBrowser,
                // A resumed run is on the account it parked on, so what it says about that account's windows is kept
                // as any run's is.
                said: _driver.Said(adapter, selection, sessionId), limits: _driver.NativeLimit(adapter),
                resume: resume,
                goesOnAs: selection,
                // A resumed run waits on its own background work as a start does, by the same judgement; where its
                // quest is closed as the look planned it, never.
                waitsOnBackground: SessionWaits.OnBackground(service, quest, sessionId, resumed: true),
                // A closed quest's resume is carried on once; where it is the person's Accept's own carry-on (the session's
                // yes is kept), the Accept's count bounds it and the run carries nothing on.
                carriesOn: SessionWaits.CarriesOn(
                    service, quest, workTree, resumed: true, () => _context.Processes.StopMade(sessionId) is not null,
                    accepted: new CarriedAccepts(home).Of(sessionId) is not null),
                stopMade: stop => stoppedFirst = stop,
                movedFirst: true,
                moved: proc,
                spawned: () =>
                {
                    spawned = true;
                    // Went-on at the spawn of the run built with the ticket's word, at that moment.
                    if (ticket is { } went) _ = _context.Supervisor.WentOnAsync(went, DateTimeOffset.UtcNow, CancellationToken.None);
                },
                // A resume keeps its asked choice and never moves to another, so a refusal at the open ends the
                // open and puts the record back (ConcludeResumedAsync); what the open lists is kept as its account's reading.
                offers: new OpenOffers(selection.Version, selection.Profile, _ => Task.FromResult<(ActorAsked Asked, string Choice)?>(null)),
                // A continuation of a driven quest session is tried again as its start is, read against
                // the quest as the look planned it, so a closed quest's word and an answered question's resume are this run's.
                transientWanted: retryCt => TransientRetries.WantedAsync(service, quest.Id, sessionId, quest.Status, quest.Awaits, retryCt),
                conclude: (exitCode, used, turnFailed, ended) =>
                    _conclusion.ConcludeResumedAsync(quest, park, adapter, selection, resume, workTree, transcript, before, exitCode, used, turnFailed, ended,
                        openedAt, ct))
                .ConfigureAwait(false);
            // A record that parked again, or goes on at the next look, still holds its tree.
            holdsTree = run is { Opened: true, Ended: null };
            return (run, refusedWhy, resume.Spoken);
        }
        catch (Exception error) when (!spawned && ticket is not null)
        {
            // 🔴 Every failure between the admission and the wire, the driver's close among them, is not-handed:
            // nothing is spawned with the supervisor's word, and the park is restored with its queue less that word.
            return await NotHandedAsync(quest, park, ticket, error is OperationCanceledException ? "closed" : "unstarted").ConfigureAwait(false);
        }
        catch (Exception error) when (!spawned && !(error is OperationCanceledException && ct.IsCancellationRequested))
        {
            // 🔴 No run started, so this is a start failure, never a move.
            // The record ends `failed` as a first start's failed spawn does, counted by the strikes, its words waiting on it to go again;
            // nothing is handed on and no part opens for it.
            return await UnstartedAsync(
                    quest, park, agent, error.Message, resume?.Ids ?? [.. park.Waiting.Select(word => word.Id).OfType<string>()])
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested && !spawned && !park.Parked)
        {
            // 🔴 The driver closed before its run started, most often while its start
            // waited for a place. No run began, so the record the move took up goes back as it ended with its words waiting, and the
            // next start goes on with them; nothing is handed on, since no session opens as the driver closes. A park is closed below
            // as the close's interrupted take: back to waiting on the person the ledger would clear its words (a park asks
            // anew), while closed they stay on it for the next start.
            return await PutBackAsync(quest, park, unsent: ClosedFirst(quest, sessionId)).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The driver is closing under it: an interrupted take, carried on at the next start. Unless a stop came first,
            // whose record it is, never interrupted, as a first run's close reads it.
            var (closed, interrupted) = Observation.Closed(stoppedFirst);
            try
            {
                await service.AdvanceAsync(
                    sessionId, closed.State,
                    stoppedFirst is null && _context.Processes.Close.Build is { } build ? ShellClose.Closed(build) : closed.AsNoted(),
                    ct: CancellationToken.None, interrupted: interrupted).ConfigureAwait(false);
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
            // moved on first, and the next look reads it as it now is. Never a park ended for a fallback here, since a run
            // that failed is no refusal of its conversation.
            try
            {
                await service.AdvanceAsync(sessionId, "failed", Observation.Failure(error.Message), ct: CancellationToken.None).ConfigureAwait(false);
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
            // A run put back before its spawn, or whose spawn made none, tells its token so.
            if (!spawned) LineageTokens.Current.Unspawned(proc);
            output?.Close(sessionId);
            // A resume that has been tried lets the starting hold go however it ended, so a carry-on takes it again.
            starting?.Dispose();
            // The session lets go of its tree however this run ended, on no token, unless its record still holds it. A
            // carry-on into the same tree takes the lock in its own name.
            if (locksTree && !holdsTree) await SessionLocks.LetGoAsync(workTree, sessionId, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// What a resumed run handed another agent's findings is given beside the person's rules: the
    /// connector's <c>opinion_answer</c>, which no default names. Without it the harness would ask, and every ask is refused,
    /// so the session could check every finding and answer none. A deny the person wrote still wins.
    /// </summary>
    internal static IReadOnlyList<string> AnswersFindings => KnowledgeConnector.AnswersFindings;

    /// <summary>
    /// What a resumed run's turn carries after the person's words: the answers to the go-aheads its session asked, read from its
    /// quest's ask as the run takes the record up, since its conversation got the ask's at its
    /// start and is told the answers after the words; then the work's session language where one is set, since it
    /// may have changed since the conversation got it. Unread, or on no ask, nothing of the go-aheads; none set, and
    /// no language line.
    /// </summary>
    /// <remarks>
    /// <b>A park its go-aheads' answers woke</b> holds the blank answer the service keeps for one, and its turn is
    /// that, then this: each go-ahead it asked, which, approved or refused, and the person's words, quoted as theirs.
    /// </remarks>
    internal static async Task<string> ResumedAfterAsync(
        ServiceClient service, string session, SessionLanguage? language, CancellationToken ct)
    {
        var asked = await AskWords.ReadOfSessionAsync(service, session, ct).ConfigureAwait(false);
        return SessionLanguageText.Resumed(GoAheadsText.Resumed("", asked, session).TrimStart(), language);
    }

    /// <summary>
    /// The folder a resumed run is handed a read of for the files said with its words: the session's own
    /// files folder, outside its tree, where any word it goes on with carries a kept file; null where none does. A read of
    /// exactly that folder, as a conversation is handed its own: every other read there would be
    /// asked, and every ask is refused.
    /// </summary>
    internal static string? SaidFilesFolder(string home, string sessionId, ResumeAsk resume) =>
        resume.Files.Count > 0 ? ChatFiles.Folder(home, sessionId) : null;

    /// <summary>
    /// The record a fallback goes on from, its words as they stand now (<see cref="Continuations.HandedOn"/>): the
    /// look's words may have been taken back since, and one taken back must never reach an agent.
    /// </summary>
    internal async Task<PriorSession> HandedOnRecordAsync(PriorSession look, CancellationToken ct) =>
        look.Parked ? look : Continuations.HandedOn(look, await ReadAgainAsync(_context.Service, look.Session, ct).ConfigureAwait(false));

    /// <summary>
    /// The waited quest's result counts on the line a resume carries on, reserved before the run is handed
    /// it and once however often the start is tried. Nothing here holds the start.
    /// </summary>
    internal async Task WaitedAsync(PriorSession resumes, string answered, PriorSession? handsOn, CancellationToken ct) =>
        new AnswersAloneHold(_context.Events, _context.Marks, new ContinuationAdmissions(_context.Home)).Waited(
            resumes, answered, handsOn?.Waiting ?? [], await AnswersAloneHold.RecordsAsync(_context.Service, ct).ConfigureAwait(false));

    /// <summary>
    /// The record a resume hands words on from: the session that asked and ended waiting, as it stands now,
    /// where any word it is handed waits on it (<see cref="HandedToResume"/>); null where none does. A resume opens a new
    /// session handed the answer, so the words said to the asker meanwhile go there, or they would wait on a record no look takes up
    /// again (<see cref="WordsNever.Superseded"/>).
    /// </summary>
    internal async Task<PriorSession?> ResumeWordsAsync(PriorSession asked, CancellationToken ct)
    {
        if (!asked.WordsWaiting) return null;
        var now = await HandedOnRecordAsync(asked, ct).ConfigureAwait(false);
        return now.Waiting.Any(HandedToResume) ? now : null;
    }

    /// <summary>
    /// The words a resume is handed from the session that asked: the person's, and Daoris's own, said as Daoris's, since the
    /// resume starts anyway and Daoris's never starts one by itself. Another agent's findings and a helper's answer go to the
    /// person instead, as any new session's do.
    /// </summary>
    internal static bool HandedToResume(SaidWordView word) => word.Persons || word.Daoris;

}

