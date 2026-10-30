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

    /// <summary>
    /// The asks (PUSH1d1): <c>ask_revision.revision</c>, a count the asks' triggers move on every insert, update and delete of an
    /// ask, whoever writes it, a connector built before the count included. A write that changes no ask (no row has its id, or the
    /// go-aheads were decided to stay) finds no row and moves nothing.
    /// </summary>
    public const string Asks = "asks";

    /// <summary>
    /// The asks' channels (PUSH1d1): <c>channel_revision.revision</c>, a count the channel's triggers move on every message kept
    /// (posted here or mirrored from a teammate), changed or deleted, an ask's channel forgotten with it included, which the newest
    /// arrival number alone would miss. Not on a read mark, a sync's cursor, a teammate's message mirrored again or a post the
    /// cap refused.
    /// </summary>
    public const string Channel = "channel";

    /// <summary>
    /// The words kept on a session record without a note (PUSH1d1b): <c>words_revision.revision</c>, a count the sessions' trigger
    /// moves each time a record's <c>said</c> list grows, whoever writes it, a connector built before the count included. Such a
    /// word issues no session revision, since it never travels. Not on a take (the list shrinks or stays), a push's mark or a cursor.
    /// </summary>
    public const string Words = "words";

    /// <summary>
    /// The event journal (FOUNDATION1 row k part 2, the foundation design §4.1): its newest position, which every journalled move of a record
    /// raises, a word taken included, which moves no family above. <b>Never one of <see cref="All"/></b> and never in a default answer: a
    /// client from before it wakes its <c>ledger</c> topic for any family an answer names, so the stream names this one only to a reader that
    /// asks for it (<c>journal=1</c> on the route; <see cref="ChangeStream.WaitAsync"/>'s <c>journal</c>), and an older client is woken by
    /// nothing a journal-only append does. The host's <c>journal</c> protocol mark says it does.
    /// </summary>
    public const string Journal = "journal";

    /// <summary>Every family, in the order an answer names them. A reader that never asked for the journal is answered these alone.</summary>
    public static IReadOnlyList<string> All { get; } = [Sessions, Quests, Asks, Channel, Words];

    /// <summary>Every family the sampler follows, in <see cref="Revisions"/>' order: <see cref="All"/>, then the journal, which a reader that asks is also answered.</summary>
    internal static IReadOnlyList<string> Observed { get; } = [.. All, Journal];

    /// <summary>
    /// Each family's revision, in <see cref="Observed"/>'s order, in one autocommit read, then the journal's floor, which is no family: the
    /// columns are read by position, so what is new follows what is there and no reader's column shifts. The journal's revision is its newest
    /// position, the key's own count, so a trim or an emptied table never takes it back; its floor is the lowest position kept, one past the
    /// newest where none is kept.
    /// </summary>
    internal const string Revisions = """
        SELECT COALESCE((SELECT revision FROM session_revision WHERE one = 1), 0),
               COALESCE((SELECT MAX(position) FROM quest_log), 0),
               COALESCE((SELECT revision FROM ask_revision WHERE one = 1), 0),
               COALESCE((SELECT revision FROM channel_revision WHERE one = 1), 0),
               COALESCE((SELECT revision FROM words_revision WHERE one = 1), 0),
               COALESCE((SELECT seq FROM sqlite_sequence WHERE name = 'event_journal'), 0),
               COALESCE((SELECT MIN(position) FROM event_journal),
                        COALESCE((SELECT seq FROM sqlite_sequence WHERE name = 'event_journal'), 0) + 1)
        """;
}

/// <summary>
/// What a wait on the change stream answers: the cursor to ask from next, and the families that moved after the one asked from,
/// in <see cref="ChangeFamilies.All"/>'s order, the journal after them where the reader asked for it. Families, never records, and no
/// path (D169 §3). <paramref name="Journal"/> is the journal's place, answered only to a reader that asked for the journal.
/// </summary>
public sealed record ChangesAnswer(string Since, IReadOnlyList<string> Moved, JournalPlace? Journal = null);

/// <summary>
/// Where the event journal stands (FOUNDATION1 row k part 2, §4.1): the store's identity, which a position is of (the journal's epoch), the
/// lowest position kept (<paramref name="Floor"/>), and the newest issued (<paramref name="Newest"/>). A reader whose cursor is below the floor
/// has lost the moves between, and one whose store is another has no cursor of this one's.
/// </summary>
public sealed record JournalPlace(string Store, long Floor, long Newest);

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
/// The epoch is new when the host starts, when the observer opens after its read failed (or after a cursor went out before its
/// first open), and when the database at the path is another one. A missing, malformed, stale or future cursor is answered at
/// once with every family and the current cursor: never an error its caller must handle apart.</para>
///
/// <para><b>Bounded.</b> At most <see cref="MostWaits"/> waits are held at once, a ninth answered at once with what the last sample
/// counted, never sampling itself; a wait lasts <see cref="LongestWait"/> at most from its arrival; and the sampler runs only
/// while a wait is held.</para>
///
/// <para><b>A replaced file.</b> On Windows the observer's open handle keeps a replacement out (SQLite shares no delete), so a
/// different database can arrive only across a reconnection, which begins an epoch already. Elsewhere the path is read apart,
/// every <see cref="IdentityPace"/> while waits are held: another stamp (written once into the file), or, since a copy carries
/// the stamp, other revisions than the observer's, reconnects the observer (<see cref="IsAnother"/>).</para>
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
    private readonly TimeProvider _clock;
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
    private bool _issued;
    private int _waits;
    private Task? _pump;
    private TaskCompletionSource _moved = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Where the journal stood at the last sample, answered to a reader that asks for it; null before the observer first reads.
    private JournalPlace? _place;

    private ChangeStream(string database, MachineLog? log, TimeProvider clock, TimeSpan pace, TimeSpan? identityPace, TimeSpan reopenAfter)
    {
        _database = database;
        _log = log;
        _clock = clock;
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
    /// <param name="clock">
    /// The clock a wait's bound is read on: the machine's unless a test holds it (TESTCLOCK3), so a wait the clock never ends is
    /// answered only by a move, a cursor or a slot, and "at once" is a claim the test states without timing it. The sampler's
    /// paces are how often it looks, not a bound, and stay on the machine's clock.
    /// </param>
    public static async Task<ChangeStream> OpenAsync(
        string database, MachineLog? log = null, TimeSpan? pace = null, TimeSpan? identityPace = null, TimeSpan? reopenAfter = null,
        TimeProvider? clock = null, CancellationToken ct = default)
    {
        var stream = new ChangeStream(
            database, log, clock ?? TimeProvider.System, pace ?? Pace, identityPace ?? (OperatingSystem.IsWindows() ? null : IdentityPace),
            reopenAfter ?? ReopenAfter);
        await stream.SampleAsync(ct).ConfigureAwait(false);
        return stream;
    }

    /// <summary>The current cursor, <c>&lt;epoch&gt;.&lt;n&gt;</c>.</summary>
    public string Cursor
    {
        get
        {
            lock (_gate)
            {
                _issued = true;
                return CursorOf();
            }
        }
    }

    /// <summary>How many writes the observer has seen (a move of <c>data_version</c>), whether or not they moved a family.</summary>
    internal long Seen => Interlocked.Read(ref _seen);

    /// <summary>
    /// Holds the sampler as a sample under way holds it, until the answer is disposed: what a writer's lock does to a sample, for as
    /// long as a test needs (TESTCLOCK3), so "never queues behind it" is a wait that never ends rather than one timed.
    /// </summary>
    internal async Task<IDisposable> HoldSamplerAsync()
    {
        await _sampling.WaitAsync().ConfigureAwait(false);
        return new Released(_sampling);
    }

    private sealed class Released(SemaphoreSlim held) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) held.Release();
        }
    }

    /// <summary>
    /// What moved after <paramref name="since"/>, answered as soon as a family moves or at <paramref name="wait"/> with nothing.
    /// A cancelled wait answers what it would have answered then.
    /// </summary>
    /// <param name="journal">
    /// Whether the reader asks for the event journal (FOUNDATION1 row k part 2): it is then woken by, and answered with, the
    /// <see cref="ChangeFamilies.Journal"/> family and the journal's place. By default it is neither: a reader from before the journal is
    /// woken by nothing a journal-only append does.
    /// </param>
    public async Task<ChangesAnswer> WaitAsync(string? since, TimeSpan wait, CancellationToken ct = default, bool journal = false)
    {
        if (wait < TimeSpan.Zero) wait = TimeSpan.Zero;
        if (wait > LongestWait) wait = LongestWait;

        // The wait is counted from its arrival, whatever a sample ahead of it takes.
        var arrived = _clock.GetTimestamp();
        lock (_gate)
        {
            // A wait past the bound is answered at once with what the last sample counted, and never samples itself: it never
            // queues behind a sample a writer's lock holds, so the bound bounds the work too.
            if (_waits >= MostWaits) return Judged(since, journal).Answer;
            _waits++;
            _pump ??= Task.Run(PumpAsync);
        }

        try
        {
            // A sample at once, unless one is under way: what moved while nobody waited is counted now, as the observer kept its
            // place meanwhile. One under way is followed by the pump's next, which this wait now keeps running.
            if (await _sampling.WaitAsync(TimeSpan.Zero, CancellationToken.None).ConfigureAwait(false))
            {
                try
                {
                    if (!_disposed) await SampleHeldAsync().ConfigureAwait(false);
                }
                finally
                {
                    _sampling.Release();
                }
            }

            while (true)
            {
                var (answer, moved) = Judged(since, journal);
                var left = wait - _clock.GetElapsedTime(arrived);
                if (answer.Moved.Count > 0 || left <= TimeSpan.Zero || ct.IsCancellationRequested) return answer;

                try
                {
                    await moved.WaitAsync(left, _clock, ct).ConfigureAwait(false);
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
            lock (_gate) _waits--;
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
    private (ChangesAnswer Answer, Task Moved) Judged(string? since, bool journal)
    {
        lock (_gate)
        {
            _issued = true;
            var current = CursorOf();
            // The journal is named, and its place answered, to the reader that asked for it and to none else.
            var families = journal ? ChangeFamilies.Observed : ChangeFamilies.All;
            var place = journal ? _place : null;
            if (!TryParse(since, out var epoch, out var n) || epoch != _epoch || n > _count)
            {
                return (new ChangesAnswer(current, families, place), _moved.Task);
            }

            IReadOnlyList<string> moved = [.. families.Where(family => _movedAt.GetValueOrDefault(family) > n)];
            return (new ChangesAnswer(current, moved, place), _moved.Task);
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
                // The path's read sits between two of the observer's, so a write to the same file between them cannot pass for
                // another database: where the observer held still, the path's revisions are its own.
                var before = await GatedAsync(observer, Revisions).ConfigureAwait(false);
                var found = await IdentityAtPathAsync().ConfigureAwait(false);
                var after = await GatedAsync(observer, Revisions).ConfigureAwait(false);
                if (found.Known && IsAnother(_identity, before, found.Id, found.Revisions, after))
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

            var moved = ChangeFamilies.Observed.Where((_, i) => revisions[i] != _revisions[i]).ToList();
            _revisions = revisions;
            if (moved.Count == 0)
            {
                // The floor moves with no family (a trim comes with the insert that moves the newest position, a person's own delete does not).
                lock (_gate) _place = PlaceOf(_identity, revisions);
                return;
            }

            lock (_gate)
            {
                _place = PlaceOf(_identity, revisions);
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

            lock (_gate)
            {
                _place = PlaceOf(identity, revisions);
                // The start's own open keeps the epoch only while no cursor went out: one issued before the observer could read
                // anything holds no baseline, and what moved since it would otherwise be taken as the baseline and never answered.
                if (first && !_issued) return;
                _epoch = NewEpoch();
                _count = 0;
                _movedAt.Clear();
                Signal();
            }

            _log?.Info("changes.epoch", ("why", first ? "opened" : why));
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
    /// The file's identity: the stamp kept in it, which the session store's schema writes once (FOUNDATION1 row k: it is the event
    /// journal's epoch, so the schema owns it, not this observer), and which a file stamped by an earlier build's observer already
    /// carries. A file with none is not stamped here: the observer's open waits and tries again once the schema has run. A different
    /// database at the path has its own, or none.
    /// </summary>
    internal static string Identity(ConnectionGate observer) =>
        StampOf(observer) ?? throw new InvalidOperationException("the store's identity is not written yet");

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

    /// <summary>
    /// The stamp and the families' revisions of the database at the path now, read apart from the observer; unknown where a
    /// writer held it.
    /// </summary>
    private async Task<(bool Known, string? Id, long[]? Revisions)> IdentityAtPathAsync()
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
            var (stamp, revisions) = await GatedAsync(probe, gate => (StampOf(gate), RevisionsIfAny(gate))).ConfigureAwait(false);
            return (true, stamp, revisions);
        }
        catch (SqliteException held) when (held.SqliteErrorCode is Busy or Locked)
        {
            return (false, null, null);
        }
        catch (SqliteException)
        {
            // No database there to read: not the one observed.
            return (true, null, null);
        }
    }

    private static long[]? RevisionsIfAny(ConnectionGate probe)
    {
        try
        {
            return Revisions(probe);
        }
        catch (SqliteException none) when (none.SqliteErrorCode == 1)
        {
            // SQLITE_ERROR: no family's table there.
            return null;
        }
    }

    /// <summary>
    /// Whether the database at the path is another than the one observed (FABRIC-EVENT3b's review): another stamp or none; no
    /// family's revisions; or, since a copy of the file carries the stamp, revisions that differ from the observer's while the
    /// observer's held still around the path's read (<paramref name="before"/>, <paramref name="after"/>). Where the observer
    /// moved between, nothing is told this time. A copy of the current state with nothing written since is told by the first
    /// write that reaches only one of the two.
    /// </summary>
    internal static bool IsAnother(string? observed, long[] before, string? stamp, long[]? atPath, long[] after) =>
        !string.Equals(stamp, observed, StringComparison.Ordinal)
        || atPath is null
        || (before.SequenceEqual(after) && !atPath.SequenceEqual(before));

    /// <summary>The families' revisions, the journal's newest position after them, then its floor.</summary>
    private static long[] Revisions(ConnectionGate observer)
    {
        using var read = observer.Command();
        read.CommandText = ChangeFamilies.Revisions;
        using var row = read.ExecuteReader();
        if (!row.Read()) throw new InvalidOperationException("the families' revisions read no row");
        return [.. Enumerable.Range(0, ChangeFamilies.Observed.Count + 1).Select(row.GetInt64)];
    }

    private static JournalPlace? PlaceOf(string? identity, long[] revisions) =>
        identity is null ? null : new JournalPlace(identity, Floor: revisions[ChangeFamilies.Observed.Count], Newest: revisions[ChangeFamilies.Observed.Count - 1]);

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
