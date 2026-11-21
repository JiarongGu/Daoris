using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Daoris.Ports.Store;

namespace Daoris.Driver;

/// <summary>
/// The bus log's store today: two tables of the desktop store (<c>bus_log</c>, <c>bus_measures</c>, made by
/// <see cref="BusLogStoreStep"/>), on a connection of its own that the lease holder keeps open, write-ahead logged with
/// <c>synchronous = NORMAL</c>: a commit survives the process's crash, and a power cut may lose the last second of entries, which the
/// store's other writers' full syncs and the next checkpoint make durable. A commit-class batch is synced in full.
/// </summary>
/// <remarks>
/// <para><b>Under the store's own rules.</b> The tables come from the store's numbered steps, and each write reads the writer stamp
/// inside its transaction, as the store's engine does: a store a newer Daoris stamped is written by no one here. A write moves no
/// family's revision and touches no wake, since nothing reads the log yet.</para>
/// <para><b>One writer, one order.</b> The transaction takes the store for writing at its start; inside it the batch's writer (the
/// home's lease) is asked, and the batch is seated after the newest order kept (<see cref="BusWritten.Shift"/>), so a log opened again
/// goes on after its last, and one that lost the lease and regained it goes on after what the other holder wrote, never colliding.</para>
/// <para><b>Retention</b>: the newest <see cref="Cap"/> rows, the older cut in the write that passes the cap, and the measures of the last
/// <see cref="MeasureDays"/> days. Nothing reads behind the cap yet; the cursors and snapshots that will hold the cut at
/// their floor when they come.</para>
/// <para><b>Two costs.</b> The rows' own bytes, as encoded (each text's UTF-8 length and eight for each number), and the pages each write
/// put on disk, read from the engine after its commit (<see cref="DesktopStoreEngines.PagesWritten"/>, SQLite's cache-write count): the
/// write-ahead log's frames, at least one page a flush, a checkpoint's copy into the file aside. The log file is bounded after a checkpoint
/// as the engine bounds it (<see cref="JournalLimit"/>).</para>
/// </remarks>
public sealed class DesktopBusLogStore : IBusLogStore, IBusQueueStore
{
    /// <summary>
    /// The rows kept: about 13 MiB of rows at the 270 bytes a row a check's facts measured (BusLogTests), fifty days at a thousand
    /// entries a day, until the install's own measures (<c>daoris bus stats</c>) set the budget.
    /// </summary>
    public const int Cap = 50_000;

    /// <summary>The days of measures kept.</summary>
    public const int MeasureDays = 90;

    /// <summary>The bytes the write-ahead log is cut back to after a checkpoint: the store engine's own bound.</summary>
    public const long JournalLimit = 33_554_432;

    private readonly string _home;
    private readonly int _cap;
    private readonly bool _reading;
    private readonly Func<StoreSettled>? _settled;
    private readonly Lock _gate = new();
    private DbConnection? _connection;
    private bool _stepped;
    private long _pageSize;
    private string? _measuresCutOn;

    // Whether the store keeps the timers (its step 34 ran), and the queue's consumers (its step 35); a writer's store always does, migrated as it opens.
    private bool _timers = true;
    private bool _queued = true;

    /// <param name="home">The home whose store holds the log.</param>
    /// <param name="cap">The rows kept; <see cref="Cap"/> unless a test bounds it lower.</param>
    /// <param name="settled">The reading of the start's confirmation; null reads this install's swap journal.</param>
    public DesktopBusLogStore(string home, int cap = Cap, Func<StoreSettled>? settled = null)
        : this(home, cap, settled, reading: false)
    {
    }

    private DesktopBusLogStore(string home, int cap, Func<StoreSettled>? settled, bool reading)
    {
        _home = home;
        _cap = Math.Max(1, cap);
        _settled = settled;
        _reading = reading;
    }

    /// <summary>
    /// The home's log opened for reading only, made and migrated by nothing: null where the home has no store or its store keeps no log
    /// yet, which a terminal verb says rather than create it.
    /// </summary>
    public static DesktopBusLogStore? Reading(string home)
    {
        bool timers, queued;
        using (var reader = DesktopStoreReader.Open(DesktopStore.LocationIn(home)))
        {
            if (reader is not { Version: >= DesktopStore.BusLogVersion }) return null;
            timers = reader.Version >= DesktopStore.BusTimersVersion;
            queued = reader.Version >= DesktopStore.BusQueueVersion;
        }

        return new DesktopBusLogStore(home, Cap, null, reading: true) { _timers = timers, _queued = queued };
    }

    /// <inheritdoc />
    public BusWritten Write(BusBatch batch)
    {
        if (_reading) throw new InvalidOperationException("this log was opened for reading only.");
        lock (_gate)
        {
            var opened = _connection is null;
            return Guarded(connection =>
            {
                if (batch.Commit) Pragma(connection, "PRAGMA synchronous = FULL");
                try
                {
                    // Taken for writing at its start (BEGIN IMMEDIATE, the provider's default), so no other holder writes between the lease's answer and
                    // the rows, and the newest order read is the one the rows follow.
                    using var transaction = connection.BeginTransaction();
                    var newest = Admit(connection, transaction);
                    batch.AskWriter();
                    var shift = batch.Entries.Count > 0 && batch.Entries[0].Seq <= newest ? newest + 1 - batch.Entries[0].Seq : 0;
                    var (bytes, most) = Append(connection, transaction, batch.Entries, shift);
                    foreach (var entry in batch.Entries)
                    {
                        foreach (var op in BusTimers.Of(entry, entry.Seq + shift)) Time(connection, transaction, op);
                    }

                    foreach (var row in batch.Measures) Measure(connection, transaction, row);
                    var cut = 0;
                    if (batch.Entries.Count > 0)
                    {
                        // Never past what a consumer still needs: its cursor, or an entry it parked.
                        cut = Execute(connection, transaction,
                            """
                            DELETE FROM bus_log WHERE seq <= MIN($floor,
                              COALESCE((SELECT MIN(cursor) FROM bus_consumers), $floor),
                              COALESCE((SELECT MIN(seq) - 1 FROM bus_parked), $floor))
                            """,
                            ("$floor", batch.Entries[^1].Seq + shift - _cap));
                    }

                    var today = batch.Entries.Count > 0 ? Day(batch.Entries[^1].At) : batch.Measures.Count > 0 ? batch.Measures[^1].Day : null;
                    if (today is not null && today != _measuresCutOn)
                    {
                        var oldest = DateOnly.ParseExact(today, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddDays(-MeasureDays);
                        Execute(connection, transaction, "DELETE FROM bus_measures WHERE day < $oldest", ("$oldest", oldest.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                    }

                    transaction.Commit();
                    if (today is not null) _measuresCutOn = today;
                    return new BusWritten(bytes, cut, most) { Pages = StoreEngineSource.PagesWritten(connection), PageSize = _pageSize, Shift = shift, Opened = opened };
                }
                finally
                {
                    if (batch.Commit) Pragma(connection, "PRAGMA synchronous = NORMAL");
                }
            });
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<BusEnvelope> Read(long after, int limit)
    {
        lock (_gate)
        {
            return Guarded(connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText =
                    "SELECT seq, id, kind, subject, emitter, answerer, deadline, effect, causes, payload, at FROM bus_log WHERE seq > $after ORDER BY seq LIMIT $limit";
                command.Bind(("$after", after), ("$limit", Math.Max(0, limit)));
                return Entries(command);
            });
        }
    }

    /// <summary>The envelopes a select of the log's columns, in their order, reads.</summary>
    private static List<BusEnvelope> Entries(DbCommand command) => Entries(command, out _);

    /// <summary>The envelopes, and a twelfth column's count beside each where the select carries one (a parked entry's tries).</summary>
    private static List<BusEnvelope> Entries(DbCommand command, out List<int> extra)
    {
        using var reader = command.ExecuteReader();
        var entries = new List<BusEnvelope>();
        extra = [];
        while (reader.Read())
        {
            entries.Add(new BusEnvelope(
                reader.GetString(1), reader.GetInt64(0), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                Text(reader, 5), reader.IsDBNull(6) ? null : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(6)), Text(reader, 7),
                Ids(Text(reader, 8)), PayloadOf(Text(reader, 9)), DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(10))));
            if (reader.FieldCount > 11) extra.Add(reader.GetInt32(11));
        }

        return entries;
    }

    /// <summary>One timer operation, in the write's transaction (<see cref="BusTimers"/>).</summary>
    private static void Time(DbConnection connection, DbTransaction transaction, BusTimerOp op)
    {
        switch (op)
        {
            case BusTimerOp.Arm(var timer):
                Execute(connection, transaction,
                    """
                    INSERT OR IGNORE INTO bus_timers (entry, seq, kind, subject, effect, obliged, armed, due, retry)
                    VALUES ($entry, $seq, $kind, $subject, $effect, $obliged, $armed, $due, 0)
                    """,
                    ("$entry", timer.Entry), ("$seq", timer.Seq), ("$kind", timer.Kind), ("$subject", timer.Subject), ("$effect", timer.Effect),
                    ("$obliged", timer.Obliged), ("$armed", timer.Armed.ToUnixTimeMilliseconds()), ("$due", timer.Due.ToUnixTimeMilliseconds()));
                break;
            case BusTimerOp.ClearTimers(var kind, var by, var key):
                Execute(connection, transaction,
                    by == BusEnds.Effect ? "DELETE FROM bus_timers WHERE kind = $kind AND effect = $key" : "DELETE FROM bus_timers WHERE kind = $kind AND subject = $key",
                    ("$kind", kind), ("$key", key));
                break;
            case BusTimerOp.Advance(var entry, var retry, var due):
                Execute(connection, transaction, "UPDATE bus_timers SET retry = $retry, due = $due WHERE entry = $entry AND retry < $retry",
                    ("$entry", entry), ("$retry", retry), ("$due", due.ToUnixTimeMilliseconds()));
                break;
            case BusTimerOp.Drop(var entry):
                Execute(connection, transaction, "DELETE FROM bus_timers WHERE entry = $entry", ("$entry", entry));
                break;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<BusMeasureRow> Measures(string fromDay)
    {
        lock (_gate)
        {
            return Guarded(connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT day, measure, name, count, total, most FROM bus_measures WHERE day >= $from ORDER BY day, measure, name";
                command.Bind("$from", fromDay);
                using var reader = command.ExecuteReader();
                var rows = new List<BusMeasureRow>();
                while (reader.Read())
                {
                    rows.Add(new BusMeasureRow(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt64(3), reader.GetInt64(4), reader.GetInt64(5)));
                }

                return rows;
            });
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<BusTimer> Timers(DateTimeOffset? by, int limit)
    {
        if (!_timers) return [];
        lock (_gate)
        {
            return Guarded(connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText =
                    "SELECT entry, seq, kind, subject, effect, obliged, armed, due, retry FROM bus_timers WHERE $by IS NULL OR due <= $by ORDER BY due, seq LIMIT $limit";
                command.Bind(("$by", by?.ToUnixTimeMilliseconds()), ("$limit", Math.Max(0, limit)));
                using var reader = command.ExecuteReader();
                var timers = new List<BusTimer>();
                while (reader.Read())
                {
                    timers.Add(new BusTimer(
                        reader.GetString(0), reader.GetInt64(1), reader.GetString(2), reader.GetString(3), Text(reader, 4), reader.GetString(5),
                        DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(6)), DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(7)),
                        reader.GetInt32(8)));
                }

                return timers;
            });
        }
    }

    /// <inheritdoc />
    public void DropTimers(IReadOnlyList<string> entries)
    {
        if (entries.Count == 0) return;
        lock (_gate)
        {
            Guarded(connection =>
            {
                using var transaction = connection.BeginTransaction();
                foreach (var entry in entries) Execute(connection, transaction, "DELETE FROM bus_timers WHERE entry = $entry", ("$entry", entry));
                transaction.Commit();
                return 0;
            });
        }
    }

    /// <inheritdoc />
    public BusClaim Claim(string consumer, IReadOnlyList<string> kinds, Func<bool?>? writer, DateTimeOffset at) => Queued(writer, (connection, transaction) =>
    {
        var now = at.ToUnixTimeMilliseconds();
        var kept = Consumer(connection, transaction, consumer);
        if (kept is null)
        {
            Execute(connection, transaction,
                "INSERT INTO bus_consumers (consumer, kinds, cursor, epoch, at) VALUES ($consumer, $kinds, (SELECT COALESCE(MAX(seq), 0) FROM bus_log), 1, $at)",
                ("$consumer", consumer), ("$kinds", JsonSerializer.Serialize(kinds)), ("$at", now));
        }
        else
        {
            Execute(connection, transaction, "UPDATE bus_consumers SET epoch = epoch + 1, kinds = $kinds, at = $at WHERE consumer = $consumer",
                ("$consumer", consumer), ("$kinds", JsonSerializer.Serialize(kinds)), ("$at", now));
        }

        // A parked entry retention cut (an older build's write) is gone with its row.
        Execute(connection, transaction, "DELETE FROM bus_parked WHERE consumer = $consumer AND seq NOT IN (SELECT seq FROM bus_log)", ("$consumer", consumer));
        var (cursor, epoch, _) = Consumer(connection, transaction, consumer)!.Value;
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            SELECT b.seq, b.id, b.kind, b.subject, b.emitter, b.answerer, b.deadline, b.effect, b.causes, b.payload, b.at, p.tries
            FROM bus_parked p JOIN bus_log b ON b.seq = p.seq WHERE p.consumer = $consumer ORDER BY p.seq
            """;
        command.Bind("$consumer", consumer);
        var entries = Entries(command, out var tries);
        return new BusClaim(cursor, epoch, [.. entries.Select((entry, n) => new BusParkedEntry(entry, tries[n]))]);
    });

    /// <inheritdoc />
    public void Acknowledge(BusAck ack) => Queued(ack.Writer, (connection, transaction) =>
    {
        Fence(connection, transaction, ack.Consumer, ack.Epoch);
        Execute(connection, transaction, "UPDATE bus_consumers SET cursor = MAX(cursor, $cursor) WHERE consumer = $consumer",
            ("$consumer", ack.Consumer), ("$cursor", ack.Cursor));
        if (ack.Ends is { } obliged)
        {
            Execute(connection, transaction, "DELETE FROM bus_timers WHERE entry = $entry AND obliged = $obliged", ("$entry", ack.Entry.Id), ("$obliged", obliged));
        }

        if (ack.Unpark)
        {
            Execute(connection, transaction, "DELETE FROM bus_parked WHERE consumer = $consumer AND seq = $seq", ("$consumer", ack.Consumer), ("$seq", ack.Entry.Seq));
        }

        return 0;
    });

    /// <inheritdoc />
    public void Park(BusPark park) => Queued(park.Writer, (connection, transaction) =>
    {
        Fence(connection, transaction, park.Consumer, park.Epoch);
        foreach (var (seq, tries) in park.Rows)
        {
            Execute(connection, transaction, "INSERT OR REPLACE INTO bus_parked (consumer, seq, tries) VALUES ($consumer, $seq, $tries)",
                ("$consumer", park.Consumer), ("$seq", seq), ("$tries", tries));
        }

        Execute(connection, transaction, "UPDATE bus_consumers SET cursor = MAX(cursor, $cursor) WHERE consumer = $consumer",
            ("$consumer", park.Consumer), ("$cursor", park.Cursor));
        return 0;
    });

    /// <inheritdoc />
    public IReadOnlyList<BusEnvelope> Read(long after, IReadOnlyList<string> kinds, int limit)
    {
        if (kinds.Count == 0) return [];
        lock (_gate)
        {
            return Guarded(connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText =
                    $"SELECT seq, id, kind, subject, emitter, answerer, deadline, effect, causes, payload, at FROM bus_log WHERE seq > $after AND kind IN ({string.Join(", ", kinds.Select((_, n) => $"$k{n}"))}) ORDER BY seq LIMIT $limit";
                command.Bind([("$after", after), .. kinds.Select((kind, n) => ($"$k{n}", (object?)kind)), ("$limit", Math.Max(0, limit))]);
                return Entries(command);
            });
        }
    }

    /// <inheritdoc />
    public (long Cursor, long Epoch, int Parked)? Consumer(string consumer)
    {
        if (!_queued) return null;
        lock (_gate)
        {
            return Guarded(connection => Consumer(connection, null, consumer));
        }
    }

    private static (long Cursor, long Epoch, int Parked)? Consumer(DbConnection connection, DbTransaction? transaction, string consumer)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT cursor, epoch, (SELECT count(*) FROM bus_parked WHERE consumer = $consumer) FROM bus_consumers WHERE consumer = $consumer";
        command.Bind("$consumer", consumer);
        using var reader = command.ExecuteReader();
        return reader.Read() ? (reader.GetInt64(0), reader.GetInt64(1), reader.GetInt32(2)) : null;
    }

    /// <summary>The consumer's epoch read inside the write: an older one's is refused.</summary>
    private static void Fence(DbConnection connection, DbTransaction transaction, string consumer, long epoch)
    {
        var current = Consumer(connection, transaction, consumer)?.Epoch ?? 0;
        if (current != epoch) throw new BusFencedException(consumer, epoch, current);
    }

    /// <summary>One queue write: the store's stamp and the lease asked inside its transaction, as every write of the log's.</summary>
    private T Queued<T>(Func<bool?>? writer, Func<DbConnection, DbTransaction, T> work)
    {
        if (_reading) throw new InvalidOperationException("this log was opened for reading only.");
        lock (_gate)
        {
            return Guarded(connection =>
            {
                using var transaction = connection.BeginTransaction();
                Admit(connection, transaction);
                new BusBatch([], [], false) { Writer = writer }.AskWriter();
                var done = work(connection, transaction);
                transaction.Commit();
                return done;
            });
        }
    }

    /// <summary>The newest entries of <paramref name="kinds"/>, newest first, at most <paramref name="limit"/>: what the Stalls view reads.</summary>
    public IReadOnlyList<BusEnvelope> Newest(IReadOnlyList<string> kinds, int limit)
    {
        lock (_gate)
        {
            return Guarded(connection =>
            {
                using var command = connection.CreateCommand();
                command.CommandText =
                    $"SELECT seq, id, kind, subject, emitter, answerer, deadline, effect, causes, payload, at FROM bus_log WHERE kind IN ({string.Join(", ", kinds.Select((_, n) => $"$k{n}"))}) ORDER BY seq DESC LIMIT $limit";
                command.Bind([.. kinds.Select((kind, n) => ($"$k{n}", (object?)kind)), ("$limit", Math.Max(0, limit))]);
                return Entries(command);
            });
        }
    }

    /// <summary>How many rows the log keeps now, the lowest and the highest order: what the verb says of the cap.</summary>
    public (long Rows, long Lowest, long Highest) Kept()
    {
        lock (_gate)
        {
            return Guarded(connection => (
                Scalar(connection, null, "SELECT count(*) FROM bus_log"),
                Scalar(connection, null, "SELECT COALESCE(MIN(seq), 0) FROM bus_log"),
                Scalar(connection, null, "SELECT COALESCE(MAX(seq), 0) FROM bus_log")));
        }
    }

    /// <summary>A setting of the kept connection, as SQLite reads it (<c>PRAGMA name</c>): what a test reads of how the log is opened.</summary>
    internal long Setting(string name)
    {
        if (!name.All(letter => letter is '_' or (>= 'a' and <= 'z'))) throw new ArgumentException($"`{name}` is no setting's name.", nameof(name));
        lock (_gate)
        {
            return Guarded(connection => Scalar(connection, null, "PRAGMA " + name));
        }
    }

    /// <summary>The UTC day of <paramref name="at"/>, as the measures key it.</summary>
    public static string Day(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public void Dispose()
    {
        lock (_gate)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }

    /// <summary>
    /// The work on the kept connection, opened at first use; a failure lets the connection go, so the next use connects afresh, and one a
    /// later try may pass (<see cref="Transient"/>) is said as <see cref="BusStoreBusyException"/>.
    /// </summary>
    private T Guarded<T>(Func<DbConnection, T> work)
    {
        DbConnection? connection = null;
        try
        {
            connection = _connection ??= Open();
            return work(connection);
        }
        catch (BusNotWriterException)
        {
            // Its transaction rolled back; the connection is sound and stays.
            throw;
        }
        catch (Exception error)
        {
            _connection = null;
            connection?.Dispose();
            if (Transient(error)) throw new BusStoreBusyException(error);
            throw;
        }
    }

    /// <summary>The lock held past the wait, or the file held by another process this moment.</summary>
    private static bool Transient(Exception error) => error is IOException or UnauthorizedAccessException || StoreEngineSource.Busy(error);

    private DbConnection Open()
    {
        var location = DesktopStore.LocationIn(_home);
        if (!_reading && !_stepped)
        {
            // Made and migrated by the store's own steps, as every family's tables are, once for this store: a connection let go after a
            // failure connects again without them.
            using var store = DesktopStore.Open(location, StoreProcess.Options(_settled ?? StartConfirmation.Read));
            if (store.Stamp.Version < DesktopStore.BusQueueVersion)
            {
                throw new DesktopStoreException($"the desktop's store keeps no bus log yet: {store.Held ?? "its step has not run"}.");
            }

            _stepped = true;
        }

        var connection = StoreEngineSource.Connect(location, _reading ? DesktopOpen.ReadOnly : DesktopOpen.ReadWrite);
        try
        {
            if (!_reading)
            {
                Pragma(connection, $"PRAGMA busy_timeout = {(int)DesktopStoreEngineOptions.DefaultBusyWait.TotalMilliseconds}");
                Pragma(connection, "PRAGMA synchronous = NORMAL");
                Pragma(connection, $"PRAGMA journal_size_limit = {JournalLimit}");
                _pageSize = Scalar(connection, null, "PRAGMA page_size");
                // The count starts with this connection's first write.
                StoreEngineSource.PagesWritten(connection);
            }

            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>
    /// The writer stamp read inside the write, as the store's engine reads it (a newer Daoris's store is written by no one here), and the
    /// newest order the log keeps, which the batch is seated after.
    /// </summary>
    private static long Admit(DbConnection connection, DbTransaction transaction)
    {
        using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT version, writer FROM store_version WHERE one = 1";
            using var reader = command.ExecuteReader();
            if (!reader.Read()) throw new DesktopStoreException("the desktop's store has no schema yet.");
            var (version, writer) = (reader.GetInt32(0), reader.GetInt32(1));
            if (writer > DesktopStore.WriterStamp) throw new DesktopStoreNewerException(writer, DesktopStore.WriterStamp);
            if (version < DesktopStore.BusQueueVersion) throw new DesktopStoreException("the desktop's store keeps no bus log, timers or queue yet.");
        }

        return Scalar(connection, transaction, "SELECT COALESCE(MAX(seq), 0) FROM bus_log");
    }

    /// <summary>The entries inserted in order, each order moved by <paramref name="shift"/> to follow the newest kept.</summary>
    private static (long Bytes, long Most) Append(DbConnection connection, DbTransaction transaction, IReadOnlyList<BusEnvelope> entries, long shift)
    {
        if (entries.Count == 0) return (0, 0);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT INTO bus_log (seq, id, kind, subject, emitter, answerer, deadline, effect, causes, payload, at)
            VALUES ($seq, $id, $kind, $subject, $emitter, $answerer, $deadline, $effect, $causes, $payload, $at)
            """;
        long bytes = 0, most = 0;
        foreach (var entry in entries)
        {
            var causes = entry.Causes.Count == 0 ? null : JsonSerializer.Serialize(entry.Causes);
            var payload = entry.Payload.Count == 0 ? null : Encode(entry.Payload);
            command.Parameters.Clear();
            command.Bind(
                ("$seq", entry.Seq + shift), ("$id", entry.Id), ("$kind", entry.Kind), ("$subject", entry.Subject), ("$emitter", entry.Emitter),
                ("$answerer", entry.Answerer), ("$deadline", entry.Deadline?.ToUnixTimeMilliseconds()), ("$effect", entry.Effect),
                ("$causes", causes), ("$payload", payload), ("$at", entry.At.ToUnixTimeMilliseconds()));
            command.ExecuteNonQuery();
            var row = 8 + 8 + (entry.Deadline is null ? 0 : 8)
                      + Utf8(entry.Id) + Utf8(entry.Kind) + Utf8(entry.Subject) + Utf8(entry.Emitter) + Utf8(entry.Answerer) + Utf8(entry.Effect)
                      + Utf8(causes) + Utf8(payload);
            bytes += row;
            most = Math.Max(most, row);
        }

        return (bytes, most);
    }

    private static void Measure(DbConnection connection, DbTransaction transaction, BusMeasureRow row) =>
        Execute(connection, transaction,
            """
            INSERT INTO bus_measures (day, measure, name, count, total, most) VALUES ($day, $measure, $name, $count, $total, $most)
            ON CONFLICT (day, measure, name) DO UPDATE SET
              count = count + excluded.count, total = total + excluded.total, most = MAX(most, excluded.most)
            """,
            ("$day", row.Day), ("$measure", row.Measure), ("$name", row.Name), ("$count", row.Count), ("$total", row.Total), ("$most", row.Most));

    /// <summary>A payload as one JSON object, its times as milliseconds since the epoch.</summary>
    private static string Encode(IReadOnlyList<(string Key, object? Value)> payload)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var (key, value) in payload)
            {
                writer.WritePropertyName(key);
                switch (value)
                {
                    case null: writer.WriteNullValue(); break;
                    case bool flag: writer.WriteBooleanValue(flag); break;
                    case int number: writer.WriteNumberValue(number); break;
                    case long number: writer.WriteNumberValue(number); break;
                    case double number: writer.WriteNumberValue(number); break;
                    case DateTimeOffset at: writer.WriteNumberValue(at.ToUnixTimeMilliseconds()); break;
                    case string text: writer.WriteStringValue(text); break;
                    case IReadOnlyList<string> ids:
                        writer.WriteStartArray();
                        foreach (var id in ids) writer.WriteStringValue(id);
                        writer.WriteEndArray();
                        break;
                    default: throw new ArgumentException($"`{key}` is no id, code, count or time.", nameof(payload));
                }
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static IReadOnlyList<(string Key, object? Value)> PayloadOf(string? json)
    {
        if (json is null) return [];
        using var document = JsonDocument.Parse(json);
        var pairs = new List<(string Key, object? Value)>();
        foreach (var property in document.RootElement.EnumerateObject())
        {
            object? value = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number when property.Value.TryGetInt64(out var whole) => whole,
                JsonValueKind.Number => property.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Array => (IReadOnlyList<string>)[.. property.Value.EnumerateArray().Select(item => item.GetString() ?? "")],
                _ => null,
            };
            pairs.Add((property.Name, value));
        }

        return pairs;
    }

    private static IReadOnlyList<string> Ids(string? json) => json is null ? [] : JsonSerializer.Deserialize<string[]>(json) ?? [];

    private static string? Text(DbDataReader reader, int column) => reader.IsDBNull(column) ? null : reader.GetString(column);

    private static int Utf8(string? text) => text is null ? 0 : Encoding.UTF8.GetByteCount(text);

    private static long Scalar(DbConnection connection, DbTransaction? transaction, string sql)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static int Execute(DbConnection connection, DbTransaction transaction, string sql, params (string Name, object? Value)[] values)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Bind(values);
        return command.ExecuteNonQuery();
    }

    private static void Pragma(DbConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }
}
