using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// What a bulk write leaves the store's file: a log that stays at the size of the biggest commit, and pages a large delete freed that the file
/// keeps. At the store's open, a quiet point, the log is truncated once fully checkpointed, and the file is vacuumed where it holds free
/// pages worth returning (at least <see cref="FreeFloor"/> bytes and a quarter of the file). A host that cannot take the exclusive hold it needs
/// leaves both for a later open.
/// </summary>
internal static class StoreCompaction
{
    /// <summary>The free bytes below which a vacuum is not worth its rewrite of the file.</summary>
    internal const long FreeFloor = 8L * 1024 * 1024;

    internal static Task RunAsync(SqliteConnection connection, CancellationToken ct)
    {
        var gate = ConnectionGate.For(connection);
        return gate.RunAsync(async () =>
        {
            try
            {
                await using var command = gate.Command();
                command.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
                command.CommandText = "SELECT page_count * page_size, freelist_count * page_size FROM pragma_page_count, pragma_page_size, pragma_freelist_count;";
                long total, free;
                await using (var read = await command.ExecuteReaderAsync(ct).ConfigureAwait(false))
                {
                    await read.ReadAsync(ct).ConfigureAwait(false);
                    (total, free) = (read.GetInt64(0), read.GetInt64(1));
                }

                if (free < FreeFloor || free * 4 < total) return;
                command.CommandText = "VACUUM; PRAGMA wal_checkpoint(TRUNCATE);";
                await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
            }
            catch (SqliteException)
            {
                // Another host holds the file open for a write: the next open compacts it.
            }
        }, ct);
    }
}
