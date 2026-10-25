using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// The desktop store opened read-only (FOUNDATION1 z-i, the design §13.4's GitDoor, §19.10): a connection that creates nothing, migrates
/// nothing and writes nothing, so a process that only reads a family's table (the git door, once its families move and its start passes
/// z-i's limit) never waits on a writer, which write-ahead logging promises, and never changes the file's stamp.
/// </summary>
/// <remarks>
/// <para><b>Compiled in as the door's other files are</b>, so it uses the base library and <c>Microsoft.Data.Sqlite</c> alone, and names
/// no other type of the driver: the measurement of the door's start with it (<c>GitDoorStartTests</c>) links this one file into a
/// door-shaped program. Nothing of the door takes the package yet, and nothing reads the store.</para>
/// <para>It reads the tables the build knows from a store of any version: a store written by a newer Daoris is read as it stands, the
/// refusal being a writer's (<see cref="DesktopStore"/>).</para>
/// </remarks>
public sealed class DesktopStoreReader : IDisposable
{
    /// <summary>How long a read waits on the file's lock before it is refused, in seconds: the door never waits on a writer for long.</summary>
    private const int BusySeconds = 5;

    private readonly SqliteConnection _connection;

    private DesktopStoreReader(SqliteConnection connection) => _connection = connection;

    /// <summary>
    /// The store at <paramref name="path"/>, opened for reading only; null where there is no file, which this never makes.
    /// </summary>
    /// <exception cref="SqliteException">The file is there and is not a database this can read.</exception>
    public static DesktopStoreReader? Open(string path)
    {
        if (!File.Exists(path)) return null;
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false,
            DefaultTimeout = BusySeconds,
        }.ToString());
        try
        {
            connection.Open();
            using (var wait = connection.CreateCommand())
            {
                // The connection string's timeout is the commands', and the file's lock wait is its own to set.
                wait.CommandText = $"PRAGMA busy_timeout = {BusySeconds * 1000}";
                wait.ExecuteNonQuery();
            }

            return new DesktopStoreReader(connection);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>The schema version the store holds, or 0 where the store holds no <c>store_version</c> yet.</summary>
    public int Version => Stamp().Version;

    /// <summary>The oldest writer stamp that may write the store, or 0 where it holds none yet.</summary>
    public int Writer => Stamp().Writer;

    /// <summary>Each family's revision, as the last writer left it.</summary>
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

    /// <summary>Rows read by <paramref name="map"/> from one statement; no parameter changes what the file holds.</summary>
    public IReadOnlyList<T> Query<T>(string sql, Func<SqliteDataReader, T> map, params (string Name, object? Value)[] values)
    {
        using var command = _connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
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
        command.Parameters.AddWithValue("$name", table);
        return command.ExecuteScalar() is not null;
    }
}
