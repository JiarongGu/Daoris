using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Data.Sqlite;

namespace Daoris.Driver;

/// <summary>
/// The desktop's store: <c>data/desktop.db</c>, one SQLite file the driver library alone
/// opens, machine-local and never synced, holding what Daoris owns and more than one reader or writer sets: its own tables
/// (<c>store_version</c>, <c>store_revisions</c>, <c>store_runs</c> and <c>imports</c>) and each family's, one numbered step each.
/// </summary>
/// <remarks>
/// <para><b>One connection per process</b> behind a lock, as the service's <c>ConnectionGate</c> keeps its own: write-ahead
/// logging, so a reader never waits on the writer; <c>synchronous = FULL</c>, since a claim or a mark lost to a power cut could repeat an
/// effect; and a busy wait of five seconds, then a refusal said in the process's own words (<see cref="DesktopStoreBusyException"/>).</para>
/// <para><b>Every write is one transaction under the write lock taken at its start</b> (<c>BEGIN IMMEDIATE</c>), and <b>the writer stamp is read
/// inside it</b>, before anything changes: a process that opened the store before another's migration, and kept its connection, cannot write
/// after it (<see cref="DesktopStoreNewerException"/>).</para>
/// <para><b>Migrations</b> are numbered steps in code (<see cref="DesktopStoreSteps"/>), forward only, each in one exclusive transaction: the first
/// process to open an older store migrates it while the others wait on the write lock, and the stamp is never lowered. A step that raises the
/// stamp waits for the start's confirmation, <see cref="Settled"/>'s reading, so a build rolled back before it confirmed still writes the store.</para>
/// <para><b>Wakes.</b> A write moves its family's revision in the same transaction and, once committed, touches the wake file
/// <c>data/desktop.db.wake</c>, which holds nothing; what watches it reads the revisions. No reader watches it yet.</para>
/// <para><b>The git door</b> reads through <see cref="DesktopStoreReader"/>, read-only, once its families move and its start passes the
/// limit; until then it opens none.</para>
/// </remarks>
public sealed class DesktopStore : IDisposable
{
    /// <summary>
    /// The writer stamp this build writes with: 3, raised by a role's staffing policy's step, so a build before it
    /// reads the store and writes nothing there.
    /// </summary>
    public const int WriterStamp = PolicyWriter;

    /// <summary>The stamp of the first build that knows the store, which every step before the delivery record's leaves the store at.</summary>
    public const int FirstWriter = 1;

    /// <summary>The stamp the delivery record's step raised the store to, which a build of its fifteen steps writes with.</summary>
    public const int DeliveryWriter = 2;

    /// <summary>The stamp a role's staffing policy's step raised the store to.</summary>
    public const int PolicyWriter = 3;

    /// <summary>The version whose step gives the store today's names, run by the home's migration's <c>StoreNamesStep</c>.</summary>
    public const int NamesVersion = 22;

    public const int GenerationVersion = 24;

    public const int ClaimExpectVersion = GenerationVersion + 1;

    /// <summary>How long a write waits on the file's lock before it is refused, in seconds.</summary>
    private const int BusySeconds = 5;

    /// <summary>The wake file's suffix: it sits beside the store and holds nothing.</summary>
    public const string WakeSuffix = ".wake";

    /// <summary>The build's own migrations: version 1 makes the store's four tables.</summary>
    public static IReadOnlyList<StoreMigration> Steps { get; } = DesktopStoreSteps.All;

    /// <summary>
    /// The rows of <c>moves</c> that stand for their record left: every phase but the three a move ends at, a phase this
    /// build does not know among them. The partial index <c>moves_standing</c> is over these, and a read of the standing move names the same.
    /// </summary>
    public const string StandingMoves = "phase NOT IN ('delivered', 'fell-back', 'closed')";

    private readonly object _turn = new();

    private readonly SqliteConnection _connection;

    private readonly IReadOnlyList<StoreMigration> _steps;

    private readonly Func<StoreSettled> _settled;

    private readonly TimeProvider _clock;

    private readonly string _build;

    private readonly string _kind;

    private bool _registered;

    private bool _disposed;

    private DesktopStore(string location, SqliteConnection connection, DesktopStoreOptions options)
    {
        Location = location;
        _connection = connection;
        _steps = options.Steps ?? Steps;
        _settled = options.Settled ?? (() => StoreSettled.Not("no reading of the start's confirmation was handed to the store"));
        _clock = options.Clock ?? TimeProvider.System;
        _build = options.Build ?? typeof(DesktopStore).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0";
        _kind = options.Kind;
        Own = options.Writer;
        Run = options.Run ?? Guid.NewGuid().ToString("N");
    }

    /// <summary>The store's file in a home: the home table's <c>desktop-db</c> entry.</summary>
    public static string LocationIn(string home) => DaorisHome.Entry(home, HomeEntry.DesktopDb);

    /// <summary>The wake file beside a store's file.</summary>
    public static string WakeOf(string location) => location + WakeSuffix;

    /// <summary>The store's file.</summary>
    public string Location { get; }

    /// <summary>The wake file a write touches once it has committed.</summary>
    public string Wake => WakeOf(Location);

    /// <summary>The writer stamp this process writes with.</summary>
    public int Own { get; }

    /// <summary>This process's run: the id its first write records in <c>store_runs</c>.</summary>
    public string Run { get; }

    /// <summary>
    /// Why a migration is waiting, in words, or null where none is: the start's confirmation is not settled and the next step raises the
    /// stamp. Read again by <see cref="Migrate"/> at each come-up and look until it is null.
    /// </summary>
    public string? Held { get; private set; }

    /// <summary>The schema version and the writer stamp the file holds now.</summary>
    public (int Version, int Writer) Stamp
    {
        get
        {
            lock (_turn)
            {
                ThrowIfDisposed();
                return ReadStamp(null);
            }
        }
    }

    /// <summary>Whether the file's writer stamp is past this build's: it is read as it stands and written by no one here.</summary>
    public bool Newer => Stamp.Writer > Own;

    /// <summary>
    /// The store at <paramref name="location"/>, made if there is none and migrated as far as the start allows; its folder is made, and
    /// nothing is written to a store a newer Daoris stamped.
    /// </summary>
    /// <exception cref="DesktopStoreBusyException">Another process held the write lock past the wait.</exception>
    public static DesktopStore Open(string location, DesktopStoreOptions? options = null)
    {
        options ??= new DesktopStoreOptions();
        if (Path.GetDirectoryName(Path.GetFullPath(location)) is { Length: > 0 } folder) Directory.CreateDirectory(folder);
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = location,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false,
            DefaultTimeout = BusySeconds,
        }.ToString());
        DesktopStore? store = null;
        try
        {
            connection.Open();
            Pragma(connection, $"PRAGMA busy_timeout = {BusySeconds * 1000}");
            Pragma(connection, "PRAGMA journal_mode = WAL");
            Pragma(connection, "PRAGMA synchronous = FULL");
            // A move's claim names a row of `store_runs` and a word in transfer its move, and the store holds them to it.
            Pragma(connection, "PRAGMA foreign_keys = ON");
            store = new DesktopStore(location, connection, options);
            store.Migrate();
            return store;
        }
        catch (Exception error)
        {
            if (store is not null) store.Dispose();
            else connection.Dispose();
            if (error is SqliteException busy && Busy(busy)) throw new DesktopStoreBusyException(busy);
            throw;
        }
    }

    /// <summary>
    /// Migrate as far as the start allows, which is every step but one that raises the stamp while the confirmation is not settled: that
    /// one is held, <see cref="Held"/> says why, and the call is made again at each come-up and look until it is not. A step another
    /// process has already run is skipped, and a file stamped past this build is left as it is.
    /// </summary>
    /// <returns>True where nothing is waiting.</returns>
    public bool Migrate()
    {
        lock (_turn)
        {
            ThrowIfDisposed();
            Held = null;
            foreach (var step in _steps.OrderBy(each => each.Version))
            {
                var stamp = ReadStamp(null);
                if (stamp.Writer > Own) return true;
                if (stamp.Version >= step.Version) continue;

                // Creating the store is no raise: a stamp of 0 is a file nobody has written.
                if (stamp.Writer > 0 && step.Writer > stamp.Writer && _settled() is { Is: false } waiting)
                {
                    Held = $"migration {step.Version} raises the store's writer stamp from {stamp.Writer} to {step.Writer}, and waits for the start's confirmation: {waiting.Said}";
                    return false;
                }

                Apply(step);
            }

            return true;
        }
    }

    /// <summary>
    /// One write transaction: <c>BEGIN IMMEDIATE</c>, the writer stamp read under the lock and refused where it is past this build's, this
    /// process's run recorded, <paramref name="work"/>, the family's revision moved, the commit; then the wake file touched. Rolled back
    /// where the work throws.
    /// </summary>
    /// <exception cref="DesktopStoreNewerException">The file's stamp is past this build's.</exception>
    /// <exception cref="DesktopStoreBusyException">The write lock stayed held past the wait; nothing is written.</exception>
    public T Write<T>(string family, Func<DesktopWrite, T> work)
    {
        T result;
        lock (_turn)
        {
            ThrowIfDisposed();
            using var transaction = Begin();
            try
            {
                var stamp = ReadStamp(transaction);
                if (stamp.Writer > Own) throw new DesktopStoreNewerException(stamp.Writer, Own);
                if (stamp.Version == 0) throw new DesktopStoreException($"the desktop's store has no schema yet: {Held ?? "its first migration did not run"}.");
                var write = new DesktopWrite(_connection, transaction, stamp.Version);
                var registering = !_registered;
                if (registering) Register(write);
                result = work(write);
                write.Execute(
                    "INSERT INTO store_revisions (family, revision) VALUES ($family, 1) ON CONFLICT (family) DO UPDATE SET revision = revision + 1",
                    ("$family", family));
                transaction.Commit();
                if (registering) _registered = true;
            }
            catch (SqliteException error) when (Busy(error))
            {
                throw new DesktopStoreBusyException(error);
            }
        }

        Touch();
        return result;
    }

    /// <summary>
    /// This process's run recorded in <c>store_runs</c>, and nothing else: no family's revision moves and no wake is touched. A shell's come-up enters before it reads the runs, so another shell coming up on the home finds it there.
    /// </summary>
    /// <exception cref="DesktopStoreNewerException">The file's stamp is past this build's: nothing is written.</exception>
    /// <exception cref="DesktopStoreBusyException">The write lock stayed held past the wait; nothing is written.</exception>
    public void Enter()
    {
        lock (_turn)
        {
            ThrowIfDisposed();
            if (_registered) return;
            using var transaction = Begin();
            try
            {
                var stamp = ReadStamp(transaction);
                if (stamp.Writer > Own) throw new DesktopStoreNewerException(stamp.Writer, Own);
                if (stamp.Version == 0) throw new DesktopStoreException($"the desktop's store has no schema yet: {Held ?? "its first migration did not run"}.");
                Register(new DesktopWrite(_connection, transaction));
                transaction.Commit();
                _registered = true;
            }
            catch (SqliteException error) when (Busy(error))
            {
                throw new DesktopStoreBusyException(error);
            }
        }
    }

    /// <summary><see cref="Write{T}"/> for work that answers nothing.</summary>
    public void Write(string family, Action<DesktopWrite> work) =>
        Write<bool>(family, write =>
        {
            work(write);
            return true;
        });

    /// <summary>
    /// Rows read by <paramref name="map"/> from one statement, in a snapshot of their own; a read never waits on a writer under write-ahead
    /// logging and writes nothing.
    /// </summary>
    public IReadOnlyList<T> Query<T>(string sql, Func<SqliteDataReader, T> map, params (string Name, object? Value)[] values)
    {
        lock (_turn)
        {
            ThrowIfDisposed();
            using var command = _connection.CreateCommand();
            command.CommandText = sql;
            foreach (var (name, value) in values) command.Parameters.AddWithValue(name, value ?? DBNull.Value);
            using var reader = command.ExecuteReader();
            var rows = new List<T>();
            while (reader.Read()) rows.Add(map(reader));
            return rows;
        }
    }

    /// <summary>Each family's revision, as the last write left it; a family nothing has written has none.</summary>
    public IReadOnlyDictionary<string, long> Revisions() =>
        Query("SELECT family, revision FROM store_revisions", reader => (reader.GetString(0), reader.GetInt64(1)))
            .ToDictionary(each => each.Item1, each => each.Item2, StringComparer.Ordinal);

    /// <summary>Every process run that has written the store, oldest first.</summary>
    public IReadOnlyList<StoreRun> Runs() =>
        Query(
            "SELECT run, kind, pid, started, began FROM store_runs ORDER BY began, run",
            reader => new StoreRun(reader.GetString(0), reader.GetString(1), reader.GetInt32(2), reader.IsDBNull(3) ? null : reader.GetInt64(3), reader.GetString(4)));

    /// <summary>
    /// Import one family's file, once the start's confirmation is settled: <paramref name="load"/> writes the family's rows and the import's
    /// own row is written with them, in one transaction that moves the family's revision. Where it is not settled nothing is written and
    /// the outcome says what it waits on, to be asked again at the next look.
    /// </summary>
    public ImportOutcome Import(StoreImport import, Action<DesktopWrite>? load = null)
    {
        if (_settled() is { Is: false } waiting) return ImportOutcome.Held(waiting.Said);
        var id = Write(import.Family, write =>
        {
            load?.Invoke(write);
            write.Execute(
                "INSERT INTO imports (family, path, size, sha256, at, build, rows, class) VALUES ($family, $path, $size, $sha256, $at, $build, $rows, $class)",
                ("$family", import.Family), ("$path", import.Path), ("$size", import.Size), ("$sha256", import.Sha256),
                ("$at", Now()), ("$build", _build), ("$rows", import.Rows), ("$class", import.Class));
            return write.Scalar<long>("SELECT last_insert_rowid()");
        });
        return new ImportOutcome(true, null, id);
    }

    /// <summary>The latest import of a file into a family, which the return compares a left file's SHA-256 with; null where none.</summary>
    public ImportRecord? LastImport(string family, string path) =>
        Query(
            "SELECT id, family, path, size, sha256, rows, class, at, build FROM imports WHERE family = $family AND path = $path ORDER BY id DESC LIMIT 1",
            reader => new ImportRecord(
                reader.GetInt64(0),
                new StoreImport(reader.GetString(1), reader.GetString(2), reader.GetInt64(3), reader.GetString(4), reader.GetInt32(5), reader.GetString(6)),
                reader.GetString(7),
                reader.GetString(8)),
            ("$family", family), ("$path", path)).FirstOrDefault();

    public void Dispose()
    {
        lock (_turn)
        {
            if (_disposed) return;
            _disposed = true;
            _connection.Dispose();
        }
    }

    /// <summary>One migration step in its own exclusive transaction, read again inside it: another process may have run it first.</summary>
    private void Apply(StoreMigration step)
    {
        try
        {
            using var transaction = Begin();
            var stamp = ReadStamp(transaction);
            if (stamp.Writer > Own || stamp.Version >= step.Version) return;
            step.Apply(new DesktopWrite(_connection, transaction));
            using var command = _connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                INSERT INTO store_version (one, version, writer, build, at) VALUES (1, $version, $writer, $build, $at)
                ON CONFLICT (one) DO UPDATE SET version = MAX(version, excluded.version), writer = MAX(writer, excluded.writer), build = excluded.build, at = excluded.at
                """;
            command.Parameters.AddWithValue("$version", step.Version);
            command.Parameters.AddWithValue("$writer", Math.Max(stamp.Writer, step.Writer));
            command.Parameters.AddWithValue("$build", _build);
            command.Parameters.AddWithValue("$at", Now());
            command.ExecuteNonQuery();
            transaction.Commit();
        }
        catch (SqliteException error) when (Busy(error))
        {
            throw new DesktopStoreBusyException(error);
        }
    }

    /// <summary>This process's run, written inside the first transaction that writes.</summary>
    private void Register(DesktopWrite write)
    {
        using var self = Process.GetCurrentProcess();
        write.Execute(
            "INSERT OR IGNORE INTO store_runs (run, kind, pid, started, began) VALUES ($run, $kind, $pid, $started, $began)",
            ("$run", Run), ("$kind", _kind), ("$pid", Environment.ProcessId), ("$started", StartOf(self)), ("$began", Now()));
    }

    private SqliteTransaction Begin()
    {
        try
        {
            return _connection.BeginTransaction(deferred: false);
        }
        catch (SqliteException error) when (Busy(error))
        {
            throw new DesktopStoreBusyException(error);
        }
    }

    /// <summary>The version and stamp the file holds, read inside <paramref name="transaction"/> where one is open; (0, 0) for a file with none.</summary>
    private (int Version, int Writer) ReadStamp(SqliteTransaction? transaction)
    {
        using var has = _connection.CreateCommand();
        has.Transaction = transaction;
        has.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'store_version'";
        if (has.ExecuteScalar() is null) return (0, 0);
        using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT version, writer FROM store_version WHERE one = 1";
        using var reader = command.ExecuteReader();
        return reader.Read() ? (reader.GetInt32(0), reader.GetInt32(1)) : (0, 0);
    }

    /// <summary>The wake file touched after a commit: a file that holds nothing, whose change is what a watcher reads. It never fails the write.</summary>
    private void Touch()
    {
        try
        {
            using (new FileStream(Wake, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
            {
            }

            File.SetLastWriteTimeUtc(Wake, DateTime.UtcNow);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // The write is committed; the watchers' own period is the fallback for a wake that did not land.
        }
    }

    private string Now() => _clock.GetUtcNow().ToString("O", System.Globalization.CultureInfo.InvariantCulture);

    private static long? StartOf(Process process)
    {
        try
        {
            return process.StartTime.ToUniversalTime().Ticks;
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            return null;
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
