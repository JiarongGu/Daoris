namespace Daoris.Knowledge;

/// <summary>
/// The waits the memory log keeps for no session: an end the sweep wrote, or one whose session is gone, which has no session to
/// join a row of, so each is a wait of its own, kept by its operation, the entry it ends and why.
/// </summary>
internal sealed class MemoryOpWaits(ConnectionGate db)
{
    /// <summary>Each awaiting end the sweep wrote, or whose session is gone, oldest first.</summary>
    public async Task<IReadOnlyList<MemoryWait>> SweepsAsync(CancellationToken ct)
    {
        await using var command = db.Command();
        command.CommandText = """
            SELECT id, repository, target, revision, text, at FROM memory_ops
            WHERE state = 'awaiting' AND kind = 'end' AND target IS NOT NULL
              AND (session IS NULL OR (carried IS NULL AND session NOT IN (SELECT id FROM sessions)))
            ORDER BY rowid
            """;
        var waits = new List<MemoryWait>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
        {
            var reason = reader.IsDBNull(4) ? null : MemoryEndToken.Strip(reader.GetString(4));
            var end = new MemoryEndWait(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.IsDBNull(3) ? null : reader.GetInt64(3), reason);
            waits.Add(new MemoryWait(null, 1, 0, 0, Moments.ReadRequired(reader.GetString(5)), end));
        }

        return waits;
    }
}
