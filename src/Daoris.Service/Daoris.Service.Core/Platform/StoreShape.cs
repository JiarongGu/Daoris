using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// A store an older build wrote and no purge emptied: its tables lack a column this build's one creating statement holds, so its
/// first query would fail. Refused at open, naming the file and what to do, before anything writes to it.
/// </summary>
public sealed class OlderStoreException(string store, IReadOnlyList<string> missing) : Exception(
    $"{store} was written by an older Daoris and its history was never purged: {string.Join(", ", missing)}. "
    + "Close Daoris, then purge the home's history or start a fresh home; nothing was changed.")
{
    /// <summary>The file, as the service names it.</summary>
    public string Store { get; } = store;

    /// <summary>Each table's missing column, as <c>table.column</c>.</summary>
    public IReadOnlyList<string> Missing { get; } = missing;
}

/// <summary>
/// The service store's shape against this build's: the current shape is made by the stores' own openers over a store in memory,
/// so no list of columns is kept beside the creating statements, and every table both hold is compared column by column.
/// </summary>
internal static class StoreShape
{
    /// <summary>
    /// Refuses <paramref name="path"/> where a table this build makes is there without one of its columns. A file that is not
    /// there, or a table it does not hold yet, is the openers' to make.
    /// </summary>
    /// <param name="open">The stores' openers, run over the connection handed them as the service runs them.</param>
    /// <exception cref="OlderStoreException">A table lacks a column.</exception>
    public static async Task RequireCurrentAsync(string path, Func<SqliteConnection, Task> open)
    {
        if (!File.Exists(path)) return;

        await using var fresh = new SqliteConnection("Data Source=:memory:");
        await fresh.OpenAsync().ConfigureAwait(false);
        await open(fresh).ConfigureAwait(false);
        var current = await ShapeAsync(fresh).ConfigureAwait(false);

        await using var home = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path, Mode = SqliteOpenMode.ReadOnly, Pooling = false,
        }.ToString());
        await home.OpenAsync().ConfigureAwait(false);
        var held = await ShapeAsync(home).ConfigureAwait(false);

        var missing = new List<string>();
        foreach (var (table, columns) in current)
        {
            if (!held.TryGetValue(table, out var have)) continue;
            missing.AddRange(columns.Where(column => !have.Contains(column)).Select(column => $"{table}.{column}"));
        }

        if (missing.Count > 0) throw new OlderStoreException(path, missing);
    }

    /// <summary>Each table's columns, read inside the connection's gate, as every command on a connection is.</summary>
    private static Task<Dictionary<string, HashSet<string>>> ShapeAsync(SqliteConnection connection)
    {
        var gate = ConnectionGate.For(connection);
        return gate.RunAsync(() => Task.FromResult(Shape(gate)), CancellationToken.None);
    }

    private static Dictionary<string, HashSet<string>> Shape(ConnectionGate gate)
    {
        var tables = new List<string>();
        using (var list = gate.Command())
        {
            list.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
            using var reader = list.ExecuteReader();
            while (reader.Read()) tables.Add(reader.GetString(0));
        }

        var shape = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var table in tables)
        {
            using var info = gate.Command();
            info.CommandText = "SELECT name FROM pragma_table_info($table)";
            info.Parameters.AddWithValue("$table", table);
            using var reader = info.ExecuteReader();
            var columns = new HashSet<string>(StringComparer.Ordinal);
            while (reader.Read()) columns.Add(reader.GetString(0));
            shape[table] = columns;
        }

        return shape;
    }
}
