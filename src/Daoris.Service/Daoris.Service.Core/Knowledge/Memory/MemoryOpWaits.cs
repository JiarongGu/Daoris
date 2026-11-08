using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// What waits on the person in the memory log: a row per session (ends and replacements awaiting their approver, a stopped session's
/// staged rows), then a row per end the sweep wrote, which names no session and so is kept by its own id, repository and entry.
/// </summary>
internal sealed class MemoryOpWaits(ConnectionGate db)
{
    /// <summary>The waits, the sessions' oldest first, then the sweep's ends oldest first.</summary>
    public async Task<IReadOnlyList<MemoryWait>> ReadAsync(CancellationToken ct)
    {
        var found = new List<(string Session, MemoryWait Wait)>();
        await using (var command = db.Command())
        {
            command.CommandText = """
                SELECT m.session, m.state, m.kind, MIN(m.at), COUNT(*), MIN(m.rowid)
                FROM memory_ops m JOIN sessions s ON s.id = m.session
                WHERE (m.state = 'awaiting' AND m.kind IN ('end', 'supersede'))
                   OR (m.state = 'staged' AND s.state NOT IN (SELECT value FROM json_each($running)))
                GROUP BY m.session, m.state, m.kind
                ORDER BY MIN(m.rowid)
                """;
            command.Parameters.AddWithValue("$running", JsonSerializer.Serialize(MemoryOpStore.Running));
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                var (state, kind, count) = (reader.GetString(1), reader.GetString(2), (int)reader.GetInt64(4));
                var since = Moments.ReadRequired(reader.GetString(3));
                var wait = state == MemoryOpState.Staged ? new MemoryWait(reader.GetString(0), 0, 0, count, since)
                    : kind == MemoryOpKind.Supersede ? new MemoryWait(reader.GetString(0), 0, count, 0, since)
                    : new MemoryWait(reader.GetString(0), count, 0, 0, since);
                found.Add((reader.GetString(0), wait));
            }
        }

        var waits = found.GroupBy(each => each.Session).Select(group => new MemoryWait(
            group.Key, group.Sum(each => each.Wait.Ends), group.Sum(each => each.Wait.Replacements), group.Sum(each => each.Wait.Staged),
            group.Min(each => each.Wait.Since))).ToList();

        await using var sweeps = db.Command();
        sweeps.CommandText = """
            SELECT id, repository, target, revision, text, at FROM memory_ops
            WHERE state = 'awaiting' AND kind = 'end' AND session IS NULL AND target IS NOT NULL
            ORDER BY rowid
            """;
        await using var ends = await sweeps.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await ends.ReadAsync(ct).ConfigureAwait(false))
        {
            var reason = ends.IsDBNull(4) ? null : MemoryEndToken.Strip(ends.GetString(4));
            var end = new MemoryEndWait(ends.GetString(0), ends.GetString(1), ends.GetString(2), ends.IsDBNull(3) ? null : ends.GetInt64(3), reason);
            waits.Add(new MemoryWait(null, 1, 0, 0, Moments.ReadRequired(ends.GetString(5)), end));
        }

        return waits;
    }
}
