using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// A column added to a store that already exists — how the stores holding a record grow.
/// </summary>
/// <remarks>
/// Quests, sessions, registrations and asks add columns rather than rebuild, because a record that
/// vanished on an upgrade is the failure those stores exist to prevent. Called inside the connection's gate, as every command is.
/// </remarks>
internal static class SchemaColumns
{
    /// <summary>Whether <paramref name="table"/> has <paramref name="column"/> yet.</summary>
    public static async Task<bool> HasAsync(
        ConnectionGate db, string table, string column, CancellationToken ct)
    {
        await using var probe = db.Command();
        probe.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = $name";
        probe.Parameters.AddWithValue("$name", column);
        return Convert.ToInt32(await probe.ExecuteScalarAsync(ct).ConfigureAwait(false)) > 0;
    }

    /// <summary>
    /// Add <paramref name="column"/> when it is missing. <paramref name="definition"/> is the whole
    /// clause after <c>ADD COLUMN</c>, its name included.
    /// </summary>
    public static async Task EnsureAsync(
        ConnectionGate db, string table, string column, string definition, CancellationToken ct)
    {
        if (await HasAsync(db, table, column, ct).ConfigureAwait(false)) return;
        await AddAsync(db, table, definition, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// A column's alter and the statements that fill it, or a rebuild's copy, drop and rename, as one step: a crash or a failure between
    /// them leaves the store as it was, so the next open does the whole upgrade again. Each statement alone is its own commit, and a
    /// column found without its backfill is skipped by every later open.
    /// </summary>
    public static Task ApplyAsync(ConnectionGate db, string script, CancellationToken ct) =>
        db.InTransactionAsync(async (transaction, inside) =>
        {
            await using var command = db.Command();
            command.Transaction = transaction;
            command.CommandText = script;
            await command.ExecuteNonQueryAsync(inside).ConfigureAwait(false);
            return true;
        }, ct);

    /// <summary>
    /// The alter, where another connection opening the same new file may have added the column since the check: the check and
    /// the alter are not one transaction, and that connection's column is the one wanted.
    /// </summary>
    public static async Task AddAsync(ConnectionGate db, string table, string definition, CancellationToken ct)
    {
        await using var alter = db.Command();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {definition}";
        try
        {
            await alter.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
        }
        catch (SqliteException error) when (error.Message.Contains("duplicate column name", StringComparison.OrdinalIgnoreCase))
        {
            // Added by the other open between the check and here.
        }
    }
}
