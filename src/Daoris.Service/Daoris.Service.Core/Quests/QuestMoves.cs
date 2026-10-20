namespace Daoris.Knowledge;

/// <summary>
/// One operation of a quest's history read as a move (FLOWCHART1a2; D186, the flowchart design §2.1): what the quest's page draws
/// its path from and a trace says. Every field is by allow-list, so a shared host answers it as a local one does (D47 §4): never
/// the published payload, a note's body, the answers or a path. A field a move does not keep is null, and a reader says it is not
/// recorded.
/// </summary>
/// <param name="Kind">What happened.</param>
/// <param name="At">When, on the machine that made it: each machine's own clock, so never an order.</param>
/// <param name="Machine">The machine that made it.</param>
/// <param name="Sequence">Its sequence on that machine: with the machine, its name everywhere, which a dismissal names its conflict by.</param>
/// <param name="Number">Where the remote placed it in its order; null while it is pending, and always on a machine with no remote.</param>
/// <param name="Applied">
/// Whether the replay applied it (<see cref="QuestLog.Applies"/>), as every machine's replay decides: one that does not apply moves
/// nothing, and is recorded, not applied.
/// </param>
/// <param name="From">The status the quest stood at as the replay reached it; null where no quest stood: before its publish, or after a delete.</param>
public sealed record QuestMoveReading(
    QuestOperationKind Kind, DateTimeOffset At, string Machine, long Sequence, long? Number, bool Applied, QuestStatus? From)
{
    /// <summary>A conflict's: the status the move that lost attempted (D68 §5).</summary>
    public QuestStatus? Attempted { get; init; }

    /// <summary>A dismissal's: the conflict it settles, by the machine and sequence the conflict keeps.</summary>
    public QuestOperationRef? Dismisses { get; init; }

    /// <summary>A wait's: the quest it waits on (D79), which its note is; null where the note does not read as a quest's id.</summary>
    public string? WaitsOn { get; init; }

    /// <summary>A submission's: the session that said its <c>ready</c>; null where none did. A take keeps no session.</summary>
    public string? Session { get; init; }

    /// <summary>An evidence read's: <see cref="QuestMoves.Found"/>, <see cref="QuestMoves.Missing"/> or <see cref="QuestMoves.CommitOnly"/>.</summary>
    public string? Evidence { get; init; }

    /// <summary>A set-up's: <see cref="QuestMoves.Local"/> or <see cref="QuestMoves.Deployed"/>, where it was shown.</summary>
    public string? SetUp { get; init; }

    /// <summary>A verdict's: what the person said, <c>reviewed</c>, <c>not-yet</c> or <c>skipped</c> (<see cref="Reviews.Verdicts"/>).</summary>
    public string? Verdict { get; init; }

    /// <summary>
    /// The hold an applied move left the quest under, where it changed it: a hold's code (<see cref="QuestEvidenceCodes.Spell"/>), or
    /// <see cref="QuestMoves.NoHold"/> where it let one go. Null where the move left the hold as it was, or did not apply.
    /// </summary>
    public string? Hold { get; init; }
}

/// <summary>
/// A quest's history read as its moves (FLOWCHART1a2; the flowchart design §2.1, §7 traps 13 and 20): what
/// <c>GET /api/quests/{id}/moves</c> answers on both hosts, and <c>TRACE</c>'s quest link carries.
/// </summary>
/// <remarks>
/// <para><b>In the order the history replays</b> (<see cref="QuestStore.HistoryAsync(string, CancellationToken)"/>): what a remote
/// accepted, by its number, then this machine's pending ones, in the order made. Never sorted by time: the times are each machine's
/// own.</para>
///
/// <para><b>Applied as the replay decides.</b> No operation keeps whether it applied: the history is folded once, as
/// <see cref="QuestLog.Replay"/> folds it, and each operation is judged by <see cref="QuestLog.Applies"/> against the quest the
/// operations before it replay to, which is the same judgement as replaying each prefix.</para>
///
/// <para><b>Each kind has its row</b> (<c>.claude/knowledge/quest-operations.md</c>, the moves read): a kind with none throws, and the
/// census (<c>QuestOperationKindsTests</c>) fails on it before a page draws nothing of it.</para>
/// </remarks>
public static class QuestMoves
{
    /// <summary>A move's hold where it let one go.</summary>
    public const string NoHold = "none";

    /// <summary>An evidence read that found every item.</summary>
    public const string Found = "found";

    /// <summary>An evidence read that did not find some item.</summary>
    public const string Missing = "missing";

    /// <summary>A submission's commit read with no evidence to find (FABRIC-CLOSE1c6).</summary>
    public const string CommitOnly = "commit";

    /// <summary>A set-up shown in a local environment: its tab is on the machine that made it.</summary>
    public const string Local = "local";

    /// <summary>A set-up shown in a deployed environment, at its page.</summary>
    public const string Deployed = "deployed";

    /// <summary>A kind as the wire and the log spell it: its name, lowercased.</summary>
    public static string Spell(QuestOperationKind kind) => kind.ToString().ToLowerInvariant();

    /// <summary>The history, in the order given, each operation as a move.</summary>
    public static IReadOnlyList<QuestMoveReading> Read(IEnumerable<QuestOperation> history)
    {
        var moves = new List<QuestMoveReading>();
        Quest? quest = null;
        foreach (var operation in history)
        {
            var applied = QuestLog.Applies(quest, operation);
            var after = applied ? QuestLog.Step(quest, operation) : quest;
            var move = Own(operation, new QuestMoveReading(
                operation.Kind, operation.At, operation.Machine, operation.Sequence, operation.Number, applied, quest?.Status));
            if (applied && quest?.Hold != after?.Hold)
            {
                move = move with { Hold = after?.Hold is { } hold ? QuestEvidenceCodes.Spell(hold) : NoHold };
            }

            moves.Add(move);
            quest = after;
        }

        return moves;
    }

    /// <summary>A kind's own fields, by allow-list: every other field it carries stays in the log.</summary>
    private static QuestMoveReading Own(QuestOperation operation, QuestMoveReading move) => operation.Kind switch
    {
        QuestOperationKind.Published or QuestOperationKind.Taken or QuestOperationKind.Done or QuestOperationKind.Declined
            or QuestOperationKind.Deleted or QuestOperationKind.Accepted or QuestOperationKind.Bound or QuestOperationKind.Reopened
            or QuestOperationKind.Rebound => move,
        QuestOperationKind.Conflict => move with { Attempted = operation.Attempted },
        QuestOperationKind.Dismissed => move with { Dismisses = operation.Dismisses },
        QuestOperationKind.Waited => move with { WaitsOn = IsQuestId(operation.Note) ? operation.Note : null },
        QuestOperationKind.Submitted => move with { Session = operation.Session },
        QuestOperationKind.Evidenced => move with
        {
            Evidence = operation.Evidence is not { } read ? null
                : read.Items.Count == 0 ? CommitOnly
                : read.Found ? Found
                : Missing,
        },
        QuestOperationKind.SetUp => move with { SetUp = operation.SetUp is not { } setUp ? null : setUp.Local ? Local : Deployed },
        QuestOperationKind.Verdict => move with { Verdict = operation.Verdict?.Said },
        _ => throw new InvalidOperationException(
            $"No move is read of `{operation.Kind}`: QuestMoves has no row for it (.claude/knowledge/quest-operations.md, the moves read)."),
    };

    /// <summary>Whether a wait's note reads as a quest's id: at most 64 letters, digits, <c>-</c> or <c>_</c>, so no sentence or path is answered.</summary>
    private static bool IsQuestId(string? note) =>
        note is { Length: > 0 and <= 64 } && note.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
}
