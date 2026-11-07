using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// A taken quest bound to the closure graph that closes it: its note names the graph's digest. It moves no status.
/// No door of this build writes one: a bound quest arrives through a sync from an older build, and the log reads and replays it.
/// </summary>
[QuestOperation("bound", QuestOperationPhase.Log)]
internal sealed class QuestBindOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.Bound;

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
        if (history.Count == 0) history = await tx.GiveHistoryAsync(held).ConfigureAwait(false);
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
