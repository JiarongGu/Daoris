using System.Data.Common;
using Daoris.Ports.Store;

namespace Daoris.Driver;

/// <summary>
/// The desktop store opened read-only: it creates, migrates and writes nothing, so a process that only reads a family's table (the git door,
/// once its families move) never waits on a writer and never changes the file's stamp.
/// </summary>
/// <remarks>
/// <para>The connection is the root's (<see cref="DesktopStoreEngines.Connect"/>, read-only), so this names no engine's type.</para>
/// <para>It reads the tables the build knows from a store of any version: a store written by a newer Daoris is read as it stands, the
/// refusal being a writer's (<see cref="DesktopStore"/>).</para>
/// </remarks>
public sealed class DesktopStoreReader : IDisposable
{
    private readonly DbConnection _connection;

    private DesktopStoreReader(DbConnection connection) => _connection = connection;

    /// <summary>
    /// The store at <paramref name="path"/>, opened for reading only; null where there is no file, which this never makes.
    /// </summary>
    /// <exception cref="DbException">The file is there and is not a database this can read.</exception>
    public static DesktopStoreReader? Open(string path)
    {
        if (!File.Exists(path)) return null;
        return new DesktopStoreReader(StoreEngineSource.Connect(path, DesktopOpen.ReadOnly));
    }

    /// <summary>The schema version the store holds, or 0 where the store holds no <c>store_version</c> yet.</summary>
    public int Version => Stamp().Version;

    /// <summary>The oldest writer stamp that may write the store, or 0 where it holds none yet.</summary>
    public int Writer => Stamp().Writer;

    public IReadOnlyDictionary<string, long> Revisions()
    {
        var found = new Dictionary<string, long>(StringComparer.Ordinal);
        if (!Has("store_revisions")) return found;
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT family, revision FROM store_revisions";
        using var reader = command.ExecuteReader();
        while (reader.Read()) found[reader.GetString(0)] = reader.GetInt64(1);
        return found;
    }

    public IReadOnlyList<T> Query<T>(string sql, Func<DbDataReader, T> map, params (string Name, object? Value)[] values)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        command.Bind(values);
        using var reader = command.ExecuteReader();
        var rows = new List<T>();
        while (reader.Read()) rows.Add(map(reader));
        return rows;
    }

    public void Dispose() => _connection.Dispose();

    private (int Version, int Writer) Stamp()
    {
        if (!Has("store_version")) return (0, 0);
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT version, writer FROM store_version WHERE one = 1";
        using var reader = command.ExecuteReader();
        return reader.Read() ? (reader.GetInt32(0), reader.GetInt32(1)) : (0, 0);
    }

    private bool Has(string table)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name";
        command.Bind("$name", table);
        return command.ExecuteScalar() is not null;
    }
}
