using System.Diagnostics;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Daoris.Knowledge;

/// <summary>
/// The ledger's families the change stream answers (FABRIC-EVENT3b, the fabric events design §A4.2), each by its own revision.
/// A family is added once its revision is shown to move on every write a look acts on and on none it makes itself; a family not
/// here stays on the look's clock.
/// </summary>
public static class ChangeFamilies
{
    /// <summary>
    /// The session records: <c>session_revision.revision</c>, the highest revision issued (HIST1a). It moves on each write that
    /// issues a record's revision (an open, a state, a mirror, a took, a note kept with words); not on words kept or taken without
    /// a note, a push's mark, or the cursors, which never travel either.
    /// </summary>
    public const string Sessions = "sessions";

    /// <summary>
    /// The quests: the newest <c>quest_log.position</c>. It moves on each operation kept, made here (a publish, a take, a
    /// respond, a review, a wait) or integrated from a remote; not on a pass's record (<c>quest_passes</c>), a cursor, a push's
    /// numbers, a rebase's rewrite in place, or a delete of older operations.
    /// </summary>
    public const string Quests = "quests";

    /// <summary>Every family, in the order an answer names them.</summary>
    public static IReadOnlyList<string> All { get; } = [Sessions, Quests];

    /// <summary>Each family's revision, in <see cref="All"/>'s order, in one autocommit read.</summary>
    internal const string Revisions = """
        SELECT COALESCE((SELECT revision FROM session_revision WHERE one = 1), 0),
               COALESCE((SELECT MAX(position) FROM quest_log), 0)
        """;
}

/// <summary>
/// What a wait on the change stream answers: the cursor to ask from next, and the families that moved after the one asked from,
/// in <see cref="ChangeFamilies.All"/>'s order. Families, never records, and no path (D169 §3).
/// </summary>
public sealed record ChangesAnswer(string Since, IReadOnlyList<string> Moved);

/// <summary>
/// The local host's change stream (FABRIC-EVENT3b, the fabric events design §A4.2): one sampler serving every wait, on an observer
/// connection of its own, apart from the store's (which the composed stores share). It reads <c>PRAGMA data_version</c> in short
/// autocommit reads, which moves when any other connection commits: the host's own writes, and each connector's from its own
/// process. When it moves, each family's own revision is read, and only a family whose revision moved is marked.
/// </summary>
/// <remarks>
/// <para><b>Families, never the database.</b> A database-wide hint would loop: the driver's look syncs, each pass records itself
/// and rewrites its <c>tried</c>, and the look would wake its next look for ever (canary 5). A write that moves no family's
/// revision wakes nobody.</para>
///
/// <para><b>The cursor</b> is <c>&lt;epoch&gt;.&lt;n&gt;</c>: <c>n</c> counts the samples that moved a family, and each family keeps
/// the last <c>n</c> it moved at, so a family that moved several times since a cursor is answered once and nothing is replayed.
/// The epoch is new when the host starts, when the observer reconnects after its read failed, and when the database at the path
/// is another one (its identity, a stamp kept in the file, changed). A missing, malformed, stale or future cursor is answered at
/// once with every family and the current cursor: never an error its caller must handle apart.</para>
///
/// <para><b>Bounded.</b> At most <see cref="MostWaits"/> waits are held at once, a ninth answered at once with the current
/// cursor; a wait lasts <see cref="LongestWait"/> at most; and the sampler runs only while a wait is held.</para>
///
/// <para><b>A replaced file.</b> On Windows the observer's open handle keeps a replacement out (SQLite shares no delete), so a
/// different database can arrive only across a reconnection, which begins an epoch already; elsewhere the path's stamp is read
/// apart, every <see cref="IdentityPace"/> while waits are held, and a different one reconnects the observer.</para>
/// </remarks>
public sealed class ChangeStream : IAsyncDisposable
{
    /// <summary>The door it answers at (a local host's only).</summary>
    public const string Route = "/api/changes";

    /// <summary>The most waits held at once; a ninth is answered at once.</summary>
    public const int MostWaits = 8;

    /// <summary>The longest a wait is held.</summary>
    public static readonly TimeSpan LongestWait = TimeSpan.FromSeconds(30);

    /// <summary>How often the sampler reads while a wait is held: a read of a counter, in microseconds.</summary>
    public static readonly TimeSpan Pace = TimeSpan.FromMilliseconds(250);

    /// <summary>How often the path's identity is read where a replacement can happen under an open handle.</summary>
    public static readonly TimeSpan IdentityPace = TimeSpan.FromSeconds(30);

    /// <summary>How long a lost observer waits before it opens again.</summary>
    public static readonly TimeSpan ReopenAfter = TimeSpan.FromSeconds(5);

    // SQLite's busy and locked: a writer held the file for the moment; the next sample reads again, and nothing was lost.
    private const int Busy = 5;
    private const int Locked = 6;

    private readonly string _database;
    private readonly MachineLog? _log;
    private readonly TimeSpan _pace;
    private readonly TimeSpan? _identityPace;
    private readonly TimeSpan _reopenAfter;

    // The sampler's own state, touched only inside `_sampling`.
    private readonly SemaphoreSlim _sampling = new(1, 1);
    private SqliteConnection? _observer;
    private string? _identity;
    private long _version;
    private long[] _revisions = [];
    private long _identityAt;
    private long _reopenAt;
    private bool _opened;
    private bool _disposed;
    private long _seen;

    // What the waits read, under `_gate`.
    private readonly object _gate = new();
    private readonly CancellationTokenSource _closing = new();
    private readonly Dictionary<string, long> _movedAt = new(StringComparer.Ordinal);
    private string _epoch = NewEpoch();
    private long _count;
    private int _waits;
    private Task? _pump;
    private TaskCompletionSource _moved = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private ChangeStream(string database, MachineLog? log, TimeSpan pace, TimeSpan? identityPace, TimeSpan reopenAfter)
    {
        _database = database;
        _log = log;
        _pace = pace;
        _identityPace = identityPace;
        _reopenAfter = reopenAfter;
    }

    /// <summary>
    /// The stream over the database at <paramref name="database"/>, its observer opened now, as the host starts. A file that
    /// does not open yet is opened again at the first wait, after <paramref name="reopenAfter"/>.
    /// </summary>
    /// <param name="pace">How often the sampler reads while a wait is held; <see cref="Pace"/> unless a test hurries it.</param>
    /// <param name="identityPace">How often the path's identity is read; by default <see cref="IdentityPace"/>, and never on Windows.</param>
    public static async Task<ChangeStream> OpenAsync(
        string database, MachineLog? log = null, TimeSpan? pace = null, TimeSpan? identityPace = null, TimeSpan? reopenAfter = null,
        CancellationToken ct = default)
    {
        var stream = new ChangeStream(
            database, log, pace ?? Pace, identityPace ?? (OperatingSystem.IsWindows() ? null : IdentityPace), reopenAfter ?? ReopenAfter);
        await stream.SampleAsync(ct).ConfigureAwait(false);
        return stream;
    }

    /// <summary>The current cursor, <c>&lt;epoch&gt;.&lt;n&gt;</c>.</summary>
    public string Cursor
    {
        get
        {
            lock (_gate) return CursorOf();
        }
    }

    /// <summary>How many writes the observer has seen (a move of <c>data_version</c>), whether or not they moved a family.</summary>
    internal long Seen => Interlocked.Read(ref _seen);

    /// <summary>
    /// What moved after <paramref name="since"/>, answered as soon as a family moves or at <paramref name="wait"/> with nothing.
    /// A cancelled wait answers what it would have answered then.
    /// </summary>
    public async Task<ChangesAnswer> WaitAsync(string? since, TimeSpan wait, CancellationToken ct = default)
    {
        if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;
        if (wait > LongestWait) wait = LongestWait;

        bool held;
        lock (_gate)
        {
            held = _waits < MostWaits;
            if (held)
            {
                _waits++;
                _pump ??= Task.Run(PumpAsync);
            }
        }

        try
        {
            try
            {
                // A sample at once: what moved while nobody waited is counted now, and the observer kept its place meanwhile.
                await SampleAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return Judged(since).Answer;
            }

            var until = Stopwatch.GetTimestamp() + (long)(wait.TotalSeconds * Stopwatch.Frequency);
            while (true)
            {
                var (answer, moved) = Judged(since);
                var left = held ? Stopwatch.GetElapsedTime(Stopwatch.GetTimestamp(), until) : TimeSpan.Zero;
                if (answer.Moved.Count > 0 || left <= TimeSpan.Zero || ct.IsCancellationRequested) return answer;

                try
                {
                    await moved.WaitAsync(left, ct).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
        finally
        {
            if (held)
            {
                lock (_gate) _waits--;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _closing.CancelAsync().ConfigureAwait(false);
        await _sampling.WaitAsync().ConfigureAwait(false);
        try
        {
            _disposed = true;
            Close();
        }
        finally
        {
            _sampling.Release();
        }

        if (_pump is { } pump) await pump.ConfigureAwait(false);
        lock (_gate) Signal();
    }

    /// <summary>The answer to a cursor now, and what the next move will complete.</summary>
    private (ChangesAnswer Answer, Task Moved) Judged(string? since)
    {
        lock (_gate)
        {
            var current = CursorOf();
            if (!TryParse(since, out var epoch, out var n) || epoch != _epoch || n > _count)
            {
                return (new ChangesAnswer(current, ChangeFamilies.All), _moved.Task);
            }

            IReadOnlyList<string> moved = [.. ChangeFamilies.All.Where(family => _movedAt.GetValueOrDefault(family) > n)];
            return (new ChangesAnswer(current, moved), _moved.Task);
        }
    }

    private string CursorOf() => _epoch + "." + _count.ToString(CultureInfo.InvariantCulture);

    private static bool TryParse(string? since, out string epoch, out long n)
    {
        (epoch, n) = ("", 0);
        if (string.IsNullOrEmpty(since)) return false;
        var dot = since.IndexOf('.');
        if (dot <= 0) return false;
        epoch = since[..dot];
        return long.TryParse(since[(dot + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out n);
    }

    private static string NewEpoch() => Guid.NewGuid().ToString("N")[..12];

    // Every wait reads the cursor again: a family moved, or the epoch began anew.
    private void Signal() => Interlocked.Exchange(ref _moved, new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).TrySetResult();

    /// <summary>The sampler, while a wait is held: it ends with the last wait, and the next wait starts it again.</summary>
    private async Task PumpAsync()
    {
        try
        {
            while (true)
            {
                await Task.Delay(_pace, _closing.Token).ConfigureAwait(false);
                lock (_gate)
                {
                    if (_waits == 0)
                    {
                        _pump = null;
                        return;
                    }
                }

                await SampleAsync(_closing.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task SampleAsync(CancellationToken ct)
    {
        await _sampling.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!_disposed) await SampleHeldAsync().ConfigureAwait(false);
        }
        finally
        {
            _sampling.Release();
        }
    }

    /// <summary>One sample: the counter, and each family's revision when it moved. Inside <c>_sampling</c>.</summary>
    private async Task SampleHeldAsync()
    {
        var now = Stopwatch.GetTimestamp();
        if (_observer is not { } observer)
        {
            if (now >= _reopenAt) await ReopenAsync(now, "reconnected").ConfigureAwait(false);
            return;
        }

        try
        {
            if (_identityPace is { } identityPace && now >= _identityAt)
            {
                _identityAt = now + Ticks(identityPace);
                if (await IdentityAtPathAsync().ConfigureAwait(false) is { Known: true } found
                    && !string.Equals(found.Id, _identity, StringComparison.Ordinal))
                {
                    Close();
                    await ReopenAsync(now, "replaced").ConfigureAwait(false);
                    return;
                }
            }

            var version = await GatedAsync(observer, gate => Scalar(gate, "PRAGMA data_version;")).ConfigureAwait(false);
            if (version == _version) return;

            // The counter is kept only once the revisions read, so a read a writer held off is read again at the next sample.
            var revisions = await GatedAsync(observer, Revisions).ConfigureAwait(false);
            _version = version;
            Interlocked.Increment(ref _seen);

            var moved = ChangeFamilies.All.Where((_, i) => revisions[i] != _revisions[i]).ToList();
            _revisions = revisions;
            if (moved.Count == 0) return;

            lock (_gate)
            {
                _count++;
                foreach (var family in moved) _movedAt[family] = _count;
                Signal();
            }
        }
        catch (SqliteException held) when (held.SqliteErrorCode is Busy or Locked)
        {
        }
        catch (Exception lost) when (lost is SqliteException or InvalidOperationException or IOException)
        {
            // Lost: closed, and opened again after a pause; the epoch is new once it reads again, as what moved meanwhile is unknown.
            _log?.Warn("changes.lost", ("code", (lost as SqliteException)?.SqliteErrorCode));
            Close();
            _reopenAt = now + Ticks(_reopenAfter);
        }
    }

    /// <summary>
    /// The observer opened, its identity and baseline read; a new epoch for every open but the first, said in the machine log
    /// with <paramref name="why"/> (<c>reconnected</c> after a lost read, <c>replaced</c> for another database at the path).
    /// </summary>
    private async Task ReopenAsync(long now, string why)
    {
        SqliteConnection? observer = null;
        try
        {
            observer = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = _database,
                // Never created here: the store makes the file, and a path with none is a store not there yet.
                Mode = SqliteOpenMode.ReadWrite,
                // Its own handle, apart from every pool, so it is the observer's alone and closes when it goes.
                Pooling = false,
                DefaultTimeout = 2,
            }.ToString());
            observer.Open();
            var (identity, version, revisions) = await GatedAsync(
                observer, gate => (Identity(gate), Scalar(gate, "PRAGMA data_version;"), Revisions(gate))).ConfigureAwait(false);

            (_observer, _identity, _version, _revisions) = (observer, identity, version, revisions);
            _identityAt = now + Ticks(_identityPace ?? TimeSpan.Zero);
            var first = !_opened;
            _opened = true;
            if (first) return;

            lock (_gate)
            {
                _epoch = NewEpoch();
                _count = 0;
                _movedAt.Clear();
                Signal();
            }

            _log?.Info("changes.epoch", ("why", why));
        }
        catch (Exception failed) when (failed is SqliteException or InvalidOperationException or IOException)
        {
            observer?.Dispose();
            _reopenAt = now + Ticks(_reopenAfter);
        }
    }

    private void Close()
    {
        _observer?.Dispose();
        _observer = null;
    }

    /// <summary>
    /// The file's identity: a stamp kept in it, written once by the first observer to find none. A different database at the
    /// path has its own, or none.
    /// </summary>
    private static string Identity(ConnectionGate observer)
    {
        if (StampOf(observer) is { } stamp) return stamp;

        using var write = observer.Command();
        write.CommandText = """
            CREATE TABLE IF NOT EXISTS store_identity (one INTEGER PRIMARY KEY CHECK (one = 1), id TEXT NOT NULL);
            INSERT OR IGNORE INTO store_identity (one, id) VALUES (1, lower(hex(randomblob(16))));
            """;
        write.ExecuteNonQuery();
        return StampOf(observer) ?? throw new InvalidOperationException("the store's identity was written and does not read");
    }

    private static string? StampOf(ConnectionGate connection)
    {
        try
        {
            using var read = connection.Command();
            read.CommandText = "SELECT id FROM store_identity WHERE one = 1;";
            return read.ExecuteScalar() as string;
        }
        catch (SqliteException none) when (none.SqliteErrorCode == 1)
        {
            // SQLITE_ERROR: no such table yet.
            return null;
        }
    }

    /// <summary>The stamp of the database at the path now, read apart from the observer; unknown where a writer held it.</summary>
    private async Task<(bool Known, string? Id)> IdentityAtPathAsync()
    {
        try
        {
            using var probe = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = _database,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
                DefaultTimeout = 2,
            }.ToString());
            probe.Open();
            return (true, await GatedAsync(probe, StampOf).ConfigureAwait(false));
        }
        catch (SqliteException held) when (held.SqliteErrorCode is Busy or Locked)
        {
            return (false, null);
        }
        catch (SqliteException)
        {
            // No database there to read: not the one observed.
            return (true, null);
        }
    }

    private static long[] Revisions(ConnectionGate observer)
    {
        using var read = observer.Command();
        read.CommandText = ChangeFamilies.Revisions;
        using var row = read.ExecuteReader();
        if (!row.Read()) throw new InvalidOperationException("the families' revisions read no row");
        return [.. Enumerable.Range(0, ChangeFamilies.All.Count).Select(row.GetInt64)];
    }

    private static long Scalar(ConnectionGate connection, string sql)
    {
        using var read = connection.Command();
        read.CommandText = sql;
        return Convert.ToInt64(read.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Short reads on a connection of the stream's own, made through its gate as every command in the service is (SQLITETX1): only
    /// the sampler uses it, under <c>_sampling</c>, so the gate is never waited on.
    /// </summary>
    private static Task<T> GatedAsync<T>(SqliteConnection connection, Func<ConnectionGate, T> read)
    {
        var gate = ConnectionGate.For(connection);
        return gate.RunAsync(() => Task.FromResult(read(gate)), CancellationToken.None);
    }

    private static long Ticks(TimeSpan span) => (long)(span.TotalSeconds * Stopwatch.Frequency);
}
