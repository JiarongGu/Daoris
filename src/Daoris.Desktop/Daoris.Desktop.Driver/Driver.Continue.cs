namespace Daoris.Driver;

/// <summary>
/// What a run that resumes a harness conversation carries, and what it learns on the way (ANSWER1a, D131 §1): the
/// conversation, the answer that is its next prompt, and the run's first line; whether the agent would not resume it,
/// and whether the native door's harness named its conversation at all.
/// </summary>
internal sealed class ResumeAsk(string conversation, string answer, string firstLine)
{
    public string Conversation => conversation;

    public string Answer => answer;

    /// <summary>Why the agent would not resume it, said by the door; null while nothing refused.</summary>
    public ContinueReason? Refused { get; set; }

    /// <summary>The native door's harness named its conversation on its <c>init</c> line, so it opened it.</summary>
    public bool Named { get; set; }

    /// <summary>The run's opening was written to the record: the person's answer is there, beneath what it answers.</summary>
    public bool Spoken { get; private set; }

    /// <summary>The record's opening for the resumed run: the driver's first line, then the person's answer as theirs.</summary>
    public IReadOnlyList<SessionEvent> Opening()
    {
        Spoken = true;
        return
        [
            new SessionEvent { Kind = SessionEventKind.Note, Text = firstLine },
            new SessionEvent { Kind = SessionEventKind.User, Origin = "person", Text = answer },
        ];
    }
}

/// <summary>
/// An answer continues its session (ANSWER1a, D131): the park the person answered goes on itself, its record moving
/// <c>awaiting-person</c> → <c>working</c> and its harness's own conversation resumed with the answer as its next prompt,
/// where the account, the adapter and the tree are the same and the door can. Otherwise the park ends, and the answer is
/// carried on in a new session by <see cref="RunAsync"/>, saying why.
/// </summary>
/// <remarks>
/// <para><b>One record for one harness conversation</b> (D131 §3). A resume reopens the record that parked, so the page,
/// the log, the review and a teammate see one session going on; a fallback is a new record because it is a new
/// conversation.</para>
///
/// <para><b>The wire may still refuse</b>: an agent that offers no resume, one that no longer has the conversation, or a
/// native door whose harness ended before naming it. The resumed run then ends the park with the reason, nothing done,
/// and the carry-on starts in the same look.</para>
/// </remarks>
public sealed partial class Driver
{
    // The harness's own conversation per session (ANSWER1a), beside the transcripts under the same home.
    private readonly HarnessConversations _conversations = new(home);

    /// <summary>
    /// Continue an answered park, or end it saying why (ANSWER1a, D131 §1–§2).
    /// </summary>
    /// <returns>
    /// The resumed run, whatever it came to; or null and why not, the park ended where it was still parked, for
    /// <see cref="RunAsync"/> to carry the answer on in a new session; and whether the answer is already in the park's
    /// record, as a resume that was tried puts it.
    /// </returns>
    private async Task<(StartRun? Run, ContinueReason? FellBack, bool AnswerKept)> ContinueAsync(
        Consideration start, PriorSession park, HarnessSelection selection, IReadOnlyList<RepoView> registry,
        TreeLock? starting, Action onOpened, CancellationToken ct)
    {
        var quest = start.Quest;
        ISessionAdapter? adapter = null;
        try { adapter = _adapters.Resolve(config.Adapter); } catch (DriverException) { }

        var kept = _conversations.Read(park.Session);
        var why = adapter is null
            ? ContinueWhy.Of(ContinueWhy.Refused)
            : Continuations.Judge(park, adapter.Name, adapter.Resumes, selection.Profile, kept);
        if (why is not null || adapter is null || kept is null)
        {
            var reason = why ?? ContinueWhy.Of(ContinueWhy.Refused);
            if (await EndParkAsync(park, reason, ct).ConfigureAwait(false) is { } refused)
            {
                return (new StartRun($"held  #{quest.Id} → {quest.To}: {refused}", false, null, refused), null, false);
            }

            return (null, reason, false);
        }

        // The park holds its tree in the ledger the whole time (D51), so a replay already sees it in use (LEFT2).
        starting?.Dispose();

        var sessionId = park.Session;
        var workTree = park.Tree!;
        var answer = park.Answer!;
        var before = await WorkingTree.HeadAsync(workTree, ct).ConfigureAwait(false);
        var across = AcrossRules.Reach(config, registry, quest.To, start.Workspace);
        var target = SessionTarget.ForQuest(quest, workTree, service.BaseUrl) with
        {
            ReadsAcross = across.Reads,
            WritesAcross = across.Writes,
            Session = sessionId,
        };
        var resume = new ResumeAsk(kept.Conversation, answer, Continuations.Opening(adapter.Name, selection.Version, park.HarnessVersion));
        var transcript = Path.Combine(home, "sessions", $"{sessionId}.log");

        try
        {
            // The protocol door resumes on its wire, from the same spawn a start has; the native door is run on the
            // conversation it kept.
            var prepared = adapter.Wire == SessionWire.Acp
                ? null
                : adapter.PrepareResume(target, config.Commands.GetValueOrDefault(adapter.Name), kept.Conversation, answer);
            var (info, harnessNotice) = Prepare(adapter, target, selection, prepared);
            var (servers, browserNotice, drivesBrowser) = await InAppBrowserServers.HandAsync(_servers, browser, ct).ConfigureAwait(false);
            hooks?.Log.Served(_catalog, sessionId, servers);
            var handed = SpawnServers.Hand(adapter, info, home, sessionId, servers);
            var rules = HandRules(adapter, info, sessionId, start.Workspace, quest.To, workTree, target.AttachmentsDirectory, across: across);

            onOpened();
            _runs.Live[quest.Id] = sessionId;
            using var live = new Disposer(() => _runs.Live.TryRemove(quest.Id, out var _));

            var (run, refusedWhy) = await HoldAsync(
                adapter, info, target, sessionId, transcript, workTree, JoinNotices(harnessNotice, browserNotice), rules, handed,
                refusesInput: TakesNoMessages(quest),
                ct: ct,
                preamble: browserNotice,
                handedServers: servers,
                drivesBrowser: drivesBrowser,
                resume: resume,
                workingNote: Continuations.Working,
                conclude: (exitCode, used, turnFailed) =>
                    ConcludeResumedAsync(quest, park, adapter, selection, resume, workTree, transcript, before, exitCode, used, turnFailed, ct))
                .ConfigureAwait(false);
            return (run, refusedWhy, resume.Spoken);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The driver is closing under it (D104): an interrupted take, carried on at the next start.
            try
            {
                await service.AdvanceAsync(
                    sessionId, "stopped",
                    note: "the driver was stopped while this ran; the session's process was ended with it.",
                    ct: CancellationToken.None, interrupted: true).ConfigureAwait(false);
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
            // Failed after it was working: a failure, which the strikes bound. Failed before, the record is still parked
            // and refuses that move, so the park ends as any fallback does and the answer is carried on in a new session.
            try
            {
                await service.AdvanceAsync(sessionId, "failed", note: error.Message, ct: CancellationToken.None).ConfigureAwait(false);
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
        }
    }

    /// <summary>
    /// The resumed run's conclusion: from the exit code and the quest's own state (D46 §4) as any start's, its evidence
    /// counted from the record's own base so the review's range is the whole session's — or, where the door said the
    /// agent would not resume, the park ended with the reason and the answer left to a new session.
    /// </summary>
    private async Task<(StartRun? Run, ContinueReason? FellBack)> ConcludeResumedAsync(
        QuestView quest, PriorSession park, ISessionAdapter adapter, HarnessSelection selection, ResumeAsk resume,
        string workTree, string transcript, string? before, int? exitCode, AcpUsage? used, string? turnFailed, CancellationToken ct)
    {
        var sessionId = park.Session;

        // 🔴 The native door's harness ended on a failed exit before naming its conversation: it never opened it, which is
        // how a conversation it no longer has ends there. Read from the structure, never from its words.
        if (resume.Refused is null && adapter.Wire == SessionWire.Pipe && !resume.Named && exitCode is int failed && failed != 0)
        {
            resume.Refused = ContinueWhy.Of(ContinueWhy.Gone);
        }

        if (resume.Refused is { } refused)
        {
            await service.AdvanceAsync(sessionId, "completed", note: Continuations.EndedNote(park, refused), ct: ct).ConfigureAwait(false);
            service.AccountSaid(Continuations.Answered(sessionId, adapter.Name, refused));
            return (null, refused);
        }

        if (used is not null)
        {
            usage?.Record(new UsageEntry(sessionId, quest.To, adapter.Name, selection.Profile, used.Used, used.Size, DateTimeOffset.UtcNow));
        }

        var after = await service.FindQuestAsync(quest.Id, ct).ConfigureAwait(false);
        var status = after?.Status ?? "Open";
        var stoppedFor = _processes.StopReason(sessionId);
        var conclusion = stoppedFor is not null
            ? new SessionConclusion("stood-down", stoppedFor)
            : _processes.WasStopRequested(sessionId)
            ? new SessionConclusion("stopped", "the person stopped it.")
            : exitCode is int code
                // It carried on a take this machine already held, so ending with it still taken is a park, not a stand-down.
                ? Observation.Conclude(
                    code, status, quest.Awaits, after?.Awaits, turnFailed, resumed: true,
                    took: status == "Taken" && await service.TookAsync(sessionId, ct).ConfigureAwait(false),
                    lastWords: ParkedWords(_events, sessionId, transcript))
                : new SessionConclusion("failed", $"timed out after {config.TimeoutMinutes} minutes and was killed.");

        conclusion = AccountRefused(conclusion, adapter, selection, transcript);
        (conclusion, var limited) = AccountLimited(conclusion, adapter, selection, turnFailed, sessionId, used);

        var evidence = await WorkingTree.CommitsSinceAsync(workTree, park.BaseCommit ?? before, ct).ConfigureAwait(false);
        await service.AdvanceAsync(sessionId, conclusion.State, note: conclusion.Note, evidence: evidence, ct: ct, limit: limited)
            .ConfigureAwait(false);
        service.AccountSaid(Continuations.Answered(sessionId, adapter.Name, why: null));

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
    /// a service from before ANSWER1b already ended moves no further. Null when done; else the service's refusal.
    /// </summary>
    private async Task<string?> EndParkAsync(PriorSession park, ContinueReason why, CancellationToken ct)
    {
        if (park.AnsweredPark)
        {
            try
            {
                await service.AdvanceAsync(park.Session, "completed", note: Continuations.EndedNote(park, why), ct: ct).ConfigureAwait(false);
            }
            catch (DriverException refused)
            {
                // The person stopped or answered it again between the look and here: the next look reads it as it now is.
                return refused.Message;
            }
        }

        service.AccountSaid(Continuations.Answered(park.Session, config.Adapter, why));
        return null;
    }
}
