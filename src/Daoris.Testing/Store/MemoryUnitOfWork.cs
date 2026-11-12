using System.Collections.Concurrent;
using System.Data.Common;
using System.Globalization;
using Daoris.Ports.Store;
using Microsoft.Data.Sqlite;

namespace Daoris.Testing;

/// <summary>
/// The fake of the desktop store's engine, which is SQLite still: a location written <c>:memory:name</c> is a database held in memory
/// under that name for as long as the process keeps it (<see cref="Forget"/> lets it go), and any other location is a file as the real
/// adapter's. It runs the application's own schema steps and its own SQL, and is held to the same <see cref="UnitOfWorkContract"/> as
/// the real engine, so what it does that the real one does not is a failing case there. What the file adds (the lock across
/// processes, the journal, a crash, an older build's open) is the real side's to prove.
/// </summary>
public sealed class MemoryUnitOfWork : IDesktopStoreEngine
{
    private const string MemoryPrefix = ":memory:";

    private static readonly ConcurrentDictionary<string, SqliteConnection> Anchors = new();
    private static readonly ConcurrentDictionary<string, int> Wakes = new();

    private readonly SemaphoreSlim _turn = new(1, 1);
    private readonly SqliteConnection _connection;
    private readonly DesktopStoreEngineOptions _options;
    private bool _disposed;

    private MemoryUnitOfWork(string location, SqliteConnection connection, DesktopStoreEngineOptions options)
    {
        Location = location;
        _connection = connection;
        _options = options;
    }

    /// <summary>The engines a unit test's root hands the application: this fake over files and memory, and a plain connection for the rest.</summary>
    public static DesktopStoreEngines Engines { get; } = new(Open, Connect, Backup, SqliteConnection.ClearAllPools);

    /// <inheritdoc />
    public string Location { get; }

    /// <summary>The location of an in-memory store named <paramref name="name"/>.</summary>
    public static string MemoryPlace(string name) => MemoryPrefix + name;

    /// <summary>Whether the readers of the in-memory store at <paramref name="location"/> were woken since it was made.</summary>
    public static bool Woken(string location) => Wakes.ContainsKey(location);

    /// <summary>Lets go the in-memory store at <paramref name="location"/>: its tables go with its last connection.</summary>
    public static void Forget(string location)
    {
        Wakes.TryRemove(location, out _);
        if (Anchors.TryRemove(location, out var anchor)) anchor.Dispose();
    }

    /// <summary>The store at <paramref name="location"/>, made if there is none.</summary>
    public static IDesktopStoreEngine Open(string location, DesktopStoreEngineOptions options)
    {
        var wait = options.BusyWait ?? TimeSpan.FromSeconds(5);
        var memory = location.StartsWith(MemoryPrefix, StringComparison.Ordinal);
        if (!memory && Path.GetDirectoryName(Path.GetFullPath(location)) is { Length: > 0 } folder) Directory.CreateDirectory(folder);
        var text = memory
            ? new SqliteConnectionStringBuilder { DataSource = location[MemoryPrefix.Length..], Mode = SqliteOpenMode.Memory, Cache = SqliteCacheMode.Shared }
            : new SqliteConnectionStringBuilder { DataSource = location, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false };
        text.DefaultTimeout = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
        if (memory) Anchors.GetOrAdd(location, _ => Opened(text.ToString()));
        var connection = Opened(text.ToString());
        Exec(connection, $"PRAGMA busy_timeout = {(int)wait.TotalMilliseconds}");
        if (!memory)
        {
            Exec(connection, "PRAGMA journal_mode = WAL");
            Exec(connection, "PRAGMA synchronous = FULL");
        }

        Exec(connection, "PRAGMA foreign_keys = ON");
        return new MemoryUnitOfWork(location, connection, options);
    }

    /// <inheritdoc />
    public StoreStamp Stamp() => Turn(() => ReadStamp(null));

    /// <inheritdoc />
    public T Read<T>(Func<DbConnection, T> read) => Turn(() => read(_connection));

    /// <inheritdoc />
    public async ValueTask<T> ReadAsync<T>(Func<DbConnection, CancellationToken, ValueTask<T>> read, CancellationToken ct)
    {
        await _turn.WaitAsync(ct);
        try
        {
            ThrowIfDisposed();
            return await read(_connection, ct);
        }
        finally
        {
            _turn.Release();
        }
    }

    /// <inheritdoc />
    public T Write<T>(string? family, Func<DbConnection, DbTransaction, StoreStamp, T> work)
    {
        var result = Turn(() => Committed(family, transaction => work(_connection, transaction, Admitted(transaction))));
        if (family is not null) Wake();
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<T> WriteAsync<T>(string family, Func<DbTransaction, CancellationToken, ValueTask<T>> write, CancellationToken ct)
    {
        T result;
        await _turn.WaitAsync(ct);
        try
        {
            ThrowIfDisposed();
            using var transaction = Begin();
            try
            {
                Admitted(transaction);
                result = await write(transaction, ct);
                MoveRevision(transaction, family);
                transaction.Commit();
            }
            catch (SqliteException error) when (Busy(error))
            {
                throw new StoreLockBusyException(error);
            }
        }
        finally
        {
            _turn.Release();
        }

        Wake();
        return result;
    }

    /// <inheritdoc />
    public bool Apply(int version, int writer, Action<DbConnection, DbTransaction> step) => Turn(() =>
    {
        using var transaction = Begin();
        var stamp = ReadStamp(transaction);
        if (stamp.Writer > _options.Writer || stamp.Version >= version) return false;
        step(_connection, transaction);
        Exec(
            _connection,
            "INSERT INTO store_version (one, version, writer, build, at) VALUES (1, $version, $writer, $build, $at) "
            + "ON CONFLICT (one) DO UPDATE SET version = MAX(version, excluded.version), writer = MAX(writer, excluded.writer), build = excluded.build, at = excluded.at",
            transaction,
            ("$version", version), ("$writer", Math.Max(stamp.Writer, writer)), ("$build", _options.Build),
            ("$at", _options.Clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture)));
        transaction.Commit();
        return true;
    });

    /// <inheritdoc />
    public void Dispose()
    {
        _turn.Wait();
        try
        {
            if (_disposed) return;
            _disposed = true;
            _connection.Dispose();
        }
        finally
        {
            _turn.Release();
        }
    }

    private static SqliteConnection Opened(string text)
    {
        var connection = new SqliteConnection(text);
        connection.Open();
        return connection;
    }

    private static void Backup(string source, string target)
    {
        using (var from = (SqliteConnection)Connect(source, DesktopOpen.ReadWrite))
        using (var to = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = target, Pooling = false }.ToString()))
        {
            to.Open();
            from.BackupDatabase(to);
        }

        SqliteConnection.ClearAllPools();
    }

    private static DbConnection Connect(string path, DesktopOpen mode)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode switch { DesktopOpen.ReadOnly => SqliteOpenMode.ReadOnly, DesktopOpen.ReadWrite => SqliteOpenMode.ReadWrite, _ => SqliteOpenMode.ReadWriteCreate },
            Pooling = false,
        }.ToString());
        connection.Open();
        return connection;
    }

    private static void Exec(SqliteConnection connection, string sql, SqliteTransaction? transaction = null, params (string Name, object Value)[] values)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value);
        command.ExecuteNonQuery();
    }

    private static bool Busy(SqliteException error) => error.SqliteErrorCode is 5 or 6;

    private T Turn<T>(Func<T> body)
    {
        _turn.Wait();
        try
        {
            ThrowIfDisposed();
            try
            {
                return body();
            }
            catch (SqliteException error) when (Busy(error))
            {
                throw new StoreLockBusyException(error);
            }
        }
        finally
        {
            _turn.Release();
        }
    }

    private T Committed<T>(string? family, Func<SqliteTransaction, T> body)
    {
        using var transaction = Begin();
        var result = body(transaction);
        if (family is not null) MoveRevision(transaction, family);
        transaction.Commit();
        return result;
    }

    private StoreStamp Admitted(SqliteTransaction transaction)
    {
        var stamp = ReadStamp(transaction);
        if (stamp.Writer > _options.Writer) throw new StoreNewerException(stamp.Writer, _options.Writer);
        if (stamp.Version == 0) throw new StoreWithoutSchemaException();
        return stamp;
    }

    private SqliteTransaction Begin()
    {
        try
        {
            return _connection.BeginTransaction(deferred: false);
        }
        catch (SqliteException error) when (Busy(error))
        {
            throw new StoreLockBusyException(error);
        }
    }

    private void MoveRevision(SqliteTransaction transaction, string family) => Exec(
        _connection,
        "INSERT INTO store_revisions (family, revision) VALUES ($family, 1) ON CONFLICT (family) DO UPDATE SET revision = revision + 1",
        transaction,
        ("$family", family));

    private StoreStamp ReadStamp(SqliteTransaction? transaction)
    {
        using var has = _connection.CreateCommand();
        has.Transaction = transaction;
        has.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'store_version'";
        if (has.ExecuteScalar() is null) return new StoreStamp(0, 0);
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT version, writer FROM store_version WHERE one = 1";
        using var reader = command.ExecuteReader();
        return reader.Read() ? new StoreStamp(reader.GetInt32(0), reader.GetInt32(1)) : new StoreStamp(0, 0);
    }

    private void Wake()
    {
        if (Location.StartsWith(MemoryPrefix, StringComparison.Ordinal))
        {
            Wakes[Location] = 1;
            return;
        }

        try
        {
            File.WriteAllBytes(Location + ".wake", []);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The write is committed; a wake that did not land is the watchers' period's to cover.
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
