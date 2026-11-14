namespace Daoris.Knowledge;

/// <summary>A quest is its operations in order; its current state is what replaying them gives.</summary>
public static class QuestLog
{
    /// <summary>
    /// Fold a history, in order, through the transition table. An operation that does not apply is
    /// not a move and changes nothing — so no order of operations, from whichever machines, can reach
    /// a state the table forbids. The first publish is the quest; a later one is the same ask again —
    /// unless a delete came between, after which a publish begins the quest anew.
    /// </summary>
    /// <returns>The quest as it stands, or null when nothing in the history published it, or a delete ended it.</returns>
    public static Quest? Replay(IEnumerable<QuestOperation> history) =>
        history.Aggregate((Quest?)null, (quest, operation) => Applies(quest, operation) ? Step(quest, operation) : quest);

    /// <summary>
    /// Whether an operation moves a quest standing at <paramref name="quest"/>: a publish only begins
    /// one, a move goes only where the table allows, and a conflict is recorded on any quest there is.
    /// A dismissal applies to any quest there is, whether or not its conflict is still there: two
    /// people dismissing one conflict is one dismissal, never a refusal or a new conflict. A delete
    /// applies only to an open quest: taken, somebody's work stands on it. So does a decline made
    /// while open: it is judged on an open quest, so after a take the take stands, and the
    /// rebase keeps the decline as a conflict. A dismissal that brings its machine's lost take forward applies only to that
    /// machine's own take conflict, still on the quest, beside a take of its own (<see cref="Brings"/>).
    /// </summary>
    public static bool Applies(Quest? quest, QuestOperation operation) => operation.Kind switch
    {
        QuestOperationKind.Declined when operation.WhileOpen => quest is { Status: QuestStatus.Open },
        QuestOperationKind.Dismissed when operation.Forward is not null => Brings(quest, operation),
        QuestOperationKind.Published => quest is null,
        QuestOperationKind.Conflict or QuestOperationKind.Dismissed => quest is not null,
        // Only a taken quest waits: an open one has nobody's work in it, and a closed one has none left.
        QuestOperationKind.Waited => quest is { Status: QuestStatus.Taken } && !string.IsNullOrEmpty(operation.Note),
        // An open quest by the person's delete, a closed one by the closed quest's remover (QuestClosedRemoval): a taken one is somebody's work.
        QuestOperationKind.Deleted => quest is { Status: QuestStatus.Open or QuestStatus.Done or QuestStatus.Declined },
        // A yes only while a departure or its evidence holds the done: a second machine's yes is the same yes,
        // never a move. A review's hold is the person's verdict's to lift, never a yes's.
        QuestOperationKind.Accepted => quest?.Hold is QuestHold.Departed or QuestHold.EvidenceUnread or QuestHold.EvidenceMissing,
        // A verdict only while the done waits on its evidence, and only one that reads exactly that: a second
        // machine's verdict on evidence already found is no move, and a verdict on what the done never named is none. On a
        // submission, only one asked for that submission: one read before a later ready replaced it is none. A commit read
        // alone keeps the commit of a submission that waits on no evidence, once (QuestEvidenceVerdict.KeepsCommitOf).
        QuestOperationKind.Evidenced => quest is not null && operation.Evidence is { } read && read.AskedFor(quest)
                                        && (quest.AwaitsEvidence ? read.Covers(quest) : read.KeepsCommitOf(quest)),
        // A set-up only on a set-up step not declined, and a second post of one already kept is no move.
        QuestOperationKind.SetUp => quest is { SetUpIn: not null, Status: not QuestStatus.Declined }
                                    && operation.SetUp is { } setUp && !quest.SetUps.Any(setUp.Same),
        QuestOperationKind.Verdict => quest is { Status: not QuestStatus.Declined } && operation.Verdict is { } verdict
                                      && QuestReviewing.Takes(quest, verdict),
        // A bind once, on a taken quest before its first submission: a quest follows the graph bound at
        // its start, so a second bind, of the same graph or another, is no move.
        QuestOperationKind.Bound => quest is { Status: QuestStatus.Taken, Closure: null, Submission: null }
                                    && !string.IsNullOrEmpty(operation.Note),
        // A submission only on a taken quest a closure graph binds. A later one replaces it; whether its successor seals it
        // is the store's to judge inside the write, since only the take's machine makes one.
        QuestOperationKind.Submitted => quest is { Status: QuestStatus.Taken, Closure: not null },
        // The person's reopen: of the current submission, on the taken quest that made it; or on the open or
        // taken step that submission published, named by the submission it grew from (a step from before names none, and grew
        // from its parent's), not already withdrawn by it. A step closed stays closed: a reopen takes no close back.
        QuestOperationKind.Reopened => quest is { Status: QuestStatus.Taken or QuestStatus.Open } && operation.Submission is { } named
                                       && (SubmittedHere(quest, named) || GrewFromIt(quest, named)),
        // A rebind only lets go a step its source's reopen withdrew or held, onto another submission than the one reopened. A
        // publication's is judged on a withdrawn step, so it applies only while the step is open: after another
        // machine's take reached it, a started step goes on only by the person's word, whose rebind carries no such mark.
        QuestOperationKind.Rebound => quest is { Status: QuestStatus.Taken or QuestStatus.Open, SourceReopened: { } reopened }
                                      && operation.Submission is { } grows && grows != reopened
                                      && (!operation.WhileOpen || quest.Status == QuestStatus.Open),
        // A withdrawn step is taken by nobody until it is rebound: a take after the reopen loses, everywhere. A take
        // carries the step's source as it found it, so one made before a reopen and its rebind reached its machine
        // loses too, judged where the replay reaches it: a take that came first stands through any later rebind. One carrying none, a
        // source-free quest's or a take from before the stamp, is judged as before.
        QuestOperationKind.Taken => quest is { Status: QuestStatus.Open, SourceReopened: null }
                                    && (operation.Submission is not { } found || found == quest.GrewFrom),
        // The person's choice only before the quest starts: a run is bound at its first start, so a
        // choice that reaches a quest another machine took first is no move. Every choice applies, a second the same as the first.
        QuestOperationKind.Chosen => quest is { Status: QuestStatus.Open } && operation.Choice is not null,
        // A plan's revision of the part reaches a quest still being done, open or taken, and no quest another machine closed first.
        QuestOperationKind.Revised => quest is { Status: QuestStatus.Open or QuestStatus.Taken } && operation.Revision is not null,
        _ => quest is not null && QuestTransitions.Target(operation.Kind) is { } target
             && QuestTransitions.Allows(quest.Status, target),
    };

    /// <summary>
    /// Whether a dismissal marked as bringing its machine's lost take forward applies to
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

    /// <summary>Whether <paramref name="named"/> is <paramref name="quest"/>'s own current submission, on its take.</summary>
    private static bool SubmittedHere(Quest quest, QuestOperationRef named) =>
        quest is { Status: QuestStatus.Taken, Submission: { } current }
        && current.Machine == named.Machine && current.Sequence == named.Sequence;

    /// <summary>
    /// Whether <paramref name="quest"/> is a step <paramref name="named"/> published and still stands on: the
    /// submission it grew from, or any of its parent's where it names none, as every step from before does.
    /// </summary>
    private static bool GrewFromIt(Quest quest, QuestOperationRef named) =>
        quest.SourceReopened is null && (quest.GrewFrom is { } from ? from == named : quest.Parent is { Length: > 0 });

    /// <summary>
    /// Where <paramref name="machine"/>'s claim on a quest stands, read from the quest's operations: held once
    /// a remote numbered its take, unconfirmed while the take is only here, lost once a rebase made it a conflict, and none
    /// when it never took the quest. <see cref="QuestStore.ClaimAsync"/> answers from it, and the store's verbs and a remote
    /// judge by it (<see cref="OnALostTake"/>), so the driver, the store and the remote read one claim.
    /// </summary>
    public static QuestClaim Claim(IEnumerable<QuestOperation> history, string machine)
    {
        Quest? quest = null;
        var claim = QuestClaim.None;
        var taken = false;
        foreach (var operation in history)
        {
            // A publish that applies begins the quest anew, after a delete: a take an earlier generation held is not this one's.
            // A take that lost stays lost, since one that lost to the delete lands on no quest, before the publish.
            if (operation.Kind == QuestOperationKind.Published && Applies(quest, operation) && taken) (claim, taken) = (QuestClaim.None, false);
            if (Applies(quest, operation)) quest = Step(quest, operation);
            if (operation.Machine != machine || taken) continue;
            if (operation.Kind == QuestOperationKind.Taken)
            {
                (claim, taken) = (operation.Number is null ? QuestClaim.Unconfirmed : QuestClaim.Held, true);
            }
            else if (operation is { Kind: QuestOperationKind.Conflict, Attempted: QuestStatus.Taken })
            {
                claim = QuestClaim.Lost;
            }
        }

        return claim;
    }

    /// <summary>
    /// Whether <paramref name="operation"/>, judged on <paramref name="standing"/>, is made on a take its machine lost: a
    /// done, a decline or a wait on a taken quest, where that machine's <see cref="Claim"/> in <paramref name="history"/>
    /// reads lost. The take that beat it stands, so the operation is made on a claim its machine never held.
    /// </summary>
    /// <remarks>
    /// One rule for the three places that judge it. The store's verbs refuse such an operation inside their write: the pass that finds a take lost leaves the session running until its driver's next look stops it,
    /// and the session does not know. A remote refuses it as a push arrives, because a machine on a build
    /// that does not refuse it locally still pushes it, and applied it would move the winner's quest. A rebase loses one
    /// such a build left pending, so an upgraded machine stops pushing what the remote refuses. A dismissal,
    /// a yes and a verdict are no move on the take. An open quest is nobody's work, whatever an earlier incarnation of it held. And a machine whose take is numbered reads held, never lost, so a winner's own moves are never judged here.
    /// A set-up is the taker's too: its driver posts what its session showed, and a set-up from the tree of a
    /// take that lost would show the winner's step as work it never held.
    /// <para>A take lost to the person's reopen leaves its step open, withdrawn or rebound, where the table allows a done
    /// or a decline from open. A done, a decline or a set-up its machine makes there is made on that take all the same, and would close or
    /// decline the step over the person's reopen, so it is judged lost too (<see cref="LostToTheReopen"/>). So is one whose take lost
    /// with the work it rests on, which the winner's chain published anew under the same id
    /// (<see cref="LostOnTheOpenStep"/>): applied, it would close the winner's step on the loser's work. An open quest is otherwise
    /// nobody's work, as above. A take is never judged so: a fresh one of the open step is that machine's to make, and its claim reads by
    /// it from then on.</para>
    /// <para>A dismissal marked as bringing the take forward is judged so too, being made on the fresh take beside it: where
    /// that take lost, the conflict it resolves stays, and the mark is refused at a remote as a done is. A plain one never is.</para>
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
    /// Whether <paramref name="machine"/>'s take, the latest that lost, lost on the step open: its conflict landed
    /// where the step stood open, so the take is made on work the step no longer stands on. A take loses on an open step only so:
    /// withdrawn by the person's reopen of its source, grown since from another submission than the one its stamp
    /// names, which is how the winner's chain meets a step a lost submission published under the same id, or rewritten as a conflict
    /// when that submission went and gone up on the step published anew, whose publication comes first. A take that lost
    /// to another machine's take lands on the step taken, and one that lost to a delete on no quest, so neither is judged by it.
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
    /// Whether <paramref name="machine"/>'s take lost to the person's reopen: its take's conflict, the latest, landed on
    /// the step open after a reopen of its source withdrew it, rebound since or not, on the quest as last published. False where the
    /// take lost to another machine's take or move, to a delete, or with its parent's submission, and where it never lost;
    /// true again where it went with its parent's submission made on a take lost so, which its conflict says
    /// (<see cref="QuestOperation.LostToTheReopen"/>), since that step's history holds nothing else to read it from.
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
            // A plain dismissal of this machine's conflict supersedes the reopen it lost to, so a fresh take lost after
            // it lost to what stands then, not to the reopen. A later reopen sets it again.
            if (operation is { Kind: QuestOperationKind.Dismissed, Forward: null, Dismisses: { } settled } && settled.Machine == machine)
                reopened = false;
            quest = Step(quest, operation);
        }

        return lost;
    }

    /// <summary>
    /// Who holds a quest's step and how each take that lost on it stands or ended, read in one pass
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

                    // The rule, for each machine: a plain dismissal of its conflict supersedes the reopen it lost to.
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
    /// Whether a kind is made on a take, by the machine holding it: a move, a wait, a set-up, or a
    /// closure graph's bind and submission. One list for <see cref="OnALostTake"/> and for a rebase's
    /// losing of what a lost take made, both through <see cref="TakersOwn(QuestOperation)"/>, which adds the one operation of another kind
    /// that is a taker's.
    /// </summary>
    public static bool TakersOwn(QuestOperationKind kind) =>
        QuestTransitions.Target(kind) is not null
        || kind is QuestOperationKind.Waited or QuestOperationKind.SetUp or QuestOperationKind.Bound or QuestOperationKind.Submitted;

    /// <summary>
    /// Whether an operation is made on a take, by the machine holding it: every kind <see cref="TakersOwn(QuestOperationKind)"/> names,
    /// and a dismissal marked as bringing that machine's lost take forward (<see cref="QuestOperation.Forward"/>), which is
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
        // The verdict as the quest keeps it: when and by which machine are the operation's own.
        QuestOperationKind.Evidenced => quest! with
        {
            Evidence = operation.Evidence! with { At = operation.At, Machine = operation.Machine }, Updated = operation.At,
        },
        // A set-up and a verdict as the quest keeps them: when, where and which operation are the operation's own.
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
        // The choice as the quest keeps it: when it is made is the operation's own.
        QuestOperationKind.Chosen => quest! with
        {
            WorkflowChoices = [.. quest.WorkflowChoices, operation.Choice! with { At = operation.At }], Updated = operation.At,
        },
        QuestOperationKind.Revised => quest! with
        {
            Title = operation.Revision!.Title, Body = operation.Revision.Body, Weight = operation.Revision.Weight,
            Paths = operation.Revision.NormalizedPaths, Updated = operation.At,
        },
        // The latest submission is the current one, judged again from the start: its answers are the
        // quest's, and a yes or an evidence verdict given on the one before is not given on it. It answers the last reopen, at
        // whatever commit: a submission is named by its machine and sequence, never by its commit.
        QuestOperationKind.Submitted => quest! with
        {
            Submission = new QuestSubmission(operation.Machine, operation.Sequence, operation.At, operation.Note, operation.Session),
            Answers = operation.Answers ?? [], Accepted = null, Evidence = null, Updated = operation.At, Reopened = null,
        },
        // On the quest that submitted it, the submission is current no more and its answers, yes and verdict go with
        // it, so the next ready is judged from the start; on the step it published, the step stands withdrawn or held for the person.
        // The quest keeps the reopen until a later submission answers it, so its driver can tell a reopened quest
        // from one not yet ready, and the session that said the ready what the person said. The reopen names that session,
        // which it clears with the submission, so the taker's machine knows whom to tell.
        QuestOperationKind.Reopened when SubmittedHere(quest!, operation.Submission!) => quest! with
        {
            Submission = null, Answers = [], Accepted = null, Evidence = null, Updated = operation.At,
            Reopened = new QuestReopen(
                operation.Machine, operation.Sequence, operation.Submission!, operation.Note, operation.At, quest.Submission!.Session),
        },
        QuestOperationKind.Reopened => quest! with { SourceReopened = operation.Submission, Updated = operation.At },
        // A held step's own result (its submission, answers, yes and verdict) is made on the work reopened, so the
        // person's go-on clears it as a reopen of it would, and the step submits anew once its branch holds the new source; the go-on
        // alone never makes old work finished again. It sets no reopen of its own. A withdrawn open step has none.
        QuestOperationKind.Rebound => quest! with
        {
            GrewFrom = operation.Submission, SourceReopened = null,
            Submission = null, Answers = [], Accepted = null, Evidence = null, Updated = operation.At,
        },
        // A done carries how it answered each requirement; a take or a decline answers none. The note's parts are the
        // move's own, so a note written without them clears the last one's. A close also clears
        // the question the quest waited on: it names one only while taken, the Waited operation staying in the history.
        _ => quest! with
        {
            Status = QuestTransitions.Target(operation.Kind)!.Value, Note = operation.Note, NoteParts = operation.NoteParts,
            Updated = operation.At, Answers = operation.Answers ?? [],
            Awaits = QuestTransitions.Target(operation.Kind) == QuestStatus.Taken ? quest.Awaits : null,
        },
    };

    /// <summary>
    /// What a rebase does with a pending operation of <paramref name="kind"/> that no longer applies. Every kind has its rule here, and only a move becomes a conflict: a conflict names the status it
    /// attempted, and the wire refuses one that names none (<see cref="QuestWire.Shape"/>), so a conflict made of
    /// anything else would stop every later push of its circle. A kind with no rule throws rather than make one.
    /// </summary>
    public static QuestLoss Lost(QuestOperationKind kind) => kind switch
    {
        // The same ask, published first elsewhere: the first publish is the quest, and a second copy is nobody's decision.
        QuestOperationKind.Published => QuestLoss.Forgotten,
        // A move that lost to another machine's, or made on a take that lost: kept for a person, never dropped.
        QuestOperationKind.Taken or QuestOperationKind.Done or QuestOperationKind.Declined => QuestLoss.Conflict,
        // It applies to any quest there is, so it is lost only with its quest, and kept so that machine's claim
        // still reads lost.
        QuestOperationKind.Conflict => QuestLoss.Kept,
        // Nothing left to say. A dismissal is lost only with its quest; one bringing a lost take forward also with the take it made, or
        // on a conflict another machine settled first, so a failed bring-forward never reads as resolved, and the conflict
        // it resolves stays. A wait, once its quest is no longer taken: another
        // machine closed or deleted it first; or made on this machine's take that lost, which the take's
        // conflict names (QuestStore.RebaseAsync), or which an older build made after that conflict is written
        // and the remote never kept. A delete that lost to a take, or that another machine's delete
        // already made. A yes to a done nothing holds any more: its done lost, or another machine's yes came
        // first. A verdict on evidence nothing waits on any more: another machine's verdict found it first, or the
        // person accepted the done as it stood. A set-up on a step another machine declined or deleted, the same set-up
        // posted first elsewhere, or one made on this machine's take that lost. A review's verdict another
        // machine's came before, or on a set-up a newer one stands beside. A closure graph's bind or submission once its quest
        // is no longer taken, or made on this machine's take that lost: the take's conflict names a lost
        // submission, and the step a submission published goes with it (QuestStore.RebaseAsync). A reopen once the submission it
        // names is gone or closed, or its step closed or lost; a rebind once another let the step go, or it closed,
        // and a publication's once another machine's take reached the step first: the person's word lets that one go.
        // A workflow choice once another machine's take started the quest first: its run is bound there, at that start. A revision
        // once another machine closed the quest first.
        QuestOperationKind.Dismissed or QuestOperationKind.Waited or QuestOperationKind.Deleted
            or QuestOperationKind.Accepted or QuestOperationKind.Evidenced
            or QuestOperationKind.SetUp or QuestOperationKind.Verdict
            or QuestOperationKind.Bound or QuestOperationKind.Submitted
            or QuestOperationKind.Reopened or QuestOperationKind.Rebound or QuestOperationKind.Chosen
            or QuestOperationKind.Revised => QuestLoss.Forgotten,
        _ => throw new ArgumentOutOfRangeException(
            nameof(kind), kind,
            "A rebase has no rule for this kind of quest operation; .claude/knowledge/quest-operations.md names every place a kind goes."),
    };
}
