using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>Words a record could not go on with, by their ids, why by a code, and when it was judged (MSG1b).</summary>
/// <param name="Said">The ids of the words judged, as the record's <c>said</c> names them.</param>
/// <param name="Why">The reason's code (<see cref="ContinueWhy"/>): what the page says its own sentence for.</param>
public sealed record GoOnMark(IReadOnlyList<string> Said, string Why, DateTimeOffset At);

/// <summary>
/// The marks a record's words could not go on with (MSG1b, closing MSG1a's open point):
/// <c>&lt;home&gt;/sessions/&lt;session&gt;.cannot.json</c>, beside the session's transcript and conversation id. Written where
/// a reopen could not take the words and nothing carries them on by itself (a closed quest, a never; a chat's, MSG1c), so
/// a later look leaves them waiting as said instead of trying them again.
/// </summary>
/// <remarks>
/// <para><b>By the words' ids.</b> The service keeps the words and says nothing of a failed try (MSG1a), and a word said
/// after the mark is tried again, since what held the record (an account, its tree) may have moved. So a record is judged
/// only while every word waiting on it was marked (<see cref="Judged"/>).</para>
///
/// <para><b>This machine's, never the record's</b>, like the conversation id beside it. A file that does not read is no
/// mark: one more try, never the words lost.</para>
/// </remarks>
public sealed class GoOnMarks(string home)
{
    public const string Suffix = ".cannot.json";

    /// <summary>Where a session's mark is kept.</summary>
    public string PathOf(string session) => Path.Combine(home, "sessions", session + Suffix);

    /// <summary>
    /// Mark these words as judged unable to go on, replacing any mark before. An id that is not a session's keeps nothing,
    /// and a write that fails is lost, which costs one more try at the next look.
    /// </summary>
    public void Mark(string session, IEnumerable<string> said, ContinueReason why, DateTimeOffset at)
    {
        var ids = said.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToList();
        if (!SessionEvents.IsId(session) || ids.Count == 0) return;

        try
        {
            var path = PathOf(session);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            using var buffer = new MemoryStream();
            using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
            {
                writer.WriteStartObject();
                writer.WriteStartArray("said");
                foreach (var id in ids) writer.WriteStringValue(id);
                writer.WriteEndArray();
                writer.WriteString("why", why.Code);
                writer.WriteString("at", at.ToString("O", CultureInfo.InvariantCulture));
                writer.WriteEndObject();
            }

            // LF on every platform, as every file under the home is written.
            AtomicFile.WriteText(path, Encoding.UTF8.GetString(buffer.ToArray()).Replace("\r\n", "\n") + "\n");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Lost: the next look tries the words again and marks them then.
        }
    }

    /// <summary>The mark kept for this session, or null for none, a file that does not read, or an id that is not one.</summary>
    public GoOnMark? Read(string session)
    {
        if (!SessionEvents.IsId(session)) return null;

        try
        {
            var path = PathOf(session);
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("said", out var said) || said.ValueKind != JsonValueKind.Array
                || !root.TryGetProperty("why", out var why) || why.ValueKind != JsonValueKind.String || why.GetString() is not { Length: > 0 } code)
            {
                return null;
            }

            var at = root.TryGetProperty("at", out var when) && when.ValueKind == JsonValueKind.String
                     && DateTimeOffset.TryParse(when.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : DateTimeOffset.MinValue;
            return new GoOnMark(
                [.. said.EnumerateArray().Where(id => id.ValueKind == JsonValueKind.String).Select(id => id.GetString()!)], code, at);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Take a session's mark away: its words went on, or were taken. Nothing to take is no failure.</summary>
    public void Clear(string session)
    {
        if (!SessionEvents.IsId(session)) return;
        try
        {
            File.Delete(PathOf(session));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // A mark left behind judges only the words it names; a word said since is tried again all the same.
        }
    }

    /// <summary>The marks a look plans by: one per record the person's words wait on that has one, by session.</summary>
    public IReadOnlyDictionary<string, GoOnMark> For(IEnumerable<PriorSession> records)
    {
        var marks = new Dictionary<string, GoOnMark>(StringComparer.OrdinalIgnoreCase);
        foreach (var record in records.Where(record => record.WordsWaiting))
        {
            if (Read(record.Session) is { } mark) marks[record.Session] = mark;
        }

        return marks;
    }

    /// <summary>
    /// Whether every word waiting on the record was judged unable to go on: then a look leaves it waiting. A word said since
    /// the mark, or no mark, is tried; a record with nothing waiting has nothing to judge.
    /// </summary>
    public static bool Judged(PriorSession record, GoOnMark? mark) =>
        mark is not null
        && record.Waiting is { Count: > 0 } waiting
        && waiting.All(word => word.Id is { } id && mark.Said.Contains(id, StringComparer.Ordinal));
}
