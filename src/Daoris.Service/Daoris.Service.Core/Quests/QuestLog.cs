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

    /// <summary>
    /// A set-up step said where it showed the work for the person's review (REVIEWENV1b, D154 point 9): what to look at,
    /// how to show it again, and the commit its tree held, which the driver read. It applies only to a set-up step, and a
    /// second post of the same set-up is no move. It moves no status; a newer one holds the step for the person again.
    /// </summary>
    SetUp,

    /// <summary>
    /// The person's verdict on a set-up step (REVIEWENV1b, D154 point 8): <c>reviewed</c> or <c>not-yet</c> on its newest
    /// set-up, or <c>skipped</c> for the work. A <c>reviewed</c> or a skip lets go what the step's review held, and nothing
    /// else; it moves no status.
    /// </summary>
    Verdict,

    /// <summary>
    /// Its taker's driver bound it to a graph that closes its quest itself (FABRIC-CLOSE1a, the fabric events design §E5):
    /// its note is the graph's digest. It applies once, to a taken quest with no submission yet, so a quest follows a closure
    /// graph only from its first start, and quests already in flight keep today's done. From then on its taker's agent says
    /// <c>ready</c>, never <c>done</c>, and the graph's close door closes it. It moves no status.
    /// </summary>
    Bound,

    /// <summary>
    /// Its taker's agent submitted its work in a closure graph (FABRIC-CLOSE1a, §E5): the <c>ready</c> a done would have been,
    /// carrying a done's note and answers. The quest stays taken: a submission is not a status. The holds a done carries hang
    /// on the latest submission, and a later one replaces it, judged again from the start. It moves no status.
    /// </summary>
    Submitted,

    /// <summary>
    /// The person reopened a sealed submission (FABRIC-CLOSE1c, §E5): it names the submission by its machine and sequence. On the
    /// quest that submitted it, it applies while that submission is current, and the quest stays taken with none, so a close naming
    /// it is stale and a later <c>ready</c> is judged anew. On the step that submission published, it applies while the step is open
    /// or taken: an open step is withdrawn, never taken while so, and a taken one is held with its reason until the person chooses.
    /// It moves no status.
    /// </summary>
    Reopened,

    /// <summary>
    /// A step withdrawn or held by its source's reopen grows from the source's submission it names now (FABRIC-CLOSE1c, §E5): an
    /// untaken one as that submission publishes it, a started one on the person's word to go on. Its hold is let go, and the step
    /// keeps its id and its history. It moves no status.
    /// </summary>
    Rebound,

    /// <summary>
    /// The person chose the quest's kind and workflow on its page before it started (WORKFLOW1e2, the workflow design §4.1 row 1,
    /// §4.3): a quest published with no ask has no ask's choice, so its own is the task's. It applies only while the quest is open,
    /// since a run is bound at its first start (§4.5); the latest stands, and one naming neither clears it. It moves no status.
    /// </summary>
    Chosen,
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
/// For a <see cref="QuestOperationKind.Rebound"/>: a publication's, judged on a withdrawn step, so pushed after another
/// machine's take it is forgotten rather than letting go a started step without the person's word (FABRIC-CLOSE1c15).
/// False on every other kind, on the person's go-on, and on every decline or rebind made before it, which is a plain one.
/// </param>
/// <param name="Evidence">For a <see cref="QuestOperationKind.Evidenced"/>: what was read (EVID1a). Null on every other kind.</param>
/// <param name="SetUp">For a <see cref="QuestOperationKind.SetUp"/>: the set-up said (REVIEWENV1b). Null on every other kind.</param>
/// <param name="Verdict">For a <see cref="QuestOperationKind.Verdict"/>: the person's verdict (REVIEWENV1b). Null on every other kind.</param>
/// <param name="NoteParts">
/// Its note's parts beside the note (QUESTCLOSE1b1, D142 point 2), as <see cref="Knowledge.NoteParts"/> keeps them: Daoris's
/// lead by its code, then someone's words. Only with a note, and null where its writer gave none, which is every operation
/// from before and every note that is all someone's words. <see cref="Note"/> stays byte for byte.
/// </param>
/// <param name="Close">
/// For a <see cref="QuestOperationKind.Done"/> the graph's close door wrote (FABRIC-CLOSE1a, §E5): the invocation's key and the
/// close token, as its provenance. Null on every other done, and on every other kind.
/// </param>
/// <param name="Submission">
/// For a <see cref="QuestOperationKind.Reopened"/>: the submission reopened; for a <see cref="QuestOperationKind.Rebound"/>: the
/// source's submission the step grows from now (FABRIC-CLOSE1c); for a <see cref="QuestOperationKind.Taken"/>: the step's source as the
/// take found it inside its own write, which the take is judged by (FABRIC-CLOSE1c18), null on a quest with no source and on every take
/// from before. Null on every other kind.
/// </param>
/// <param name="Session">
/// For a <see cref="QuestOperationKind.Submitted"/>: the session that said the <c>ready</c>, as its connector's intake named it
/// (FABRIC-CLOSE1c4). Null on every other kind, and where no session said it.
/// </param>
/// <param name="LostToTheReopen">
/// For a <see cref="QuestOperationKind.Conflict"/>: the take of a step that went with its source's take, lost to the person's reopen
/// (FABRIC-CLOSE1c22). The step's publication went too, so its history holds nothing the reopen could be read from, and the conflict
/// says it instead, for <see cref="QuestLog.LostToTheReopen"/>. False on every other conflict, every one from before, and every other kind.
/// </param>
/// <param name="Forward">
/// For a <see cref="QuestOperationKind.Dismissed"/>: the fresh take it made where it brings its machine's lost take forward (SYNCMERGE1a,
/// D178 point 5), written beside it in the same write; its presence is the mark. A marked dismissal is the taker's own
/// (<see cref="QuestLog.TakersOwn(QuestOperation)"/>): it applies only where that take stands, on its machine's own take conflict still
/// on the quest, and a rebase loses it with the take. Null on a plain dismissal, every one from before, and every other kind, which an
/// older build reads every dismissal as.
/// </param>
/// <param name="Choice">For a <see cref="QuestOperationKind.Chosen"/>: the person's kind and workflow (WORKFLOW1e2). Null on every other kind.</param>
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
    QuestEvidenceVerdict? Evidence = null,
    QuestSetUp? SetUp = null,
    QuestReviewVerdict? Verdict = null,
    string? NoteParts = null,
    QuestClose? Close = null,
    QuestOperationRef? Submission = null,
    string? Session = null,
    bool LostToTheReopen = false,
    QuestOperationRef? Forward = null,
    QuestWorkflowChoice? Choice = null);

/// <summary>
/// What a graph's close names its Done by (FABRIC-CLOSE1a, the fabric events design §E5): the invocation's key, which the
/// driver persists before it sends and sends again after a timeout, and the close token it computed from the unit, the
/// revision, the graph's digest and the prerequisite acceptances. A second close carrying the same key answers the first's
/// outcome, so a close is never applied twice.
/// </summary>
public sealed record QuestClose(string Key, string Token);

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

/// <summary>How a settled conflict ended (SYNCMERGE1b0, D178 point 7).</summary>
public enum QuestConflictEnd
{
    /// <summary>A plain dismissal (SYNC6c): the person dropped their machine's work, which stays where it is.</summary>
    Dropped,

    /// <summary>A dismissal marked with the fresh take it made (SYNCMERGE1a): the person brought their machine's work forward.</summary>
    BroughtForward,
}

/// <summary>
/// A bring-forward of a lost take (SYNCMERGE1b0, D178 points 3 and 8): the fresh take it made, when, and where that take stands as a claim
/// reads one (<see cref="QuestLog.Claim"/>): held once a remote numbered it, unconfirmed while it is only here, lost where a rebase kept
/// it as a conflict.
/// </summary>
public sealed record QuestForwardTake(QuestOperationRef Take, DateTimeOffset At, QuestClaim Standing);

/// <summary>
/// A bring-forward this machine made (SYNCMERGE1b0): the conflict it was to settle and the take it made, kept by this machine alone and
/// never pushed. The log forgets a bring-forward whose take lost (D178 point 5), so this is what still says the second loss was one.
/// </summary>
public sealed record QuestForwardPress(QuestOperationRef Conflict, QuestOperationRef Take, DateTimeOffset At);

/// <summary>
/// One conflict as its quest's log tells it (SYNCMERGE1b0): the conflict; whether the take lost to the person's reopen of the work its
/// step grew from (<see cref="QuestLog.LostToTheReopen"/>'s reading, for this conflict); how it ended, null while it stands; and its
/// bring-forward: the one that settled it, or else this machine's latest that lost again.
/// </summary>
public sealed record QuestConflictReading(
    QuestConflict Conflict, bool LostToTheReopen = false, QuestConflictEnd? Ended = null, QuestForwardTake? Forward = null);

/// <summary>
/// Who holds a quest's step and how each take that lost on it stands or ended, read from its log (SYNCMERGE1b0, D178 point 7): the take the
/// quest stands taken by, null unless it is taken; the conflicts still on it; and those settled, in the order they were.
/// </summary>
public sealed record QuestTakeReading(
    QuestOperation? Take, IReadOnlyList<QuestConflictReading> Conflicts, IReadOnlyList<QuestConflictReading> Settled)
{
    /// <summary>Nothing taken and nothing lost: a quest with no history, or none of either.</summary>
    public static readonly QuestTakeReading None = new(null, [], []);

    /// <summary>The machine whose take holds the step, an id no person gave; null unless the quest is taken.</summary>
    public string? Holder => Take?.Machine;

    /// <summary>
    /// The bring-forward the step is held by, where <paramref name="machine"/>'s take holds it and a bring-forward made that take: the
    /// one take a stood-down session of that machine's is carried on over (SessionLedger), and the claim's answer names. Null otherwise.
    /// </summary>
    public QuestForwardTake? BroughtForwardBy(string machine) =>
        Take is { } held && held.Machine == machine
            ? Settled.Where(conflict => conflict.Ended == QuestConflictEnd.BroughtForward)
                .Select(conflict => conflict.Forward)
                .FirstOrDefault(forward => forward is not null && forward.Take == new QuestOperationRef(held.Machine, held.Sequence))
            : null;
}

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
    /// rebase keeps the decline as a conflict (D68 rule 2). A dismissal that brings its machine's lost take forward applies only to that
    /// machine's own take conflict, still on the quest, beside a take of its own (SYNCMERGE1a, <see cref="Brings"/>).
    /// </summary>
    public static bool Applies(Quest? quest, QuestOperation operation) => operation.Kind switch
    {
        QuestOperationKind.Declined when operation.WhileOpen => quest is { Status: QuestStatus.Open },
        QuestOperationKind.Dismissed when operation.Forward is not null => Brings(quest, operation),
        QuestOperationKind.Published => quest is null,
        QuestOperationKind.Conflict or QuestOperationKind.Dismissed => quest is not null,
        // Only a taken quest waits: an open one has nobody's work in it, and a closed one has none left.
        QuestOperationKind.Waited => quest is { Status: QuestStatus.Taken } && !string.IsNullOrEmpty(operation.Note),
        QuestOperationKind.Deleted => quest is { Status: QuestStatus.Open },
        // A yes only while a departure or its evidence holds the done (DRIFT1d, EVID1a): a second machine's yes is the same yes,
        // never a move. A review's hold is the person's verdict's to lift, never a yes's (REVIEWENV1b, D154 point 9).
        QuestOperationKind.Accepted => quest?.Hold is QuestHold.Departed or QuestHold.EvidenceUnread or QuestHold.EvidenceMissing,
        // A verdict only while the done waits on its evidence, and only one that reads exactly that (EVID1a): a second
        // machine's verdict on evidence already found is no move, and a verdict on what the done never named is none. On a
        // submission, only one asked for that submission (FABRIC-CLOSE1e): one read before a later ready replaced it is none. A commit read
        // alone keeps the commit of a submission that waits on no evidence, once (FABRIC-CLOSE1c6, QuestEvidenceVerdict.KeepsCommitOf).
        QuestOperationKind.Evidenced => quest is not null && operation.Evidence is { } read && read.AskedFor(quest)
                                        && (quest.AwaitsEvidence ? read.Covers(quest) : read.KeepsCommitOf(quest)),
        // A set-up only on a set-up step not declined, and a second post of one already kept is no move (REVIEWENV1b).
        QuestOperationKind.SetUp => quest is { SetUpIn: not null, Status: not QuestStatus.Declined }
                                    && operation.SetUp is { } setUp && !quest.SetUps.Any(setUp.Same),
        QuestOperationKind.Verdict => quest is { Status: not QuestStatus.Declined } && operation.Verdict is { } verdict
                                      && QuestReviewing.Takes(quest, verdict),
        // A bind once, on a taken quest before its first submission (FABRIC-CLOSE1a, §E5): a quest follows the graph bound at
        // its start, so a second bind, of the same graph or another, is no move.
        QuestOperationKind.Bound => quest is { Status: QuestStatus.Taken, Closure: null, Submission: null }
                                    && !string.IsNullOrEmpty(operation.Note),
        // A submission only on a taken quest a closure graph binds (§E5). A later one replaces it; whether its successor seals it
        // is the store's to judge inside the write, since only the take's machine makes one.
        QuestOperationKind.Submitted => quest is { Status: QuestStatus.Taken, Closure: not null },
        // The person's reopen (FABRIC-CLOSE1c, §E5): of the current submission, on the taken quest that made it; or on the open or
        // taken step that submission published, named by the submission it grew from (a step from before names none, and grew
        // from its parent's), not already withdrawn by it. A step closed stays closed: a reopen takes no close back.
        QuestOperationKind.Reopened => quest is { Status: QuestStatus.Taken or QuestStatus.Open } && operation.Submission is { } named
                                       && (SubmittedHere(quest, named) || GrewFromIt(quest, named)),
        // A rebind only lets go a step its source's reopen withdrew or held, onto another submission than the one reopened. A
        // publication's was judged on a withdrawn step, so it applies only while the step is open (FABRIC-CLOSE1c15): after another
        // machine's take reached it, a started step goes on only by the person's word, whose rebind carries no such mark.
        QuestOperationKind.Rebound => quest is { Status: QuestStatus.Taken or QuestStatus.Open, SourceReopened: { } reopened }
                                      && operation.Submission is { } grows && grows != reopened
                                      && (!operation.WhileOpen || quest.Status == QuestStatus.Open),
        // A withdrawn step is taken by nobody until it is rebound (FABRIC-CLOSE1c): a take after the reopen loses, everywhere. A take
        // carries the step's source as it found it (FABRIC-CLOSE1c18), so one made before a reopen and its rebind reached its machine
        // loses too, judged where the replay reaches it: a take that came first stands through any later rebind. One carrying none, a
        // source-free quest's or a take from before the stamp, is judged as before.
        QuestOperationKind.Taken => quest is { Status: QuestStatus.Open, SourceReopened: null }
                                    && (operation.Submission is not { } found || found == quest.GrewFrom),
        // The person's choice only before the quest starts (WORKFLOW1e2, design §4.5): a run is bound at its first start, so a
        // choice that reaches a quest another machine took first is no move. Every choice applies, a second the same as the first.
        QuestOperationKind.Chosen => quest is { Status: QuestStatus.Open } && operation.Choice is not null,
        _ => quest is not null && QuestTransitions.Target(operation.Kind) is { } target
             && QuestTransitions.Allows(quest.Status, target),
    };

    /// <summary>
    /// Whether a dismissal marked as bringing its machine's lost take forward (SYNCMERGE1a, D178 point 5) applies to
    /// <paramref name="quest"/>: the conflict it names is its own machine's take that lost, still on the quest, and the take it names is
    /// that machine's too. A conflict already settled, by a plain dismissal or this one, authorizes no take, so the mark lands nowhere
    /// there, and a rebase loses the take it made with it (<see cref="QuestStore"/>'s rebase). Whether the take itself stands is the
    /// claim's: the mark is the taker's own (<see cref="TakersOwn(QuestOperation)"/>, <see cref="OnALostTake"/>).
    /// </summary>
    public static bool Brings(Quest? quest, QuestOperation dismissal) =>
        quest is not null && dismissal is { Dismisses: { } named, Forward: { } take }
        && named.Machine == dismissal.Machine && take.Machine == dismissal.Machine
        && quest.Conflicts.Any(conflict => conflict.Machine == named.Machine && conflict.Sequence == named.Sequence
                                           && conflict.Attempted == QuestStatus.Taken);

    /// <summary>Whether <paramref name="named"/> is <paramref name="quest"/>'s own current submission, on its take (FABRIC-CLOSE1c).</summary>
    private static bool SubmittedHere(Quest quest, QuestOperationRef named) =>
        quest is { Status: QuestStatus.Taken, Submission: { } current }
        && current.Machine == named.Machine && current.Sequence == named.Sequence;

    /// <summary>
    /// Whether <paramref name="quest"/> is a step <paramref name="named"/> published and still stands on (FABRIC-CLOSE1c): the
    /// submission it grew from, or any of its parent's where it names none, as every step from before does.
    /// </summary>
    private static bool GrewFromIt(Quest quest, QuestOperationRef named) =>
        quest.SourceReopened is null && (quest.GrewFrom is { } from ? from == named : quest.Parent is { Length: > 0 });

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
    /// One rule for the three places that judge it. The store's verbs refuse such an operation inside their write
    /// (WAITCLAIM2): the pass that finds a take lost leaves the session running until its driver's next look stops it,
    /// and the session does not know. A remote refuses it as a push arrives (WAITCLAIM3), because a machine on a build
    /// that does not refuse it locally still pushes it, and applied it would move the winner's quest. A rebase loses one
    /// such a build left pending, so an upgraded machine stops pushing what the remote refuses (WAITCLAIM4). A dismissal,
    /// a yes and a verdict are no move on the take. An open quest is nobody's work, whatever an earlier incarnation of it held
    /// (D95). And a machine whose take was numbered reads held, never lost, so a winner's own moves are never judged here.
    /// A set-up is the taker's too (REVIEWENV1b): its driver posts what its session showed, and a set-up from the tree of a
    /// take that lost would show the winner's step as work it never held.
    /// <para>A take lost to the person's reopen leaves its step open, withdrawn or rebound (FABRIC-CLOSE1c18), where the table allows a done
    /// or a decline from open. A done, a decline or a set-up its machine makes there is made on that take all the same, and would close or
    /// decline the step over the person's reopen, so it is judged lost too (<see cref="LostToTheReopen"/>). So is one whose take lost
    /// with the work it was made on, which the winner's chain published anew under the same id (FABRIC-CLOSE1c24,
    /// <see cref="LostOnTheOpenStep"/>): applied, it would close the winner's step on the loser's work. An open quest is otherwise
    /// nobody's work, as above. A take is never judged so: a fresh one of the open step is that machine's to make, and its claim reads by
    /// it from then on.</para>
    /// <para>A dismissal marked as bringing the take forward (SYNCMERGE1a) is judged so too, being made on the fresh take beside it: where
    /// that take lost, the conflict it was to resolve stays, and the mark is refused at a remote as a done is. A plain one never is.</para>
    /// </remarks>
    public static bool OnALostTake(Quest? standing, IEnumerable<QuestOperation> history, QuestOperation operation) =>
        TakersOwn(operation) && standing switch
        {
            { Status: QuestStatus.Taken } => Claim(history, operation.Machine) == QuestClaim.Lost,
            { Status: QuestStatus.Open } => operation.Kind != QuestOperationKind.Taken
                                            && Claim(history, operation.Machine) == QuestClaim.Lost
                                            && (LostOnTheOpenStep(history, operation.Machine)
                                                || LostToTheReopen(history, operation.Machine)),
            _ => false,
        };

    /// <summary>
    /// Whether <paramref name="machine"/>'s take, the latest that lost, lost on the step open (FABRIC-CLOSE1c24): its conflict landed
    /// where the step stood open, so the take was made on work the step no longer stands on. A take loses on an open step only so:
    /// withdrawn by the person's reopen of its source (FABRIC-CLOSE1c18), grown since from another submission than the one its stamp
    /// names, which is how the winner's chain meets a step a lost submission published under the same id, or rewritten as a conflict
    /// when that submission went (FABRIC-CLOSE1e) and gone up on the step published anew, whose publication comes first. A take that lost
    /// to another machine's take lands on the step taken, and one that lost to a delete on no quest (D95), so neither is judged by it.
    /// </summary>
    public static bool LostOnTheOpenStep(IEnumerable<QuestOperation> history, string machine)
    {
        Quest? quest = null;
        var lost = false;
        foreach (var operation in history)
        {
            if (operation is { Kind: QuestOperationKind.Conflict, Attempted: QuestStatus.Taken } && operation.Machine == machine)
            {
                lost = quest is { Status: QuestStatus.Open };
            }

            if (Applies(quest, operation)) quest = Step(quest, operation);
        }

        return lost;
    }

    /// <summary>
    /// Whether <paramref name="machine"/>'s take lost to the person's reopen (FABRIC-CLOSE1c18): its take's conflict, the latest, landed on
    /// the step open after a reopen of its source withdrew it, rebound since or not, on the quest as last published (D95). False where the
    /// take lost to another machine's take or move, to a delete, or with its parent's submission (FABRIC-CLOSE1e), and where it never lost;
    /// true again where it went with its parent's submission made on a take lost so, which its conflict says (FABRIC-CLOSE1c22,
    /// <see cref="QuestOperation.LostToTheReopen"/>), since that step's history holds nothing else to read it from.
    /// The store's refusals, the claim's answer and the driver's stop say the loss by it.
    /// </summary>
    public static bool LostToTheReopen(IEnumerable<QuestOperation> history, string machine)
    {
        Quest? quest = null;
        var reopened = false;
        var lost = false;
        foreach (var operation in history)
        {
            if (operation is { Kind: QuestOperationKind.Conflict, Attempted: QuestStatus.Taken } && operation.Machine == machine)
            {
                lost = operation.LostToTheReopen || (reopened && quest is { Status: QuestStatus.Open });
            }

            if (!Applies(quest, operation)) continue;
            if (operation.Kind == QuestOperationKind.Published) reopened = false;
            if (operation.Kind == QuestOperationKind.Reopened && quest is { Status: QuestStatus.Open }) reopened = true;
            // SYNCMERGE1a2: a plain dismissal of this machine's conflict supersedes the reopen it was lost to, so a fresh take lost after
            // it lost to what stands then, not to the reopen. A later reopen sets it again.
            if (operation is { Kind: QuestOperationKind.Dismissed, Forward: null, Dismisses: { } settled } && settled.Machine == machine)
                reopened = false;
            quest = Step(quest, operation);
        }

        return lost;
    }

    /// <summary>
    /// Who holds a quest's step and how each take that lost on it stands or ended (SYNCMERGE1b0, D178 points 3, 7 and 8), read in one pass
    /// as the replay reads it. A conflict is lost to the reopen by <see cref="LostToTheReopen"/>'s rule, read for each conflict where it
    /// lands rather than for a machine's latest. A dismissal that removed a conflict settles it, dropped or brought forward by its mark; one
    /// that found it already gone settles nothing, so the first choice stands. A conflict not settled by a mark names this machine's latest
    /// bring-forward of it whose take lost again, from <paramref name="presses"/>, since the log forgets that one with its take.
    /// </summary>
    /// <param name="presses">This machine's own bring-forwards on the quest (<see cref="QuestForwardPress"/>); none for another's reading.</param>
    public static QuestTakeReading Read(IEnumerable<QuestOperation> history, IEnumerable<QuestForwardPress>? presses = null)
    {
        Quest? quest = null;
        QuestOperation? take = null;
        var named = new Dictionary<QuestOperationRef, QuestOperation>();
        var toTheReopen = new HashSet<QuestOperationRef>();
        var settled = new List<(QuestConflict Conflict, QuestOperation Dismissal)>();
        var reopened = false;
        var superseded = new HashSet<string>(StringComparer.Ordinal);
        foreach (var operation in history)
        {
            var name = new QuestOperationRef(operation.Machine, operation.Sequence);
            if (operation.Kind is QuestOperationKind.Taken or QuestOperationKind.Conflict) named[name] = operation;
            if (operation is { Kind: QuestOperationKind.Conflict, Attempted: QuestStatus.Taken }
                && (operation.LostToTheReopen || (reopened && !superseded.Contains(operation.Machine) && quest is { Status: QuestStatus.Open })))
            {
                toTheReopen.Add(name);
            }

            if (!Applies(quest, operation)) continue;
            switch (operation.Kind)
            {
                case QuestOperationKind.Published:
                    (reopened, take) = (false, null);
                    superseded.Clear();
                    settled.Clear();
                    break;
                case QuestOperationKind.Reopened when quest is { Status: QuestStatus.Open }:
                    reopened = true;
                    superseded.Clear();
                    break;
                case QuestOperationKind.Taken:
                    take = operation;
                    break;
                case QuestOperationKind.Dismissed when operation.Dismisses is { } dismissed:
                    if (quest!.Conflicts.FirstOrDefault(c => c.Machine == dismissed.Machine && c.Sequence == dismissed.Sequence) is { } gone)
                    {
                        settled.Add((gone, operation));
                    }

                    // SYNCMERGE1a2's rule, for each machine: a plain dismissal of its conflict supersedes the reopen it was lost to.
                    if (operation.Forward is null) superseded.Add(dismissed.Machine);
                    break;
            }

            quest = Step(quest, operation);
        }

        if (quest is null) return QuestTakeReading.None;

        static QuestClaim StandingOf(QuestOperation made) => made.Kind == QuestOperationKind.Conflict
            ? QuestClaim.Lost
            : made.Number is null ? QuestClaim.Unconfirmed : QuestClaim.Held;

        var pressed = (presses ?? []).OrderBy(press => press.At).ToList();
        QuestForwardTake? Forward(QuestConflict conflict, QuestOperation? dismissal)
        {
            if (dismissal?.Forward is { } made)
            {
                return named.TryGetValue(made, out var fresh)
                    ? new QuestForwardTake(made, fresh.At, StandingOf(fresh))
                    : new QuestForwardTake(made, dismissal.At, dismissal.Number is null ? QuestClaim.Unconfirmed : QuestClaim.Held);
            }

            var of = new QuestOperationRef(conflict.Machine, conflict.Sequence);
            return pressed
                .Where(press => press.Conflict == of && named.TryGetValue(press.Take, out var fresh) && StandingOf(fresh) == QuestClaim.Lost)
                .Select(press => new QuestForwardTake(press.Take, press.At, QuestClaim.Lost))
                .LastOrDefault();
        }

        QuestConflictReading Reading(QuestConflict conflict, QuestOperation? dismissal) => new(
            conflict,
            toTheReopen.Contains(new QuestOperationRef(conflict.Machine, conflict.Sequence)),
            dismissal is null ? null : dismissal.Forward is null ? QuestConflictEnd.Dropped : QuestConflictEnd.BroughtForward,
            Forward(conflict, dismissal));

        return new QuestTakeReading(
            quest.Status == QuestStatus.Taken ? take : null,
            quest.Conflicts.Select(conflict => Reading(conflict, null)).ToList(),
            settled.Select(ended => Reading(ended.Conflict, ended.Dismissal)).ToList());
    }

    /// <summary>
    /// Whether a kind is made on a take, by the machine holding it: a move, a wait, a set-up (WAITCLAIM1, REVIEWENV1b), or a
    /// closure graph's bind and submission (FABRIC-CLOSE1a, §E5). One list for <see cref="OnALostTake"/> and for a rebase's
    /// losing of what a lost take made, both through <see cref="TakersOwn(QuestOperation)"/>, which adds the one operation of another kind
    /// that is a taker's.
    /// </summary>
    public static bool TakersOwn(QuestOperationKind kind) =>
        QuestTransitions.Target(kind) is not null
        || kind is QuestOperationKind.Waited or QuestOperationKind.SetUp or QuestOperationKind.Bound or QuestOperationKind.Submitted;

    /// <summary>
    /// Whether an operation is made on a take, by the machine holding it: every kind <see cref="TakersOwn(QuestOperationKind)"/> names,
    /// and a dismissal marked as bringing that machine's lost take forward (SYNCMERGE1a, <see cref="QuestOperation.Forward"/>), which is
    /// written with its fresh take and stands or falls with it. A plain dismissal is no move on a take, and is no taker's. What
    /// <see cref="OnALostTake"/> and a rebase's losing of what a lost take made read.
    /// </summary>
    public static bool TakersOwn(QuestOperation operation) =>
        TakersOwn(operation.Kind) || operation is { Kind: QuestOperationKind.Dismissed, Forward: not null };

    /// <summary>The quest after an operation that <see cref="Applies"/> — none after a delete.</summary>
    public static Quest? Step(Quest? quest, QuestOperation operation) => operation.Kind switch
    {
        QuestOperationKind.Deleted => null,
        QuestOperationKind.Published => operation.Published! with
        {
            Status = QuestStatus.Open, Note = null, NoteParts = null, Filed = operation.At, Updated = operation.At, Conflicts = [],
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
        // A set-up and a verdict as the quest keeps them: when, where and which operation are the operation's own (REVIEWENV1b).
        QuestOperationKind.SetUp => quest! with
        {
            SetUps = [.. quest.SetUps, operation.SetUp! with { At = operation.At, Machine = operation.Machine, Sequence = operation.Sequence }],
            Updated = operation.At,
        },
        QuestOperationKind.Verdict => quest! with
        {
            Verdicts = [.. quest.Verdicts, operation.Verdict! with { At = operation.At, Machine = operation.Machine }],
            Updated = operation.At,
        },
        QuestOperationKind.Bound => quest! with { Closure = operation.Note, Updated = operation.At },
        // The choice as the quest keeps it: when it was made is the operation's own (WORKFLOW1e2).
        QuestOperationKind.Chosen => quest! with
        {
            WorkflowChoices = [.. quest.WorkflowChoices, operation.Choice! with { At = operation.At }], Updated = operation.At,
        },
        // The latest submission is the current one, judged again from the start (FABRIC-CLOSE1a, §E5): its answers are the
        // quest's, and a yes or an evidence verdict given on the one before is not given on it. It answers the last reopen, at
        // whatever commit (FABRIC-CLOSE1c4): a submission is named by its machine and sequence, never by its commit.
        QuestOperationKind.Submitted => quest! with
        {
            Submission = new QuestSubmission(operation.Machine, operation.Sequence, operation.At, operation.Note, operation.Session),
            Answers = operation.Answers ?? [], Accepted = null, Evidence = null, Updated = operation.At, Reopened = null,
        },
        // FABRIC-CLOSE1c: on the quest that submitted it, the submission is current no more and its answers, yes and verdict go with
        // it, so the next ready is judged from the start; on the step it published, the step stands withdrawn or held for the person.
        // FABRIC-CLOSE1c4: the quest keeps the reopen until a later submission answers it, so its driver can tell a reopened quest
        // from one not yet ready, and the session that said the ready what the person said. FABRIC-CLOSE1c8: the reopen names that session,
        // which it clears with the submission, so the taker's machine knows whom to tell.
        QuestOperationKind.Reopened when SubmittedHere(quest!, operation.Submission!) => quest! with
        {
            Submission = null, Answers = [], Accepted = null, Evidence = null, Updated = operation.At,
            Reopened = new QuestReopen(
                operation.Machine, operation.Sequence, operation.Submission!, operation.Note, operation.At, quest.Submission!.Session),
        },
        QuestOperationKind.Reopened => quest! with { SourceReopened = operation.Submission, Updated = operation.At },
        // FABRIC-CLOSE1c10: a held step's own result (its submission, answers, yes and verdict) was made on the work reopened, so the
        // person's go-on clears it as a reopen of it would, and the step submits anew once its branch holds the new source; the go-on
        // alone never makes old work finished again. It sets no reopen of its own. A withdrawn open step has none, and reads as it did.
        QuestOperationKind.Rebound => quest! with
        {
            GrewFrom = operation.Submission, SourceReopened = null,
            Submission = null, Answers = [], Accepted = null, Evidence = null, Updated = operation.At,
        },
        // A done carries how it answered each requirement (DRIFT1d); a take or a decline answers none. The note's parts are the
        // move's own, so a note written without them clears the last one's (QUESTCLOSE1b1, D142 point 2). A close also clears
        // the question the quest waited on: it names one only while taken, the Waited operation staying in the history (AWAITSTALE1, D79).
        _ => quest! with
        {
            Status = QuestTransitions.Target(operation.Kind)!.Value, Note = operation.Note, NoteParts = operation.NoteParts,
            Updated = operation.At, Answers = operation.Answers ?? [],
            Awaits = QuestTransitions.Target(operation.Kind) == QuestStatus.Taken ? quest.Awaits : null,
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
        // Nothing left to say. A dismissal is lost only with its quest; one bringing a lost take forward also with the take it made, or
        // on a conflict another machine settled first (SYNCMERGE1a), so a failed bring-forward never reads as resolved, and the conflict
        // it was to resolve stays. A wait, once its quest is no longer taken: another
        // machine closed or deleted it first (QUESTOP1); or made on this machine's take that lost, which the take's
        // conflict names (WAITCLAIM1, QuestStore.RebaseAsync), or which an older build made after that conflict was written
        // and the remote never kept (WAITCLAIM4). A delete that lost to a take, or that another machine's delete
        // already made (D95). A yes to a done nothing holds any more (DRIFT1d): its done lost, or another machine's yes came
        // first. A verdict on evidence nothing waits on any more (EVID1a): another machine's verdict found it first, or the
        // person accepted the done as it stood. A set-up on a step another machine declined or deleted, the same set-up
        // posted first elsewhere, or one made on this machine's take that lost (REVIEWENV1b). A review's verdict another
        // machine's came before, or on a set-up a newer one stands beside. A closure graph's bind or submission once its quest
        // is no longer taken, or made on this machine's take that lost (FABRIC-CLOSE1a, §E5): the take's conflict names a lost
        // submission, and the step a submission published goes with it (QuestStore.RebaseAsync). A reopen once the submission it
        // names is gone or closed, or its step closed or lost; a rebind once another let the step go, or it closed (FABRIC-CLOSE1c),
        // and a publication's once another machine's take reached the step first (FABRIC-CLOSE1c15): the person's word lets that one go.
        // A workflow choice once another machine's take started the quest first (WORKFLOW1e2): its run was bound there, at that start.
        QuestOperationKind.Dismissed or QuestOperationKind.Waited or QuestOperationKind.Deleted
            or QuestOperationKind.Accepted or QuestOperationKind.Evidenced
            or QuestOperationKind.SetUp or QuestOperationKind.Verdict
            or QuestOperationKind.Bound or QuestOperationKind.Submitted
            or QuestOperationKind.Reopened or QuestOperationKind.Rebound or QuestOperationKind.Chosen => QuestLoss.Forgotten,
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind), kind,
            "A rebase has no rule for this kind of quest operation; .claude/knowledge/quest-operations.md names every place a kind goes."),
    };
}
