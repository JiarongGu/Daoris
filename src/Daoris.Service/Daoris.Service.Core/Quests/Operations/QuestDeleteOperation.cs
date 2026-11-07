using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// An open quest deleted. A quest that may have left this machine, or whose history a remote numbered, is tombstoned by this
/// operation, which the next pass carries, because a copy elsewhere would otherwise come back on the next fetch. Any other quest simply
/// goes, its history with it.
/// </summary>
[QuestOperation("deleted", QuestOperationPhase.Log)]
internal sealed class QuestDeleteOperation : IQuestOperation
{
    public QuestOperationKind Kind => QuestOperationKind.Deleted;

    public void WritePayload(Utf8JsonWriter writer, QuestOperation operation)
    {
        writer.WriteStartObject();
        if (operation.Note is not null) writer.WriteString("note", operation.Note);
        QuestRecordCodec.WriteNoteParts(writer, operation.Note, operation.NoteParts);
        writer.WriteEndObject();
    }

    public QuestOperation ReadPayload(QuestOperation read, JsonElement payload) => read;

    /// <summary>
    /// Delete an open quest, judged against the replayed history inside the write, as every move is: only an open quest goes. Whether
    /// anything outside this store stands on it, a session or a waiting quest, is the exchange's judgement, made before this is asked.
    /// </summary>
    /// <param name="travels">Whether the quest may have left this machine: its circle has a remote and its receiver is joined.</param>
    internal async Task<QuestDeletion> DeleteAsync(QuestTx tx, string id, DateTimeOffset now, bool travels)
    {
        var history = await tx.HistoryAsync(id).ConfigureAwait(false);
        var quest = history.Count > 0
            ? QuestLog.Replay(history)
            // A row the log never saw has never been pushed: what it says is all there is of it.
            : await tx.FindAsync(id).ConfigureAwait(false);
        if (quest is null) return new QuestDeletion(null, Deleted: false);
        if (quest.Status != QuestStatus.Open) return new QuestDeletion(quest, Deleted: false);

        // A push reads the log, so a quest with no history has not been pushed, and goes like a local one.
        var tombstoned = history.Count > 0 && (travels || history.Any(operation => operation.Number is not null));
        await using (var command = tx.Command())
        {
            command.CommandText = tombstoned
                ? "DELETE FROM quests WHERE id = $id"
                : "DELETE FROM quest_log WHERE quest = $id; DELETE FROM quest_forwards WHERE quest = $id; DELETE FROM quests WHERE id = $id";
            command.Parameters.AddWithValue("$id", id);
            await command.ExecuteNonQueryAsync(tx.Ct).ConfigureAwait(false);
        }

        if (tombstoned) await tx.AppendAsync(new QuestOperation(id, QuestOperationKind.Deleted, tx.Machine, 0, now)).ConfigureAwait(false);

        return new QuestDeletion(quest, Deleted: true, tombstoned);
    }
}
