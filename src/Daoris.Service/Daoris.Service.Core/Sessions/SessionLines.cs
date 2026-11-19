namespace Daoris.Knowledge;

/// <summary>
/// A record's conversation, its <c>line</c>: given as the record opens and never moved, so a conversation longer than any walk keeps its
/// id.
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

    /// <summary>A record of this machine names a part by its own id; a teammate's is named under its member.</summary>
    private static bool IsLocal(string id) => !id.Contains('/', StringComparison.Ordinal) && !id.Contains('\\', StringComparison.Ordinal);
}
