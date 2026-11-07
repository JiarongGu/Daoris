namespace Daoris.Knowledge;

/// <summary>
/// The indexes a list's per-record reads stand on. A list answers each record with its entry key and the head of its line,
/// and each of those asks the table by a column that is no key: who continues a record, a quest's last run, the conversation that
/// took a quest, a teammate's mirror by the id its machine knows it by. Unindexed, each is a scan of every record, so a list of
/// three thousand makes some ten thousand scans of three thousand rows.
/// </summary>
internal static class SessionIndexes
{
    public static async Task EnsureAsync(ConnectionGate db, CancellationToken ct)
    {
        await using var command = db.Command();
        // The mirror's index is on the expression its read compares, kept for mirrors alone, so the read's `OR` is two index reads.
        command.CommandText = """
            CREATE INDEX IF NOT EXISTS sessions_from ON sessions (from_session);
            CREATE INDEX IF NOT EXISTS sessions_quest ON sessions (quest, created);
            CREATE INDEX IF NOT EXISTS sessions_took_quest ON sessions (took_quest COLLATE NOCASE);
            CREATE INDEX IF NOT EXISTS sessions_mirrored_id ON sessions (substr(id, instr(id, '/') + 1)) WHERE origin IS NOT NULL;
            """;
        await command.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
