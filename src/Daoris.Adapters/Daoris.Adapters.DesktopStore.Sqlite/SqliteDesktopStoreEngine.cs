using System.Data.Common;
using System.Globalization;
using Daoris.Ports.Store;
using Microsoft.Data.Sqlite;

namespace Daoris.Adapters.DesktopStore.Sqlite;

/// <summary>
/// The desktop store's engine over one SQLite file: one connection behind one turn, write-ahead logging so a reader never waits on the
/// writer, <c>synchronous = FULL</c> since a claim or a mark lost to a power cut could repeat an effect, and a busy wait after which a
/// write is refused as <see cref="StoreLockBusyException"/>. Every write is one transaction under the write lock taken at its start
/// (<c>BEGIN IMMEDIATE</c>) with the writer stamp read inside it, before anything changes.
/// </summary>
public sealed class SqliteDesktopStoreEngine : IDesktopStoreEngine
{
    private const int BusySeconds = 5;

    private readonly SemaphoreSlim _turn = new(1, 1);
    private readonly SqliteConnection _connection;
    private readonly DesktopStoreEngineOptions _options;
    private bool _disposed;

    private SqliteDesktopStoreEngine(string location, SqliteConnection connection, DesktopStoreEngineOptions options)
    {
        Location = location;
        _connection = connection;
        _options = options;
    }

    /// <inheritdoc />
    public string Location { get; }

    /// <summary>The store at <paramref name="location"/>, made if there is none; its folder is made.</summary>
    /// <exception cref="StoreLockBusyException">Another process held the write lock past the wait.</exception>
    public static IDesktopStoreEngine Open(string location, DesktopStoreEngineOptions options)
    {
        if (Path.GetDirectoryName(Path.GetFullPath(location)) is { Length: > 0 } folder) Directory.CreateDirectory(folder);
        var wait = options.BusyWait ?? TimeSpan.FromSeconds(BusySeconds);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = location,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            DefaultTimeout = Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds)),
        }.ToString());
        try
        {
            connection.Open();
            Pragma(connection, $"PRAGMA busy_timeout = {(int)wait.TotalMilliseconds}");
            Pragma(connection, "PRAGMA journal_mode = WAL");
            Pragma(connection, "PRAGMA synchronous = FULL");
            Pragma(connection, "PRAGMA journal_size_limit = 33554432");
            // A move's claim names a row of `store_runs` and a word in transfer its move, and the store holds them to it.
            Pragma(connection, "PRAGMA foreign_keys = ON");
            return new SqliteDesktopStoreEngine(location, connection, options);
        }
        catch (SqliteException error) when (Busy(error))
        {
            connection.Dispose();
            throw new StoreLockBusyException(error);
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <inheritdoc />
    public StoreStamp Stamp()
    {
        _turn.Wait();
        try
        {
            ThrowIfDisposed();
            return ReadStamp(null);
        }
        finally
        {
            _turn.Release();
        }
    }

    /// <inheritdoc />
    public T Read<T>(Func<DbConnection, T> read)
    {
        _turn.Wait();
        try
        {
            ThrowIfDisposed();
            return read(_connection);
        }
        finally
        {
            _turn.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask<T> ReadAsync<T>(Func<DbConnection, CancellationToken, ValueTask<T>> read, CancellationToken ct)
    {
        await _turn.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            return await read(_connection, ct).ConfigureAwait(false);
        }
        finally
        {
            _turn.Release();
        }
    }

    /// <inheritdoc />
    public T Write<T>(string? family, Func<DbConnection, DbTransaction, StoreStamp, T> work)
    {
        T result;
        _turn.Wait();
        try
        {
            ThrowIfDisposed();
            using var transaction = Begin();
            try
            {
                result = work(_connection, transaction, Admitted(transaction));
                if (family is not null) MoveRevision(transaction, family);
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

        if (family is not null) Touch();
        return result;
    }

    /// <inheritdoc />
    public async ValueTask<T> WriteAsync<T>(string family, Func<DbTransaction, CancellationToken, ValueTask<T>> write, CancellationToken ct)
    {
        T result;
        await _turn.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            using var transaction = Begin();
            try
            {
                Admitted(transaction);
                result = await write(transaction, ct).ConfigureAwait(false);
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

        Touch();
        return result;
    }

    /// <inheritdoc />
    public bool Apply(int version, int writer, Action<DbConnection, DbTransaction> step)
    {
        _turn.Wait();
        try
        {
            ThrowIfDisposed();
            using var transaction = Begin();
            var stamp = ReadStamp(transaction);
            if (stamp.Writer > _options.Writer || stamp.Version >= version) return false;
            step(_connection, transaction);
            using var command = _connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO store_version (one, version, writer, build, at) VALUES (1, $version, $writer, $build, $at)
                ON CONFLICT (one) DO UPDATE SET version = MAX(version, excluded.version), writer = MAX(writer, excluded.writer), build = excluded.build, at = excluded.at
                """;
            command.Parameters.AddWithValue("$version", version);
            command.Parameters.AddWithValue("$writer", Math.Max(stamp.Writer, writer));
            command.Parameters.AddWithValue("$build", _options.Build);
            command.Parameters.AddWithValue("$at", _options.Clock.GetUtcNow().ToString("O", CultureInfo.InvariantCulture));
            command.ExecuteNonQuery();
            transaction.Commit();
            return true;
        }
        catch (SqliteException error) when (Busy(error))
        {
            throw new StoreLockBusyException(error);
        }
        finally
        {
            _turn.Release();
        }
    }

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

    private void MoveRevision(SqliteTransaction transaction, string family)
    {
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO store_revisions (family, revision) VALUES ($family, 1) ON CONFLICT (family) DO UPDATE SET revision = revision + 1";
        command.Parameters.AddWithValue("$family", family);
        command.ExecuteNonQuery();
    }

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

    /// <summary>The wake file touched after a commit: a file that holds nothing, whose change is what a watcher reads. It never fails the write.</summary>
    private void Touch()
    {
        var wake = Location + ".wake";
        try
        {
            using (new FileStream(wake, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            {
            }

            File.SetLastWriteTimeUtc(wake, _options.Clock.GetUtcNow().UtcDateTime);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The write is committed; the watchers' own period is the fallback for a wake that did not land.
        }
    }

    private static bool Busy(SqliteException error) => error.SqliteErrorCode is 5 or 6;

    private static void Pragma(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
