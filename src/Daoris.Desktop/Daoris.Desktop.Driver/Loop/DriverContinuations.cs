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
    /// <remarks>
    /// 🔴 SAYHELD1: never judged by the quest's <c>awaits</c>. The planner plans words going on only where no question holds them, and
    /// a quest resumed on its answer still names that question (Planner's <c>ResumedOnAnswer</c>), so its park's answer took the walk.
    /// </remarks>
    internal PriorSession? ResumesOwn(Consideration start, string agent)
    {
        if (!start.GoesOn || start.Resumes is not { WordsWaiting: true } words) return null;
        var ranOn = words.Adapter ?? _context.Conversations.Read(words.Session)?.Adapter;
        return ranOn is not { Length: > 0 } || string.Equals(ranOn, agent, StringComparison.OrdinalIgnoreCase) ? words : null;
    }

    /// <summary>
    /// Whether a record an account's limit ended could resume its own conversation on <paramref name="agent"/> (LIMITRESUME1b, D206; the
    /// limit-resume design §3.3 step 1): the judge passing as if words waited, since with none it answers <c>ended</c> (the same agent and
    /// account, its tree, a kept id, a door that resumes), on a door whose resume of a missing conversation is known to fail closed. The
    /// protocol door refuses one with <c>resource_not_found</c> before any prompt; the native door goes by the brief until row 1f has
    /// recorded what its <c>--resume</c> of a missing id does.
    /// </summary>
    internal bool LimitResumes(PriorSession limited, string agent)
    {
        ISessionAdapter adapter;
        try { adapter = _context.Adapters.Resolve(agent); } catch (DriverException) { return false; }
        if (adapter.Wire != SessionWire.Acp) return false;

        var asIfWords = limited with { Said = [new SaidWordView(null, LimitResume.Words, DateTimeOffset.UnixEpoch, [], true) { From = DaorisWords.From }] };
        return Continuations.Judge(
            ConversationTrees.Stands, asIfWords, adapter.Name, adapter.Resumes, limited.Profile, _context.Conversations.Read(limited.Session)) is null;
    }

    /// <summary>
    /// Daoris's limit word kept on the ended record through the say door (LIMITRESUME1b, the design §3.3 step 1), as
    /// <see cref="RebaseTelling"/> keeps its own, under the key of the cool-off that held it, so a later look's start keeps one; then the
    /// record as it stands now, its words the person's first and Daoris's last, as the say door keeps them. Null where the door refused
    /// it, and the line goes on as the carry-on it was planned.
    /// </summary>
    internal async Task<(PriorSession Record, string Word)?> KeepLimitWordAsync(PriorSession limited, CancellationToken ct)
    {
        SayAnswer said;
        try
        {
            said = await _context.Service.SayAsync(
                    limited.Session, LimitResume.Words, ct: ct, from: DaorisWords.From,
                    key: DaorisKeys.LimitReset(limited.Session, LimitResume.Until(limited)))
                .ConfigureAwait(false);
        }
        catch (Exception error) when (error is DriverException or HttpRequestException or System.Text.Json.JsonException)
        {
            return null;
        }

        if (!said.Kept || said.Word?.Id is not { } word) return null;
        var now = await ReadAgainAsync(_context.Service, limited.Session, ct).ConfigureAwait(false);
        if (now is null) return null;
        return (Continuations.HandedOn(limited, now), word);
    }

    /// <summary>
    /// Daoris's limit words taken off a record by their ids (LIMITRESUME1b): at every conclusion of the run they went with, and where its
    /// own conversation could not take them, the record's own session named as the one that took them. A refusal costs nothing more:
    /// the next attempt keeps its own under its own cool-off's key.
    /// </summary>
    internal async Task TakeLimitWordsOffAsync(string session, IReadOnlyList<string> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return;
        try
        {
            await _context.Service.TakenAsync(session, ids, by: session, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is HttpRequestException or DriverException
                                          || (error is OperationCanceledException && !ct.IsCancellationRequested))
        {
            _context.Output?.Append(session, $"— Daoris's limit word could not be taken off: {error.Message}");
        }
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
    /// <see cref="ResumeWords.NotYetOn"/>'s, by code (CONVNOTE2g); the last event is compared by its English, as it always was. A start
    /// held because the record's tree is gone and cannot be made again says so the same way (TREEGONE1).
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
    /// <param name="permissions">
    /// The mode its repository's permission prompts name on its harness now (AUTONOMY1b, D184 point 3), which it goes on under, saying
    /// the change from the setup its record started under; null keeps the adapter's own.
    /// </param>
    /// <param name="treeStands">
    /// Whether the start found the record's tree there as git lists it (TREEGONE1): false where it is not, whatever folder stands at its
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

        // 🔴 SESSCTX1c (D189's amendment): an adapter no longer here proves nothing of the conversation, and only the person can choose
        // the agent it goes on with, so the record waits as it is, a park still parked, its words unmarked, saying so with the hand-over's
        // offer. Never a park ended for a carry-on that could not start on that agent either.
        if (adapter is null)
        {
            var gone = Continuations.AgentGone(agent, park.Session);
            HeldSaying(park, gone);
            return (new StartRun($"held  #{quest.Id} → {quest.To}: {gone.Note}", false, null, gone.Note), null, false);
        }

        var kept = _context.Conversations.Read(park.Session);
        var why = Continuations.Judge(
            tree => treeStands && ConversationTrees.Stands(tree), park, adapter.Name, adapter.Resumes, selection.Profile, kept, account);
        if (why is not null || kept is null)
        {
            var reason = why ?? ContinueWhy.Of(ContinueWhy.Unkept);
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
            Asked = park.Asked ?? ActorAsked.Before(adapter.Name),
            ReadsAcross = across.Reads,
            WritesAcross = across.Writes,
            Session = sessionId,
            // It goes on under the permission prompts standing now, on either door and in each run after (AUTONOMY1b).
            Access = permissions,
        };
        // Another agent's findings among the words (XAGENT1e, D155 point 7): said as that agent's, and answered with the one
        // tool they need, which the person's rules may not name.
        var findings = park.Findings.Count > 0;
        // A helper's answer (MIXAGENT2a-ii) is another agent's claim too: said as its, never as the person's.
        var helper = park.Helpers.Count > 0;
        var persons = park.Waiting.Any(word => word.Persons);
        // Daoris's own words (COWORK1b): a reopen told, a step's go-on, said as Daoris's, never as the person's.
        var daoris = park.Waiting.Any(word => word.Daoris);
        var transcript = DaorisHome.Entry(home, HomeEntry.Sessions, $"{sessionId}.log");

        // 🔴 SAIDBACK1e (D137 §2.4): the ledger's move to working comes before the words are read and the run is spawned, so a
        // word taken back between the look and the move never goes (Continuations.HandedAfterMove). A park's note is replaced
        // while it works; an ended record's keeps what ended it and says it goes on (MSG1b), with another agent's findings where
        // those are all it goes on with (XAGENT1e), never "your words".
        var workingNote = park.Parked ? Continuations.WorkingNoted
            : findings && !persons ? Continuations.GoingOnWithFindingsNoted
            : helper && !persons ? Continuations.GoingOnWithHelperNoted
            : daoris && !persons ? Continuations.GoingOnWithDaorisNoted
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

        // TREELOCK1 (D51's note): its tree locked in its name again while it goes on, a park's kept from its run before, asked before
        // the starting hold goes, as a start's is. Never a reason to hold it.
        var locksTree = _context.Trees.Holds(workTree);
        if (locksTree && await SessionLocks.HoldAsync(workTree, sessionId, ct).ConfigureAwait(false) is { } unlocked)
        {
            _driver.Log?.Warn("tree.unlocked", ("session", sessionId), ("why", unlocked));
        }

        // The ledger holds its tree again (LEFT2).
        starting?.Dispose();

        // Under permission prompts changed since it started it says so first, with the mode it got (AUTONOMY1b, D183 point 3 as
        // HELPMODE1a2 says Ask Daoris's). Its record keeps the setup it opened under.
        if (permissions is not null && DrivenAccess.GoingOnNote(park, permissions) is { } changed)
        {
            _context.Events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = changed }, say: null);
        }

        // AUTONOMY1b2 (D184 point 1): and its record names the setup this run goes on under, before anything is spawned, as Ask Daoris's
        // record names its mode (HELPMODE1a2). A refusal is said on its console; the run goes on, since the setup it runs under is
        // already set.
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

        ResumeAsk? resume = null;
        // Whether its process started: a failure before then started no run, so the record goes back with its words (SAIDBACK1e),
        // and so does the driver's close before then (PARALLEL1f).
        var spawned = false;
        // The stop made of it before a close cut its conclusion off, told as its run lets go (FLAKE3).
        SessionStop? stoppedFirst = null;
        // TREELOCK1: whether its record still holds its tree as the run ends.
        var holdsTree = false;
        try
        {
            // Every word waiting once it moved, as the move left them (SAIDBACK1d): none can be taken back from a record that runs.
            var words = Continuations.HandedAfterMove(park.Waiting, await ReadAgainAsync(service, sessionId, ct).ConfigureAwait(false));
            if (words.Count == 0) return await PutBackAsync(quest, park).ConfigureAwait(false);
            // PARKVIEW1 (Fable's read, Q4): a word pressed as an answer of its park's question carries the mark it was shown under, so it
            // is handed as that answer chosen and recorded as one.
            words = Continuations.Marked(words, _context.Events.ChoicesOf(sessionId));

            // Each word's files where they were kept as it was said (MSG1d3, D137 §2.4), handed with the words; what its start did with
            // its tree against the line first after them (LANDSYNC2), since that is where its next commit goes.
            var after = await ResumedAfterAsync(service, quest, sessionId, target.Language, ct).ConfigureAwait(false);
            resume = new ResumeAsk(
                kept.Conversation, words,
                // HELPERWAIT1: a helper's answer handed beside the person's at this look is among the words, though not among the look's.
                Continuations.OpeningNoted(
                    adapter.Name, selection.Version, park.HarnessVersion, answer: park.Parked, findings, persons,
                    helper || words.Any(word => word.Helper is not null), daoris),
                // Where its go-aheads changed since it started (AUTONOMY1c), told after the rest, since its instruction is not handed again.
                Continuations.AfterWords(
                    Continuations.AfterWords(inSync, after),
                    Continuations.GoAheadsChanged(park, AccessRules.Resolve(config, quest.To, start.Workspace))),
                files: word => ChatFiles.Kept(home, sessionId, word.Files));

            // The protocol door resumes on its wire, from the same spawn a start has; the native door is run on the
            // conversation it kept.
            var prepared = adapter.Wire == SessionWire.Acp
                ? null
                : adapter.PrepareResume(target, config.Commands.GetValueOrDefault(adapter.Name), kept.Conversation, resume.Prompt);
            var (info, harnessNotice) = _driver.Prepare(adapter, target, selection, prepared);
            await _driver.KeepHandedAsync(sessionId, info, ct).ConfigureAwait(false);
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
            // HELPPLUG1b (D170 point 4): the skills plugins hand sessions, as its first run was handed them, gone with this run.
            var (skills, skillPlugins) = SpawnSkills.Hand(adapter, info, home, sessionId, _context.Catalog, PluginServerTo.Sessions);
            using var skilled = new Disposer(() => SpawnSkills.Remove(skills));
            // FABRIC-PIPELINE1b (D192 point 5): it goes on under the checks its repository sets now, as it goes on under the prompts
            // standing now: their deny rules handed again where its agent takes Daoris's rules, and kept by its id for `run-heavy` while
            // this run lasts. Its instruction is not handed again, so its words are its first start's.
            // A named workflow's run takes its bound version's (FABRIC-PIPELINE1b2), so it is read wherever a run here is bound to one; a
            // graph version's `callMinutes` is kept beside them, as its first start kept it (FABRIC-PIPELINE1b3).
            var process = SessionChecks.For(config, quest.To) is null && !WorkflowRunBindings.AnyNamed(home, quest.To)
                ? null
                : await WorkflowProcesses.ReadAsync(
                    home, config, new ServiceReviewWorld(service), quest.Id, quest.To, start.Workspace, ct).ConfigureAwait(false);
            var checks = SessionChecks.Of(config, quest.To, process);
            var checksKept = SessionChecks.Hold(home, sessionId, checks, LongCalls.MinutesOf(process));
            using var checksHeld = new Disposer(() => SessionChecks.LetGo(checksKept));
            var rules = _driver.HandRules(
                adapter, info, sessionId, start.Workspace, quest.To, workTree, target.AttachmentsDirectory, across: across,
                said: SaidFilesFolder(home, sessionId, resume), job: findings ? AnswersFindings : null, plugins: skillPlugins,
                heavy: HeavyTold.Hand(workTree, home, quest.To, checks, adapter.TakesSettings));

            onOpened();
            var openedAt = DateTimeOffset.UtcNow;
            // PARKVIEW1 (the park's ask design §2; Fable's read, Q2): the park's question is over as its record goes on, so a press another
            // window still shows it with is refused as changed at once; one the disk keeps is logged.
            _driver.Questions(sessionId, new QuestionFiles(_context.Home).Clear(sessionId));
            _context.Runs.Live[quest.Id] = sessionId;
            using var live = new Disposer(() => _context.Runs.Live.TryRemove(quest.Id, out var _));

            var (run, refusedWhy) = await _driver.HoldAsync(
                park.Actor ?? "build", adapter, info, target, sessionId, transcript, workTree, Driver.JoinNotices(harnessNotice, browserNotice), rules, handed,
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
            // A record that parked again, or goes on at the next look, still holds its tree (TREELOCK1).
            holdsTree = run is { Opened: true, Ended: null };
            return (run, refusedWhy, resume.Spoken);
        }
        catch (Exception error) when (!spawned && !(error is OperationCanceledException && ct.IsCancellationRequested))
        {
            // 🔴 SESSCTX1c (D189's amendment, the session-context design §2): no run started, so this is a start failure, never a move.
            // The record ends `failed` as a first start's failed spawn does, counted by the strikes, its words waiting on it to go again;
            // nothing is handed on and no part opens for it.
            return await UnstartedAsync(
                    quest, park, agent, error.Message, resume?.Ids ?? [.. park.Waiting.Select(word => word.Id).OfType<string>()])
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested && !spawned && !park.Parked)
        {
            // 🔴 PARALLEL1f (D181's PARALLEL1de note, D137 §2.4): the driver closed before its run started, most often while its start
            // waited for a place. No run began, so the record the move took up goes back as it ended with its words waiting, and the
            // next start goes on with them; nothing is handed on, since no session opens as the driver closes. A park is closed below
            // as the close's interrupted take (D104): back to waiting on the person the ledger would clear its words (a park asks
            // anew), while closed they stay on it for the next start.
            return await PutBackAsync(quest, park, unsent: ClosedFirst(quest, sessionId)).ConfigureAwait(false);
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
            // moved on first, and the next look reads it as it now is. SESSCTX1c: never a park ended for a fallback here, since a run
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
            output?.Close(sessionId);
            // A resume that was tried lets the starting hold go however it ended, so a carry-on takes it again (LEFT2).
            starting?.Dispose();
            // TREELOCK1: the session lets go of its tree however this run ended, on no token, unless its record still holds it. A
            // carry-on into the same tree takes the lock in its own name.
            if (locksTree && !holdsTree) await SessionLocks.LetGoAsync(workTree, sessionId, CancellationToken.None).ConfigureAwait(false);
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

        // 🔴 The native door's harness ended on a failed exit before naming its conversation, once more after its one retry as the same
        // resume (SESSCTX1c, D189's amendment; HoldAsync tried it): it never opened it, which a crash, a failed sign-in or a flag it
        // rejected ends the same way, so it is `exited`, never `gone`, and the record carries on as any refused resume's does. Read from
        // the structure, never from its words.
        if (resume.Refused is null && adapter.Wire == SessionWire.Pipe && !resume.Named && exitCode is int failed && failed != 0)
        {
            resume.Refused = ContinueWhy.Of(ContinueWhy.Exited);
        }

        var after = await service.FindQuestAsync(quest.Id, ct).ConfigureAwait(false);
        var status = after?.Status ?? "Open";
        // 🔴 Whether its quest had closed, as the look planned the run: never as the run left it, since a resumed run closes
        // its own quest, and read after it an answered park that finished its work took a closed quest's ending (MSG1b).
        var closed = quest.Status is not ("Open" or "Taken");

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
        // ACCESSSWAP1b: a run Daoris ended at its turn's end for a changed setup is neither a park nor a failure, and goes on again.
        var swappedOn = stop is null && ended.SetupChanged is not null;
        if (swappedOn) conclusion = Observation.SetupChanged(conclusion, ended.SetupChanged!);

        conclusion = _driver.AccountRefused(conclusion, adapter, selection, ended);
        conclusion = _driver.AccountSignedOut(conclusion, adapter, selection, turnFailed);
        (conclusion, var limited) = _driver.AccountLimited(conclusion, adapter, selection, turnFailed, sessionId, used);

        // 🔴 TURNFAIL1: words a failed turn carried were never answered. They stay waiting on the record, unmarked, as a cooling
        // account's do (D137 §2.2), and its note and conversation say why; failing a second time running, they wait for the
        // person (FailedTurnWords). Every other run took them. LIMITRESUME1b: a limited end's always wait (D137's note as D206 amends it).
        var words = stop is null && resume.Prompted && resume.Ids.Count > 0
            ? FailedTurnWords.Judge(conclusion, turnFailed, ended, park, limited)
            : WordsAfter.Taken;
        // LIMITRESUME1b (the design §3.3, round 3's B8 item 3): Daoris's limit word comes off at every conclusion, so it never reaches a
        // later attempt, a fallback or a new part; the rule that the words wait is for the person's, and with none of theirs nothing waits.
        var limitWords = words == WordsAfter.Taken ? [] : LimitResume.WordIds(resume.Words);
        await TakeLimitWordsOffAsync(sessionId, limitWords, ct).ConfigureAwait(false);
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
        service.AccountSaid(Took(park, adapter.Name, why: null));

        // REVIEWENV1c (design §2.6, §3.4): a set-up step that went on with the person's not yet says a new set-up, posted with
        // the commit its tree holds now, its correction among it.
        await ReviewSetUps.PostAsync(service, config, _context.Events, after, sessionId, workTree, quest.Workspace, openedAt, ct, _driver.Reviews)
            .ConfigureAwait(false);

        // LAND2b: as a first run's ending, so a resumed session that closes its quest done is due too. One that went on after
        // its landing moves its own branch on at the next look (LAND2c). FABRIC-CLOSE1h (§E5): a closure graph's submission reads as its done.
        await _driver.ConcludedForLandingAsync(sessionId, quest, QuestResults.Status(after) ?? status, conclusion.State, workTree, quest.Workspace, ct)
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
                    Quest: quest.Id, Adapter: adapter.Name, Account: selection.Profile) { Limit = limited }), null);
    }

    /// <summary>
    /// The record the move to working took up goes back where no run of it started and nothing went (SAIDBACK1e, D137 §2.4), as
    /// <see cref="Continuations.PutBack"/> says: every word taken back first, or the driver closed first (PARALLEL1f). Nothing is
    /// sent and nothing holds it. Where the ledger will not move it back, its refusal holds the start, and the next look reads the
    /// record as it now is. A run that could not start is <see cref="UnstartedAsync"/>'s (SESSCTX1c).
    /// </summary>
    /// <param name="unsent">
    /// The look's line: every word taken back first, unless the driver closed before its run started (PARALLEL1f,
    /// <see cref="ClosedFirst"/>), its words still waiting.
    /// </param>
    private async Task<(StartRun? Run, ContinueReason? FellBack, bool AnswerKept)> PutBackAsync(
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
    /// A resumed run that could not start (SESSCTX1c, D189's amendment; the session-context design §2): a start failure, never a move.
    /// The record the move to working took up ends <c>failed</c> with the spawn's words, as <see cref="Continuations.Unstarted"/> says,
    /// so the strikes count it as they count a first start's failed spawn (DRV6), and the words wait on it to go again; failing twice
    /// running they are marked and wait for the person, as a failed turn's are (TURNFAIL1). Its conversation says so beside them, by
    /// their ids. No new session opens and nothing is handed on.
    /// </summary>
    /// <param name="ids">The ids of the words that were to go, said beside the line.</param>
    private async Task<(StartRun? Run, ContinueReason? FellBack, bool AnswerKept)> UnstartedAsync(
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

    /// <summary>
    /// The record a fallback goes on from, its words as they stand now (SAIDBACK1f, <see cref="Continuations.HandedOn"/>): the
    /// look's words may have been taken back since, and one taken back must never reach an agent.
    /// </summary>
    internal async Task<PriorSession> HandedOnRecordAsync(PriorSession look, CancellationToken ct) =>
        look.Parked ? look : Continuations.HandedOn(look, await ReadAgainAsync(_context.Service, look.Session, ct).ConfigureAwait(false));

    /// <summary>
    /// The record a resume (D79) hands words on from (SAYHELD1, A): the session that asked and ended waiting, as it stands now
    /// (SAIDBACK1f), where any word it is handed waits on it (<see cref="HandedToResume"/>); null where none does. A resume opens a new
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
    /// The words a resume is handed from the session that asked (SAYHELD1): the person's, and Daoris's own, said as Daoris's, since the
    /// resume starts anyway and Daoris's never starts one by itself (COWORK1b). Another agent's findings and a helper's answer go to the
    /// person instead, as any new session's do (XAGENT1e, MIXAGENT2a-ii).
    /// </summary>
    internal static bool HandedToResume(SaidWordView word) => word.Persons || word.Daoris;

    /// <summary>The look's line where every word waiting on the record was taken back before it went on (SAIDBACK1d, SAIDBACK1e).</summary>
    internal static StartRun TakenBackFirst(QuestView quest, string session) => new(
        $"held  #{quest.Id} → {quest.To}: every word waiting on session {session} was taken back before it went on; nothing is sent.",
        false);

    /// <summary>
    /// The look's line where the driver closed before a record's run started (PARALLEL1f): put back as it ended, its words waiting on
    /// it for the next start, which goes on with them.
    /// </summary>
    internal static StartRun ClosedFirst(QuestView quest, string session) => new(
        $"held  #{quest.Id} → {quest.To}: the driver closed before session {session}'s run started; it is put back as it ended, "
        + "its words waiting for the next start.",
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
    /// A closed quest's session whose own conversation is proven lost goes on by itself as a conversation in its tree (SESSCTX1c part B,
    /// D189 point 3): handed to the shell's chat runner, which opens the new part, hands it the brief and the person's words and takes
    /// them off the record, naming it. Another agent's findings or answer among the words stay, marked, and go to the person (XAGENT1e),
    /// since the conversation was handed only the person's. Null where nothing went on there, and the session then waits for the person.
    /// </summary>
    internal async Task<StartRun?> GoesOnLostAsync(
        Func<string, ContinueReason, CancellationToken, Task<StartedFrom>> goesOn, QuestView quest, PriorSession record, ContinueReason why,
        CancellationToken ct)
    {
        if (!record.Waiting.Any(word => word.Persons)) return null;
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

        // A choice of a new session is spent with the words it named, which went now (MSG1g).
        _context.NewSessions.Clear(record.Session);
        if (record.Waiting.Any(word => !word.Persons))
        {
            _context.Marks.Mark(record.Session, record.Waiting.Where(word => !word.Persons).Select(word => word.Id).OfType<string>(), why, DateTimeOffset.UtcNow);
            _driver.Deliverer.FindingsToPerson(record, why);
        }

        return new StartRun($"moved   session {record.Session} (#{quest.Id} → {quest.To}): {moved.Message}", false);
    }

    /// <summary>
    /// The words a session could not go on with went to a new one (MSG1b, D137 §2.2): taken off the record they were said to
    /// by their ids, naming the session that took them, so the service keeps those said after it ended on the ask as
    /// <c>reopened</c>; and, for a record that had ended, said in its conversation where the person wrote them. A park's
    /// answer is said beneath its question as it always was. A refusal costs a line, never the start.
    /// </summary>
    /// <param name="resumed">
    /// A resume's (SAYHELD1): the session opened anyway, so Daoris's own words go on in it too (<see cref="HandedToResume"/>).
    /// </param>
    internal async Task HandedOnAsync(PriorSession record, string to, ContinueReason why, CancellationToken ct, bool resumed = false)
    {
        if (!record.WordsWaiting) return;

        var output = _context.Output;
        bool Handed(SaidWordView word) => resumed ? HandedToResume(word) : word.Persons;
        // A choice of a new session is spent with the words it named, which went now (MSG1g).
        _context.NewSessions.Clear(record.Session);
        // 🔴 Only the person's words go on in a new session (XAGENT1e, design §6.7): another agent's findings would reach it as the
        // person's, which DRIFT1 would make requirements. They stay on the record, marked as words that cannot go on there, and
        // go to the person instead. So do Daoris's own words (COWORK1b): a message of Daoris's never starts a new session (the
        // message-urgency design §4.1), bar a resume's, which starts whatever they say (SAYHELD1).
        if (record.Waiting.Any(word => !Handed(word)))
        {
            _context.Marks.Mark(record.Session, record.Waiting.Where(word => !Handed(word)).Select(word => word.Id).OfType<string>(), why, DateTimeOffset.UtcNow);
            _driver.Deliverer.FindingsToPerson(record, why);
        }

        var ids = record.Waiting.Where(Handed).Select(word => word.Id).OfType<string>().ToList();
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
    /// <param name="setupChanged">
    /// Asked before the held words go on, whether the setup the record's start ran under changed since (ACCESSSWAP1b, D183's note): each
    /// run here goes on under the start's spawn, so where it did, the words are handed back to <paramref name="left"/> and the record
    /// concludes, its next look going on under the setup standing then. Null never asks.
    /// </param>
    internal async Task<(int? ExitCode, AcpUsage? Used)> GoOnAsync(
        DrivenInbox held, Func<string, string, System.Diagnostics.ProcessStartInfo?> goOn, ISessionAdapter adapter,
        string sessionId, string transcript, string? refusesInput, bool drivesBrowser, int? exitCode, AcpUsage? used,
        ResumeAsk? resume, Action<System.Text.Json.JsonElement>? said, List<IDisposable> keep, List<ChatMessage> left,
        CancellationToken ct, HarnessWords? own = null, Func<bool>? setupChanged = null)
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

            if (held.TakeAllOrClose() is not { Count: > 0 } taken) break;
            // ACCESSSWAP1b: its setup changed while the run worked, so the words go back unjudged, to go on under the new one at the next
            // look rather than in a run spawned under the start's; the look judges Daoris's words on the record before its go-on.
            if (setupChanged?.Invoke() == true)
            {
                left.AddRange([.. taken, .. held.Close()]);
                return (exitCode, used);
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
            var conversation = _context.Conversations.Read(sessionId)?.Conversation ?? resume?.Conversation;
            var info = conversation is null ? null : goOn(conversation, NativeWords.Prompt(words));
            if (info is null)
            {
                Went(words);
                Untaken(sessionId, held.Close().Count + words.Count, UnreachedWhy.Unkept);
                return (exitCode, used);
            }

            // COWORK1c (the message-urgency design §6): each kept word marked attempted on its record before the run that carries it.
            foreach (var word in words)
            {
                if (word.Attempting is { } attempting) await attempting().ConfigureAwait(false);
            }

            // Through the start gate (PROCSTART2), as the first run's start is, waiting for its place on the loop's token
            // (PARALLEL1f). A driver closing while it waits starts no run, so the words it took are said never to have reached
            // the session, as a stop's are, and the close ends the record as it ends any run it cuts off (D104). Through the launcher
            // (TERMTRUST1a), as the first run's is: its harness born in this process's agents' job.
            System.Diagnostics.Process? started;
            try
            {
                started = await AgentStarts.StartAsync(info, ContainedKind.Agent, StartClass.Person, _context.Home, _driver.Log, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                Went(words);
                Untaken(sessionId, held.Close().Count + words.Count, UnreachedWhy.EndedFirst);
                throw;
            }

            var process = started ?? throw new DriverException($"the {adapter.Name} adapter's process did not start");
            // COWORK1c (the design §7): the resumed run carries them from its start, their receipt `sent`, and they leave the record.
            foreach (var word in words)
            {
                if (word.Id is { } id) _context.Events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Receipt, Id = id, Receipt = Receipts.Sent }, say: null);
            }

            Went(words);
            // As the first run's: the process, tracked so a stop reaches it, and a reaper that ends it while still tracked.
            keep.Add(process);
            keep.Add(processes.Track(sessionId, process, refusesInput, drivesBrowser));
            keep.Add(new Disposer(() => SessionProcesses.EndIfRunning(process)));

            var capture = adapter.StructuredOutput() is { } mapper
                ? _driver.KeepingAsync(mapper, HarnessCapture.CaptureStructuredAsync(
                    _driver.Hearing(process.StandardOutput, sessionId), _driver.Hearing(process.StandardError, sessionId), transcript, sessionId,
                    _context.Output, _context.Events, mapper,
                    prompt: null, ct, opened: NativeWords.Opening(adapter.Name, words), append: true, said: said, own: own,
                    lineageHome: _context.Home),
                    sessionId, adapter.Name, resume: null)
                : null;
            exitCode = await _driver.WaitAsync(process, ct).ConfigureAwait(false);
            // State E (SESSWATCH1a): the capture bounded past the exit, as the first run's is.
            if (capture is not null && await _driver.CaptureWithinAsync(capture, sessionId).ConfigureAwait(false)
                && await capture.ConfigureAwait(false) is { } reported) used = reported;
        }

        // Words held when it could go on no more: a stop, a timeout. Said, never dropped without a trace.
        if (held.Close() is { Count: > 0 } unreached) Untaken(sessionId, unreached.Count, UnreachedWhy.EndedFirst);
        return (exitCode, used);
    }

    /// <summary>
    /// The one retry of a native resume whose harness ended on a failed exit before it named its conversation (SESSCTX1c, D189's
    /// amendment; the session-context design §2): the same start again, its conversation, account, access and the files the first was
    /// handed, which stand until the record concludes. Kept and tracked as the first run was, its output appended to the same record,
    /// its opening not said again. What it came to is read as the first run's would have been: a conversation it named goes on, and a
    /// second such end is <c>exited</c>.
    /// </summary>
    /// <param name="keep">Where its process, its tracking and its reaper are kept until the record has concluded.</param>
    internal async Task<(int? ExitCode, AcpUsage? Used)> AgainAsync(
        System.Diagnostics.ProcessStartInfo info, ISessionAdapter adapter, string sessionId, string transcript, string? refusesInput,
        bool drivesBrowser, ResumeAsk resume, Action<System.Text.Json.JsonElement>? said, List<IDisposable> keep, string? keepAs,
        HarnessWords own, CancellationToken ct)
    {
        _context.Output?.Append(sessionId, "— its harness ended before it reopened its conversation, so the same resume is tried once more.");
        resume.Again();
        // The first run's start again, already the launcher's where that one was (TERMTRUST1a): left as it is, and said again.
        var process = await AgentStarts.StartAsync(info, ContainedKind.Agent, StartClass.Person, _context.Home, _driver.Log, ct).ConfigureAwait(false)
            ?? throw new DriverException($"the {adapter.Name} adapter's process did not start");
        keep.Add(process);
        keep.Add(_context.Processes.Track(sessionId, process, refusesInput, drivesBrowser));
        keep.Add(new Disposer(() => SessionProcesses.EndIfRunning(process)));

        var capture = adapter.StructuredOutput() is { } mapper
            ? _driver.KeepingAsync(mapper, HarnessCapture.CaptureStructuredAsync(
                _driver.Hearing(process.StandardOutput, sessionId), _driver.Hearing(process.StandardError, sessionId), transcript, sessionId,
                _context.Output, _context.Events, mapper,
                prompt: null, ct, append: true, said: said, own: own, lineageHome: _context.Home),
                sessionId, keepAs, resume)
            : null;
        var exitCode = await _driver.WaitAsync(process, ct).ConfigureAwait(false);
        // State E (SESSWATCH1a): the capture bounded past the exit, as the first run's is.
        return (exitCode, capture is null || !await _driver.CaptureWithinAsync(capture, sessionId).ConfigureAwait(false)
            ? null : await capture.ConfigureAwait(false));
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
    /// <remarks>Daoris's own words alone (COWORK1b) are said as Daoris's, never as what the person said.</remarks>
    public static IReadOnlyList<SessionEvent> Opening(string adapter, IReadOnlyList<ChatMessage> words)
    {
        var daoris = words.Count > 0 && words.All(word => word.Daoris is not null);
        var line = daoris
            ? $"— what Daoris told it while it worked is the next turn of its own conversation, resumed on `{adapter}`."
            : $"— what you said while it worked is the next turn of its own conversation, resumed on `{adapter}`.";
        return
        [
            new SessionEvent
            {
                Kind = SessionEventKind.Note,
                Text = line,
                Parts = [(daoris ? NoteCodes.ResumedNativeDaoris : NoteCodes.ResumedNative).Part(line, ("adapter", adapter))],
            },
            .. words.Select(ChatMessage.Words),
        ];
    }
}
