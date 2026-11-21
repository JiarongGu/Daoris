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
/// <para><b>Retention</b>: the newest <see cref="Cap"/> rows, the older cut in the write that passes the cap, and the measures of the last
/// <see cref="MeasureDays"/> days. Nothing reads behind the cap yet; the cursors and snapshots that will hold the cut at
/// their floor when they come.</para>
/// <para><b>Bytes</b> are the rows' own, as encoded: each text's UTF-8 length and eight for each number. A write also costs SQLite's pages
/// (at least one 4 KiB frame of the write-ahead log for each flush), which this does not count.</para>
/// </remarks>
public sealed class DesktopBusLogStore : IBusLogStore
{
    /// <summary>
    /// The rows kept: about 13 MiB of rows at the 270 bytes a row a check's facts measured (BusLogTests), fifty days at a thousand
    /// entries a day, until the install's own measures (<c>daoris bus stats</c>) set the budget.
    /// </summary>
    public const int Cap = 50_000;

    /// <summary>The days of measures kept.</summary>
    public const int MeasureDays = 90;

    /// <summary>The ids of one payload list kept, the rest counted by its writer.</summary>
    public const int MostListed = 16;

    /// <summary>The longest id or code a payload or an envelope's field carries.</summary>
    public const int Longest = 200;

    private readonly string _home;
    private readonly int _cap;
    private readonly bool _reading;
    private readonly Func<StoreSettled>? _settled;
    private readonly Lock _gate = new();
    private DbConnection? _connection;
    private string? _measuresCutOn;

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
        using (var reader = DesktopStoreReader.Open(DesktopStore.LocationIn(home)))
        {
            if (reader is not { Version: >= DesktopStore.BusLogVersion }) return null;
        }

        return new DesktopBusLogStore(home, Cap, null, reading: true);
    }

    /// <inheritdoc />
    public long Newest()
    {
        lock (_gate)
        {
            return Guarded(connection => Scalar(connection, null, "SELECT COALESCE(MAX(seq), 0) FROM bus_log"));
        }
    }

    /// <inheritdoc />
    public BusWritten Write(BusBatch batch)
    {
        if (_reading) throw new InvalidOperationException("this log was opened for reading only.");
        lock (_gate)
        {
            return Guarded(connection =>
            {
                if (batch.Commit) Pragma(connection, "PRAGMA synchronous = FULL");
                try
                {
                    using var transaction = connection.BeginTransaction();
                    Admit(connection, transaction);
                    var (bytes, most) = Append(connection, transaction, batch.Entries);
                    foreach (var row in batch.Measures) Measure(connection, transaction, row);
                    var cut = 0;
                    if (batch.Entries.Count > 0)
                    {
                        cut = Execute(connection, transaction, "DELETE FROM bus_log WHERE seq <= $floor", ("$floor", batch.Entries[^1].Seq - _cap));
                    }

                    var today = batch.Entries.Count > 0 ? Day(batch.Entries[^1].At) : batch.Measures.Count > 0 ? batch.Measures[^1].Day : null;
                    if (today is not null && today != _measuresCutOn)
                    {
                        var oldest = DateOnly.ParseExact(today, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddDays(-MeasureDays);
                        Execute(connection, transaction, "DELETE FROM bus_measures WHERE day < $oldest", ("$oldest", oldest.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
                    }

                    transaction.Commit();
                    if (today is not null) _measuresCutOn = today;
                    return new BusWritten(bytes, cut, most);
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
                using var reader = command.ExecuteReader();
                var entries = new List<BusEnvelope>();
                while (reader.Read())
                {
                    entries.Add(new BusEnvelope(
                        reader.GetString(1), reader.GetInt64(0), reader.GetString(2), reader.GetString(3), reader.GetString(4),
                        Text(reader, 5), reader.IsDBNull(6) ? null : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(6)), Text(reader, 7),
                        Ids(Text(reader, 8)), PayloadOf(Text(reader, 9)), DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(10))));
                }

                return entries;
            });
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

    /// <summary>The UTC day of <paramref name="at"/>, as the measures key it.</summary>
    public static string Day(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>
    /// Whether <paramref name="value"/> is a payload's: a short id or code with no line in it, a number, a flag, a time, null, or a list
    /// of such ids.
    /// </summary>
    public static bool Fits(object? value) => value switch
    {
        null or bool or int or long or double or DateTimeOffset => true,
        string text => Short(text),
        IReadOnlyList<string> ids => ids.Count <= MostListed && ids.All(Short),
        _ => false,
    };

    /// <summary>A short id or code: no longer than <see cref="Longest"/> and with no control character.</summary>
    public static bool Short(string? text) => text is null || (text.Length <= Longest && !text.Any(char.IsControl));

    public void Dispose()
    {
        lock (_gate)
        {
            _connection?.Dispose();
            _connection = null;
        }
    }

    /// <summary>The work on the kept connection, opened at first use; a failure lets the connection go, so the next use opens afresh.</summary>
    private T Guarded<T>(Func<DbConnection, T> work)
    {
        var connection = _connection ??= Open();
        try
        {
            return work(connection);
        }
        catch
        {
            _connection = null;
            connection.Dispose();
            throw;
        }
    }

    private DbConnection Open()
    {
        var location = DesktopStore.LocationIn(_home);
        if (!_reading)
        {
            // Made and migrated by the store's own steps, as every family's tables are.
            using var store = DesktopStore.Open(location, StoreProcess.Options(_settled ?? StartConfirmation.Read));
            if (store.Stamp.Version < DesktopStore.BusLogVersion)
            {
                throw new DesktopStoreException($"the desktop's store keeps no bus log yet: {store.Held ?? "its step has not run"}.");
            }
        }

        var connection = StoreEngineSource.Connect(location, _reading ? DesktopOpen.ReadOnly : DesktopOpen.ReadWrite);
        try
        {
            if (!_reading)
            {
                Pragma(connection, "PRAGMA busy_timeout = 2000");
                Pragma(connection, "PRAGMA synchronous = NORMAL");
            }

            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>The writer stamp read inside the write, as the store's engine reads it: a newer Daoris's store is written by no one here.</summary>
    private static void Admit(DbConnection connection, DbTransaction transaction)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT version, writer FROM store_version WHERE one = 1";
        using var reader = command.ExecuteReader();
        if (!reader.Read()) throw new DesktopStoreException("the desktop's store has no schema yet.");
        var (version, writer) = (reader.GetInt32(0), reader.GetInt32(1));
        if (writer > DesktopStore.WriterStamp) throw new DesktopStoreNewerException(writer, DesktopStore.WriterStamp);
        if (version < DesktopStore.BusLogVersion) throw new DesktopStoreException("the desktop's store keeps no bus log yet.");
    }

    private static (long Bytes, long Most) Append(DbConnection connection, DbTransaction transaction, IReadOnlyList<BusEnvelope> entries)
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
                ("$seq", entry.Seq), ("$id", entry.Id), ("$kind", entry.Kind), ("$subject", entry.Subject), ("$emitter", entry.Emitter),
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
