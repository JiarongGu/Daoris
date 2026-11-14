namespace Daoris.Knowledge;

/// <summary>One operation in a quest's history.</summary>
/// <param name="Quest">The quest it happened to.</param>
/// <param name="Kind">What happened.</param>
/// <param name="Machine">
/// The machine that made it — a stable id kept in the store, never the key, which rotates.
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
/// Where the remote placed it in its order — null while it is pending, and always on a
/// machine with no remote.
/// </param>
/// <param name="Dismisses">For a <see cref="QuestOperationKind.Dismissed"/>: the conflict it dismisses.</param>
/// <param name="Answers">
/// For a <see cref="QuestOperationKind.Done"/>: how it answered each of the quest's requirements. Null
/// where it carries none, which is every operation of a build before answers.
/// </param>
/// <param name="WhileOpen">
/// For a <see cref="QuestOperationKind.Declined"/>: it applies only to a quest still open where it lands, so pushed after another machine's take it becomes a conflict rather than declining their work.
/// For a <see cref="QuestOperationKind.Rebound"/>: a publication's, judged on a withdrawn step, so pushed after another
/// machine's take it is forgotten rather than letting go a started step without the person's word.
/// False on every other kind, on the person's go-on, and on every decline or rebind made before it, which is a plain one.
/// </param>
/// <param name="Evidence">For a <see cref="QuestOperationKind.Evidenced"/>: what is read. Null on every other kind.</param>
/// <param name="SetUp">For a <see cref="QuestOperationKind.SetUp"/>: the set-up said. Null on every other kind.</param>
/// <param name="Verdict">For a <see cref="QuestOperationKind.Verdict"/>: the person's verdict. Null on every other kind.</param>
/// <param name="NoteParts">
/// Its note's parts beside the note, as <see cref="Knowledge.NoteParts"/> keeps them: Daoris's
/// lead by its code, then someone's words. Only with a note, and null where its writer gave none, which is every operation
/// from before and every note that is all someone's words. <see cref="Note"/> stays byte for byte.
/// </param>
/// <param name="Close">
/// For a <see cref="QuestOperationKind.Done"/> the graph's close door wrote: the invocation's key and the
/// close token, as its provenance. Null on every other done, and on every other kind.
/// </param>
/// <param name="Submission">
/// For a <see cref="QuestOperationKind.Reopened"/>: the submission reopened; for a <see cref="QuestOperationKind.Rebound"/>: the
/// source's submission the step grows from now; for a <see cref="QuestOperationKind.Taken"/>: the step's source as the
/// take found it inside its own write, which the take is judged by, null on a quest with no source and on every take
/// from before. Null on every other kind.
/// </param>
/// <param name="Session">
/// For a <see cref="QuestOperationKind.Submitted"/>: the session that said the <c>ready</c>, as its connector's intake named it;
/// for a <see cref="QuestOperationKind.Done"/> or a <see cref="QuestOperationKind.Declined"/>: the session that
/// wrote its handback through its connector. Null on every other kind, and where no session said it.
/// </param>
/// <param name="LostToTheReopen">
/// For a <see cref="QuestOperationKind.Conflict"/>: the take of a step that went with its source's take, lost to the person's reopen.
/// The step's publication went too, so its history holds nothing the reopen could be read from, and the conflict
/// says it instead, for <see cref="QuestLog.LostToTheReopen"/>. False on every other conflict, every one from before, and every other kind.
/// </param>
/// <param name="Forward">
/// For a <see cref="QuestOperationKind.Dismissed"/>: the fresh take it made where it brings its machine's lost take forward,
/// written beside it in the same write; its presence is the mark. A marked dismissal is the taker's own
/// (<see cref="QuestLog.TakersOwn(QuestOperation)"/>): it applies only where that take stands, on its machine's own take conflict still
/// on the quest, and a rebase loses it with the take. Null on a plain dismissal, every one from before, and every other kind, which an
/// older build reads every dismissal as.
/// </param>
/// <param name="Choice">For a <see cref="QuestOperationKind.Chosen"/>: the person's kind and workflow. Null on every other kind.</param>
/// <param name="Revision">For a <see cref="QuestOperationKind.Revised"/>: the part's words and scope as the plan now says them. Null on every other kind.</param>
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
    QuestWorkflowChoice? Choice = null,
    QuestRevision? Revision = null);
