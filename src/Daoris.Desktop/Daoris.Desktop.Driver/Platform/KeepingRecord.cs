using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// <c>data/keeping.json</c>, what the disk cannot say again of the keeping rules' runs (CLEANUP1, D195 point 2; the cleanup design §2.3):
/// each kind's last run, <c>last.&lt;kind&gt;</c>; KEEP1b adds each download's first-unused time beside it. A machine record: never synced,
/// and no route answers it raw.
/// </summary>
/// <remarks>
/// <para><b>One way to write it</b> (Codex 4.1): <see cref="JsonKeeping.Save"/>, the file's keep lock held from its re-read to its rename,
/// each writer's edit only the key paths it changed, so the look's pass, a press's run and a terminal acting alone keep each other's
/// entries. Beside a desktop a terminal's run holds no lease that would order them (D194 point 3): the lock does.</para>
/// <para><b>A file that does not read is never written over</b>: the read says why, and the write refuses with it. Every field is added,
/// never renamed (a stored shape); a reader meeting no file reads every kind as never cleaned.</para>
/// </remarks>
public static class KeepingRecord
{
    /// <summary>The shape this build writes.</summary>
    public const int Schema = 1;

    private const string Moment = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    /// <summary>Where the record is: the home table's <c>keeping</c> entry.</summary>
    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.Keeping);

    /// <summary>The record as it stands; a file that does not read says why, and claims nothing.</summary>
    public static KeepingFile Read(string home)
    {
        var path = PathOf(home);
        string text;
        try
        {
            if (!File.Exists(path)) return new(false, null, new Dictionary<string, KeepingLast>(), new Dictionary<string, string>(), null);
            text = File.ReadAllText(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return Unread(error.Message);
        }

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return Unread("it holds no JSON object");
            if (JsonKeeping.Twice(text) is { } key) return Unread($"it holds the key `{key}` twice in one object");
            var schema = root.TryGetProperty("schema", out var stamped) ? stamped.GetRawText() : null;
            var last = new Dictionary<string, KeepingLast>(StringComparer.Ordinal);
            var raw = new Dictionary<string, string>(StringComparer.Ordinal);
            if (root.TryGetProperty("last", out var kinds) && kinds.ValueKind == JsonValueKind.Object)
            {
                foreach (var kind in kinds.EnumerateObject())
                {
                    raw[kind.Name] = kind.Value.GetRawText();
                    if (LastOf(kind.Value) is { } read) last[kind.Name] = read;
                }
            }
            else if (root.TryGetProperty("last", out _))
            {
                return Unread("its `last` is not an object");
            }

            return new(true, null, last, raw, schema);
        }
        catch (JsonException error)
        {
            return Unread(error.Message);
        }

        static KeepingFile Unread(string why) =>
            new(true, $"keeping.json could not be read ({why.TrimEnd('.')})", new Dictionary<string, KeepingLast>(), new Dictionary<string, string>(), null);
    }

    /// <summary>
    /// <paramref name="kind"/>'s last run written over the record as it stands now, <paramref name="read"/> being the record as this writer
    /// read it: only <c>last.&lt;kind&gt;</c> is this edit's. Null where it was written; else why not, nothing written.
    /// </summary>
    public static string? WriteLast(string home, KeepingFile read, string kind, KeepingLast last)
    {
        if (read.Problem is { } problem) return $"{problem}, so this run's last was not written: a file that does not read is never written over";
        var understood = JsonFile.Serialize(writer =>
        {
            writer.WriteStartObject();
            if (read.Schema is { } schema)
            {
                writer.WritePropertyName("schema");
                writer.WriteRawValue(schema);
            }

            writer.WriteStartObject("last");
            if (read.Raw.TryGetValue(kind, out var was))
            {
                writer.WritePropertyName(kind);
                writer.WriteRawValue(was);
            }

            writer.WriteEndObject();
            writer.WriteEndObject();
        });
        var edited = JsonFile.Serialize(writer =>
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema", Schema);
            writer.WriteStartObject("last");
            writer.WritePropertyName(kind);
            Write(writer, last);
            writer.WriteEndObject();
            writer.WriteEndObject();
        });
        try
        {
            JsonKeeping.Save(PathOf(home), read.Found, understood, edited);
            return null;
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            return error.Message;
        }
    }

    private static void Write(Utf8JsonWriter writer, KeepingLast last)
    {
        writer.WriteStartObject();
        writer.WriteString("at", last.At.UtcDateTime.ToString(Moment, CultureInfo.InvariantCulture));
        writer.WriteString("door", last.Door);
        writer.WriteString("run", last.Run);
        writer.WriteStartObject("removed");
        writer.WriteNumber("count", last.Removed);
        writer.WriteNumber("bytes", last.Bytes);
        writer.WriteEndObject();
        writer.WriteStartObject("kept");
        foreach (var (why, count) in last.Kept.OrderBy(each => each.Key, StringComparer.Ordinal)) writer.WriteNumber(why, count);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    /// <summary>A kind's last run as written; null where it does not read as one.</summary>
    private static KeepingLast? LastOf(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("at", out var at) || at.ValueKind != JsonValueKind.String
            || !DateTimeOffset.TryParse(at.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var when)
            || !element.TryGetProperty("door", out var door) || door.ValueKind != JsonValueKind.String
            || !element.TryGetProperty("removed", out var removed) || removed.ValueKind != JsonValueKind.Object
            || !removed.TryGetProperty("count", out var count) || !count.TryGetInt32(out var removedCount)
            || !removed.TryGetProperty("bytes", out var bytes) || !bytes.TryGetInt64(out var removedBytes))
        {
            return null;
        }

        var kept = new Dictionary<string, int>(StringComparer.Ordinal);
        if (element.TryGetProperty("kept", out var whys) && whys.ValueKind == JsonValueKind.Object)
        {
            foreach (var why in whys.EnumerateObject())
            {
                if (why.Value.TryGetInt32(out var n)) kept[why.Name] = n;
            }
        }

        var run = element.TryGetProperty("run", out var id) && id.ValueKind == JsonValueKind.String ? id.GetString()! : "";
        return new(when, door.GetString()!, run, removedCount, removedBytes, kept);
    }
}

/// <summary><c>keeping.json</c> as read (<see cref="KeepingRecord.Read"/>).</summary>
/// <param name="Found">Whether a file was there at the read.</param>
/// <param name="Problem">Why it did not read; null where it did, or where there was none.</param>
/// <param name="Last">Each kind's last run that reads as one.</param>
/// <param name="Raw">Each kind's last run as written, which an edit of it is made from.</param>
/// <param name="Schema">The record's <c>schema</c> as written; null where it holds none.</param>
public sealed record KeepingFile(
    bool Found, string? Problem, IReadOnlyDictionary<string, KeepingLast> Last, IReadOnlyDictionary<string, string> Raw, string? Schema);

/// <summary>A kind's last run (the cleanup design §2.3): when, through which door, which run, what it removed and what it kept, by why.</summary>
/// <param name="Door"><c>look</c>, <c>floor</c>, <c>press</c> or <c>terminal</c> (<see cref="KeepingRun"/>'s doors).</param>
/// <param name="Removed">How many it removed: folders purged and package folders dropped.</param>
/// <param name="Bytes">The bytes those held.</param>
/// <param name="Kept">How many it kept, by the code of why.</param>
public sealed record KeepingLast(DateTimeOffset At, string Door, string Run, int Removed, long Bytes, IReadOnlyDictionary<string, int> Kept);
