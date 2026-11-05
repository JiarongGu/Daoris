using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// How the store's file is written (D236): ahead into a log beside it (<c>-wal</c>), each commit synced in full. SQLite's rollback
/// journal made and deleted a file at every commit, 13 to 16 ms of a write; the log measured 3.8 ms with the same sync.
/// </summary>
/// <remarks>
/// The mode is the file's own once set, so every host that opens it after writes the same way, an older build's included; the sync
/// is each connection's, so every open sets it. A copy of the file is a copy of it and its log together.
/// </remarks>
internal static class StoreJournal
{
    /// <summary>Set the mode and the sync on <paramref name="connection"/>, open: a store in memory keeps its own journal.</summary>
    internal static async Task SetAsync(SqliteConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = FULL;";
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
