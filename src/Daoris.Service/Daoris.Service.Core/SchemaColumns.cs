using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// A column added to a store that already exists — how the stores holding a record grow.
/// </summary>
/// <remarks>
/// Quests, sessions, registrations and asks add columns rather than rebuild, because a record that
/// vanished on an upgrade is the failure those stores exist to prevent. Each of the four wrote this
/// probe for itself (REV3 CLEAN1).
/// </remarks>
internal static class SchemaColumns
{
    /// <summary>Whether <paramref name="table"/> has <paramref name="column"/> yet.</summary>
    public static async Task<bool> HasAsync(
        SqliteConnection connection, string table, string column, CancellationToken ct)
    {
        await using var probe = connection.CreateCommand();
        probe.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{table}') WHERE name = $name";
        probe.Parameters.AddWithValue("$name", column);
        return Convert.ToInt32(await probe.ExecuteScalarAsync(ct).ConfigureAwait(false)) > 0;
    }

    /// <summary>
    /// Add <paramref name="column"/> when it is missing. <paramref name="definition"/> is the whole
    /// clause after <c>ADD COLUMN</c>, its name included.
    /// </summary>
    public static async Task EnsureAsync(
        SqliteConnection connection, string table, string column, string definition, CancellationToken ct)
    {
        if (await HasAsync(connection, table, column, ct).ConfigureAwait(false)) return;
        await using var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {definition}";
        await alter.ExecuteNonQueryAsync(ct).ConfigureAwait(false);
    }
}
