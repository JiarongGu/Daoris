using System.Globalization;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>One session archived on this machine, and when.</summary>
public sealed record ArchiveMark(string Session, DateTimeOffset At);

/// <summary>What an archive did with one session asked of it (D126 §5.2).</summary>
public enum ArchiveVerdict
{
    /// <summary>Archived now, or already: its mark stands.</summary>
    Archived,

    /// <summary>Still running or waiting; stopped first, it ends, and then it may be archived.</summary>
    Live,

    /// <summary>Waiting on you, or with work to review: archive never hides what needs the person.</summary>
    NeedsYou,

    /// <summary>No record here is this session.</summary>
    Unknown,
}

/// <summary>One session's outcome, with the group that kept it where that is what refused it.</summary>
public sealed record ArchiveOutcome(string Session, ArchiveVerdict Verdict)
{
    /// <summary>For <see cref="ArchiveVerdict.NeedsYou"/>: <see cref="SessionGroup.You"/> or <see cref="SessionGroup.Review"/>.</summary>
    public string? Group { get; init; }
}

/// <summary>An archive's outcomes, and the marks as they now stand.</summary>
public sealed record ArchiveAnswer(IReadOnlyList<ArchiveOutcome> Outcomes, IReadOnlyList<ArchiveMark> Marks);

/// <summary>The sessions an unarchive found not archived, said as information (D48 §6), and the marks as they now stand.</summary>
public sealed record UnarchiveAnswer(IReadOnlyList<string> NotArchived, IReadOnlyList<ArchiveMark> Marks);

/// <summary>
/// The archive marks (SESSUX1a, D126 §5.2): <c>&lt;home&gt;/sessions/archived.json</c>, each archived session and when,
/// written whole by both doors, the screen's route and the terminal's verb.
/// </summary>
/// <remarks>
/// <para><b>This machine's, never the record's.</b> The record is the service's and travels (D47 §4); what one person
/// keeps in their list is not a fact about the work, so the record is unchanged and a teammate's may be archived too.</para>
///
/// <para><b>A file that does not read is nothing archived</b>: a mark lost costs a row back in the list, never a row
/// gone. The next write starts it again.</para>
///
/// <para><b>Archive never hides what needs the person.</b> Each session is judged by where <see cref="SessionGroups"/>
/// places it as the archive is asked: a live one, one waiting on you and one with work to review are refused, and the
/// reader shows a mark that later comes to need the person in that group all the same.</para>
/// </remarks>
public sealed class SessionArchive(string home)
{
    public const string FileName = "archived.json";

    // One writer at a time in this process: two presses would otherwise read the same file, and the second write would
    // drop the first's marks.
    private static readonly object Gate = new();

    /// <summary>Beside the sessions' transcripts and conversations, which are this machine's by the same rule.</summary>
    public string FilePath => Path.Combine(home, "sessions", FileName);

    /// <summary>Every mark, by session; none when the file is missing or does not read.</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> Marks() =>
        All().ToDictionary(mark => mark.Session, mark => mark.At, StringComparer.Ordinal);

    /// <summary>
    /// Archive these sessions, each judged by its place in <paramref name="groups"/>: what is ended and needs nobody is
    /// marked, keeping when it was first archived; what is live, needs the person or is no record here is refused and
    /// left as it was. Nothing is written when nothing was archived.
    /// </summary>
    /// <param name="groups">Where the reader placed the sessions asked about, as the archive is asked.</param>
    /// <param name="known">Every record's id: a mark for a record that is gone is dropped as this writes.</param>
    public ArchiveAnswer Archive(
        IReadOnlyCollection<string> ids, IReadOnlyList<SessionGrouping> groups, IReadOnlyCollection<string> known, DateTimeOffset now)
    {
        var placed = groups.GroupBy(group => group.Session, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var outcomes = ids.Distinct(StringComparer.Ordinal).Select(id => Judge(id, placed.GetValueOrDefault(id))).ToList();
        var archived = outcomes.Where(outcome => outcome.Verdict == ArchiveVerdict.Archived).Select(outcome => outcome.Session).ToList();

        lock (Gate)
        {
            if (archived.Count == 0) return new(outcomes, All());

            var marks = All().ToDictionary(mark => mark.Session, mark => mark.At, StringComparer.Ordinal);
            foreach (var id in archived) marks.TryAdd(id, now);
            var kept = new HashSet<string>(known, StringComparer.Ordinal);
            return new(outcomes, Write(marks.Where(mark => kept.Contains(mark.Key))));
        }
    }

    /// <summary>
    /// Take these sessions' marks away, so each is back in the group its state puts it in. One that was not archived is
    /// named in the answer, never refused. Nothing is written when no mark went.
    /// </summary>
    /// <param name="known">Every record's id where the door has them, to drop a mark for a record that is gone; null keeps every other mark.</param>
    public UnarchiveAnswer Unarchive(IReadOnlyCollection<string> ids, IReadOnlyCollection<string>? known = null)
    {
        lock (Gate)
        {
            var marks = All().ToDictionary(mark => mark.Session, mark => mark.At, StringComparer.Ordinal);
            var asked = ids.Distinct(StringComparer.Ordinal).ToList();
            var notArchived = asked.Where(id => !marks.ContainsKey(id)).ToList();
            if (notArchived.Count == asked.Count) return new(notArchived, [.. Sorted(marks)]);

            foreach (var id in asked) marks.Remove(id);
            var kept = known is null ? null : new HashSet<string>(known, StringComparer.Ordinal);
            return new(notArchived, Write(marks.Where(mark => kept is null || kept.Contains(mark.Key))));
        }
    }

    /// <summary>
    /// The machine log's <c>sessions.archived</c> (SESSUX1g, D126 §7.4): how many an archive took, and from which door,
    /// <see cref="PluginEvents.Screen"/> or <see cref="PluginEvents.Terminal"/>. An archive changes no work, so it is counted,
    /// never listed; one that took nothing writes nothing.
    /// </summary>
    public static void Said(MachineLog? log, int count, string door)
    {
        if (count > 0) log?.Info("sessions.archived", ("count", count), ("door", door));
    }

    private static ArchiveOutcome Judge(string id, SessionGrouping? placed) => placed?.Group switch
    {
        null => new(id, ArchiveVerdict.Unknown),
        SessionGroup.Working => new(id, ArchiveVerdict.Live),
        SessionGroup.You or SessionGroup.Review => new(id, ArchiveVerdict.NeedsYou) { Group = placed.Group },
        _ => new(id, ArchiveVerdict.Archived),
    };

    /// <summary>Every mark in the file, oldest first; none where the file is missing or does not read.</summary>
    private IReadOnlyList<ArchiveMark> All()
    {
        try
        {
            if (!File.Exists(FilePath)) return [];
            using var document = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (!document.RootElement.TryGetProperty("archived", out var archived) || archived.ValueKind != JsonValueKind.Array) return [];
            return [.. Sorted(archived.EnumerateArray()
                .Select(Read)
                .OfType<ArchiveMark>()
                .GroupBy(mark => mark.Session, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Min(mark => mark.At), StringComparer.Ordinal))];
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return [];
        }
    }

    private IReadOnlyList<ArchiveMark> Write(IEnumerable<KeyValuePair<string, DateTimeOffset>> marks)
    {
        var sorted = Sorted(marks).ToList();
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        AtomicFile.WriteText(FilePath, ToJson(sorted));
        return sorted;
    }

    private static IEnumerable<ArchiveMark> Sorted(IEnumerable<KeyValuePair<string, DateTimeOffset>> marks) =>
        marks.Select(mark => new ArchiveMark(mark.Key, mark.Value))
            .OrderBy(mark => mark.At).ThenBy(mark => mark.Session, StringComparer.Ordinal);

    /// <summary>Written by hand, as the driver's other files are, for the AOT reason <see cref="DriverConfig"/> gives.</summary>
    private static string ToJson(IReadOnlyList<ArchiveMark> marks)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("archived");
            foreach (var mark in marks)
            {
                writer.WriteStartObject();
                writer.WriteString("session", mark.Session);
                writer.WriteString("at", mark.At.ToString("O", CultureInfo.InvariantCulture));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n";
    }

    /// <summary>One mark, or null where it names no session.</summary>
    private static ArchiveMark? Read(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("session", out var session) || session.ValueKind != JsonValueKind.String
            || session.GetString() is not { Length: > 0 } id)
        {
            return null;
        }

        var at = element.TryGetProperty("at", out var when) && when.ValueKind == JsonValueKind.String
                 && DateTimeOffset.TryParse(when.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
            ? parsed
            : DateTimeOffset.MinValue;
        return new ArchiveMark(id, at);
    }
}
