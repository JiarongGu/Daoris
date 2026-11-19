using Microsoft.Data.Sqlite;
using static Daoris.Knowledge.QuestRecordCodec;

namespace Daoris.Knowledge;

/// <summary>A quest's history as the log keeps it.</summary>
internal sealed class QuestHistories(QuestStore store)
{
    private readonly ConnectionGate _db = store.Db;

    /// <summary>
    /// A quest's history, in the order it replays: what a remote accepted, by its number,
    /// then what this machine has not pushed, in the order it is made. Empty for an unknown id.
    /// </summary>
    public Task<IReadOnlyList<QuestOperation>> HistoryAsync(string id, CancellationToken ct = default) =>
        _db.RunAsync(() => HistoryAsync(id, transaction: null, ct), ct);

    internal async Task<IReadOnlyList<QuestOperation>> HistoryAsync(
        string id, SqliteTransaction? transaction, CancellationToken ct)
    {
        await using var command = _db.Command();
        // None handed in keeps the one the command is made in: the connection hands a new command the transaction it
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
    /// reentrant within the work that holds it, and the read joins that work's transaction.
    /// </summary>
    internal Task<IReadOnlyList<QuestOperation>> HistoryWithinAsync(string id, CancellationToken ct) =>
        HistoryAsync(id, ct);
}
