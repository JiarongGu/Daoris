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
public sealed record QuestOperation(
    string Quest,
    QuestOperationKind Kind,
    string Machine,
    long Sequence,
    DateTimeOffset At,
    string? Note = null,
    Quest? Published = null);

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

    /// <summary>The status an operation moves a quest to. A publish moves nothing; it begins.</summary>
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
    /// Fold a history, in order, through the transition table. An operation the table refuses is not
    /// a move and changes nothing — so no order of operations, from whichever machines, can reach a
    /// state the table forbids. The first publish is the quest; a later one is the same ask again.
    /// </summary>
    /// <returns>The quest as it stands, or null when nothing in the history published it.</returns>
    public static Quest? Replay(IEnumerable<QuestOperation> history)
    {
        Quest? quest = null;
        foreach (var operation in history)
        {
            if (operation.Kind == QuestOperationKind.Published)
            {
                quest ??= operation.Published! with
                {
                    Status = QuestStatus.Open, Note = null, Filed = operation.At, Updated = operation.At, Home = null,
                };
                continue;
            }

            if (quest is null || QuestTransitions.Target(operation.Kind) is not { } target
                || !QuestTransitions.Allows(quest.Status, target))
            {
                continue;
            }

            quest = quest with { Status = target, Note = operation.Note, Updated = operation.At };
        }

        return quest;
    }
}
