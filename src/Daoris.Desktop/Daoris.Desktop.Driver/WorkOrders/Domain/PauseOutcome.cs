namespace Daoris.Driver;

/// <summary>
/// What a pause does with one piece of the work (D132 point 2, design §2.1), in the public spelling the page and the terminal
/// share: a word, never a sentence, so each door says it in its own words and the page in the reader's language.
/// </summary>
public static class PauseAct
{
    /// <summary>A quest, open or taken here: nothing of it starts on this machine until *Resume*; its place, take, tree and strikes stay.</summary>
    public const string Paused = "paused";

    /// <summary>A quest done or declined: a pause changes no work.</summary>
    public const string Closed = "closed";

    /// <summary>A quest taken with no session of this machine's on it: its taker's, on another machine or outside Daoris.</summary>
    public const string Elsewhere = "elsewhere";

    /// <summary>A live session this machine runs: stopped, as the person's stop, which *Resume* releases.</summary>
    public const string Stopped = "stopped";

    /// <summary>A session waiting on you: left parked, since stopping it would end the question it asked.</summary>
    public const string Parked = "parked";

    /// <summary>The ask's running intake: left running, since an ask has one; what it publishes is paused with the rest.</summary>
    public const string Intake = "intake";

    /// <summary>A teammate's live session: its process is on their machine (D47 §4), and nothing here reaches it.</summary>
    public const string Teammate = "teammate";

    /// <summary>A session that ended: a record, untouched.</summary>
    public const string Ended = "ended";
}

/// <summary>
/// The words a pause keeps a live session for when it could not stop it (<see cref="WorkKept.Why"/>), beside
/// <see cref="PauseAct"/>'s: <c>WorkPausing</c> forwards them under the names its callers use (STRUCT3g-i).
/// </summary>
public static class PauseKept
{
    /// <summary>The Daoris process that runs it took no request in time.</summary>
    public const string Elsewhere = "elsewhere";

    /// <summary>Queued, and nothing on this machine runs it yet to stop.</summary>
    public const string NotRunning = "not-running";

    /// <summary>The service did not answer, or refused, the move of its record.</summary>
    public const string Unanswered = "unanswered";
}

/// <summary>A quest of the work and what a pause does with it, with the pause that holds it now where one does.</summary>
public sealed record WorkPlanQuest(WorkQuest Quest, string Pause)
{
    public PausedBy? PausedBy { get; init; }
}

/// <summary>A session of the work and what a pause does with it.</summary>
public sealed record WorkPlanSession(WorkSession Session, string Pause);

/// <summary>
/// The work of an ask or a quest with what a pause would do with each piece (PAUSE1b, design §1, §2.1): what <c>WORK_PLAN</c>
/// answers and what a pause acts on, so the screen and the pause cannot disagree. Abandon's half is PAUSE1d's.
/// </summary>
public sealed record WorkPlan(WorkPieces Pieces, IReadOnlyList<WorkPlanQuest> Quests, IReadOnlyList<WorkPlanSession> Sessions)
{
    /// <summary>This scope's own pause, as <c>driver.json</c> keeps it, or null where it is not paused here.</summary>
    public WorkPause? Paused { get; init; }

    /// <summary>Whether a pause would hold anything: a quest open or taken here, or for an ask still proposed, its intake.</summary>
    public bool Pausable { get; init; }

    /// <summary>The ask itself, for an ask's work, as the service answered it; null for a quest's.</summary>
    public AskView? Ask { get; init; }
}

/// <summary>How a pause came out.</summary>
public enum PauseVerdict
{
    /// <summary>The pause is written, and what of the work ran here was stopped or asked to stop.</summary>
    Paused,

    /// <summary>Nothing of the work is open or taken here, and no intake would start: nothing was written (D48 §6, information).</summary>
    Nothing,

    /// <summary>No such ask on this machine, or no such quest here: <c>WORK_UNKNOWN</c>.</summary>
    Unknown,
}

/// <summary>How a resume came out.</summary>
public enum ResumeVerdict
{
    /// <summary>The pause is gone and every stop it made released.</summary>
    Resumed,

    /// <summary>It was not paused here: nothing changed (D48 §6, information).</summary>
    NotPaused,

    /// <summary>No such ask on this machine, or no such quest here, and no pause of it: <c>WORK_UNKNOWN</c>.</summary>
    Unknown,
}

/// <summary>A session a pause stopped, or a stop a resume released, and its quest.</summary>
public sealed record WorkStop(string Session, string? Quest);

/// <summary>
/// A live session a pause did not stop, and why: <c>parked</c>, <c>intake</c> and <c>teammate</c> by design; <c>elsewhere</c>
/// (the Daoris process that runs it took no request in time), <c>not-running</c> (queued, and nothing here runs it yet) and
/// <c>unanswered</c> (the service did not move its record), which the person can try again.
/// </summary>
public sealed record WorkKept(string Session, string? Quest, string Why)
{
    /// <summary>For a teammate's session, the machine it runs on.</summary>
    public string? Machine { get; init; }

    /// <summary>Whether the pause meant to stop it and could not: the ones a person may try again.</summary>
    public bool Unreached => Why is PauseKept.Elsewhere or PauseKept.NotRunning or PauseKept.Unanswered;
}

/// <summary>What a pause did.</summary>
public sealed record PauseOutcome(WorkScope Scope, string Id, PauseVerdict Verdict)
{
    /// <summary>It was paused already: its time and its earlier stops stand, and what started since was stopped.</summary>
    public bool Already { get; init; }

    public IReadOnlyList<WorkStop> Stopped { get; init; } = [];

    public IReadOnlyList<WorkKept> Kept { get; init; } = [];
}

/// <summary>
/// A quest of the work something still holds after a resume (design §2.4): the planner's verdict and sentence, and the facts
/// the page says that sentence from in the reader's language (CARRY2d), as the tick's consideration carries them. The sentence
/// stays the terminal's and an older page's; each fact is null for every verdict but its own.
/// </summary>
public sealed record WorkHold(string Quest, StartVerdict Verdict, string Reason)
{
    /// <summary>For the person's stop (<see cref="StartVerdict.Stopped"/>): the session they stopped, as the tick's <c>heldBy</c>.</summary>
    public string? HeldBy { get; init; }

    /// <summary>For another pause (<see cref="StartVerdict.Paused"/>): whose it is, an ask's or the quest's own.</summary>
    public PausedBy? PausedBy { get; init; }

    /// <summary>
    /// For a park on its failed sessions (<see cref="StartVerdict.Exhausted"/>): how many failed, the count its sentence says,
    /// which is the records' failures less the person's mark (<see cref="DriverConfig.ForgivenAt"/>).
    /// </summary>
    public int? Strikes { get; init; }

    /// <summary>For a take that is not this machine's (<see cref="StartVerdict.TakenElsewhere"/>): whose it is (CARRY2c).</summary>
    public TakenBy? TakenBy { get; init; }

    /// <summary>For a set-up step the planner sits (<see cref="StartVerdict.CannotShow"/>): the code and facts of its sentence (CANNOTSHOW1a).</summary>
    public CannotShowFact? CannotShow { get; init; }

    /// <summary>For a hold the page had no words for (<see cref="StartVerdict.NotAdopted"/>): which sentence, by code (OVERVIEWNOISE1c).</summary>
    public SittingFact? Said { get; init; }
}

/// <summary>What a resume did.</summary>
public sealed record ResumeOutcome(WorkScope Scope, string Id, ResumeVerdict Verdict)
{
    /// <summary>Each stop the pause made, now released (SESSUX1b's <c>released</c>).</summary>
    public IReadOnlyList<WorkStop> Released { get; init; } = [];

    /// <summary>What still holds each quest of the work, named and never released; null where the plan could not be read.</summary>
    public IReadOnlyList<WorkHold>? Holds { get; init; }
}
