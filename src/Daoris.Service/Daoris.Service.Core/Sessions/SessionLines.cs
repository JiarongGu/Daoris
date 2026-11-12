namespace Daoris.Knowledge;

/// <summary>
/// A record's conversation, its <c>line</c>: given as the record opens and never moved, so a conversation longer than any walk keeps its
/// id. The ledger's step gives it once to each record an older ledger kept without one, from the walk back by <c>from</c> those
/// ledgers keyed a line by; the step stays until no supported ledger holds a record without a line.
/// </summary>
internal static class SessionLines
{
    /// <summary>
    /// The line a record opens on: that of the part it continues where this machine holds it, the id of one it does not hold, else its
    /// quest's for a driven part, else its own. A teammate's part is no part of a line here, and is continued by none.
    /// </summary>
    public static async Task<string> OfNewAsync(ConnectionGate db, string id, string? from, string? quest, SessionKind kind, CancellationToken ct)
    {
        if (from is { Length: > 0 } && IsLocal(from))
        {
            await using var command = db.Command();
            command.CommandText = "SELECT line, origin FROM sessions WHERE id = $from";
            command.Parameters.AddWithValue("$from", from);
            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            if (!await reader.ReadAsync(ct).ConfigureAwait(false)) return from;
            if (reader.IsDBNull(1)) return reader.IsDBNull(0) ? from : reader.GetString(0);
        }

        return kind == SessionKind.Driven && quest is { Length: > 0 } ? quest : id;
    }

    /// <summary>The ledger's step: each record of this machine kept without a line given the one the walk back keyed it by, once.</summary>
    public static Task BackfillAsync(ConnectionGate db, CancellationToken ct) => db.InTransactionAsync<int>(async (_, token) =>
    {
        await using (var probe = db.Command())
        {
            probe.CommandText = "SELECT EXISTS (SELECT 1 FROM sessions WHERE line IS NULL AND origin IS NULL)";
            if (Convert.ToInt64(await probe.ExecuteScalarAsync(token).ConfigureAwait(false)) == 0) return 0;
        }

        var records = new Dictionary<string, Walked>(StringComparer.Ordinal);
        await using (var read = db.Command())
        {
            read.CommandText = "SELECT id, from_session, quest, kind, origin, line FROM sessions";
            await using var reader = await read.ExecuteReaderAsync(token).ConfigureAwait(false);
            while (await reader.ReadAsync(token).ConfigureAwait(false))
            {
                records[reader.GetString(0)] = new Walked(
                    reader.IsDBNull(1) ? null : reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2),
                    !reader.IsDBNull(3) && reader.GetString(3) == nameof(SessionKind.Driven), !reader.IsDBNull(4), !reader.IsDBNull(5));
            }
        }

        var given = 0;
        foreach (var (id, record) in records.Where(each => !each.Value.Lined && !each.Value.Teammate))
        {
            await using var write = db.Command();
            write.CommandText = "UPDATE sessions SET line = $line WHERE id = $id AND line IS NULL";
            write.Parameters.AddWithValue("$line", KeyOf(id, records));
            write.Parameters.AddWithValue("$id", id);
            given += await write.ExecuteNonQueryAsync(token).ConfigureAwait(false);
        }

        return given;
    }, ct);

    /// <summary>
    /// The walk an older ledger keyed a line by: back by <c>from</c> to a teammate's part or one not held, the quest of the oldest driven
    /// part reached, else the oldest part's id; a walk that meets itself or passes <see cref="Lines.Bound"/> hops keys the part to itself.
    /// </summary>
    private static string KeyOf(string id, IReadOnlyDictionary<string, Walked> records)
    {
        var parts = new List<string> { id };
        var seen = new HashSet<string>(StringComparer.Ordinal) { id };
        var at = id;
        for (var hops = 0; records.TryGetValue(at, out var record) && record.From is { Length: > 0 } from; hops++)
        {
            if (!IsLocal(from) || (records.TryGetValue(from, out var before) && before.Teammate)) break;
            if (!seen.Add(from) || hops == Lines.Bound) return id;
            parts.Add(from);
            at = from;
        }

        parts.Reverse();
        return parts.Select(part => records.TryGetValue(part, out var each) && each.Driven ? each.Quest : null)
            .FirstOrDefault(quest => quest is { Length: > 0 }) ?? parts[0];
    }

    /// <summary>A record of this machine names a part by its own id; a teammate's is named under its member.</summary>
    private static bool IsLocal(string id) => !id.Contains('/', StringComparison.Ordinal) && !id.Contains('\\', StringComparison.Ordinal);

    private sealed record Walked(string? From, string? Quest, bool Driven, bool Teammate, bool Lined);
}
