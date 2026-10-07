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

    /// <summary>
    /// A person dismissed one conflict (SYNC6c): it stops being shown, on every machine, because the
    /// dismissal travels like any other operation. It names the conflict and moves no status.
    /// </summary>
    Dismissed,

    /// <summary>
    /// Its taker asked another repository and waits on the answer (D79). Its note names the quest it
    /// waits on. It moves no status: the quest stays taken, because its work in progress is in the
    /// taker's tree, and nothing moves back to open.
    /// </summary>
    Waited,

    /// <summary>
    /// A person deleted a quest nobody had started on (D95). It applies only to an open quest and
    /// replays to no quest, so the quest is gone from every machine the operation reaches, and the
    /// remote keeping it is what stops a later fetch bringing the quest back. It carries nothing else.
    /// </summary>
    Deleted,

    /// <summary>
    /// The person accepted a done's departure from what they required (DRIFT1d, D133 §4): what it held, a chain's
    /// next step or a quest waiting on it, goes on. It applies only to a quest its departure holds, so two yeses are
    /// one, and it moves no status.
    /// </summary>
    Accepted,

    /// <summary>
    /// Daoris read a done's evidence (EVID1a, D144 point 3): the commit read, and what each item it waits on was found
    /// to be. It applies only to a done that waits on its evidence and reads exactly that, so a second machine's
    /// verdict on evidence already found is no move; found, it lets go what the done held, and it moves no status.
    /// </summary>
    Evidenced,
}

/// <summary>
/// One operation, named where it was made: the machine and its sequence. What a dismissal names the
/// conflict by — the losing move keeps its machine and sequence when it becomes a conflict, so the name
/// is the same on every machine that holds it.
/// </summary>
public sealed record QuestOperationRef(string Machine, long Sequence);

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
/// <param name="Dismisses">For a <see cref="QuestOperationKind.Dismissed"/>: the conflict it dismisses.</param>
/// <param name="Answers">
/// For a <see cref="QuestOperationKind.Done"/>: how it answered each of the quest's requirements (DRIFT1d). Null
/// where it carries none, which is every operation of a build before answers.
/// </param>
/// <param name="WhileOpen">
/// For a <see cref="QuestOperationKind.Declined"/>: it applies only to a quest still open where it lands (PAUSE1c,
/// D132 point 10), so pushed after another machine's take it becomes a conflict rather than declining their work.
/// False on every other kind, and on every decline made before it, which is a plain one.
/// </param>
/// <param name="Evidence">For a <see cref="QuestOperationKind.Evidenced"/>: what was read (EVID1a). Null on every other kind.</param>
public sealed record QuestOperation(
    string Quest,
    QuestOperationKind Kind,
    string Machine,
    long Sequence,
    DateTimeOffset At,
    string? Note = null,
    Quest? Published = null,
    QuestStatus? Attempted = null,
    long? Number = null,
    QuestOperationRef? Dismisses = null,
    IReadOnlyList<QuestAnswer>? Answers = null,
    bool WhileOpen = false,
    QuestEvidenceVerdict? Evidence = null);

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

/// <summary>What a rebase does with a pending operation that no longer applies after a fetch (D68 rule 2, sync design §8).</summary>
public enum QuestLoss
{
    /// <summary>Dropped from the log: it was never anybody's decision, or it has nothing left to say.</summary>
    Forgotten,

    /// <summary>Rewritten as a conflict on the quest, kept for a person, naming the status it attempted.</summary>
    Conflict,

    /// <summary>Left as it is: a conflict already, on a quest another machine deleted (D95).</summary>
    Kept,
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
/// <param name="Sequence">
/// The losing move's sequence on <paramref name="Machine"/> — with it, the conflict's name everywhere,
/// which is what a dismissal names (SYNC6c). Zero only in a cache written before dismissals, which the
/// store refills from the log as it opens.
/// </param>
public sealed record QuestConflict(string Machine, QuestStatus Attempted, string? Note, DateTimeOffset At, long Sequence = 0);

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
    /// a state the table forbids. The first publish is the quest; a later one is the same ask again —
    /// unless a delete came between, after which a publish begins the quest anew (D95).
    /// </summary>
    /// <returns>The quest as it stands, or null when nothing in the history published it, or a delete ended it.</returns>
    public static Quest? Replay(IEnumerable<QuestOperation> history) =>
        history.Aggregate((Quest?)null, (quest, operation) => Applies(quest, operation) ? Step(quest, operation) : quest);

    /// <summary>
    /// Whether an operation moves a quest standing at <paramref name="quest"/>: a publish only begins
    /// one, a move goes only where the table allows, and a conflict is recorded on any quest there is.
    /// A dismissal applies to any quest there is, whether or not its conflict is still there: two
    /// people dismissing one conflict is one dismissal, never a refusal or a new conflict. A delete
    /// applies only to an open quest: taken, somebody's work stands on it (D95). So does a decline made
    /// while open (PAUSE1c): it was judged on an open quest, so after a take the take stands, and the
    /// rebase keeps the decline as a conflict (D68 rule 2).
    /// </summary>
    public static bool Applies(Quest? quest, QuestOperation operation) => operation.Kind switch
    {
        QuestOperationKind.Declined when operation.WhileOpen => quest is { Status: QuestStatus.Open },
        QuestOperationKind.Published => quest is null,
        QuestOperationKind.Conflict or QuestOperationKind.Dismissed => quest is not null,
        // Only a taken quest waits: an open one has nobody's work in it, and a closed one has none left.
        QuestOperationKind.Waited => quest is { Status: QuestStatus.Taken } && !string.IsNullOrEmpty(operation.Note),
        QuestOperationKind.Deleted => quest is { Status: QuestStatus.Open },
        // A yes only while something holds the done (DRIFT1d, EVID1a): a second machine's yes is the same yes, never a move.
        QuestOperationKind.Accepted => quest is { Held: true },
        // A verdict only while the done waits on its evidence, and only one that reads exactly that (EVID1a): a second
        // machine's verdict on evidence already found is no move, and a verdict on what the done never named is none.
        QuestOperationKind.Evidenced => quest is { AwaitsEvidence: true } && operation.Evidence?.Covers(quest) == true,
        _ => quest is not null && QuestTransitions.Target(operation.Kind) is { } target
             && QuestTransitions.Allows(quest.Status, target),
    };

    /// <summary>
    /// Where <paramref name="machine"/>'s claim on a quest stands, read from the quest's operations (D68 §4, D69): held once
    /// a remote numbered its take, unconfirmed while the take is only here, lost once a rebase made it a conflict, and none
    /// when it never took the quest. <see cref="QuestStore.ClaimAsync"/> answers from it, and the store's verbs and a remote
    /// judge by it (<see cref="OnALostTake"/>; WAITCLAIM2, WAITCLAIM3), so the driver, the store and the remote read one claim.
    /// </summary>
    public static QuestClaim Claim(IEnumerable<QuestOperation> history, string machine)
    {
        var claim = QuestClaim.None;
        foreach (var operation in history.Where(operation => operation.Machine == machine))
        {
            if (operation.Kind == QuestOperationKind.Taken) return operation.Number is null ? QuestClaim.Unconfirmed : QuestClaim.Held;
            if (operation is { Kind: QuestOperationKind.Conflict, Attempted: QuestStatus.Taken }) claim = QuestClaim.Lost;
        }

        return claim;
    }

    /// <summary>
    /// Whether <paramref name="operation"/>, judged on <paramref name="standing"/>, is made on a take its machine lost: a
    /// done, a decline or a wait on a taken quest, where that machine's <see cref="Claim"/> in <paramref name="history"/>
    /// reads lost. The take that beat it stands, so the operation is made on a claim its machine never held.
    /// </summary>
    /// <remarks>
    /// One rule for both places that judge it. The store's verbs refuse such an operation inside their write
    /// (WAITCLAIM2): the pass that finds a take lost leaves the session running until its driver's next look stops it,
    /// and the session does not know. A remote refuses it as a push arrives (WAITCLAIM3), because a machine on a build
    /// that does not refuse it locally still pushes it, and applied it would move the winner's quest. A dismissal, a yes
    /// and a verdict are no move on the take. An open quest is nobody's work, whatever an earlier incarnation of it held
    /// (D95). And a machine whose take was numbered reads held, never lost, so a winner's own moves are never judged here.
    /// </remarks>
    public static bool OnALostTake(Quest? standing, IEnumerable<QuestOperation> history, QuestOperation operation) =>
        standing is { Status: QuestStatus.Taken }
        && (QuestTransitions.Target(operation.Kind) is not null || operation.Kind == QuestOperationKind.Waited)
        && Claim(history, operation.Machine) == QuestClaim.Lost;

    /// <summary>The quest after an operation that <see cref="Applies"/> — none after a delete.</summary>
    public static Quest? Step(Quest? quest, QuestOperation operation) => operation.Kind switch
    {
        QuestOperationKind.Deleted => null,
        QuestOperationKind.Published => operation.Published! with
        {
            Status = QuestStatus.Open, Note = null, Filed = operation.At, Updated = operation.At, Conflicts = [],
        },
        QuestOperationKind.Conflict => quest! with
        {
            Conflicts =
            [
                .. quest.Conflicts,
                new QuestConflict(
                    operation.Machine, operation.Attempted ?? QuestStatus.Open, operation.Note, operation.At, operation.Sequence),
            ],
        },
        QuestOperationKind.Dismissed => quest! with
        {
            Conflicts = quest.Conflicts
                .Where(conflict => operation.Dismisses is not { } named
                    || conflict.Machine != named.Machine || conflict.Sequence != named.Sequence)
                .ToList(),
        },
        QuestOperationKind.Waited => quest! with { Awaits = operation.Note, Updated = operation.At },
        QuestOperationKind.Accepted => quest! with { Accepted = operation.At, Updated = operation.At },
        // The verdict as the quest keeps it: when and by which machine are the operation's own (EVID1a).
        QuestOperationKind.Evidenced => quest! with
        {
            Evidence = operation.Evidence! with { At = operation.At, Machine = operation.Machine }, Updated = operation.At,
        },
        // A done carries how it answered each requirement (DRIFT1d); a take or a decline answers none.
        _ => quest! with
        {
            Status = QuestTransitions.Target(operation.Kind)!.Value, Note = operation.Note, Updated = operation.At,
            Answers = operation.Answers ?? [],
        },
    };

    /// <summary>
    /// What a rebase does with a pending operation of <paramref name="kind"/> that no longer applies (D68 rule 2, sync
    /// design §8). Every kind has its rule here, and only a move becomes a conflict: a conflict names the status it
    /// attempted, and the wire refuses one that names none (<see cref="QuestWire.Shape"/>), so a conflict made of
    /// anything else would stop every later push of its circle (QUESTOP1). A kind with no rule throws rather than make one.
    /// </summary>
    public static QuestLoss Lost(QuestOperationKind kind) => kind switch
    {
        // The same ask, published first elsewhere: the first publish is the quest, and a second copy was nobody's decision.
        QuestOperationKind.Published => QuestLoss.Forgotten,
        // A move that lost to another machine's, or made on a take that lost (D69): kept for a person, never dropped.
        QuestOperationKind.Taken or QuestOperationKind.Done or QuestOperationKind.Declined => QuestLoss.Conflict,
        // It applies to any quest there is, so it is lost only with its quest (D95), and kept so that machine's claim
        // still reads lost.
        QuestOperationKind.Conflict => QuestLoss.Kept,
        // Nothing left to say. A dismissal is lost only with its quest. A wait, once its quest is no longer taken: another
        // machine closed or deleted it first (QUESTOP1); or made on this machine's take that lost, which the take's
        // conflict names (WAITCLAIM1, QuestStore.RebaseAsync). A delete that lost to a take, or that another machine's delete
        // already made (D95). A yes to a done nothing holds any more (DRIFT1d): its done lost, or another machine's yes came
        // first. A verdict on evidence nothing waits on any more (EVID1a): another machine's verdict found it first, or the
        // person accepted the done as it stood.
        QuestOperationKind.Dismissed or QuestOperationKind.Waited or QuestOperationKind.Deleted
            or QuestOperationKind.Accepted or QuestOperationKind.Evidenced => QuestLoss.Forgotten,
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind), kind,
            "A rebase has no rule for this kind of quest operation; .claude/knowledge/quest-operations.md names every place a kind goes."),
    };
}
