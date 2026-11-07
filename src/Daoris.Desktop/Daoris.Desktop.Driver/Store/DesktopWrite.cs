using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// The write transaction a <see cref="DesktopStore.Write{T}"/> hands its work: every statement it runs is
/// inside the one transaction the store began with <c>BEGIN IMMEDIATE</c>, after it read the writer stamp under the write lock, so a read, a
/// check and a write never interleave across processes. It lives as long as the work does, and the work awaits nothing in another process.
/// </summary>
public sealed class DesktopWrite
{
    private readonly SqliteConnection _connection;

    private readonly SqliteTransaction _transaction;

    internal DesktopWrite(SqliteConnection connection, SqliteTransaction transaction, int version = int.MaxValue)
    {
        _connection = connection;
        _transaction = transaction;
        Version = version;
    }

    /// <summary>The store's schema version as this transaction read it; a write names a step's columns only at or past that step.</summary>
    public int Version { get; }

    /// <summary>Statements run, each <c>$name</c> parameter bound; the rows they changed.</summary>
    public int Execute(string sql, params (string Name, object? Value)[] values)
    {
        using var command = Command(sql, values);
        return command.ExecuteNonQuery();
    }

    /// <summary>Move <paramref name="family"/>'s revision as the store's own write does for its family, for a transaction that writes several.</summary>
    public void Moved(string family) => Execute(
        "INSERT INTO store_revisions (family, revision) VALUES ($family, 1) ON CONFLICT (family) DO UPDATE SET revision = revision + 1", ("$family", family));

    /// <summary>The first column of the first row, or <c>default</c> where there is none or it is null.</summary>
    public T? Scalar<T>(string sql, params (string Name, object? Value)[] values)
    {
        using var command = Command(sql, values);
        return command.ExecuteScalar() is { } found and not DBNull ? (T)Convert.ChangeType(found, typeof(T), System.Globalization.CultureInfo.InvariantCulture) : default;
    }

    /// <summary>Rows read by <paramref name="map"/>, one per row of the statement's answer.</summary>
    public IReadOnlyList<T> Query<T>(string sql, Func<SqliteDataReader, T> map, params (string Name, object? Value)[] values)
    {
        using var command = Command(sql, values);
        using var reader = command.ExecuteReader();
        var rows = new List<T>();
        while (reader.Read()) rows.Add(map(reader));
        return rows;
    }

    private SqliteCommand Command(string sql, (string Name, object? Value)[] values)
    {
        var command = _connection.CreateCommand();
        command.Transaction = _transaction;
        command.CommandText = sql;
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }
}
