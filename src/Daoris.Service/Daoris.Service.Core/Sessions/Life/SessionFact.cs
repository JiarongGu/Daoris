namespace Daoris.Knowledge;

/// <summary>
/// What a session's journal can say happened to it: a closed vocabulary. Each kind is witnessed by the door that wrote it, and the fold
/// (<see cref="SessionFold"/>) reads nothing else.
/// </summary>
public enum SessionFactKind
{
    /// <summary>The record is made, and its start waits on the machine room.</summary>
    Opened,

    /// <summary>A start is under way: its process is being opened.</summary>
    Starting,

    /// <summary>A turn began: an agent's process works on it, alive only while its lease is.</summary>
    TurnBegan,

    /// <summary>A question for a person stands open: it waits on their answer.</summary>
    QuestionOpened,

    /// <summary>Its turn ended and it waits on what the fact's holder names: its run's step, its own check.</summary>
    StepAwaited,

    /// <summary>It ended, with its <see cref="SessionOutcome"/>.</summary>
    Ended,
}

/// <summary>How an ended session ended: failed is one of them, which a resume, a word or a hand-over reopens.</summary>
public enum SessionOutcome
{
    Done,
    Declined,
    StoodDown,
    Failed,
    Stopped,
}

/// <summary>
/// What holds a session that waits, a closed set: the machine room's start, a check slot, memory, an account, a reviewer, a
/// landing, a step of its run, a question for a person or for the steward; <see cref="None"/> where nothing is named, which the fold reads
/// as a breach. A live lease is no holder: it is the working state itself.
/// </summary>
public enum HolderKind
{
    None,
    MachineRoom,
    CheckSlot,
    Memory,
    Account,
    Reviewer,
    Landing,
    Step,
    Person,
    Steward,
}

/// <summary>What holds a session: its kind and, where one is named, its subject (a run's id, a check's, an account's).</summary>
public sealed record SessionHolder(HolderKind Kind, string? Subject = null)
{
    /// <summary>Nothing named.</summary>
    public static readonly SessionHolder None = new(HolderKind.None);

    /// <summary>The machine room, which alone starts a session's step.</summary>
    public static readonly SessionHolder MachineRoom = new(HolderKind.MachineRoom);

    /// <summary>A person's answer to an open question.</summary>
    public static readonly SessionHolder Person = new(HolderKind.Person);
}

/// <summary>
/// The doors that write a session's facts, a closed set: each open and each move names its own, so a fact's witness never depends on a
/// caller's member name. <see cref="Store"/> is the store's own word, which no production door leaves to it (the census holds it).
/// </summary>
public enum WitnessDoor
{
    Store,
    QuestOpen,
    ChatOpen,
    IntakeOpen,
    HelpOpen,
    HelperOpen,
    PartnerOpen,
    OpinionOpen,
    StepOpen,
    SuperviseOpen,
    Advance,
    GoOn,
    GoOnRestore,
    HelperPark,
    AskClose,
    RunEnd,
}

/// <summary>Who witnessed a fact: the door that wrote it, and the reason code its move's note names (a closed note code), where one does.</summary>
public sealed record FactWitness(WitnessDoor Door, string? Reason = null);

/// <summary>
/// One witnessed fact of a session's journal: its kind, its witness, the application's generation its writer writes in, the step and its
/// attempt where one applies, and when. Appended only, in <see cref="Position"/> order.
/// </summary>
/// <param name="Session">The session it is a fact of.</param>
/// <param name="Kind">What happened.</param>
/// <param name="Witness">Who saw it: the door that wrote it and its reason code.</param>
/// <param name="Generation">
/// The application's generation its writer writes in (<see cref="FactWriter"/>), named by the writer and never stamped by the store; 0 for
/// a write no start of the application made (a person's door, the host's own work).
/// </param>
/// <param name="At">When.</param>
public sealed record SessionFact(string Session, SessionFactKind Kind, FactWitness Witness, long Generation, DateTimeOffset At)
{
    /// <summary>Its place in the journal, which only grows; 0 for a fact not yet appended.</summary>
    public long Position { get; init; }

    /// <summary>What it waits on; <see cref="SessionHolder.None"/> where nothing is named.</summary>
    public SessionHolder Holder { get; init; } = SessionHolder.None;

    /// <summary>The step it waits on or opened for, where one is named.</summary>
    public string? Step { get; init; }

    /// <summary>That step's attempt, where one is named.</summary>
    public int? Attempt { get; init; }

    /// <summary>How it ended, for <see cref="SessionFactKind.Ended"/> alone.</summary>
    public SessionOutcome? Outcome { get; init; }

    /// <summary>
    /// The trigger-written row of <c>event_journal</c> the write that appended it minted (its <c>opened</c> or <c>state</c> row), which
    /// the census matches every such row to; null for a move that kept its state, which mints none.
    /// </summary>
    public long? Minted { get; init; }

    /// <summary>
    /// The fact a move of the stored status witnesses: the record as moved, by <paramref name="door"/>, in <paramref name="generation"/>.
    /// Its reason is its note's first code where the move wrote its note (<paramref name="noted"/>): a move that writes none keeps an
    /// earlier move's note, and so names no reason of its own.
    /// </summary>
    public static SessionFact OfMove(Session moved, WitnessDoor door, long generation, bool noted = true) =>
        OfStatus(moved.Id, moved.State, moved.Updated, moved.NoteParts,
            new FactWitness(door, noted && NoteParts.Codes(moved.NoteParts) is [var code, ..] ? code : null), generation);

    /// <summary>
    /// The fact a stored status says, as written at <paramref name="at"/>: what <see cref="OfMove"/> appends, and what the baseline
    /// (<see cref="SessionBaseline"/>) folds for a record older than its journal. A wait on its run names the run as a step's holder, with
    /// the step and attempt its note's <c>waits.step</c> part gives; one on its own check names a check slot; a wait whose note names
    /// neither names no holder, which the fold reads as a breach.
    /// </summary>
    public static SessionFact OfStatus(
        string session, SessionState state, DateTimeOffset at, string? noteParts, FactWitness witness, long generation) => state switch
    {
        SessionState.Queued => new(session, SessionFactKind.Opened, witness, generation, at) { Holder = SessionHolder.MachineRoom },
        SessionState.Starting => new(session, SessionFactKind.Starting, witness, generation, at) { Holder = SessionHolder.MachineRoom },
        SessionState.Working => new(session, SessionFactKind.TurnBegan, witness, generation, at),
        SessionState.AwaitingPerson => new(session, SessionFactKind.QuestionOpened, witness, generation, at) { Holder = SessionHolder.Person },
        SessionState.AwaitingStep => StepWait(session, witness, generation, at, noteParts),
        _ => new(session, SessionFactKind.Ended, witness, generation, at) { Outcome = OutcomeOf(state) },
    };

    /// <summary>The outcome an ended stored state names; null for a live one.</summary>
    public static SessionOutcome? OutcomeOf(SessionState state) => state switch
    {
        SessionState.Completed => SessionOutcome.Done,
        SessionState.Declined => SessionOutcome.Declined,
        SessionState.StoodDown => SessionOutcome.StoodDown,
        SessionState.Failed => SessionOutcome.Failed,
        SessionState.Stopped => SessionOutcome.Stopped,
        _ => null,
    };

    private static SessionFact StepWait(string session, FactWitness witness, long generation, DateTimeOffset at, string? parts)
    {
        if (NoteParts.Values(parts, "waits.step") is { } step && step.TryGetValue("run", out var run) && run.Length > 0)
        {
            return new(session, SessionFactKind.StepAwaited, witness, generation, at)
            {
                Holder = new SessionHolder(HolderKind.Step, run),
                Step = step.TryGetValue("node", out var node) && node.Length > 0 ? node : null,
                Attempt = step.TryGetValue("attempt", out var attempt) && int.TryParse(attempt, out var n) ? n : null,
            };
        }

        return NoteParts.Carries(parts, "waits.check")
            ? new(session, SessionFactKind.StepAwaited, witness, generation, at) { Holder = new SessionHolder(HolderKind.CheckSlot) }
            : new(session, SessionFactKind.StepAwaited, witness, generation, at);
    }
}
