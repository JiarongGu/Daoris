using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// A quest bound to what closes it: its note names the workflow run that writes its Done at its end (<see cref="RunClosure"/>), or, from
/// an older build's sync, a closure graph's digest. It moves no status.
/// </summary>
[QuestOperation("bound", QuestOperationPhase.Log)]
internal sealed class QuestBindOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.Bound;

    /// <summary>How a quest names the run that closes it, so no run is read as a graph's digest.</summary>
    public static string RunClosure(string run) => "run:" + run;

    public static bool NamesRun(string? closure) => closure?.StartsWith("run:", StringComparison.Ordinal) == true;

    /// <summary>
    /// The driver's bind at a session's first start in a workflow's run, before its agent can say <c>done</c>: once, on an open quest or one
    /// this machine holds taken, before any submission. Its agent's done is then a submission and the run's end writes its Done.
    /// </summary>
    internal async Task<QuestMove> BindAsync(QuestTx tx, string id, string run, DateTimeOffset now)
    {
        var held = await tx.FindAsync(id).ConfigureAwait(false);
        if (held is null) return new QuestMove(null, Moved: false);
        var history = await tx.HistoryAsync(id).ConfigureAwait(false);
        var standing = QuestLog.Replay(history);
        var judged = new QuestOperation(id, QuestOperationKind.Bound, tx.Machine, 0, now, RunClosure(run));
        if (!QuestLog.Applies(standing, judged)) return new QuestMove(held, Moved: false);
        if (standing!.Status == QuestStatus.Taken && QuestLog.Claim(history, tx.Machine) is not (QuestClaim.Held or QuestClaim.Unconfirmed))
        {
            return new QuestMove(held, Moved: false) { ClaimLost = true };
        }

        var operation = await tx.AppendAsync(judged).ConfigureAwait(false);
        var moved = QuestLog.Step(standing, operation)!;
        await tx.WriteCacheAsync(moved).ConfigureAwait(false);
        return new QuestMove(moved, Moved: true);
    }

    /// <summary>The graph's digest, as its note.</summary>
    public void WritePayload(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Note is not null) writer.WriteString("note", operation.Note);
        QuestRecordCodec.WriteNoteParts(writer, operation.Note, operation.NoteParts);
        writer.WriteEndObject();
    }

    public QuestOperation ReadPayload(QuestOperation read, JsonElement payload) => read;

    /// <summary>
    /// A closure graph's submission, made by the machine holding the take, judged by the replay's rule
    /// inside the write as every move is. A submission nothing holds publishes the chain's next step in the same transaction,
    /// as an unheld done does, and is sealed by it.
    /// </summary>
    internal async Task<QuestMove> TakersMoveAsync(QuestTx tx, string id, QuestOperation judged, DateTimeOffset now)
    {
        var held = await tx.FindAsync(id).ConfigureAwait(false);
        if (held is null) return new QuestMove(null, Moved: false);

        var history = await tx.HistoryAsync(id).ConfigureAwait(false);
        var standing = QuestLog.Replay(history);
        if (!QuestLog.Applies(standing, judged)) return new QuestMove(held, Moved: false);
        if (QuestLog.OnALostTake(standing, history, judged)) return new QuestMove(held, Moved: false) { ClaimLost = true };

        // Only the machine holding the take submits: another machine's take is its own to finish.
        if (QuestLog.Claim(history, tx.Machine) is not (QuestClaim.Held or QuestClaim.Unconfirmed)) return new QuestMove(held, Moved: false);

        // Sealed: a submission whose successor stands takes no later one, so the step never stands on work replaced
        // under it. Only the take's machine submits, so this machine's store is where the seal is judged.
        if (judged.Kind == QuestOperationKind.Submitted && await tx.SealingAsync(standing!).ConfigureAwait(false) is { } successor)
        {
            return new QuestMove(held, Moved: false) { SealedBy = successor.Id };
        }

        // A step its source's reopen holds goes no further; the person chooses whether it goes on.
        if (judged.Kind == QuestOperationKind.Submitted && standing!.SourceReopened is not null)
        {
            return new QuestMove(held, Moved: false) { SourceReopened = true };
        }

        var operation = await tx.AppendAsync(judged).ConfigureAwait(false);
        var moved = QuestLog.Step(standing, operation)!;
        await tx.WriteCacheAsync(moved).ConfigureAwait(false);

        var followUp = judged.Kind == QuestOperationKind.Submitted && !moved.Held
            ? await tx.PublishNextAsync(moved, now).ConfigureAwait(false)
            : null;
        return new QuestMove(moved, Moved: true, followUp);
    }
}
