namespace Daoris.Knowledge;

/// <summary>
/// The references each session read, and which of them have since moved (the memory coordinator design §3): kept per session, told once
/// to the line the session ends, and marked moved in the transaction that settles the row that moved them.
/// </summary>
internal sealed class MemoryOpReads(ConnectionGate db)
{
    /// <summary>The newest references one session's read set keeps.</summary>
    public const int PerSession = 256;

    /// <summary>The newest references every session's read sets keep together.</summary>
    public const int InAll = 65_536;

    public const string Schema = """
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

    /// <summary>The sessions of the line <c>$session</c> ends: it and those it continues by <c>From</c>.</summary>
    private const string Line = """
        WITH RECURSIVE chain(id) AS (
          SELECT $session
          UNION SELECT s.from_session FROM sessions s JOIN chain c ON s.id = c.id WHERE s.from_session IS NOT NULL)
        """;

    /// <summary>What <paramref name="session"/> read; a reference read again is kept at its new revision, as yet unmoved.</summary>
    public Task KeepAsync(string session, IReadOnlyList<MemoryRead> reads, CancellationToken ct) =>
        db.InTransactionAsync(async (_, inside) =>
        {
            foreach (var read in reads)
            {
                await using var keep = db.Command();
                keep.CommandText = "INSERT OR REPLACE INTO memory_reads (session, repository, entry, revision) VALUES ($session, $repository, $entry, $revision)";
                keep.Parameters.AddWithValue("$session", session);
                keep.Parameters.AddWithValue("$repository", read.Reference.Repository);
                keep.Parameters.AddWithValue("$entry", read.Reference.Entry);
                keep.Parameters.AddWithValue("$revision", read.Revision);
                await keep.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            }

            await using var trim = db.Command();
            trim.CommandText = $"""
                DELETE FROM memory_reads WHERE session = $session
                  AND seq < (SELECT min(seq) FROM (SELECT seq FROM memory_reads WHERE session = $session ORDER BY seq DESC LIMIT {PerSession}));
                DELETE FROM memory_reads WHERE seq < (SELECT min(seq) FROM (SELECT seq FROM memory_reads ORDER BY seq DESC LIMIT {InAll}));
                """;
            trim.Parameters.AddWithValue("$session", session);
            return await trim.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
        }, ct);

    /// <summary>
    /// The references the line <paramref name="session"/> ends read that moved since, each told once: a continuation is told of what the
    /// parts before it read.
    /// </summary>
    public Task<IReadOnlyList<MemoryReference>> TellAsync(string session, CancellationToken ct) =>
        db.InTransactionAsync<IReadOnlyList<MemoryReference>>(async (_, inside) =>
        {
            var moved = new List<MemoryReference>();
            await using (var read = db.Command())
            {
                read.CommandText = $"""
                    {Line}
                    SELECT repository, entry FROM memory_reads
                    WHERE session IN (SELECT id FROM chain) AND moved = 1 AND told = 0 ORDER BY seq
                    """;
                read.Parameters.AddWithValue("$session", session);
                await using var reader = await read.ExecuteReaderAsync(inside).ConfigureAwait(false);
                while (await reader.ReadAsync(inside).ConfigureAwait(false)) moved.Add(new(reader.GetString(0), reader.GetString(1)));
            }

            await using var told = db.Command();
            told.CommandText = $"""
                {Line}
                UPDATE memory_reads SET told = 1 WHERE session IN (SELECT id FROM chain) AND moved = 1 AND told = 0
                """;
            told.Parameters.AddWithValue("$session", session);
            await told.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            return [.. moved.Distinct()];
        }, ct);

    /// <summary>
    /// What the line <paramref name="session"/> ends read, each reference once at its newest read, newest first and at most
    /// <see cref="PerSession"/>, with the session that read it.
    /// </summary>
    public Task<IReadOnlyList<(string Session, MemoryRead Read)>> OfLineAsync(string session, CancellationToken ct) =>
        db.RunAsync<IReadOnlyList<(string, MemoryRead)>>(async () =>
        {
            await using var read = db.Command();
            read.CommandText = $"""
                {Line}
                SELECT session, repository, entry, revision FROM memory_reads
                WHERE session IN (SELECT id FROM chain) ORDER BY seq DESC
                """;
            read.Parameters.AddWithValue("$session", session);
            var reads = new List<(string, MemoryRead)>();
            var seen = new HashSet<MemoryReference>();
            await using var reader = await read.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (reads.Count < PerSession && await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var reference = new MemoryReference(reader.GetString(1), reader.GetString(2));
                if (seen.Add(reference)) reads.Add((reader.GetString(0), new MemoryRead(reference, reader.GetInt64(3))));
            }

            return reads;
        }, ct);

    /// <summary>
    /// Marks every read of the entry an applied row moved past the revision it is read at, and of each entry a replacement ended at or
    /// before the revision its approver read, in the settle's transaction.
    /// </summary>
    public async Task MovedAsync(string id, string? entry, long revision, CancellationToken inside)
    {
        await using var mark = db.Command();
        mark.CommandText = """
            UPDATE memory_reads SET moved = 1
            WHERE moved = 0 AND revision < $revision
              AND repository = (SELECT repository FROM memory_ops WHERE id = $id)
              AND entry = COALESCE($entry, (SELECT COALESCE(entry, target) FROM memory_ops WHERE id = $id));
            UPDATE memory_reads SET moved = 1
            WHERE moved = 0 AND EXISTS (
              SELECT 1 FROM memory_ops o, json_each(o.predecessors) p
              WHERE o.id = $id AND o.kind = 'supersede' AND o.repository = memory_reads.repository
                AND json_extract(p.value, '$.entry') = memory_reads.entry
                AND memory_reads.revision <= json_extract(p.value, '$.revision'));
            """;
        mark.Parameters.AddWithValue("$id", id);
        mark.Parameters.AddWithValue("$entry", (object?)entry ?? DBNull.Value);
        mark.Parameters.AddWithValue("$revision", revision);
        await mark.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
    }
}
