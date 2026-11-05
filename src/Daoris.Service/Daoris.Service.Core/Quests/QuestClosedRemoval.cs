namespace Daoris.Knowledge;

/// <summary>
/// The one remover of a closed quest. A Done or Declined quest nobody reads any more goes from this machine: where it may have travelled
/// (its circle has a remote and its receiver is joined, or a remote numbered one of its operations) a <see cref="QuestOperationKind.Deleted"/>
/// tombstone is kept in the log and carried by the next pass, so a peer or a synced place drops its copy and no fetch brings it back; where it
/// never left, the row and its whole log simply go. An open or taken quest is never removed here: <see cref="QuestDeleteOperation"/> is the
/// person's delete of an open one.
/// </summary>
internal static class QuestClosedRemoval
{
    public static async Task<QuestDeletion> RemoveAsync(QuestTx tx, string id, DateTimeOffset now, bool travels)
    {
        var history = await tx.HistoryAsync(id).ConfigureAwait(false);
        var quest = history.Count > 0 ? QuestLog.Replay(history) : await tx.FindAsync(id).ConfigureAwait(false);
        if (quest is null) return new QuestDeletion(null, Deleted: false);
        if (quest.Status is not (QuestStatus.Done or QuestStatus.Declined)) return new QuestDeletion(quest, Deleted: false);

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
