using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// How the store's file is written: ahead into a log beside it (<c>-wal</c>), each commit synced in full. SQLite's rollback
/// journal made and deleted a file at every commit, 13 to 16 ms of a write; the log measured 3.8 ms with the same sync.
/// </summary>
/// <remarks>
/// The mode is the file's own once set, so every host that opens it after writes the same way, an older build's included; the sync
/// is each connection's, so every open sets it. A copy of the file is a copy of it and its log together.
/// </remarks>
internal static class StoreJournal
{
    /// <summary>The bytes the log is cut back to once a checkpoint has emptied it, so one big commit does not keep its size for good.</summary>
    internal const long LogLimit = 32L * 1024 * 1024;

    /// <summary>
    /// Set the mode and the sync on <paramref name="connection"/>, open: a store in memory keeps its own journal. Through the
    /// connection's gate, as every command is, and outside a transaction, where alone SQLite changes the mode.
    /// </summary>
    internal static Task SetAsync(SqliteConnection connection, CancellationToken ct)
    {
        var gate = ConnectionGate.For(connection);
        return gate.RunAsync(async () =>
        {
            await using var command = gate.Command();
            command.CommandText = $"PRAGMA journal_mode = WAL; PRAGMA synchronous = FULL; PRAGMA journal_size_limit = {LogLimit};";
            await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }, ct);
    }
}
