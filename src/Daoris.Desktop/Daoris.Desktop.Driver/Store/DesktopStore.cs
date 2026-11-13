using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;

namespace Daoris.Driver;

/// <summary>The desktop's store, <c>data/desktop.db</c>: one SQLite file the driver library alone opens, machine-local and never synced.</summary>
/// <remarks>
/// The engine (connection, pragmas, write lock, the stamp read under it, the revision, the wake file) is the port's,
/// <see cref="IDesktopStoreEngine"/>, which the root hands in the ambient set; this class is the policy over it: the numbered steps,
/// forward only, a stamp-raising one held for the start's confirmation (<see cref="Settled"/>), the run recorded, the imports.
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

    public const int HandOffsVersion = ClaimExpectVersion + 1;

    public const int ConversationFactsVersion = HandOffsVersion + 1;

    public const int SessionEventHeadsVersion = ConversationFactsVersion + 1;

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

    private readonly IDesktopStoreEngine _engine;

    private readonly IReadOnlyList<StoreMigration> _steps;

    private readonly Func<StoreSettled> _settled;

    private readonly TimeProvider _clock;

    private readonly string _build;

    private readonly string _kind;

    private bool _registered;

    private DesktopStore(IDesktopStoreEngine engine, DesktopStoreOptions options, string build)
    {
        _engine = engine;
        _steps = options.Steps ?? Steps;
        _settled = options.Settled ?? (() => StoreSettled.Not("no reading of the start's confirmation was handed to the store"));
        _clock = options.Clock ?? TimeProvider.System;
        _build = build;
        _kind = options.Kind;
        Own = options.Writer;
        Run = options.Run ?? Guid.NewGuid().ToString("N");
    }

    /// <summary>The store's file in a home: the home table's <c>desktop-db</c> entry.</summary>
    public static string LocationIn(string home) => DaorisHome.Entry(home, HomeEntry.DesktopDb);

    /// <summary>The wake file beside a store's file.</summary>
    public static string WakeOf(string location) => location + WakeSuffix;

    /// <summary>The store's file.</summary>
    public string Location => _engine.Location;

    /// <summary>The wake file a write touches once it has committed.</summary>
    public string Wake => WakeOf(Location);

    /// <summary>The writer stamp this process writes with.</summary>
    public int Own { get; }

    /// <summary>This process's run: the id its first write records in <c>store_runs</c>.</summary>
    public string Run { get; }

    /// <summary>Why a migration is waiting, in words, or null where none is: the start is not settled and the next step raises the stamp.</summary>
    public string? Held { get; private set; }

    /// <summary>The schema version and the writer stamp the file holds now.</summary>
    public (int Version, int Writer) Stamp
    {
        get
        {
            var stamp = _engine.Stamp();
            return (stamp.Version, stamp.Writer);
        }
    }

    /// <summary>Whether the file's writer stamp is past this build's: it is read as it stands and written by no one here.</summary>
    public bool Newer => Stamp.Writer > Own;

    /// <summary>The store at <paramref name="location"/>, made if there is none and migrated as far as the start allows; nothing is written to one a newer Daoris stamped.</summary>
    /// <exception cref="DesktopStoreBusyException">Another process held the write lock past the wait.</exception>
    public static DesktopStore Open(string location, DesktopStoreOptions? options = null)
    {
        options ??= new DesktopStoreOptions();
        var engines = StoreEngineSource.Current;
        var build = options.Build ?? typeof(DesktopStore).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0";
        IDesktopStoreEngine engine;
        try
        {
            engine = engines.Open(location, new DesktopStoreEngineOptions(options.Writer, options.Clock ?? TimeProvider.System, build));
        }
        catch (StoreLockBusyException busy)
        {
            throw new DesktopStoreBusyException(busy.InnerException ?? busy);
        }

        var store = new DesktopStore(engine, options, build);
        try
        {
            store.Migrate();
            return store;
        }
        catch
        {
            store.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Migrate as far as the start allows: every step but one that raises the stamp while the confirmation is not settled, which is held
    /// (<see cref="Held"/> says why) and asked again at each come-up and look. A step another process has run is skipped.
    /// </summary>
    /// <returns>True where nothing is waiting.</returns>
    public bool Migrate()
    {
        lock (_turn)
        {
            Held = null;
            foreach (var step in _steps.OrderBy(each => each.Version))
            {
                var stamp = _engine.Stamp();
                if (stamp.Writer > Own) return true;
                if (stamp.Version >= step.Version) continue;

                // Creating the store is no raise: a stamp of 0 is a file nobody has written.
                if (stamp.Writer > 0 && step.Writer > stamp.Writer && _settled() is { Is: false } waiting)
                {
                    Held = $"migration {step.Version} raises the store's writer stamp from {stamp.Writer} to {step.Writer}, and waits for the start's confirmation: {waiting.Said}";
                    return false;
                }

                Guarded(() => _engine.Apply(step.Version, step.Writer, (connection, transaction) => step.Apply(new DesktopWrite(connection, transaction))));
            }

            return true;
        }
    }

    /// <summary>One write transaction under the write lock: the stamp refused where it is past this build's, this process's run recorded, <paramref name="work"/>, the family's revision moved, the commit, the wake. Rolled back where the work throws.</summary>
    /// <exception cref="DesktopStoreNewerException">The file's stamp is past this build's.</exception>
    /// <exception cref="DesktopStoreBusyException">The write lock stayed held past the wait; nothing is written.</exception>
    public T Write<T>(string family, Func<DesktopWrite, T> work)
    {
        var registering = false;
        var result = Guarded(() => _engine.Write(family, (connection, transaction, stamp) =>
        {
            var write = new DesktopWrite(connection, transaction, stamp.Version);
            registering = !_registered;
            if (registering) Register(write);
            return work(write);
        }));
        if (registering) _registered = true;
        return result;
    }

    /// <summary>This process's run recorded in <c>store_runs</c> and nothing else: no revision moves and no wake is touched, so a shell's come-up enters before it reads the runs.</summary>
    /// <exception cref="DesktopStoreNewerException">The file's stamp is past this build's: nothing is written.</exception>
    /// <exception cref="DesktopStoreBusyException">The write lock stayed held past the wait; nothing is written.</exception>
    public void Enter()
    {
        if (_registered) return;
        Guarded(() => _engine.Write<bool>(null, (connection, transaction, _) =>
        {
            Register(new DesktopWrite(connection, transaction));
            return true;
        }));
        _registered = true;
    }

    /// <summary><see cref="Write{T}"/> for work that answers nothing.</summary>
    public void Write(string family, Action<DesktopWrite> work) =>
        Write<bool>(family, write =>
        {
            work(write);
            return true;
        });

    /// <summary>Rows read by <paramref name="map"/> from one statement, in a snapshot of their own; a read never waits on a writer.</summary>
    public IReadOnlyList<T> Query<T>(string sql, Func<DbDataReader, T> map, params (string Name, object? Value)[] values) =>
        _engine.Read(connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Bind(values);
            using var reader = command.ExecuteReader();
            var rows = new List<T>();
            while (reader.Read()) rows.Add(map(reader));
            return rows;
        });

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

    public void Dispose() => _engine.Dispose();

    /// <summary>The engine's refusals said in the process's own words.</summary>
    private T Guarded<T>(Func<T> body)
    {
        try
        {
            return body();
        }
        catch (StoreLockBusyException busy)
        {
            throw new DesktopStoreBusyException(busy.InnerException ?? busy);
        }
        catch (StoreNewerException newer)
        {
            throw new DesktopStoreNewerException(newer.Stamped, newer.Own);
        }
        catch (StoreWithoutSchemaException)
        {
            throw new DesktopStoreException($"the desktop's store has no schema yet: {Held ?? "its first migration did not run"}.");
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
}
