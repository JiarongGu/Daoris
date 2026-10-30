namespace Daoris.Knowledge;

/// <summary>A reference a session read, at the revision it read.</summary>
public sealed record MemoryRead(MemoryReference Reference, long Revision);

/// <summary>
/// The read set (the memory coordinator design §3): who read which reference at which revision, kept on this machine and bounded, so a
/// running session is told once that an entry it read moved. A reference trimmed from it is never told.
/// </summary>
public sealed partial class MemoryOpStore
{
    /// <summary>The newest references one session's set keeps.</summary>
    public const int ReadsPerSession = 256;

    /// <summary>The newest references every session's sets keep together.</summary>
    public const int ReadsInAll = 65_536;

    private const string ReadSetSchema = """
        CREATE TABLE IF NOT EXISTS memory_reads (
          seq        INTEGER PRIMARY KEY AUTOINCREMENT,
          session    TEXT NOT NULL,
          repository TEXT NOT NULL,
          entry      TEXT NOT NULL,
          revision   INTEGER NOT NULL,
          moved      INTEGER NOT NULL DEFAULT 0,
          told       INTEGER NOT NULL DEFAULT 0,
          UNIQUE (session, repository, entry)
        );
        CREATE INDEX IF NOT EXISTS memory_reads_entry ON memory_reads (repository, entry);
        """;

    /// <summary>What <paramref name="session"/> read; a reference read again is kept at its new revision, as yet unmoved.</summary>
    public Task KeepReadsAsync(string session, IReadOnlyList<MemoryRead> reads, CancellationToken ct = default) =>
        _db.InTransactionAsync(async (_, inside) =>
        {
            foreach (var read in reads)
            {
                await using var keep = _db.Command();
                keep.CommandText = "INSERT OR REPLACE INTO memory_reads (session, repository, entry, revision) VALUES ($session, $repository, $entry, $revision)";
                keep.Parameters.AddWithValue("$session", session);
                keep.Parameters.AddWithValue("$repository", read.Reference.Repository);
                keep.Parameters.AddWithValue("$entry", read.Reference.Entry);
                keep.Parameters.AddWithValue("$revision", read.Revision);
                await keep.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            }

            await using var trim = _db.Command();
            trim.CommandText = $"""
                DELETE FROM memory_reads WHERE session = $session
                  AND seq < (SELECT min(seq) FROM (SELECT seq FROM memory_reads WHERE session = $session ORDER BY seq DESC LIMIT {ReadsPerSession}));
                DELETE FROM memory_reads WHERE seq < (SELECT min(seq) FROM (SELECT seq FROM memory_reads ORDER BY seq DESC LIMIT {ReadsInAll}));
                """;
            trim.Parameters.AddWithValue("$session", session);
            return await trim.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
        }, ct);

    /// <summary>The references <paramref name="session"/> read that moved since, each told once.</summary>
    public Task<IReadOnlyList<MemoryReference>> TellAsync(string session, CancellationToken ct = default) =>
        _db.InTransactionAsync<IReadOnlyList<MemoryReference>>(async (_, inside) =>
        {
            var moved = new List<MemoryReference>();
            await using (var read = _db.Command())
            {
                read.CommandText = "SELECT repository, entry FROM memory_reads WHERE session = $session AND moved = 1 AND told = 0 ORDER BY seq";
                read.Parameters.AddWithValue("$session", session);
                await using var reader = await read.ExecuteReaderAsync(inside).ConfigureAwait(false);
                while (await reader.ReadAsync(inside).ConfigureAwait(false)) moved.Add(new(reader.GetString(0), reader.GetString(1)));
            }

            await using var told = _db.Command();
            told.CommandText = "UPDATE memory_reads SET told = 1 WHERE session = $session AND moved = 1 AND told = 0";
            told.Parameters.AddWithValue("$session", session);
            await told.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            return moved;
        }, ct);

    /// <summary>Marks every read of the entry an applied row moved past the revision it was read at, in the settle's transaction.</summary>
    private async Task MovedAsync(string id, string? entry, long revision, CancellationToken inside)
    {
        await using var mark = _db.Command();
        mark.CommandText = """
            UPDATE memory_reads SET moved = 1
            WHERE moved = 0 AND revision < $revision
              AND repository = (SELECT repository FROM memory_ops WHERE id = $id)
              AND entry = COALESCE($entry, (SELECT COALESCE(entry, target) FROM memory_ops WHERE id = $id))
            """;
        mark.Parameters.AddWithValue("$id", id);
        mark.Parameters.AddWithValue("$entry", (object?)entry ?? DBNull.Value);
        mark.Parameters.AddWithValue("$revision", revision);
        await mark.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
    }
}
