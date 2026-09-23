namespace Daoris.Knowledge;

/// <summary>What happened to a quest — one per verb that moved it (D68 §2).</summary>
public enum QuestOperationKind
{
    /// <summary>Asked. Carries the quest's words and everything it carries; every history starts here.</summary>
    Published,

    /// <summary>A repository's agent accepted it.</summary>
    Taken,

    /// <summary>Finished, with an optional note.</summary>
    Done,

    /// <summary>Turned down, with the reason.</summary>
    Declined,

    /// <summary>
    /// A move another machine's reached the remote before (D68 §5): what was attempted, kept rather
    /// than dropped, and shown on the quest until a person acts. It moves no status.
    /// </summary>
    Conflict,
}

/// <summary>One operation in a quest's history.</summary>
/// <param name="Quest">The quest it happened to.</param>
/// <param name="Kind">What happened.</param>
/// <param name="Machine">
/// The machine that made it — a stable id kept in the store, never the key, which rotates (design §2).
/// </param>
/// <param name="Sequence">
/// That machine's count of its own operations, across every quest: machine and sequence name one
/// operation anywhere, which is what a remote keys what it accepts by.
/// </param>
/// <param name="At">When it happened, on the machine that made it.</param>
/// <param name="Note">The note on a close, or the reason for a decline. Null for a publish or a take.</param>
/// <param name="Published">The quest as asked, for a <see cref="QuestOperationKind.Published"/> — open, filed and updated at <paramref name="At"/>.</param>
/// <param name="Attempted">For a <see cref="QuestOperationKind.Conflict"/>: the move that lost.</param>
/// <param name="Number">
/// Where the remote placed it in its order (design §8) — null while it is pending, and always on a
/// machine with no remote.
/// </param>
public sealed record QuestOperation(
    string Quest,
    QuestOperationKind Kind,
    string Machine,
    long Sequence,
    DateTimeOffset At,
    string? Note = null,
    Quest? Published = null,
    QuestStatus? Attempted = null,
    long? Number = null);

/// <summary>Where this machine's claim on a quest stands (D68 §4, D69).</summary>
public enum QuestClaim
{
    /// <summary>This machine never took it.</summary>
    None,

    /// <summary>A remote numbered this machine's take: the quest is this machine's, everywhere.</summary>
    Held,

    /// <summary>The take is only here — no remote answered yet, or there is none.</summary>
    Unconfirmed,

    /// <summary>Another machine's take reached the remote first; this one is a conflict on the quest.</summary>
    Lost,
}

/// <summary>A number a remote gave one operation, named by the machine and sequence that made it.</summary>
public sealed record QuestAcceptance(string Machine, long Sequence, long Number);

/// <summary>A quest a remote would not take, and why, in its own words.</summary>
public sealed record QuestPushRefusal(string Quest, string Reason);

/// <param name="Accepted">Every operation now numbered — including any the remote already held.</param>
/// <param name="Behind">Quests something else reached first: fetch, rebase, and push again.</param>
/// <param name="Refused">Quests whose operations do not apply, or whose publish the exchange refused.</param>
public sealed record QuestPush(
    IReadOnlyList<QuestAcceptance> Accepted, IReadOnlyList<string> Behind, IReadOnlyList<QuestPushRefusal> Refused);

/// <param name="Operations">A page of what a remote accepted, in its order, each carrying its number.</param>
/// <param name="Through">The last number the page covers — where the fetching machine's cursor moves.</param>
/// <param name="More">Whether another page follows.</param>
public sealed record QuestFetch(IReadOnlyList<QuestOperation> Operations, long Through, bool More);

/// <param name="Cursor">The number this machine has now fetched through.</param>
/// <param name="Conflicts">The moves the rebase turned into conflicts, as they now stand in the log.</param>
public sealed record QuestIntegration(long Cursor, IReadOnlyList<QuestOperation> Conflicts);

/// <summary>A move that lost to another machine's (D68 §5) — kept on the quest, for a person.</summary>
/// <param name="Machine">The machine whose move it was.</param>
/// <param name="Attempted">What it tried to move the quest to.</param>
/// <param name="Note">Its note or reason, as it was given.</param>
/// <param name="At">When it was made.</param>
public sealed record QuestConflict(string Machine, QuestStatus Attempted, string? Note, DateTimeOffset At);

/// <summary>
/// The one transition table (D47 §5, kept by D68): judged by the store before an operation is written,
/// and again by the replay that turns a history into a status — so the lock and the record cannot
/// disagree, on this machine or on any other that replays the same history.
/// </summary>
public static class QuestTransitions
{
    /// <summary>
    /// Taken only from open (the atomic take); closed only from live; nothing leaves a close; nothing
    /// moves back to open — one title is one quest forever (D46 §3).
    /// </summary>
    public static bool Allows(QuestStatus from, QuestStatus to) => to switch
    {
        QuestStatus.Taken => from == QuestStatus.Open,
        QuestStatus.Done or QuestStatus.Declined => from is QuestStatus.Open or QuestStatus.Taken,
        _ => false,
    };

    /// <summary>The status an operation moves a quest to. A publish begins one and a conflict moves nothing.</summary>
    public static QuestStatus? Target(QuestOperationKind kind) => kind switch
    {
        QuestOperationKind.Taken => QuestStatus.Taken,
        QuestOperationKind.Done => QuestStatus.Done,
        QuestOperationKind.Declined => QuestStatus.Declined,
        _ => null,
    };

    /// <summary>The operation that moves a quest to <paramref name="status"/> — none reaches open.</summary>
    public static QuestOperationKind? KindFor(QuestStatus status) => status switch
    {
        QuestStatus.Taken => QuestOperationKind.Taken,
        QuestStatus.Done => QuestOperationKind.Done,
        QuestStatus.Declined => QuestOperationKind.Declined,
        _ => null,
    };
}

/// <summary>A quest is its operations in order; its current state is what replaying them gives (D68 §1).</summary>
public static class QuestLog
{
    /// <summary>
    /// Fold a history, in order, through the transition table. An operation that does not apply is
    /// not a move and changes nothing — so no order of operations, from whichever machines, can reach
    /// a state the table forbids. The first publish is the quest; a later one is the same ask again.
    /// </summary>
    /// <returns>The quest as it stands, or null when nothing in the history published it.</returns>
    public static Quest? Replay(IEnumerable<QuestOperation> history) =>
        history.Aggregate((Quest?)null, (quest, operation) => Applies(quest, operation) ? Step(quest, operation) : quest);

    /// <summary>
    /// Whether an operation moves a quest standing at <paramref name="quest"/>: a publish only begins
    /// one, a move goes only where the table allows, and a conflict is recorded on any quest there is.
    /// </summary>
    public static bool Applies(Quest? quest, QuestOperation operation) => operation.Kind switch
    {
        QuestOperationKind.Published => quest is null,
        QuestOperationKind.Conflict => quest is not null,
        _ => quest is not null && QuestTransitions.Target(operation.Kind) is { } target
             && QuestTransitions.Allows(quest.Status, target),
    };

    /// <summary>The quest after an operation that <see cref="Applies"/>.</summary>
    public static Quest Step(Quest? quest, QuestOperation operation) => operation.Kind switch
    {
        QuestOperationKind.Published => operation.Published! with
        {
            Status = QuestStatus.Open, Note = null, Filed = operation.At, Updated = operation.At, Conflicts = [],
        },
        QuestOperationKind.Conflict => quest! with
        {
            Conflicts =
            [
                .. quest.Conflicts,
                new QuestConflict(operation.Machine, operation.Attempted ?? QuestStatus.Open, operation.Note, operation.At),
            ],
        },
        _ => quest! with { Status = QuestTransitions.Target(operation.Kind)!.Value, Note = operation.Note, Updated = operation.At },
    };
}
