using static Daoris.Driver.WorkflowChatSteps;
using static Daoris.Driver.WorkflowLandingSteps;
using static Daoris.Driver.WorkflowReviewSteps;

namespace Daoris.Driver;

/// <summary>
/// A run, derived from its records (WORKFLOW1c; D157 point 11, the workflow design §5.1–§5.2, §7). Pure: its reader gathers the
/// records, and this decides from them alone, so the table of states (<c>WorkflowRunTests</c>) holds every row without a process.
/// </summary>
/// <remarks>
/// <para><b>Derived, never stored, never inferred from the conversation</b> (design §5.1, §12.1; D55's <i>a timeline is
/// derived</i>): each step reads the store that keeps it. The work its quests and their sessions, and the go-aheads on its ask;
/// the second opinion its gate (<see cref="OpinionGate"/>); the look the review's gate (<see cref="ReviewGate"/>); the landing
/// this machine's landing record, the acceptance its session's conversation keeps, and the due list; the pull request the
/// landing's kept answer (D148). The reader resolves the run's stored binding once; checks remain outside this runtime.</para>
///
/// <para><b>A quest done is not a run done</b> (design §5.2): the work finished, landed, the pull request open and merged are
/// four steps apart, so a closed quest whose work waits on a merge reads as waiting.</para>
///
/// <para><b>One gate, in its order</b> (D155 §7, the second-agent design §7): the second opinion, then the person's look, then the
/// landing. The run stands at the opinion while its gate holds; the look waits while an agent is still at work on the opinion,
/// as its set-up step sits then (XAGENT1f); and nothing lands past either.</para>
/// </remarks>
public static class WorkflowRuns
{
    /// <summary>The states a session's record ends in: anything else still runs or waits.</summary>
    private static readonly HashSet<string> Endings = new(StringComparer.Ordinal) { "completed", "declined", "failed", "stopped", "stood-down" };

    /// <summary>The run of <paramref name="facts"/>: its chosen graph's steps, each where it stands, and a look the work's own choice added.</summary>
    public static WorkflowRun Derive(WorkflowRunFacts facts, string workspace, string? ask = null)
    {
        var work = facts.Quests.Where(quest => quest.SetUpIn is null).ToList();
        var workIds = new HashSet<string>(work.Select(quest => quest.Id), StringComparer.OrdinalIgnoreCase);
        var workSessions = facts.Sessions
            .Where(session => session.Quest is { } quest && workIds.Contains(quest))
            .OrderBy(session => session.Created ?? DateTimeOffset.MinValue)
            .ToList();

        var steps = new List<WorkflowRunStep>();
        // CHATFLOW1c (the chat-flow design §4.2, Codex's finding 7): a chat run's later steps are read by its candidate's readiness, never
        // by its displayed work step, and its landing by the tip its tree stands at.
        var chat = facts.Chat;
        var landed = chat is null ? null : ChatLandedAt(facts, chat);
        var workStep = chat is null
            ? Work(Cell(facts, WorkflowKinds.Work), work, workSessions, facts)
            : ChatWork(Cell(facts, WorkflowKinds.Work), facts, chat, landed);
        // INTEGRATE1o part o2 (§17.3): the requests the work's sessions made are drawn inside it, whatever state it stands at.
        if (facts.Requests.Count > 0) workStep = workStep with { Requests = facts.Requests };
        steps.Add(workStep);
        var workDone = chat is null ? workStep.State == WorkflowRunStates.Done : Candidate(facts, chat, landed);

        // The landing is read before the opinion and the look, since a landing done is what both let go.
        var landing = chat is null
            ? Landing(Cell(facts, WorkflowKinds.Landing), workDone, workSessions, facts)
            : ChatLanding(Cell(facts, WorkflowKinds.Landing), workDone, facts, landed);
        var opinion = chat is null ? Opinion(facts, workDone, landing) : ChatOpinion(facts, workDone, landing, landed);
        if (opinion is not null) steps.Add(opinion);

        var look = Look(facts, workDone, landing);
        // The look's set-up step sits while an agent is still at work on the opinion (XAGENT1f, the second-agent design §7), so
        // the person looks once, at work that already answered it: the look is not reached until then.
        if (look is not null && opinion is { State: WorkflowRunStates.Working or WorkflowRunStates.WaitingOnAgent }
            && !WorkflowRunStates.Settled(look.State))
        {
            look = look with { State = WorkflowRunStates.NotReached, Detail = "" };
        }

        if (look is not null) steps.Add(look);

        // Nothing lands past a gate that holds it (D155 §7, D154 point 7): the landing is not reached until the opinion and the
        // look are settled. A chat run's opinion that its press asks (CHATFLOW1d) is the landing's own press, which stays offered.
        if (landing.State != WorkflowRunStates.Done
            && ((opinion is not null && !WorkflowRunStates.Settled(opinion.State) && opinion.Detail != WorkflowRunDetails.ChatAtAccept)
                || (look is not null && !WorkflowRunStates.Settled(look.State))))
        {
            landing = new WorkflowRunStep(landing.Step, WorkflowRunStates.NotReached, "");
        }

        if (workDone && landing.State != WorkflowRunStates.Done)
        {
            var why = facts.WorkflowGate is { LetsGo: false } workflow ? workflow.Says
                : facts.Process is { LandingCannot: not null } process ? WorkflowGate.LandingSays(process) : null;
            if (why is not null) landing = landing with { State = WorkflowRunStates.CannotStart, Detail = WorkflowRunDetails.Refused, Words = why };
        }
        steps.Add(landing);
        if (facts.Current.Steps.FirstOrDefault(step => step.Kind == WorkflowKinds.PullRequest) is { } pullRequest)
        {
            steps.Add(PullRequest(pullRequest, landing, facts));
        }

        var newest = facts.Sessions.MaxBy(session => session.Created ?? DateTimeOffset.MinValue);
        if (facts.Process?.Named == true)
        {
            var added = steps.Where(step => step.Added is not null).ToList();
            // A version owns its ids and order. Partial kinds remain visible rather than disappearing as passed.
            steps = facts.Current.Steps.Select(cell => steps.FirstOrDefault(step => step.Step.Id == cell.Id)
                ?? new WorkflowRunStep(cell, WorkflowRunStates.CannotStart, WorkflowRunDetails.Unread)
                { Words = cell.Limit is { } limit ? WorkflowLimits.Says(limit) : "This step cannot be read here." }).ToList();
            steps.InsertRange(steps.FindIndex(step => step.Step.Kind == WorkflowKinds.Landing), added);
        }
        return new WorkflowRun(facts.Repository, workspace, facts.Current, [.. facts.Quests.Select(quest => quest.Id)], steps)
        {
            Process = facts.Process,
            WorkflowGate = facts.WorkflowGate,
            Ask = ask,
            Session = newest?.Id,
            ChatRun = chat?.Run,
        };
    }

    /// <summary>Whether two commits are one: equal, or one an abbreviation of the other of git's own floor or more.</summary>
    internal static bool SameCommit(string left, string right) =>
        left.Length >= CommitNames.Digits && right.Length >= CommitNames.Digits
        && (left.StartsWith(right, StringComparison.OrdinalIgnoreCase) || right.StartsWith(left, StringComparison.OrdinalIgnoreCase));

    /// <summary>The graph's step of a kind; the landing and the work are required (design §2.7).</summary>
    internal static WorkflowStep Cell(WorkflowRunFacts facts, string kind) =>
        facts.Current.Steps.First(step => step.Kind == kind);

    /// <summary>
    /// Whether a quest's work here is finished: done, and not held for the person's yes (D133, D144); or, bound to a closure graph, at its
    /// unheld, sealed submission (FABRIC-WORKFLOW2d, §E5), since in that graph Daoris writes no Done before the graph's end.
    /// </summary>
    internal static bool Finished(QuestView quest, IReadOnlyList<QuestView> chain) => Closed(quest) || Submitted(quest, chain);

    /// <summary>Closed done, and not held for the person's yes (D133, D144): how every quest finished before a closure graph, and still does.</summary>
    internal static bool Closed(QuestView quest) => quest.Status == "Done" && !quest.Held;

    /// <summary>
    /// Whether a quest a closure graph holds is finished at its submission (FABRIC-WORKFLOW2d, §E5): taken, its current submission unheld
    /// and sealed. Sealed is the service's own reading (<c>QuestStore.SealingAsync</c>, FABRIC-CLOSE1c): the chain's next step published
    /// and standing on that submission, grown from it (or from none, a step from before) with no reopen since. The chain's last has no step
    /// to publish, so its unheld submission is enough, as the close door judges it. Its Done is the graph's close's to write.
    /// </summary>
    internal static bool Submitted(QuestView quest, IReadOnlyList<QuestView> chain) =>
        quest is { Status: "Taken", Closure: not null, Submission: { } current, Held: false }
        && (quest.Then.Count == 0
            || (chain.FirstOrDefault(step => Same(step.Parent, quest.Id)) is { SourceReopened: null } step
                && (step.GrewFrom is null || step.GrewFrom == new QuestSubmissionRef(current.Machine, current.Sequence))));

    /// <summary>
    /// Whether the person reopened a closure graph's quest's submission and nothing was made ready since (FABRIC-CLOSE1c, FABRIC-WORKFLOW2d):
    /// taken with no current submission, its chain's next step standing reopened, withdrawn while open or held while taken.
    /// </summary>
    internal static bool Reopened(QuestView quest, IReadOnlyList<QuestView> chain) =>
        quest is { Status: "Taken", Closure: not null, Submission: null }
        && chain.Any(step => Same(step.Parent, quest.Id) && step.SourceReopened is not null);

    /// <summary>
    /// The started step a closure graph's quest's reopen still holds once that quest is made ready again (FABRIC-CLOSE1c7, CLOSE1c's choice
    /// 2), or null: taken, the source's submission unheld, as the service's <c>continue</c> takes it. An untaken step is rebound by that
    /// submission instead, so only a taken one waits on the person.
    /// </summary>
    internal static QuestView? WaitsGoOn(QuestView quest, IReadOnlyList<QuestView> chain) =>
        quest is { Status: "Taken", Closure: not null, Submission: not null, Held: false }
            ? chain.FirstOrDefault(step => Same(step.Parent, quest.Id) && step is { Status: "Taken", SourceReopened: not null })
            : null;

    /// <summary>
    /// A hold the work's words name (D133, D144): a held done's, and a closure graph's held submission's, which holds as a done does (§E5).
    /// A step held because its source was reopened is FABRIC-CLOSE1c's, which these words do not say.
    /// </summary>
    internal static bool HeldForYou(QuestView quest) =>
        quest is { Held: true, Status: "Done" }
        || quest is { Held: true, Status: "Taken", Closure: not null, Submission: not null, Hold: null or EvidenceCodes.Departed or EvidenceCodes.Unread or EvidenceCodes.MissingHold };

    internal static bool Live(TracedSession session) => !Endings.Contains(session.State);

    /// <summary>
    /// The work (design §3.2's <c>work</c>): its first quest here that is not finished, and where its latest session stands. What
    /// waits on the person comes first (a go-ahead, a held done, a park), then what an agent is doing, then how it ended.
    /// </summary>
    private static WorkflowRunStep Work(WorkflowStep cell, IReadOnlyList<QuestView> work, IReadOnlyList<TracedSession> sessions, WorkflowRunFacts facts)
    {
        WorkflowRunStep Step(string state, string detail) => new(cell, state, detail) { Of = work.Count };
        if (work.Count == 0) return Step(WorkflowRunStates.NotReached, "");

        if (work.FirstOrDefault(quest => quest.Status == "Declined") is { } declined)
        {
            return Step(WorkflowRunStates.Stopped, WorkflowRunDetails.Declined) with { Quest = declined.Id, Words = declined.Note, At = declined.Updated };
        }

        // A step another repository holds seals a submission here too (FABRIC-WORKFLOW2d), so the seal reads the whole chain where it was handed.
        var chain = facts.Chain.Count > 0 ? facts.Chain : facts.Quests;
        var done = work.Count(quest => Finished(quest, chain));
        var current = work.FirstOrDefault(quest => !Finished(quest, chain));
        if (current is null)
        {
            var last = sessions.LastOrDefault();
            // Finished at a submission, the work says so, at the newest one made: the graph's close writes the Done at its end (§E5).
            var submitted = work.Any(quest => !Closed(quest));
            return Step(WorkflowRunStates.Done, submitted ? WorkflowRunDetails.Submitted : WorkflowRunDetails.Finished) with
            {
                Count = done, Quest = work[^1].Id, Session = last?.Id, Agent = last?.Adapter,
                At = (work[^1] is { Status: "Taken", Submission.At: { } made } ? made : work[^1].Updated) ?? last?.Updated,
            };
        }

        var mine = sessions.Where(session => Same(session.Quest, current.Id)).ToList();
        var latest = mine.LastOrDefault();
        var ids = new HashSet<string>(sessions.Select(session => session.Id), StringComparer.OrdinalIgnoreCase);
        var quests = new HashSet<string>(work.Select(quest => quest.Id), StringComparer.OrdinalIgnoreCase);
        WorkflowRunStep At(string state, string detail) => Step(state, detail) with
        {
            Count = done, Quest = current.Id, Session = latest?.Id, Agent = latest?.Adapter,
        };

        // A go-ahead a session of the work asked holds that session's act until the person answers (D135, KNOWUSE1a).
        if (facts.GoAheads.FirstOrDefault(goAhead => goAhead.Answer is null
                && goAhead.Asked.Any(asked => ids.Contains(asked.Session) || (asked.Quest is { } quest && quests.Contains(quest)))) is { } asked)
        {
            var first = asked.Asked.MinBy(request => request.At)!;
            return At(WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.GoAhead) with
            {
                GoAhead = asked.Number, Session = first.Session, At = first.At, Words = asked.Act,
            };
        }

        if (HeldForYou(current))
        {
            return At(WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.Held) with { Code = current.Hold ?? EvidenceCodes.Departed, At = current.Updated };
        }

        if (latest is { State: "awaiting-person" })
        {
            return latest.Answer is null
                ? At(WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.Park) with { At = latest.Updated, Words = latest.Note }
                : At(WorkflowRunStates.Working, WorkflowRunDetails.Answered) with { At = latest.Updated };
        }

        if (latest is not null && Live(latest)) return At(WorkflowRunStates.Working, WorkflowRunDetails.Agent) with { At = latest.Created };

        // Its taker asked another repository and waits on the answer (D79): that repository's agent is the one at it.
        if (current.Awaits is { } awaits) return At(WorkflowRunStates.WaitingOnAgent, WorkflowRunDetails.Awaits) with { Quest = awaits, At = current.Updated };

        if (latest is { State: "failed" }) return At(WorkflowRunStates.Failed, WorkflowRunDetails.Failed) with { At = latest.Updated, Words = latest.Note };

        // A stop the person made holds the quest (D126 §3.4); one that was not theirs (D104) is the driver's to start again.
        if (latest is { State: "stopped", Interrupted: false }) return At(WorkflowRunStates.Stopped, WorkflowRunDetails.Stopped) with { At = latest.Updated };

        // The person reopened its submission (FABRIC-CLOSE1c): the finish is taken back, and the work waits for its agent's next ready, judged
        // from the start (FABRIC-WORKFLOW2d). Said where nobody is at it, as its done would be; an agent at it, failed or stopped is said above.
        if (Reopened(current, chain)) return At(WorkflowRunStates.WaitingOnAgent, WorkflowRunDetails.Reopened);

        // Made ready again, its started step still held waits on the person's go-on or abandon at that step's door (FABRIC-CLOSE1c7).
        if (WaitsGoOn(current, chain) is { } held)
        {
            return At(WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.GoOn) with { Quest = held.Id, At = current.Submission!.At ?? current.Updated };
        }

        if (current.Status == "Taken" && latest is { State: "completed" or "stood-down" })
        {
            return At(WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.Unclosed) with { At = latest.Updated };
        }

        return At(WorkflowRunStates.Working, WorkflowRunDetails.Queued) with { At = current.Updated };
    }

    internal static WorkflowRunStep? AddedLook(WorkflowRunFacts facts, bool workDone, bool landed) => WorkflowReviewSteps.AddedLook(facts, workDone, landed);

    /// <summary>
    /// How many commits a session's evidence names (D46 §4): none where it says none landed, the lines after its heading where
    /// it names some, and null where it has none or none could be read, which may still have work to land.
    /// </summary>
    internal static int? CommitsOf(TracedSession session) => session.Evidence switch
    {
        null => null,
        var evidence when evidence.StartsWith("no commits landed", StringComparison.Ordinal) => 0,
        var evidence when evidence.StartsWith("commits landed:", StringComparison.Ordinal) => session.Commits.Count,
        _ => null,
    };

    /// <summary>
    /// Whether a run's steps hold an <c>integrate</c> not yet settled: the workflow steps on past the landing, the only maker of its unit's
    /// integration request, so bringing its session into another's (LINESTOGETHER1d2) is refused.
    /// </summary>
    public static bool StepsOnPastLanding(IEnumerable<WorkflowRunStep> steps) =>
        steps.Any(each => each.Step.Kind == NodeKinds.Integrate && !WorkflowRunStates.Settled(each.State));

    internal static string? TextOf(WorkflowStep step, string name) =>
        step.Settings.FirstOrDefault(setting => setting.Name == name)?.Value as string;

    internal static bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
