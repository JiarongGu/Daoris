namespace Daoris.Driver;

/// <summary>
/// What a run that resumes a harness conversation carries, and what it learns on the way (ANSWER1a, D131 §1; MSG1b, D137
/// §2.2): the conversation, the person's words that are its next prompt, and the run's first line; whether the agent would
/// not resume it, whether the native door's harness named its conversation at all, and whether the words went.
/// </summary>
/// <param name="words">The person's words waiting on the record, in the order said: a park's answer, or words to an ended record.</param>
/// <param name="appendix">
/// What the prompt carries after the person's words: the answers to the go-aheads it asked, which it was not handed at its
/// start (KNOWUSE1a). The record keeps only the person's words as theirs. Null or empty for none.
/// </param>
/// <param name="files">
/// Where each word's files are kept (MSG1d3, D137 §2.4): the record names them, and the run is handed where they lie, as a
/// conversation's message is (CONV4c). Read once, as the ask is made. Null hands the words alone.
/// </param>
internal sealed class ResumeAsk(
    string conversation, IReadOnlyList<SaidWordView> words, string firstLine, string? appendix = null,
    Func<SaidWordView, IReadOnlyList<KeptFile>>? files = null)
{
    // Each word's files, by its place in the words; read once, so both doors are handed the same.
    private readonly IReadOnlyList<IReadOnlyList<KeptFile>> _files =
        [.. words.Select(word => files is null || word.Files.Count == 0 ? Array.Empty<KeptFile>() : files(word))];

    public string Conversation => conversation;

    /// <summary>The person's words, in the order said, each with the id its record's <c>said</c> gave it.</summary>
    public IReadOnlyList<SaidWordView> Words => words;

    /// <summary>
    /// Every word's files in the order said (MSG1d3): the protocol door's links after the words' blocks, and what a run is
    /// handed a read of. Empty where none was said, or none is kept any more.
    /// </summary>
    public IReadOnlyList<KeptFile> Files => [.. _files.SelectMany(each => each)];

    /// <summary>
    /// The person's words joined by a blank line, as the native door's one argument carries them: each with where its files
    /// are kept beneath it, as a conversation's message names them on that door (MSG1d3, <see cref="ChatFiles.PathLines"/>).
    /// </summary>
    public string Answer => string.Join("\n\n", words.Select((word, at) => word.Text + ChatFiles.PathLines(_files[at])));

    /// <summary>What the conversation is resumed with on the native door: the words, then the appendix after a blank line.</summary>
    public string Prompt => appendix is { Length: > 0 } more ? $"{Answer}\n\n{more}" : Answer;

    /// <summary>The same on the protocol door (D137 §2.2): each word its own text block in order, then the appendix as one more.</summary>
    public IReadOnlyList<string> Blocks =>
        [.. words.Select(word => word.Text), .. appendix is { Length: > 0 } more ? new[] { more } : []];

    /// <summary>The ids of the words its record keeps, for the service to take off once the session took them; none from before <c>said</c>.</summary>
    public IReadOnlyList<string> Ids => [.. words.Select(word => word.Id).OfType<string>()];

    /// <summary>Why the agent would not resume it, said by the door; null while nothing refused.</summary>
    public ContinueReason? Refused { get; set; }

    /// <summary>The native door's harness named its conversation on its <c>init</c> line, so it opened it.</summary>
    public bool Named { get; set; }

    /// <summary>
    /// The words went to the session (MSG1b): the protocol door's first prompt is on the wire, or the native door's harness
    /// opened the conversation its argument carried them to. Only then are they taken off the record.
    /// </summary>
    public bool Prompted { get; set; }

    /// <summary>The run's opening was written to the record: the person's words are there, beneath what they answer.</summary>
    public bool Spoken { get; private set; }

    /// <summary>
    /// The second opinions whose findings are among the words (XAGENT1e, D155 point 7), each once, in the order handed: what
    /// the run's end reads the answers of.
    /// </summary>
    public IReadOnlyList<string> Opinions => [.. words.Select(word => word.By).OfType<string>().Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// The record's opening for the resumed run: the driver's first line, then each of the person's words as theirs, under
    /// the id their record gave it, so the words shown while they waited pair with where the session took them (D137 §3.1).
    /// Another agent's findings (XAGENT1e, D155 point 7) are never the person's: they are the turn Daoris composed around that
    /// agent's claims, naming the opinion they are from.
    /// </summary>
    public IReadOnlyList<SessionEvent> Opening()
    {
        Spoken = true;
        return
        [
            new SessionEvent { Kind = SessionEventKind.Note, Text = firstLine },
            .. words.Select(word => new SessionEvent
            {
                Kind = SessionEventKind.User,
                Origin = word.Persons ? "person" : "target",
                Opinion = word.By,
                Id = word.Id,
                Text = word.Text,
                Files = word.Files.Count > 0 ? word.Files : null,
            }),
        ];
    }
}

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
public sealed partial class Driver
{
    // The harness's own conversation per session (ANSWER1a), beside the transcripts under the same home.
    private readonly HarnessConversations _conversations = new(home);

    // The words a record could not go on with (MSG1b), beside the conversations, so a later look leaves them waiting.
    private readonly GoOnMarks _marks = new(home);

    // The person's choices to go on in a new session rather than wait for a cooling account (MSG1g), beside the marks.
    private readonly NewSessionChoices _newSessions = new(home);

    /// <summary>
    /// The record a start goes on in with the person's words, where its resume asks for that record's own account (MSG1g, D137
    /// §2.2): words the planner took up, on the adapter the record ran on. Null for every other start, and for a record that ran
    /// on another adapter, whose conversation cannot go on here whichever account runs (D131 §2's <c>adapter</c>), so its words
    /// are carried on by the walk's pick as before.
    /// </summary>
    private PriorSession? ResumesOwn(Consideration start)
    {
        if (!start.GoesOn || start.Resumes is not { WordsWaiting: true } words || start.Quest.Awaits is { Length: > 0 }) return null;
        var ranOn = words.Adapter ?? _conversations.Read(words.Session)?.Adapter;
        return ranOn is not { Length: > 0 } || string.Equals(ranOn, config.Adapter, StringComparison.OrdinalIgnoreCase) ? words : null;
    }

    /// <summary>
    /// Why the words wait (MSG1g): the account, its reset and that its conversation is there, then the door out of the wait. A
    /// taken or open quest's session goes on in a new session at the person's choice; a closed quest's has nothing to carry it
    /// on by itself, so its door is a conversation with the words (D137 §2.2). Machine-local: it names the account.
    /// </summary>
    private static string WaitsFor(QuestView quest, PriorSession record, HarnessSelection held) =>
        $"{held.Refusal} "
        + (quest.Status is "Open" or "Taken" ? ResumeWords.NewSessionDoor(record.Session) : ResumeWords.ChatDoor(record.Session));

    /// <summary>
    /// The record's conversation says why its words wait (MSG1g, D143 point 1), once for each wait: a look that finds it saying
    /// the same as its last event writes nothing more, and a word said since is answered again. The person wrote to it, so it
    /// stays in view while it waits (D137 §2.3). The words stay waiting and unmarked: the reset lets them go on.
    /// </summary>
    private void HeldForAccount(PriorSession record, string why)
    {
        var line = $"{ResumeWords.NotYet}{why}";
        try
        {
            if (_events.Page(record.Session, limit: 1).Events is [{ Kind: SessionEventKind.Note, Text: var last }] && last == line) return;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or DriverException)
        {
            // A record that does not read is said again: one line more, never none.
        }

        if (!record.Parked) new SessionArchive(home).Unarchive([record.Session]);
        output?.Append(record.Session, line);
        _events.Keep(record.Session, new SessionEvent { Kind = SessionEventKind.Note, Text = line }, say: null);
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
    private async Task<(StartRun? Run, ContinueReason? FellBack, bool AnswerKept)> ContinueAsync(
        Consideration start, PriorSession park, HarnessSelection selection, IReadOnlyList<RepoView> registry,
        TreeLock? starting, Action onOpened, CancellationToken ct, ContinueReason? account = null)
    {
        var quest = start.Quest;
        ISessionAdapter? adapter = null;
        try { adapter = _adapters.Resolve(config.Adapter); } catch (DriverException) { }

        // The person wrote to a record that had ended, so it stays in view whatever comes of it (D137 §2.3): its archive mark
        // goes, and a mark of words it could not take before is past, since these are taken up now.
        if (!park.Parked && park.WordsWaiting)
        {
            new SessionArchive(home).Unarchive([park.Session]);
            _marks.Clear(park.Session);
        }

        var kept = _conversations.Read(park.Session);
        var why = adapter is null
            ? ContinueWhy.Of(ContinueWhy.Refused)
            : Continuations.Judge(park, adapter.Name, adapter.Resumes, selection.Profile, kept, account);
        if (why is not null || adapter is null || kept is null)
        {
            var reason = why ?? ContinueWhy.Of(ContinueWhy.Refused);
            if (await EndParkAsync(park, reason, ct).ConfigureAwait(false) is { } refused)
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
        var target = WithLanguage(SessionTarget.ForQuest(quest, workTree, service.BaseUrl), config, start.Workspace) with
        {
            ReadsAcross = across.Reads,
            WritesAcross = across.Writes,
            Session = sessionId,
        };
        // Another agent's findings among the words (XAGENT1e, D155 point 7): said as that agent's, and answered with the one
        // tool they need, which the person's rules may not name.
        var findings = park.Findings.Count > 0;
        var persons = park.Waiting.Any(word => word.Persons);

        // Each word's files where they were kept as it was said (MSG1d3, D137 §2.4), handed with the words.
        var resume = new ResumeAsk(
            kept.Conversation, park.Waiting,
            Continuations.Opening(adapter.Name, selection.Version, park.HarnessVersion, answer: park.Parked, findings, persons),
            await ResumedAfterAsync(service, quest, sessionId, target.Language, ct).ConfigureAwait(false),
            files: word => ChatFiles.Kept(home, sessionId, word.Files));
        var transcript = Path.Combine(home, "sessions", $"{sessionId}.log");

        try
        {
            // The protocol door resumes on its wire, from the same spawn a start has; the native door is run on the
            // conversation it kept.
            var prepared = adapter.Wire == SessionWire.Acp
                ? null
                : adapter.PrepareResume(target, config.Commands.GetValueOrDefault(adapter.Name), kept.Conversation, resume.Prompt);
            var (info, harnessNotice) = Prepare(adapter, target, selection, prepared);
            // A set-up step going on with the person's words shows it again in its own tab (REVIEWENV1d, design §3.4): opened
            // again where the person closed it, and brought forward. Said, never held, where it will not open: the words go on.
            var tabNotice = quest.SetUpIn is not null && Reviews is { } desk
                            && ReviewSetUps.Environment(config, quest, start.Workspace) is { } shownIn
                            && await desk.OpenForStepAsync(quest, shownIn, ct).ConfigureAwait(false) is { } unopened
                ? $"— {unopened}"
                : null;
            var (servers, browserNotice, drivesBrowser) = await InAppBrowserServers.HandAsync(_servers, browser, ct).ConfigureAwait(false);
            browserNotice = JoinNotices(browserNotice, tabNotice);
            hooks?.Log.Served(_catalog, sessionId, servers);
            var handed = SpawnServers.Hand(adapter, info, home, sessionId, servers);
            var rules = HandRules(
                adapter, info, sessionId, start.Workspace, quest.To, workTree, target.AttachmentsDirectory, across: across,
                said: SaidFilesFolder(home, sessionId, resume), job: findings ? AnswersFindings : null);

            onOpened();
            var openedAt = DateTimeOffset.UtcNow;
            _runs.Live[quest.Id] = sessionId;
            using var live = new Disposer(() => _runs.Live.TryRemove(quest.Id, out var _));

            var (run, refusedWhy) = await HoldAsync(
                adapter, info, target, sessionId, transcript, workTree, JoinNotices(harnessNotice, browserNotice), rules, handed,
                refusesInput: TakesNoMessages(quest),
                ct: ct,
                preamble: browserNotice,
                handedServers: servers,
                drivesBrowser: drivesBrowser,
                // A resumed run is on the account it parked on, so what it says about that account's windows is kept
                // as any run's is (TOOL6c with ANSWER1a).
                said: Said(adapter, selection, sessionId),
                resume: resume,
                // A park's note is replaced while it works; an ended record's keeps what ended it and says it goes on (MSG1b),
                // with another agent's findings where those are all it goes on with (XAGENT1e), never "your words".
                workingNote: park.Parked ? Continuations.WorkingNoted
                    : findings && !persons ? Continuations.GoingOnWithFindingsNoted
                    : Continuations.GoingOnNoted,
                goOn: GoOnWith(adapter, target, selection, rules.File, handed),
                working: () => starting?.Dispose(),
                conclude: (exitCode, used, turnFailed, ended) =>
                    ConcludeResumedAsync(quest, park, adapter, selection, resume, workTree, transcript, before, exitCode, used, turnFailed, ended,
                        openedAt, ct))
                .ConfigureAwait(false);
            return (run, refusedWhy, resume.Spoken);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The driver is closing under it (D104): an interrupted take, carried on at the next start.
            try
            {
                await service.AdvanceAsync(
                    sessionId, "stopped", Observation.DriverClosed, ct: CancellationToken.None, interrupted: true).ConfigureAwait(false);
            }
            catch
            {
                // Best-effort by construction: the host may already be gone on the same shutdown.
            }

            return (new StartRun(
                $"stopped  session {sessionId} (#{quest.Id} → {quest.To}): the driver was stopped.",
                true,
                new SessionEnded(sessionId, quest.To, "stopped", ByPerson: true, Quest: quest.Id, Adapter: config.Adapter)), null, resume.Spoken);
        }
        catch (Exception error)
        {
            // Failed after it was working: a failure, which the strikes bound. Failed before, the record is still parked or
            // ended, and refuses that move, so the words are handed on as any fallback hands them.
            try
            {
                await service.AdvanceAsync(sessionId, "failed", Observation.Failure(error.Message), ct: CancellationToken.None).ConfigureAwait(false);
            }
            catch (DriverException)
            {
                var reason = ContinueWhy.Of(ContinueWhy.Refused);
                if (await EndParkAsync(park, reason, CancellationToken.None).ConfigureAwait(false) is null)
                {
                    return (null, reason, resume.Spoken);
                }
            }
            catch (Exception)
            {
                // The first failure is the report; a host that is gone here must not turn it into a second throw.
            }

            return (new StartRun(
                $"failed  session {sessionId} (#{quest.Id} → {quest.To}): {error.Message}",
                true,
                new SessionEnded(sessionId, quest.To, "failed", ByPerson: false, error.Message, Quest: quest.Id, Adapter: config.Adapter)), null, resume.Spoken);
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
            usage?.Record(new UsageEntry(sessionId, quest.To, adapter.Name, selection.Profile, used.Used, used.Size, DateTimeOffset.UtcNow));
        }

        // The words went: off the record by their ids, before the conclusion moves it (MSG1a's taken door).
        if (resume.Prompted && resume.Ids.Count > 0)
        {
            var (taken, message) = await service.TakenAsync(sessionId, resume.Ids, by: null, ct).ConfigureAwait(false);
            if (!taken) _events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = $"— your words could not be taken off its record: {message}" }, say: null);
            // Its own account carried them, so a choice of a new session made while it cooled is spent with them (MSG1g).
            _newSessions.Clear(sessionId);
        }

        // XAGENT1e (design §6.4): another agent's findings it took are answered as its turn ends, read before the record moves,
        // with each fix's commit read from its tree as it stands now; one it left unanswered is unresolved.
        await OpinionsAnsweredAsync(resume, sessionId, workTree, ct).ConfigureAwait(false);

        var stoppedFor = _processes.StopReason(sessionId);
        var conclusion = stoppedFor is not null
            ? SessionConclusion.Of("stood-down", stoppedFor)
            : _processes.WasStopRequested(sessionId)
            // A pause's stop names the pause (PAUSE1b, design §4.1), as a first run's does.
            ? SessionConclusion.Of("stopped", _processes.StopNote(sessionId) ?? Observation.Stopped)
            : exitCode is int code
                // As any start's where its quest was open or taken as the look planned it, so a park whose run closes its
                // quest ends completed; a closed quest's session ends as its process does (MSG1b). It carried on a take this
                // machine already held, so ending with it still taken is a park, not a stand-down.
                ? Observation.Resumed(
                    code, park.State, quest.Status, status, quest.Awaits, after?.Awaits, turnFailed,
                    took: !closed && status == "Taken" && await service.TookAsync(sessionId, ct).ConfigureAwait(false),
                    lastWords: closed ? null : ParkedWords(_events, sessionId, transcript))
                : SessionConclusion.Of("failed", Observation.TimedOut(config.TimeoutMinutes));

        conclusion = AccountRefused(conclusion, adapter, selection, ended);
        conclusion = AccountSignedOut(conclusion, adapter, selection, turnFailed);
        (conclusion, var limited) = AccountLimited(conclusion, adapter, selection, turnFailed, sessionId, used);

        var evidence = await WorkingTree.CommitsSinceAsync(workTree, park.BaseCommit ?? before, ct).ConfigureAwait(false);
        await service.AdvanceAsync(
                sessionId, conclusion.State, note: conclusion.Note, evidence: evidence, ct: ct, limit: limited, parts: conclusion.Parts)
            .ConfigureAwait(false);
        service.AccountSaid(Took(park, adapter.Name, why: null));

        // REVIEWENV1c (design §2.6, §3.4): a set-up step that went on with the person's not yet says a new set-up, posted with
        // the commit its tree holds now, its correction among it.
        await ReviewSetUps.PostAsync(service, config, _events, after, sessionId, workTree, quest.Workspace, openedAt, ct, Reviews)
            .ConfigureAwait(false);

        // LAND2b: as a first run's ending, so a resumed session that closes its quest done is due too. One that went on after
        // its landing moves its own branch on at the next look (LAND2c).
        ConcludedForLanding(sessionId, quest, status, conclusion.State, workTree, quest.Workspace);

        return (new StartRun(
            $"{conclusion.State}  session {sessionId} (#{quest.Id} → {quest.To}) [resumed its conversation]: {conclusion.Note}",
            true,
            SessionStates.IsParked(conclusion.State)
                ? null
                : new SessionEnded(
                    sessionId, quest.To, conclusion.State,
                    ByPerson: stoppedFor is null && _processes.WasStopRequested(sessionId), conclusion.Note,
                    Quest: quest.Id, Adapter: adapter.Name, Account: selection.Profile)), null);
    }

    /// <summary>
    /// End the park a fallback leaves behind, keeping what it asked and saying why (D131 §2), and write the line. A record
    /// that had already ended moves no further: its words wait on it until they are handed on (MSG1b). Null when done; else
    /// the service's refusal.
    /// </summary>
    private async Task<string?> EndParkAsync(PriorSession park, ContinueReason why, CancellationToken ct)
    {
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

        service.AccountSaid(Took(park, config.Adapter, why));
        return null;
    }

    /// <summary>
    /// Where the words cannot go on in their session and nothing carries them on by itself (MSG1b, D137 §2.2): a closed
    /// quest's session, or a never. The record stays as it ended, its words waiting as said; they are marked by their ids so
    /// a later look leaves them there (<see cref="GoOnMarks"/>), and the session's conversation says why, where the person
    /// wrote them. A new conversation with them is the person's press, never the driver's (MSG1f).
    /// </summary>
    private StartRun CannotGoOn(QuestView quest, PriorSession record, ContinueReason why)
    {
        var words = record.Waiting.Select(word => word.Id).OfType<string>().ToList();
        _marks.Mark(record.Session, words, why, DateTimeOffset.UtcNow);
        // Another agent's findings among them go to the person instead, unanswered (XAGENT1e, design §6.7).
        FindingsToPerson(record, why);
        // A choice of a new session is spent with the words it named (MSG1g): words said later wait for the account again.
        _newSessions.Clear(record.Session);
        // The words' ids and the code beside the line (MSG1d), so the page says why in its own words.
        var note = Continuations.Cannot(words, why);
        output?.Append(record.Session, note.Text!);
        _events.Keep(record.Session, note, say: null);
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
    private async Task HandedOnAsync(PriorSession record, string to, ContinueReason why, CancellationToken ct)
    {
        if (!record.WordsWaiting) return;

        // A choice of a new session is spent with the words it named, which went now (MSG1g).
        _newSessions.Clear(record.Session);
        // 🔴 Only the person's words go on in a new session (XAGENT1e, design §6.7): another agent's findings would reach it as the
        // person's, which DRIFT1 would make requirements. They stay on the record, marked as words that cannot go on there, and
        // go to the person instead.
        if (record.Findings.Count > 0)
        {
            _marks.Mark(record.Session, record.Waiting.Where(word => !word.Persons).Select(word => word.Id).OfType<string>(), why, DateTimeOffset.UtcNow);
            FindingsToPerson(record, why);
        }

        var ids = record.Waiting.Where(word => word.Persons).Select(word => word.Id).OfType<string>().ToList();
        if (!record.Parked)
        {
            // The words' ids, the session and the code beside the line (MSG1d, D137 §3.1): the page links where they went.
            var note = Continuations.Went(ids, to, why);
            output?.Append(record.Session, note.Text!);
            _events.Keep(record.Session, note, say: null);
        }

        if (ids.Count == 0) return;
        try
        {
            var (taken, message) = await service.TakenAsync(record.Session, ids, by: to, ct).ConfigureAwait(false);
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
                _events.DoorOf(record.Session, [.. record.Waiting.Select(word => word.Id).OfType<string>()]));

    /// <summary>
    /// How a native run goes on with the words the person said while it ran (MSG1b, D137 §2.1): its own conversation
    /// resumed with them, with the environment, the rules and the servers a start is handed. Null on a door that cannot
    /// resume, whose words wait for nothing: it opens no inbox.
    /// </summary>
    private Func<string, string, System.Diagnostics.ProcessStartInfo?>? GoOnWith(
        ISessionAdapter adapter, SessionTarget target, HarnessSelection selection, string? settings, string? servers) =>
        adapter.Wire == SessionWire.Pipe && adapter.Resumes
            ? (conversation, words) =>
                GoOnStart(adapter, target, config.Commands.GetValueOrDefault(adapter.Name), conversation, words, settings, servers) is { } next
                    ? Prepare(adapter, target, selection, next).Info
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
    /// <param name="own">Where the harness's own words are kept (AGT3c): each run begins it again, so it says the last run's ending.</param>
    private async Task<(int? ExitCode, AcpUsage? Used)> GoOnAsync(
        DrivenInbox held, Func<string, string, System.Diagnostics.ProcessStartInfo?> goOn, ISessionAdapter adapter,
        string sessionId, string transcript, string? refusesInput, bool drivesBrowser, int? exitCode, AcpUsage? used,
        ResumeAsk? resume, Action<System.Text.Json.JsonElement>? said, List<IDisposable> keep, CancellationToken ct,
        HarnessWords? own = null)
    {
        while (exitCode is not null && !_processes.WasStopRequested(sessionId) && held.TakeAllOrClose() is { Count: > 0 } words)
        {
            // The id its harness named, kept as each run's output ended (ANSWER1a); never read from the account's home.
            var conversation = _conversations.Read(sessionId)?.Conversation ?? resume?.Conversation;
            var info = conversation is null ? null : goOn(conversation, NativeWords.Prompt(words));
            if (info is null)
            {
                Untaken(sessionId, held.Close().Count + words.Count, "Daoris kept no id for its conversation");
                return (exitCode, used);
            }

            var process = System.Diagnostics.Process.Start(info)
                ?? throw new DriverException($"the {adapter.Name} adapter's process did not start");
            // As the first run's: the process, tracked so a stop reaches it, and a reaper that ends it while still tracked.
            keep.Add(process);
            keep.Add(_processes.Track(sessionId, process, refusesInput, drivesBrowser));
            keep.Add(new Disposer(() => SessionProcesses.EndIfRunning(process)));

            var capture = adapter.StructuredOutput() is { } mapper
                ? KeepingAsync(mapper, CaptureStructuredAsync(
                    process.StandardOutput, process.StandardError, transcript, sessionId, output, _events, mapper,
                    prompt: null, ct, opened: NativeWords.Opening(adapter.Name, words), append: true, said: said, own: own),
                    sessionId, adapter.Name, resume: null)
                : null;
            exitCode = await WaitAsync(process, ct).ConfigureAwait(false);
            if (capture is not null && await capture.ConfigureAwait(false) is { } reported) used = reported;
        }

        // Words held when it could go on no more: a stop, a timeout. Said, never dropped without a trace.
        if (held.Close() is { Count: > 0 } left) Untaken(sessionId, left.Count, "it ended first");
        return (exitCode, used);
    }

    /// <summary>The driver's line in the record for words the person said that never reached the session, and why.</summary>
    private void Untaken(string sessionId, int count, string why)
    {
        var line = $"— {count} message(s) the person sent while it worked never reached it: {why}.";
        output?.Append(sessionId, line);
        _events.Keep(sessionId, new SessionEvent { Kind = SessionEventKind.Note, Text = line }, say: null);
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
    /// The run's opening in the record: the driver's line, then the same words again under the ids they were shown with
    /// while they waited, now that the session took them (D137 §3.1).
    /// </summary>
    public static IReadOnlyList<SessionEvent> Opening(string adapter, IReadOnlyList<ChatMessage> words) =>
    [
        new SessionEvent
        {
            Kind = SessionEventKind.Note,
            Text = $"— what you said while it worked is the next turn of its own conversation, resumed on `{adapter}`.",
        },
        .. words.Select(Driver.Words),
    ];
}
