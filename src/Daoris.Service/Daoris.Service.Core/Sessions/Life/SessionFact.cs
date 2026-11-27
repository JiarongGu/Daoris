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

    /// <summary>Its turn ended and it waits on its workflow's step, which the fact's holder names where one is known.</summary>
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
/// One witnessed fact of a session's journal: its kind, who witnessed it (the door and the caller's named reason), the application's
/// generation it is written in, the step and its attempt where one applies, and when. Appended only, in <see cref="Position"/> order.
/// </summary>
/// <param name="Session">The session it is a fact of.</param>
/// <param name="Kind">What happened.</param>
/// <param name="Witness">Who saw it: the door that wrote it and, where the caller named one, its reason (a note's code).</param>
/// <param name="Generation">The application's generation it is written in (<see cref="AppGenerations"/>); 0 before any start began one.</param>
/// <param name="At">When.</param>
public sealed record SessionFact(string Session, SessionFactKind Kind, string Witness, long Generation, DateTimeOffset At)
{
    /// <summary>Its place in the journal, which only grows; 0 for a fact not yet appended.</summary>
    public long Position { get; init; }

    /// <summary>What it waits on, for a fact that waits: <c>start</c>, <c>run:&lt;id&gt;</c>, <c>check</c>; null where nothing is named.</summary>
    public string? Holder { get; init; }

    /// <summary>The step it waits on or ran, where one is named.</summary>
    public string? Step { get; init; }

    /// <summary>That step's attempt, where one is named.</summary>
    public int? Attempt { get; init; }

    /// <summary>How it ended, for <see cref="SessionFactKind.Ended"/> alone.</summary>
    public SessionOutcome? Outcome { get; init; }

    /// <summary>The holder a start waits on: the machine room, which alone starts steps.</summary>
    public const string StartHolder = "start";

    /// <summary>The holder a wait on the check its agent asked names.</summary>
    public const string CheckHolder = "check";

    /// <summary>
    /// The fact a move of the stored status witnesses: the record as moved, by <paramref name="witness"/>, in <paramref name="generation"/>.
    /// A wait on its run names the run, the step and the attempt its note's <c>waits.step</c> part gives; one on its own check names
    /// <see cref="CheckHolder"/>; a wait whose note names neither names no holder, which the fold reads as a breach. The caller's reason
    /// is its note's code only where <paramref name="noted"/>: a move that writes no note keeps the record's, an earlier move's reason.
    /// </summary>
    public static SessionFact OfMove(Session moved, string witness, long generation, bool noted = true) =>
        OfStatus(moved.Id, moved.State, moved.Updated, moved.NoteParts, noted ? Witnessed(witness, moved.NoteParts) : witness, generation);

    /// <summary>
    /// The fact a stored status says, as written at <paramref name="at"/> by <paramref name="witness"/>: what <see cref="OfMove"/>
    /// appends, and what the baseline (<see cref="SessionBaseline"/>) folds for a record older than its journal.
    /// </summary>
    public static SessionFact OfStatus(
        string session, SessionState state, DateTimeOffset at, string? noteParts, string witness, long generation) => state switch
    {
        SessionState.Queued => new(session, SessionFactKind.Opened, witness, generation, at) { Holder = StartHolder },
        SessionState.Starting => new(session, SessionFactKind.Starting, witness, generation, at) { Holder = StartHolder },
        SessionState.Working => new(session, SessionFactKind.TurnBegan, witness, generation, at),
        SessionState.AwaitingPerson => new(session, SessionFactKind.QuestionOpened, witness, generation, at),
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

    /// <summary>The door's name, then the caller's reason where its note's parts name a code: what a reader of the journal is told.</summary>
    private static string Witnessed(string witness, string? parts) =>
        NoteParts.Codes(parts) is [var code, ..] ? $"{witness}: {code}" : witness;

    private static SessionFact StepWait(string session, string witness, long generation, DateTimeOffset at, string? parts)
    {
        if (NoteParts.Values(parts, "waits.step") is { } step && step.TryGetValue("run", out var run) && run.Length > 0)
        {
            return new(session, SessionFactKind.StepAwaited, witness, generation, at)
            {
                Holder = $"run:{run}",
                Step = step.TryGetValue("node", out var node) && node.Length > 0 ? node : null,
                Attempt = step.TryGetValue("attempt", out var attempt) && int.TryParse(attempt, out var n) ? n : null,
            };
        }

        return NoteParts.Carries(parts, "waits.check")
            ? new(session, SessionFactKind.StepAwaited, witness, generation, at) { Holder = CheckHolder }
            : new(session, SessionFactKind.StepAwaited, witness, generation, at);
    }
}
