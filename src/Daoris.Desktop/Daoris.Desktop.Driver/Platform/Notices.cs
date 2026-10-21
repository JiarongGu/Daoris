using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// <c>data/notices.json</c>, what ended, failed or went unanswered that the person need not answer (NOTIFY2f, D196 point 6; the
/// notification design §2.5): the notices of <i>Done and failed</i> with whether each is read. A machine record: never synced, never on a
/// wire to a shared host, and no route answers it raw.
/// </summary>
/// <remarks>
/// <para><b>A map keyed by id</b>, <c>{ "schemaVersion": 1, "notices": { "&lt;id&gt;": { … } } }</c>, never an array:
/// <see cref="JsonKeeping"/> merges an object key by key and keeps a key it did not write, so a field a later build wrote inside a notice
/// stays across every write here.</para>
/// <para><b>Written by any process on the home</b>, each write one hold of the file's <see cref="KeepLock"/> from its read to its rename
/// (<see cref="JsonKeeping.SaveHolding"/>), so writers in one process and in two are serialized. The writer marks its own write
/// (<see cref="HomeWrites.Mine"/>) and pushes its own slice. A write refused (the lock held past its wait, a key written twice, a shape
/// changed under the edit) is the reason returned, for the caller to log and drop: nothing retries it, since the sessions' records keep
/// what happened.</para>
/// <para><b>A record it cannot take</b>: one that does not read as a JSON object is set aside under a dated name and a new one begun, by
/// the writer holding the lock alone; a reader keeps its last snapshot (<see cref="Read"/> answers null). One whose
/// <c>schemaVersion</c> is newer than <see cref="Schema"/> is read as none and never written over.</para>
/// <para><b>Its age and its count</b>: a notice older than <see cref="Week"/> is never read, and goes at the next write; at most
/// <see cref="Most"/> are kept, the oldest going first, read or not.</para>
/// </remarks>
public static class Notices
{
    /// <summary>The shape this build writes.</summary>
    public const int Schema = 1;

    /// <summary>How long a notice is kept.</summary>
    public static readonly TimeSpan Week = TimeSpan.FromDays(7);

    /// <summary>How many notices are kept at most.</summary>
    public const int Most = 200;

    /// <summary>A session that ended without the person asking, by its end state.</summary>
    public const string Ended = "ended";

    /// <summary>A terminal's press that ended unanswered, refused by its time.</summary>
    public const string Expired = "expired";

    private const string Moment = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";

    /// <summary>Where the record is: the home table's <c>notices</c> entry.</summary>
    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.Notices);

    /// <summary>
    /// The notices younger than <see cref="Week"/> at <paramref name="now"/>, newest first; none where there is no record or its
    /// <c>schemaVersion</c> is newer. Null where the record does not read this moment: the reader keeps what it read last.
    /// </summary>
    public static IReadOnlyList<Notice>? Read(string home, DateTimeOffset now)
    {
        var path = PathOf(home);
        string text;
        try
        {
            if (!File.Exists(path)) return [];
            text = File.ReadAllText(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        var record = Parse(text);
        if (record is null) return null;
        if (record.Newer) return [];
        return [.. Young(record.Notices.Values, now)];
    }

    /// <summary>
    /// The notice an attention item keeps (§2.2's fourth group): a session that ended without the person asking, by its end state,
    /// its workspace with it; null for what waits on the person or is said elsewhere. Its id is the session's, so a second writer of
    /// one end keeps it once.
    /// </summary>
    public static Notice? Of(AttentionEvent item, DateTimeOffset at) => item.Kind == AttentionKind.Ended
        ? new($"ended-{item.Session}", Ended, at, Workspace: item.Workspace, Session: item.Session, Repository: item.Repository, State: item.State)
        : null;

    /// <summary>
    /// The notices a look's attention items keep, each added (<see cref="Of"/>), whether or not the person is told of them: the notify
    /// switch governs being told, never recording. Each refusal is <paramref name="refused"/>'s to log, and is dropped.
    /// </summary>
    /// <returns>The notices written.</returns>
    public static IReadOnlyList<Notice> Keep(string home, IEnumerable<AttentionEvent> items, DateTimeOffset now, Action<string> refused)
    {
        var kept = new List<Notice>();
        foreach (var item in items)
        {
            if (Of(item, now) is not { } notice) continue;
            if (Add(home, notice, now) is { } why) refused(why);
            else kept.Add(notice);
        }

        return kept;
    }

    /// <summary><paramref name="notice"/> kept; one of the same id already there is left as it is. Null where written; else why not.</summary>
    public static string? Add(string home, Notice notice, DateTimeOffset now, TimeSpan? within = null) =>
        Edit(home, now, within, notices =>
        {
            if (notices.ContainsKey(notice.Id)) return false;
            notices[notice.Id] = notice;
            return true;
        });

    /// <summary>The notices of <paramref name="ids"/> read; an id the record does not hold is nothing. Null where written; else why not.</summary>
    public static string? MarkRead(string home, IReadOnlyCollection<string> ids, DateTimeOffset now, TimeSpan? within = null) =>
        Edit(home, now, within, notices =>
        {
            var changed = false;
            foreach (var id in ids)
            {
                if (!notices.TryGetValue(id, out var notice) || notice.Read) continue;
                notices[id] = notice with { Read = true };
                changed = true;
            }

            return changed;
        });

    /// <summary>The notices of <paramref name="ids"/> removed; an id the record does not hold is nothing. Null where written; else why not.</summary>
    public static string? Dismiss(string home, IReadOnlyCollection<string> ids, DateTimeOffset now, TimeSpan? within = null) =>
        Edit(home, now, within, notices =>
        {
            var changed = false;
            foreach (var id in ids) changed |= notices.Remove(id);
            return changed;
        });

    /// <summary>
    /// One write: the record read under its lock, set aside where it does not read, <paramref name="change"/> made onto what it holds,
    /// pruned by age and count, and written over the file as it stands, keeping what this build does not know.
    /// </summary>
    private static string? Edit(string home, DateTimeOffset now, TimeSpan? within, Func<Dictionary<string, Notice>, bool> change)
    {
        var path = PathOf(home);
        try
        {
            using var held = KeepLock.Take(path, within);
            Parsed? record = null;
            var begun = false;
            if (File.Exists(path))
            {
                record = Parse(File.ReadAllText(path));
                if (record is null)
                {
                    SetAside(path, now);
                    begun = true;
                }
                else if (record.Newer)
                {
                    return $"notices.json says schemaVersion {record.SchemaVersion}, newer than this build's {Schema}, so it was left as it is";
                }
            }

            var read = record?.Notices ?? new Dictionary<string, Notice>(StringComparer.Ordinal);
            var notices = new Dictionary<string, Notice>(read, StringComparer.Ordinal);
            var changed = change(notices);
            var kept = Young(notices.Values, now).Take(Most).ToDictionary(notice => notice.Id, StringComparer.Ordinal);
            if (!changed && kept.Count == notices.Count && !begun) return null;

            JsonKeeping.SaveHolding(path, record is not null, Text(record?.SchemaVersion, read), Text(Schema.ToString(CultureInfo.InvariantCulture), kept));
            HomeWrites.Mine(path);
            return null;
        }
        catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException)
        {
            return error.Message;
        }
    }

    /// <summary>The notices younger than <see cref="Week"/>, newest first.</summary>
    private static IEnumerable<Notice> Young(IEnumerable<Notice> notices, DateTimeOffset now) =>
        notices.Where(notice => now - notice.At < Week).OrderByDescending(notice => notice.At).ThenBy(notice => notice.Id, StringComparer.Ordinal);

    /// <summary>The unreadable record moved aside under a dated name, never written over.</summary>
    private static void SetAside(string path, DateTimeOffset now)
    {
        var folder = Path.GetDirectoryName(path)!;
        var stamp = now.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
        var aside = Path.Combine(folder, $"notices.unreadable-{stamp}.json");
        for (var n = 2; File.Exists(aside); n++) aside = Path.Combine(folder, $"notices.unreadable-{stamp}-{n}.json");
        File.Move(path, aside);
    }

    /// <summary>The record as text, as this writer understands it: <c>schemaVersion</c> as given, then the notices by id.</summary>
    private static string Text(string? schemaVersion, IReadOnlyDictionary<string, Notice> notices) => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        if (schemaVersion is not null)
        {
            writer.WritePropertyName("schemaVersion");
            writer.WriteRawValue(schemaVersion);
        }

        writer.WriteStartObject("notices");
        foreach (var notice in notices.Values.OrderBy(each => each.At).ThenBy(each => each.Id, StringComparer.Ordinal))
        {
            writer.WritePropertyName(notice.Id);
            Write(writer, notice);
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    });

    private static void Write(Utf8JsonWriter writer, Notice notice)
    {
        writer.WriteStartObject();
        writer.WriteString("kind", notice.Kind);
        writer.WriteString("at", notice.At.UtcDateTime.ToString(Moment, CultureInfo.InvariantCulture));
        writer.WriteBoolean("read", notice.Read);
        Optional("workspace", notice.Workspace);
        Optional("session", notice.Session);
        Optional("quest", notice.Quest);
        Optional("repository", notice.Repository);
        Optional("state", notice.State);
        Optional("route", notice.Route);
        Optional("act", notice.Act);
        writer.WriteEndObject();

        void Optional(string name, string? value)
        {
            if (value is not null) writer.WriteString(name, value);
        }
    }

    /// <summary>The record parsed; null where it is no JSON object.</summary>
    private static Parsed? Parse(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            string? schemaVersion = null;
            var newer = false;
            if (root.TryGetProperty("schemaVersion", out var version))
            {
                schemaVersion = version.GetRawText();
                newer = version.ValueKind == JsonValueKind.Number && (!version.TryGetInt32(out var n) || n > Schema);
            }

            var notices = new Dictionary<string, Notice>(StringComparer.Ordinal);
            if (root.TryGetProperty("notices", out var map) && map.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in map.EnumerateObject())
                {
                    if (NoticeOf(entry.Name, entry.Value) is { } notice) notices[entry.Name] = notice;
                }
            }

            return new(schemaVersion, newer, notices);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A notice as written; null where it does not read as one.</summary>
    private static Notice? NoticeOf(string id, JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("kind", out var kind) || kind.ValueKind != JsonValueKind.String
            || !element.TryGetProperty("at", out var at) || at.ValueKind != JsonValueKind.String
            || !DateTimeOffset.TryParse(at.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var when))
        {
            return null;
        }

        var read = element.TryGetProperty("read", out var seen) && seen.ValueKind == JsonValueKind.True;
        return new(id, kind.GetString()!, when, read, Text("workspace"), Text("session"), Text("quest"), Text("repository"), Text("state"),
            Text("route"), Text("act"));

        string? Text(string name) => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private sealed record Parsed(string? SchemaVersion, bool Newer, Dictionary<string, Notice> Notices);
}

/// <summary>
/// A notice of <i>Done and failed</i> (the notification design §2.5's <c>news</c> partition): what it is, when, whether it is read, and what
/// it is about. Its workspace is the one it was made in, kept on it and never read again from a session that may since be gone; none is
/// machine-wide.
/// </summary>
/// <param name="Kind"><see cref="Notices.Ended"/> or <see cref="Notices.Expired"/>; a later build's kinds are read as written.</param>
/// <param name="State">A session's end state, for <see cref="Notices.Ended"/>.</param>
/// <param name="Route">A press's route, its module before it where the module is not the driver's (<c>REMOTES.SET</c>).</param>
/// <param name="Act">A press's act in its asker's own words, where it has no route the page words.</param>
public sealed record Notice(
    string Id,
    string Kind,
    DateTimeOffset At,
    bool Read = false,
    string? Workspace = null,
    string? Session = null,
    string? Quest = null,
    string? Repository = null,
    string? State = null,
    string? Route = null,
    string? Act = null);
