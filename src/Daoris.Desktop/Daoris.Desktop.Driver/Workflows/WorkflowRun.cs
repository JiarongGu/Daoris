namespace Daoris.Driver;

/// <summary>
/// Where a run's step stands (WORKFLOW1c; the workflow design §5.2), as a code a page words and a test holds. The design's table,
/// less what this build cannot reach yet: a service's wait comes with a check (WORKFLOW1k).
/// </summary>
public static class WorkflowRunStates
{
    /// <summary>The work has not come to it.</summary>
    public const string NotReached = "not-reached";

    /// <summary>Someone is at it and nobody needs to act yet: an agent, the set-up step, Daoris's own look.</summary>
    public const string Working = "working";

    /// <summary>It waits for the person, by the press its detail names.</summary>
    public const string WaitingOnYou = "waiting-on-you";

    /// <summary>It waits for an agent's answer: a set-up shown again, findings answered, a question another repository answers.</summary>
    public const string WaitingOnAgent = "waiting-on-agent";

    /// <summary>What it waits on could not be read, which is never taken as passed or failed (D148 point 5).</summary>
    public const string NotKnown = "not-known";

    /// <summary>It cannot go on as it stands: its detail says why, and the person's door fixes it.</summary>
    public const string CannotStart = "cannot-start";

    public const string Done = "done";

    /// <summary>Passed over for this work: by the person's skip, or a choice on the chain or the ask.</summary>
    public const string Skipped = "skipped";

    /// <summary>An outcome: a session failed, or a reviewer's pass ended without an opinion.</summary>
    public const string Failed = "failed";

    /// <summary>Ended without going on: the person's stop, a decline, a pull request abandoned, a tree discarded.</summary>
    public const string Stopped = "stopped";

    /// <summary>The states that let the run go past a step: what <see cref="WorkflowRun.At"/> passes over.</summary>
    public static bool Settled(string state) => state is Done or Skipped;
}

/// <summary>
/// Which row of a state's table a step stands at (WORKFLOW1c; design §5.2's <i>Said</i>), a code each, for the page's words. Read
/// with the step's kind: <see cref="Agent"/> on the work is its session, on the opinion its reviewer.
/// </summary>
public static class WorkflowRunDetails
{
    // The work.
    /// <summary>A quest of it is open, and no session works on it yet.</summary>
    public const string Queued = "queued";

    /// <summary>A session works on it, or a reviewer reads it.</summary>
    public const string Agent = "agent";

    /// <summary>The person answered a park, and the same session goes on at the driver's next look (ANSWER1c).</summary>
    public const string Answered = "answered";

    /// <summary>A session parked asking the person (D46 §3).</summary>
    public const string Park = "park";

    /// <summary>A go-ahead a session of the work asked, unanswered (D135).</summary>
    public const string GoAhead = "go-ahead";

    /// <summary>A done held for the person's yes: a departure or missing evidence (D133, D144); its code is the quest's hold.</summary>
    public const string Held = "held";

    /// <summary>Its taker waits on a question another repository answers (D79).</summary>
    public const string Awaits = "awaits";

    /// <summary>Its session finished and its quest stays taken: the person's done closes it (QUESTCLOSE1).</summary>
    public const string Unclosed = "unclosed";

    /// <summary>Its last session failed.</summary>
    public const string Failed = "failed";

    /// <summary>The person stopped its last session.</summary>
    public const string Stopped = "stopped";

    /// <summary>A quest of it was declined.</summary>
    public const string Declined = "declined";

    /// <summary>Every quest of it here is done, and none is held.</summary>
    public const string Finished = "finished";

    // The second opinion: its details are its gate's own states (OpinionGateStates, XAGENT1f), and one more.
    /// <summary>The work landed, and its landing record keeps no word of the opinion that let it go.</summary>
    public const string Landed = "landed";

    // The look (the review's states, ReviewStates, where they stand at a step).
    public const string NotShown = "not-shown";
    public const string BeingSetUp = "being-set-up";
    public const string Shown = "shown";
    public const string NotYet = "not-yet";
    public const string NotHeld = "not-held";
    public const string Reviewed = "reviewed";

    /// <summary>The person skipped it for this work.</summary>
    public const string Skip = "skip";

    /// <summary>The chain's or the ask's choice says no review for this work: its code is the level, its words the choice's.</summary>
    public const string Off = "off";

    // The landing.
    /// <summary>It waits for the person's <i>Accept…</i>.</summary>
    public const string Accept = "accept";

    /// <summary>Accepted automatically at the driver's next look (D145).</summary>
    public const string Automatic = "automatic";

    /// <summary>Landed on a branch (D102): its branch and when.</summary>
    public const string Branch = "branch";

    /// <summary>Landed into its line, as the session's record keeps the acceptance (D100).</summary>
    public const string Merge = "merge";

    /// <summary>Its work reached the line or a branch of the person's another way (LAND2b's <c>already</c> and <c>carried</c>).</summary>
    public const string Already = "already";

    /// <summary>The work made no commits, so nothing lands.</summary>
    public const string Nothing = "nothing";

    /// <summary>The automatic landing's last try could not land it: its code is the try's.</summary>
    public const string Refused = "refused";

    /// <summary>Its tree was discarded before it landed.</summary>
    public const string Gone = "gone";

    /// <summary>The work ran on another machine, which keeps its landing (D47 §4).</summary>
    public const string Elsewhere = "elsewhere";

    // The pull request.
    /// <summary>Open, or not read since it opened: the person merges it on the platform.</summary>
    public const string Merging = "merge";

    /// <summary>The platform said it completed (D148).</summary>
    public const string Merged = "merged";

    /// <summary>The platform said it was abandoned.</summary>
    public const string Abandoned = "abandoned";

    /// <summary>The plugin did not push the branch, so no pull request opened: the hand-off is the door (WSR5b).</summary>
    public const string NotPushed = "not-pushed";

    /// <summary>The last ask of its plugin failed: its code is the failure's (PLUGHOOK1a).</summary>
    public const string AskFailed = "ask-failed";

    /// <summary>The work landed into its line, so no branch was pushed and no pull request opened.</summary>
    public const string NoBranch = "no-branch";

    /// <summary>A store this step reads did not answer: its words say why.</summary>
    public const string Unread = "unread";
}

/// <summary>
/// What one run is read from (the workflow design §5.1): its chosen workflow graph and the records that already exist,
/// never a copy of its own. Every list is the run's own, gathered by its reader, so the derivation decides from these alone.
/// </summary>
/// <param name="Current">The graph: Current live or the bound version's steps, with each participant.</param>
public sealed record WorkflowRunFacts(string Repository, CurrentWorkflow Current)
{
    public WorkflowProcess? Process { get; init; }
    public WorkflowGateState? WorkflowGate { get; init; }

    /// <summary>The chain's quests in this repository, in the chain's order: its work, and any set-up step of its review.</summary>
    public IReadOnlyList<QuestView> Quests { get; init; } = [];

    /// <summary>The session records on those quests, closed ones included, in any order.</summary>
    public IReadOnlyList<TracedSession> Sessions { get; init; } = [];

    /// <summary>The go-aheads on the chain's ask (D135); the run's are those a session or a quest of it asked.</summary>
    public IReadOnlyList<GoAheadView> GoAheads { get; init; } = [];

    /// <summary>The review's gate for the chain's work here (<see cref="ReviewGate"/>), or null where it was not read.</summary>
    public ReviewGateState? Review { get; init; }

    /// <summary>
    /// The second opinion's gate for the chain's work here (XAGENT1f, <see cref="OpinionGate"/>), as every landing door reads it, or
    /// null where it was not read: no tree of the work stands here to read it in.
    /// </summary>
    public OpinionGateState? Opinion { get; init; }

    /// <summary>This machine's landings that name a session of the run, standing or traces (D102, D113).</summary>
    public IReadOnlyList<LandedBranch> Landings { get; init; } = [];

    /// <summary>When each session's work was accepted into its line, as its conversation's record keeps the acceptance (D100).</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> Accepted { get; init; } = new Dictionary<string, DateTimeOffset>();

    /// <summary>The due list's entries for the run's sessions (LAND2b).</summary>
    public IReadOnlyList<AutoLanding> AutoLandings { get; init; } = [];
}

/// <summary>
/// One step of a run (design §5.2, §7): the chosen workflow's step, where it stands, the row of that state's table,
/// and the facts its words and its door are made of, each the record's own.
/// </summary>
/// <param name="State">One of <see cref="WorkflowRunStates"/>.</param>
/// <param name="Detail">One of <see cref="WorkflowRunDetails"/>, read with the step's kind.</param>
public sealed record WorkflowRunStep(WorkflowStep Step, string State, string Detail)
{
    /// <summary>
    /// Why a step outside the chosen graph is in this run: the review's level that asked for a look for this work (<see cref="ReviewLevels"/>),
    /// or <c>opinion</c> for a reading nothing declared. Null for the graph's own steps.
    /// </summary>
    public string? Added { get; init; }

    /// <summary>The session its record is: the work's latest, the reviewer's, the set-up step's, the one landed or to land.</summary>
    public string? Session { get; init; }

    /// <summary>The quest it stands at: the work's current quest, the set-up step, the quest its taker waits on.</summary>
    public string? Quest { get; init; }

    /// <summary>Who acts, as the record names it: a session's adapter, or a reviewer as the person reads it.</summary>
    public string? Agent { get; init; }

    /// <summary>When it came to stand where it does, where the record kept a moment.</summary>
    public DateTimeOffset? At { get; init; }

    public string? Environment { get; init; }

    /// <summary>The commit a verdict or a set-up names.</summary>
    public string? Commit { get; init; }

    public string? Branch { get; init; }

    /// <summary>The pull request's address, as its plugin answered it.</summary>
    public string? PullRequest { get; init; }

    public string? Plugin { get; init; }

    /// <summary>How many: quests done, findings unanswered or given.</summary>
    public int? Count { get; init; }

    /// <summary>Of how many.</summary>
    public int? Of { get; init; }

    /// <summary>A go-ahead's number on the run's ask.</summary>
    public int? GoAhead { get; init; }

    /// <summary>Words the record kept: the person's (a skip's, a <i>not yet</i>'s) or a session's note, shown as written.</summary>
    public string? Words { get; init; }

    /// <summary>A code the record kept: a quest's hold, a landing try's, a pass's failure, an ask's, who accepted.</summary>
    public string? Code { get; init; }
}

/// <summary>A chain's work in one repository, read as steps (WORKFLOW1c; D157 point 11, design §5.1).</summary>
/// <param name="Quests">The run's quests, in the chain's order.</param>
public sealed record WorkflowRun(string Repository, string Workspace, CurrentWorkflow Current, IReadOnlyList<string> Quests, IReadOnlyList<WorkflowRunStep> Steps)
{
    public WorkflowProcess? Process { get; init; }
    public WorkflowGateState? WorkflowGate { get; init; }
    public string? Problem { get; init; }

    /// <summary>The ask the chain was asked by, or null.</summary>
    public string? Ask { get; init; }

    /// <summary>The run's newest session: the one its doors attend.</summary>
    public string? Session { get; init; }

    /// <summary>The first unsettled step; null where all settled or no graph could be read. Problem distinguishes the latter.</summary>
    public string? At => Steps.FirstOrDefault(step => !WorkflowRunStates.Settled(step.State))?.Step.Id;
}

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
        var workStep = Work(Cell(facts, WorkflowKinds.Work), work, workSessions, facts);
        steps.Add(workStep);
        var workDone = workStep.State == WorkflowRunStates.Done;

        // The landing is read before the opinion and the look, since a landing done is what both let go.
        var landing = Landing(Cell(facts, WorkflowKinds.Landing), workDone, workSessions, facts);
        var opinion = Opinion(facts, workDone, landing);
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
        // look are settled.
        if (landing.State != WorkflowRunStates.Done
            && ((opinion is not null && !WorkflowRunStates.Settled(opinion.State)) || (look is not null && !WorkflowRunStates.Settled(look.State))))
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
        };
    }

    /// <summary>The graph's step of a kind; the landing and the work are required (design §2.7).</summary>
    private static WorkflowStep Cell(WorkflowRunFacts facts, string kind) =>
        facts.Current.Steps.First(step => step.Kind == kind);

    /// <summary>Whether a quest's work here is finished: done, and not held for the person's yes (D133, D144).</summary>
    private static bool Finished(QuestView quest) => quest.Status == "Done" && !quest.Held;

    private static bool Live(TracedSession session) => !Endings.Contains(session.State);

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

        var done = work.Count(Finished);
        var current = work.FirstOrDefault(quest => !Finished(quest));
        if (current is null)
        {
            var last = sessions.LastOrDefault();
            return Step(WorkflowRunStates.Done, WorkflowRunDetails.Finished) with
            {
                Count = done, Quest = work[^1].Id, Session = last?.Id, Agent = last?.Adapter, At = work[^1].Updated ?? last?.Updated,
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

        if (current is { Status: "Done", Held: true })
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

        if (current.Status == "Taken" && latest is { State: "completed" or "stood-down" })
        {
            return At(WorkflowRunStates.WaitingOnYou, WorkflowRunDetails.Unclosed) with { At = latest.Updated };
        }

        return At(WorkflowRunStates.Working, WorkflowRunDetails.Queued) with { At = current.Updated };
    }

    /// <summary>
    /// The second opinion (design §3.2's <c>opinion</c>, D155): where Current draws one, its gate's state (XAGENT1f, the
    /// second-agent design §8.5), the detail being the gate's own code. It is not reached while the work runs, since the gate reads
    /// the chain's work once it is done here (§8.1); and a landing done is what it let go, as its landing record keeps it.
    /// </summary>
    /// <remarks>
    /// The gate's states, where they stand at a step: another agent at it (<c>not-asked</c>, which the driver's next look asks;
    /// <c>reading</c>; <c>read-again</c>) is working; the working session answering its findings is waiting on an agent; a dispute,
    /// commits no other agent read, and an absence the rule requires all wait on the person; an absence it does not require is
    /// passed over; <c>settled</c> and the person's answers (<c>anyway</c>, <c>myself</c>, <c>answered</c>) are done.
    /// </remarks>
    private static WorkflowRunStep? Opinion(WorkflowRunFacts facts, bool workDone, WorkflowRunStep landing)
    {
        if (facts.Current.Steps.FirstOrDefault(step => step.Kind == WorkflowKinds.Opinion) is not { } cell) return null;
        WorkflowRunStep Step(string state, string detail) => new(cell, state, detail);

        // A landing done is what the gate let go (no door lands what it holds): its record keeps what did.
        if (landing.State == WorkflowRunStates.Done && landing.Detail is not WorkflowRunDetails.Nothing)
        {
            var kept = facts.Landings.Select(entry => entry.Opinion).LastOrDefault(opinion => opinion is not null);
            if (kept is null) return Step(WorkflowRunStates.Done, WorkflowRunDetails.Landed);
            return Step(kept.Said == OpinionGateStates.Unavailable ? WorkflowRunStates.Skipped : WorkflowRunStates.Done, kept.Said) with
            {
                Agent = kept.Reviewer, Commit = kept.Tip, At = kept.At, Words = kept.Words, Code = kept.Code, Count = kept.Disputes,
            };
        }

        if (!workDone) return Step(WorkflowRunStates.NotReached, "");
        if (facts.Opinion is not { } gate) return Step(WorkflowRunStates.NotKnown, OpinionGateStates.Unread);

        var opinion = gate.Opinion;
        var on = Step("", gate.State) with
        {
            Session = opinion?.Session ?? gate.Session,
            Agent = opinion?.Who,
            Commit = gate.Recheck is { Given: true } recheck ? recheck.Tip : opinion?.Tip,
            Code = gate.Code,
        };
        return gate.State switch
        {
            OpinionGateStates.None => on with { State = WorkflowRunStates.Skipped },
            OpinionGateStates.WaitsChain => on with { State = WorkflowRunStates.NotReached, Quest = gate.Later },
            OpinionGateStates.NotAsked or OpinionGateStates.Reading or OpinionGateStates.ReadAgain =>
                on with { State = WorkflowRunStates.Working, Session = gate.State == OpinionGateStates.ReadAgain ? gate.Recheck?.Session ?? on.Session : on.Session },
            OpinionGateStates.WithSession => on with
            {
                State = WorkflowRunStates.WaitingOnAgent, Session = opinion?.HandedTo ?? opinion?.Working ?? gate.Session,
                Count = opinion?.Findings?.Count,
            },
            OpinionGateStates.Disputed => on with { State = WorkflowRunStates.WaitingOnYou, Session = gate.Session, Count = gate.Disputes?.Count },
            OpinionGateStates.CommitsSince => on with { State = WorkflowRunStates.WaitingOnYou, Session = gate.Session, Count = gate.Since },
            OpinionGateStates.Unavailable => on with
            {
                State = gate.Required ? WorkflowRunStates.WaitingOnYou : WorkflowRunStates.Skipped, Session = gate.Session, At = gate.Until,
            },
            OpinionGateStates.Settled => on with { State = WorkflowRunStates.Done, Count = opinion?.Findings?.Count },
            OpinionGateStates.Anyway or OpinionGateStates.Myself or OpinionGateStates.Answered => on with
            {
                State = WorkflowRunStates.Done, At = gate.Person?.At, Words = gate.Person?.Words, Code = gate.Person?.Said,
            },
            // A named workflow's opinion step that cannot start (WORKFLOW1f): it sits, saying why.
            OpinionGateStates.CannotStart => on with { State = WorkflowRunStates.CannotStart, Session = gate.Session, Words = gate.Problem },
            _ => on with { State = WorkflowRunStates.NotKnown, Detail = OpinionGateStates.Unread, Words = gate.Problem },
        };
    }

    /// <summary>
    /// The person's look (design §3.2's <c>look</c>, D154): where Current draws one, or where the work's own choice asks for one
    /// (<i>a look added for this work</i>, design §4.6). Its state is the review's gate's; a landing done is what it let go.
    /// </summary>
    private static WorkflowRunStep? Look(WorkflowRunFacts facts, bool workDone, WorkflowRunStep landing)
    {
        var cell = facts.Current.Steps.FirstOrDefault(step => step.Kind == WorkflowKinds.Look);
        var gate = facts.Review;
        var decision = gate?.Decision;
        var asks = decision?.Reviews == true;
        if (cell is null && !asks) return null;

        var environment = decision?.Environment ?? (cell is null ? null : TextOf(cell, "environment"));
        var id = WorkflowKinds.Look;
        while (cell is null && facts.Current.Steps.Any(each => each.Id == id)) id = "added-" + id;
        var step = cell ?? new WorkflowStep(
            id, WorkflowKinds.Look, WorkflowParticipation.AgentAndYou, WorkflowExecutor.Agent, WorkflowPress.Reviewed,
            new WorkflowSource(WorkflowRule.Review, LandingSource.Default), [new("environment", environment)],
            WorkflowRuntime.Partial, WorkflowLimits.LookPartial);
        var on = new WorkflowRunStep(step, "", "")
        {
            Added = cell is null ? decision!.Level : null,
            Environment = environment,
            Quest = decision?.SetUpStep?.Id,
        };

        if (gate is null) return on with { State = WorkflowRunStates.NotKnown, Detail = WorkflowRunDetails.Unread };
        if (gate.State == ReviewStates.Unread) return on with { State = WorkflowRunStates.NotKnown, Detail = WorkflowRunDetails.Unread, Words = gate.Problem };

        // The person's skip stands whenever it was given (D154 point 7, row 1).
        if (gate.State == ReviewStates.Skipped)
        {
            return on with
            {
                State = WorkflowRunStates.Skipped, Detail = WorkflowRunDetails.Skip, Quest = decision!.SkippedOn, Words = decision.Words, At = decision.Skip?.At,
            };
        }

        // Current draws a look, and the chain's or the ask's choice says none for this work.
        if (!asks)
        {
            return on with { State = WorkflowRunStates.Skipped, Detail = WorkflowRunDetails.Off, Code = decision!.Level, Words = decision.Choice };
        }

        // A landing done is what the look let go: no door lands work the gate holds (REVIEWENV1c), so it was reviewed then. Its
        // record keeps the commit where it was a branch; a tree gone since cannot show what it held.
        if (landing.State == WorkflowRunStates.Done && landing.Detail is not WorkflowRunDetails.Nothing)
        {
            var kept = facts.Landings.Select(entry => entry.Review).LastOrDefault(review => review?.Said == ReviewVerdicts.Reviewed);
            return on with
            {
                State = WorkflowRunStates.Done, Detail = WorkflowRunDetails.Reviewed,
                Commit = kept?.Commit ?? gate.SetUp?.Commit, At = kept?.At ?? gate.Verdict?.At,
            };
        }

        if (!workDone) return on with { State = WorkflowRunStates.NotReached, Detail = "" };

        var setUp = gate.SetUp;
        var setUpSession = setUp?.Session ?? facts.Sessions
            .Where(session => decision!.SetUpStep is { } stepQuest && Same(session.Quest, stepQuest.Id))
            .MaxBy(session => session.Created ?? DateTimeOffset.MinValue)?.Id;
        return gate.State switch
        {
            ReviewStates.Reviewed => on with
            {
                State = WorkflowRunStates.Done, Detail = WorkflowRunDetails.Reviewed, Commit = setUp?.Commit, At = gate.Verdict?.At, Session = setUpSession,
            },
            ReviewStates.NotShown => on with { State = WorkflowRunStates.WaitingOnYou, Detail = WorkflowRunDetails.NotShown },
            ReviewStates.CannotStart => on with { State = WorkflowRunStates.CannotStart, Detail = WorkflowRunDetails.Unread, Words = gate.Says },
            ReviewStates.BeingSetUp => on with { State = WorkflowRunStates.Working, Detail = WorkflowRunDetails.BeingSetUp, Session = setUpSession },
            ReviewStates.Shown => on with
            {
                State = WorkflowRunStates.WaitingOnYou, Detail = WorkflowRunDetails.Shown, Commit = setUp?.Commit, At = setUp?.At, Session = setUpSession,
            },
            ReviewStates.NotYet => on with
            {
                State = WorkflowRunStates.WaitingOnAgent, Detail = WorkflowRunDetails.NotYet, Commit = setUp?.Commit, Words = gate.Verdict?.Words,
                At = gate.Verdict?.At, Session = setUpSession,
            },
            ReviewStates.NotHeld => on with
            {
                State = WorkflowRunStates.WaitingOnAgent, Detail = WorkflowRunDetails.NotHeld, Commit = setUp?.Commit, Session = setUpSession,
            },
            _ => on with { State = WorkflowRunStates.NotKnown, Detail = WorkflowRunDetails.Unread, Words = gate.Says },
        };
    }

    /// <summary>
    /// The landing (design §3.2's <c>landing</c>): reached once the work is done, read on the newest of its sessions that has work
    /// to land. Done where this machine's record names it, a branch or the line; else it waits for the person's <i>Accept…</i>,
    /// or for Daoris's next look where the rule accepts automatically (D145).
    /// </summary>
    private static WorkflowRunStep Landing(WorkflowStep cell, bool workDone, IReadOnlyList<TracedSession> sessions, WorkflowRunFacts facts)
    {
        var ended = sessions.Where(session => !Live(session)).ToList();
        // A session whose evidence says it made no commits has nothing to land; one whose evidence is unread may have.
        var landable = ended.Where(session => CommitsOf(session) != 0).ToList();
        if (!workDone)
        {
            return new WorkflowRunStep(cell, WorkflowRunStates.NotReached, "");
        }

        if (landable.LastOrDefault() is not { } newest)
        {
            return new WorkflowRunStep(cell, WorkflowRunStates.Done, WorkflowRunDetails.Nothing) { Session = ended.LastOrDefault()?.Id };
        }

        var on = new WorkflowRunStep(cell, "", "") { Session = newest.Id };
        // Another machine ran it, and keeps its landing (D47 §4): nothing here can say.
        if (newest.Teammate) return on with { State = WorkflowRunStates.NotKnown, Detail = WorkflowRunDetails.Elsewhere };

        if (facts.Landings.LastOrDefault(entry => entry.Names(newest.Id)) is { } landed)
        {
            return on with
            {
                State = WorkflowRunStates.Done, Detail = WorkflowRunDetails.Branch, Branch = landed.Branch, At = landed.AcceptedAt(newest.Id),
                Code = landed.AcceptedByOf(newest.Id), Plugin = landed.Plugin,
            };
        }

        if (facts.Accepted.TryGetValue(newest.Id, out var accepted))
        {
            return on with { State = WorkflowRunStates.Done, Detail = WorkflowRunDetails.Merge, At = accepted };
        }

        var due = facts.AutoLandings.LastOrDefault(entry => Same(entry.Session, newest.Id));
        if (due is { Closed: { } closed, Last.Code: var code })
        {
            switch (code)
            {
                case AutoLandingCode.Already or AutoLandingCode.Carried:
                    return on with { State = WorkflowRunStates.Done, Detail = WorkflowRunDetails.Already, Code = code, At = closed };
                case AutoLandingCode.Nothing:
                    return on with { State = WorkflowRunStates.Done, Detail = WorkflowRunDetails.Nothing, At = closed };
                case AutoLandingCode.Gone:
                    return on with { State = WorkflowRunStates.Stopped, Detail = WorkflowRunDetails.Gone, At = closed };
            }
        }

        // Accepted automatically: Daoris's own look lands it, unless its last try met what only a change or a press moves on.
        if (cell.Participation == WorkflowParticipation.Automatic && due is not { Closed: not null })
        {
            return due?.Last is { Code: AutoLandingCode.Uncommitted or AutoLandingCode.Exists or AutoLandingCode.Completed or AutoLandingCode.Refused } tried
                ? on with { State = WorkflowRunStates.CannotStart, Detail = WorkflowRunDetails.Refused, Code = tried.Code, At = tried.At, Branch = tried.Branch }
                : on with { State = WorkflowRunStates.Working, Detail = WorkflowRunDetails.Automatic, At = due?.DueAt };
        }

        return on with { State = WorkflowRunStates.WaitingOnYou, Detail = WorkflowRunDetails.Accept, At = newest.Updated };
    }

    /// <summary>
    /// The pull request (design §3.2's <c>pull-request</c>, D148): opened by the plugin that pushed the landed branch; its state is
    /// the answer its plugin last gave, kept on the landing record, which Current asks only when branches are cleaned up (§5.4).
    /// </summary>
    private static WorkflowRunStep PullRequest(WorkflowStep cell, WorkflowRunStep landing, WorkflowRunFacts facts)
    {
        if (landing.State != WorkflowRunStates.Done) return new WorkflowRunStep(cell, WorkflowRunStates.NotReached, "");
        if (landing.Detail == WorkflowRunDetails.Nothing) return new WorkflowRunStep(cell, WorkflowRunStates.Skipped, WorkflowRunDetails.Nothing);
        if (landing.Detail != WorkflowRunDetails.Branch || landing.Session is not { } session
            || facts.Landings.LastOrDefault(entry => entry.Names(session)) is not { } entry)
        {
            return new WorkflowRunStep(cell, WorkflowRunStates.Skipped, WorkflowRunDetails.NoBranch);
        }

        var on = new WorkflowRunStep(cell, "", "")
        {
            Session = session, Branch = entry.Branch, Plugin = entry.Plugin ?? TextOf(cell, "plugin"),
            PullRequest = entry.PullRequest ?? entry.PullRequestState?.PullRequest,
        };
        var kept = entry.PullRequestState;
        if (kept?.State == PullRequestStates.Completed)
        {
            return on with { State = WorkflowRunStates.Done, Detail = WorkflowRunDetails.Merged, At = kept.At ?? kept.AskedAt };
        }

        if (kept?.State == PullRequestStates.Abandoned)
        {
            return on with { State = WorkflowRunStates.Stopped, Detail = WorkflowRunDetails.Abandoned, At = kept.At ?? kept.AskedAt };
        }

        if (!entry.Pushed) return on with { State = WorkflowRunStates.CannotStart, Detail = WorkflowRunDetails.NotPushed };

        // A failure never overwrites an answer (PLUGHOOK1a): the newer of the two is what is known now.
        if (entry.PullRequestAskFailed is { } failed && failed.At > (kept?.AskedAt ?? DateTimeOffset.MinValue))
        {
            return on with { State = WorkflowRunStates.NotKnown, Detail = WorkflowRunDetails.AskFailed, Code = failed.Code, Plugin = failed.Plugin, At = failed.At };
        }

        return on with { State = WorkflowRunStates.WaitingOnYou, Detail = WorkflowRunDetails.Merging, Code = kept?.State, At = kept?.AskedAt };
    }

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

    private static string? TextOf(WorkflowStep step, string name) =>
        step.Settings.FirstOrDefault(setting => setting.Name == name)?.Value as string;

    private static bool Same(string? left, string? right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
}
