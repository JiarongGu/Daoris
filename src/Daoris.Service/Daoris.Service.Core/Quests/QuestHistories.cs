using Microsoft.Data.Sqlite;
using static Daoris.Knowledge.QuestRecordCodec;

namespace Daoris.Knowledge;

/// <summary>A quest's history as the log keeps it, and a history given to a quest an older store held only as a row.</summary>
internal sealed class QuestHistories(QuestStore store)
{
    private readonly ConnectionGate _db = store.Db;

    /// <summary>
    /// Give every quest of this store's own that the log has never seen its history, from what its row
    /// says: published when it was filed, and the move to where it stands when it last moved.
    /// </summary>
    /// <remarks>
    /// A quest is not derivable from anything, so a store from before the log is migrated rather than
    /// rebuilt (the index's rule is for what can be re-read). Checked again inside the transaction,
    /// because two hosts open one store and only one of them may write the histories.
    /// </remarks>
    internal async Task GiveHistoriesAsync(CancellationToken ct)
    {
        const string Unlogged = """
            SELECT * FROM quests
            WHERE NOT EXISTS (SELECT 1 FROM quest_log WHERE quest_log.quest = quests.id)
            ORDER BY filed, id
            """;

        await using (var probe = _db.Command())
        {
            probe.CommandText = $"SELECT EXISTS ({Unlogged})";
            if (Convert.ToInt32(await probe.ExecuteScalarAsync(ct).ConfigureAwait(false)) == 0) return;
        }

        await _db.InTransactionAsync(async (transaction, inside) =>
        {
            var unlogged = new List<Quest>();
            await using (var select = _db.Command())
            {
                select.Transaction = transaction;
                select.CommandText = Unlogged;
                await using var reader = await select.ExecuteReaderAsync(inside).ConfigureAwait(false);
                while (await reader.ReadAsync(inside).ConfigureAwait(false)) unlogged.Add(Read(reader));
            }

            foreach (var quest in unlogged) await GiveHistoryAsync(quest, transaction, inside).ConfigureAwait(false);
            return unlogged.Count;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>One quest's history, from its row — the operations that replay to exactly what it says.</summary>
    internal async Task<IReadOnlyList<QuestOperation>> GiveHistoryAsync(
        Quest quest, SqliteTransaction transaction, CancellationToken ct)
    {
        var history = new List<QuestOperation>
        {
            await store.AppendAsync(quest.Id, QuestOperationKind.Published, quest.Filed, null, quest, transaction, ct)
                .ConfigureAwait(false),
        };

        if (QuestTransitions.KindFor(quest.Status) is { } moved)
        {
            history.Add(await store.AppendAsync(quest.Id, moved, quest.Updated, quest.Note, null, transaction, ct, noteParts: quest.NoteParts)
                .ConfigureAwait(false));
        }

        return history;
    }

    /// <summary>
    /// A quest's history, in the order it replays (design §8): what a remote accepted, by its number,
    /// then what this machine has not pushed, in the order it was made. Empty for an unknown id.
    /// </summary>
    public Task<IReadOnlyList<QuestOperation>> HistoryAsync(string id, CancellationToken ct = default) =>
        _db.RunAsync(() => HistoryAsync(id, transaction: null, ct), ct);

    internal async Task<IReadOnlyList<QuestOperation>> HistoryAsync(
        string id, SqliteTransaction? transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        // None handed in keeps the one the command was made in: the connection hands a new command the transaction it
        // has open, and a caller holding one without its object (HistoryDesk's, HistoryWithinAsync) must not lose it.
        if (transaction is not null) command.Transaction = transaction;
        command.CommandText = $"""
            SELECT {QuestStore.OperationColumns} FROM quest_log WHERE quest = $id
            ORDER BY remote IS NULL, remote, position
            """;
        command.Parameters.AddWithValue("$id", id);

        var history = new List<QuestOperation>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false)) history.Add(ReadOperation(reader));
        return history;
    }

    /// <summary>
    /// A quest's history read inside a transaction the caller already holds — <see cref="HistoryDesk"/>'s, which judges a
    /// unit again where it clears it. The same read as <see cref="HistoryAsync(string, CancellationToken)"/>: the gate is
    /// reentrant within the work that holds it (SQLITETX1), and the read joins that work's transaction.
    /// </summary>
    internal Task<IReadOnlyList<QuestOperation>> HistoryWithinAsync(string id, CancellationToken ct) =>
        HistoryAsync(id, ct);
}
