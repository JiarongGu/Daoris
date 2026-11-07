using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// The person's reopen of a sealed submission, and their word to go on with a step it held. A reopen names the
/// submission by its machine and sequence, on the quest that submitted it and on the step that submission published; it moves no status.
/// </summary>
[QuestOperation("reopened", QuestOperationPhase.Log)]
internal sealed class QuestReopenOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.Reopened;

    /// <summary>The person's words beside their parts, and the submission it reopens.</summary>
    public void WritePayload(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Note is not null) writer.WriteString("note", operation.Note);
        QuestRecordCodec.WriteNoteParts(writer, operation.Note, operation.NoteParts);
        if (operation.Submission is { } submission) QuestRecordCodec.WriteRef(writer, "submission", submission);
        writer.WriteEndObject();
    }

    public QuestOperation ReadPayload(QuestOperation read, JsonElement payload) => read with
    {
        Submission = payload.TryGetProperty("submission", out var submission) ? QuestRecordCodec.ReadRef(submission) : null,
    };

    /// <summary>
    /// The person's reopen of a sealed submission, in one write on the machine holding the take: a
    /// <see cref="QuestOperationKind.Reopened"/> on the quest, which leaves it taken with no current submission, and one on the step
    /// that submission published, which withdraws it while open and holds it while taken.
    /// </summary>
    /// <remarks>
    /// Judged inside the write, so a close or a <c>ready</c> landing a moment earlier is seen: a close first leaves the quest Done,
    /// and the reopen is refused; a reopen first leaves the close naming a submission that is current no more, and the close door
    /// refuses it as stale. A step closed on the submission refuses the reopen: a reopen takes no close back, and a push made is
    /// never retracted.
    /// </remarks>
    internal async Task<QuestReopening> ReopenAsync(
        QuestTx tx, string id, QuestOperationRef submission, string? note, DateTimeOffset now)
    {
        var held = await tx.FindAsync(id).ConfigureAwait(false);
        if (held is null) return new QuestReopening(QuestReopenRefusal.NotFound, null, null);
        var history = await tx.HistoryAsync(id).ConfigureAwait(false);
        if (history.Count == 0) history = await tx.GiveHistoryAsync(held).ConfigureAwait(false);
        var standing = QuestLog.Replay(history)!;

        QuestReopening Refused(QuestReopenRefusal refusal, Quest? step = null) => new(refusal, standing, step);
        if (standing.Status != QuestStatus.Taken) return Refused(QuestReopenRefusal.NotTaken);
        // Only the take's machine submits, so only its store judges the seal.
        if (QuestLog.OnALostTake(standing, history, new QuestOperation(id, QuestOperationKind.Done, tx.Machine, 0, now))
            || QuestLog.Claim(history, tx.Machine) is not (QuestClaim.Held or QuestClaim.Unconfirmed))
        {
            return Refused(QuestReopenRefusal.NotHeld);
        }

        // The quest's own current submission, never one it grew from: that is its source's to reopen.
        if (standing.Submission is not { } current || current.Machine != submission.Machine || current.Sequence != submission.Sequence)
        {
            return Refused(QuestReopenRefusal.NotCurrent);
        }

        var step = await tx.SuccessorAsync(standing).ConfigureAwait(false);
        if (step is { Status: QuestStatus.Done or QuestStatus.Declined } && QuestSubmitOperation.Grows(step, submission))
        {
            return Refused(QuestReopenRefusal.StepClosed, step);
        }

        if (step is null || await tx.SealingAsync(standing).ConfigureAwait(false) is null)
        {
            return Refused(QuestReopenRefusal.NotSealed, step);
        }

        var operation = await tx.AppendAsync(
            new QuestOperation(id, QuestOperationKind.Reopened, tx.Machine, 0, now, note, Submission: submission)).ConfigureAwait(false);
        var reopened = QuestLog.Step(standing, operation)!;
        await tx.WriteCacheAsync(reopened).ConfigureAwait(false);

        // The step it published, in the same write: withdrawn while open, held while taken, by the same submission's name.
        var stepStanding = QuestLog.Replay(await tx.HistoryAsync(step.Id).ConfigureAwait(false));
        var onStep = await tx.AppendAsync(
            new QuestOperation(step.Id, QuestOperationKind.Reopened, tx.Machine, 0, now, Submission: submission)).ConfigureAwait(false);
        var withdrawn = QuestLog.Step(stepStanding, onStep)!;
        await tx.WriteCacheAsync(withdrawn).ConfigureAwait(false);
        return new QuestReopening(QuestReopenRefusal.None, reopened, withdrawn);
    }

    /// <summary>
    /// The person's word to go on with a step its source's reopen held: a <see cref="QuestOperationKind.Rebound"/>
    /// naming the source's current submission, once that is unheld; and in the same write, a <see cref="QuestOperationKind.Reopened"/> on
    /// the step's own successor, which the submission the rebind cleared had published.
    /// </summary>
    internal async Task<QuestReopening> ContinueAsync(QuestTx tx, string id, DateTimeOffset now)
    {
        var held = await tx.FindAsync(id).ConfigureAwait(false);
        if (held is null) return new QuestReopening(QuestReopenRefusal.NotFound, null, null);
        var standing = QuestLog.Replay(await tx.HistoryAsync(id).ConfigureAwait(false)) ?? held;
        if (standing is not { Status: QuestStatus.Open or QuestStatus.Taken, SourceReopened: not null })
        {
            return new QuestReopening(QuestReopenRefusal.NotWithdrawn, standing, null);
        }

        // The go-on clears the step's own submission, and a successor already closed on it is not reopened (a reopen takes no close back),
        // so the step's next submission would never be sealed. Refused as the person's reopen of it is.
        if (standing.Submission is { } closedOn
            && await tx.SuccessorAsync(standing).ConfigureAwait(false) is { Status: QuestStatus.Done or QuestStatus.Declined } closed
            && QuestSubmitOperation.Grows(closed, new QuestOperationRef(closedOn.Machine, closedOn.Sequence)))
        {
            return new QuestReopening(QuestReopenRefusal.StepClosed, standing, closed);
        }

        // The source as this store holds it: its current submission, unheld, is what the step grows from now. Another machine's
        // submission reaches this store by sync, and the word waits for it.
        var source = standing.Parent is { Length: > 0 } parent ? await tx.FindAsync(parent).ConfigureAwait(false) : null;
        if (source is not { Status: QuestStatus.Taken, Submission: { } current, Held: false })
        {
            return new QuestReopening(QuestReopenRefusal.SourceNotReady, standing, source);
        }

        var rebound = await tx.ReboundInAsync(id, new QuestOperationRef(current.Machine, current.Sequence), whileOpen: false, now)
            .ConfigureAwait(false);
        if (rebound is null) return new QuestReopening(QuestReopenRefusal.SourceNotReady, standing, source);

        // The rebind cleared the step's own submission, so the successor that submission published is reopened in the same write, as the
        // person's reopen of it would be: its next submission then rebinds a withdrawn one, and a held one waits on the person's word, so
        // the step's close is never left unsealed.
        var successor = standing.Submission is { } cleared
            ? await ReopenSuccessorAsync(tx, standing, new QuestOperationRef(cleared.Machine, cleared.Sequence), now).ConfigureAwait(false)
            : null;
        return new QuestReopening(QuestReopenRefusal.None, rebound, source) { Successor = successor };
    }

    /// <summary>
    /// A <see cref="QuestOperationKind.Reopened"/> naming <paramref name="submission"/> on the chain's next step after
    /// <paramref name="quest"/>, judged by the replay's rule: the step as it then stands, withdrawn or held, or null where none stands on
    /// that submission (none published, closed, or already reopened).
    /// </summary>
    private static async Task<Quest?> ReopenSuccessorAsync(QuestTx tx, Quest quest, QuestOperationRef submission, DateTimeOffset now)
    {
        if (await tx.SuccessorAsync(quest).ConfigureAwait(false) is not { } step) return null;
        var standing = QuestLog.Replay(await tx.HistoryAsync(step.Id).ConfigureAwait(false));
        var judged = new QuestOperation(step.Id, QuestOperationKind.Reopened, tx.Machine, 0, now, Submission: submission);
        if (!QuestLog.Applies(standing, judged)) return null;
        var reopened = QuestLog.Step(standing, await tx.AppendAsync(judged).ConfigureAwait(false))!;
        await tx.WriteCacheAsync(reopened).ConfigureAwait(false);
        return reopened;
    }
}
