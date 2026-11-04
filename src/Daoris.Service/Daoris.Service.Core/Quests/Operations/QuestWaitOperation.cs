using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// Its taker waits on a question it asked another repository (D79): its note names the quest it waits on. It moves no status; the
/// quest stays taken, since its work in progress is in the taker's tree. A wait made on a take that lost goes with the take, which the
/// rebase names on the take's conflict.
/// </summary>
[QuestOperation("waited", QuestOperationPhase.Log)]
internal sealed class QuestWaitOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.Waited;

    /// <summary>The question it waits on, as its note.</summary>
    public void WritePayload(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Note is not null) writer.WriteString("note", operation.Note);
        QuestStore.WriteNoteParts(writer, operation.Note, operation.NoteParts);
        writer.WriteEndObject();
    }

    public QuestOperation ReadPayload(QuestOperation read, JsonElement payload) => read;

    /// <summary>A wait on <paramref name="on"/>, judged by the replay's rule inside the write. It moves no status.</summary>
    /// <returns>
    /// The quest as it now stands and whether this call marked it; a null quest means no such id. Refused with
    /// <see cref="QuestMove.ClaimLost"/> where this machine's take on the quest lost (WAITCLAIM2).
    /// </returns>
    internal async Task<QuestMove> WaitAsync(QuestTx tx, string id, string on, DateTimeOffset now)
    {
        var history = await tx.HistoryAsync(id).ConfigureAwait(false);
        if (QuestLog.Replay(history) is not { } quest) return new QuestMove(await tx.FindAsync(id).ConfigureAwait(false), Moved: false);

        // Only a taken quest waits (QuestLog.Applies), and a wait the log held but the quest did not show would be a move nobody could see.
        if (quest.Status != QuestStatus.Taken || string.IsNullOrWhiteSpace(on)) return new QuestMove(quest, Moved: false);

        // A wait on the winner's take, made here after the pass that found this machine's take lost (WAITCLAIM2).
        var judged = new QuestOperation(id, QuestOperationKind.Waited, tx.Machine, 0, now, on);
        if (QuestLog.OnALostTake(quest, history, judged)) return new QuestMove(quest, Moved: false) { ClaimLost = true };

        var waiting = QuestLog.Step(quest, await tx.AppendAsync(judged).ConfigureAwait(false))!;
        await tx.WriteCacheAsync(waiting).ConfigureAwait(false);
        return new QuestMove(waiting, Moved: true);
    }
}
