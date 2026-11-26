using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>One change of a class, as it is kept: when, on which ask or quest of which workspace, the class, why and by whom.</summary>
/// <param name="Target"><see cref="PriorityBook.Ask"/> or <see cref="PriorityBook.Quest"/>.</param>
/// <param name="Id">The ask's or the quest's id, with no <c>#</c>.</param>
/// <param name="Workspace">The workspace it is of, which an urgent class is counted in.</param>
/// <param name="By">Who set it, as a wait line names them: <c>the person</c>, <c>session s-1</c>.</param>
public sealed record PriorityEvent(DateTimeOffset At, string Target, string Id, string Workspace, string Class, string Why, string By)
{
    /// <summary>When it ends, after which the class set before it stands again; an urgent class always has one.</summary>
    public DateTimeOffset? Until { get; init; }
}

/// <summary>A class as read at a look: the class, where it comes from (the quest's own, its ask's, or none set) and the change that set it.</summary>
public sealed record PriorityReading(string Class, string Source, PriorityEvent? Event)
{
    /// <summary>The source where nothing is set: the class is <see cref="PriorityClasses.Normal"/>.</summary>
    public const string Unset = "unset";

    public static PriorityReading None { get; } = new(PriorityClasses.Normal, Unset, null);
}

/// <summary>
/// What a workspace may set of the framework: how many asks and quests hold urgent at once, and how long an urgent class
/// lasts where its setter names no end.
/// </summary>
public sealed record PriorityRules(int UrgentCap, TimeSpan UrgentLife)
{
    /// <summary>Two at once, for a day: the defaults until a workspace sets its own.</summary>
    public static PriorityRules Default { get; } = new(2, TimeSpan.FromHours(24));
}

/// <summary>
/// The classes set on asks and quests on this machine: each change one event, kept in the order made in the home
/// (<c>priority</c>, <c>data/priority.json</c>), never a field copied into a run. A quest's class is read at each look: its own newest
/// change still in force, else its ask's, else <see cref="PriorityClasses.Normal"/>. A change whose end has passed is no longer in
/// force, so the one before it stands again.
/// </summary>
/// <remarks>
/// <para><b>Urgent carries an end and a cap.</b> Setting urgent names when it ends, and a workspace holds at most
/// <see cref="PriorityRules.UrgentCap"/> asks and quests urgent at once: one more is refused, naming those that hold it and their ends.
/// The count and the write happen under the file's keeping lock, so two setters never both take the last place.</para>
/// <para><b>Machine-local.</b> The heavy-run slots it orders are this machine's; another machine's classes are its own.</para>
/// </remarks>
public static class PriorityBook
{
    /// <summary>A change set on an ask: every quest of the ask with none of its own reads it.</summary>
    public const string Ask = "ask";

    /// <summary>A change set on a quest.</summary>
    public const string Quest = "quest";

    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.Priority);

    /// <summary>Every change kept, in the order made; a file missing or that does not read is none.</summary>
    public static IReadOnlyList<PriorityEvent> Read(string home)
    {
        try
        {
            using var stream = new FileStream(PathOf(home), FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var document = JsonDocument.Parse(stream);
            return document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("events", out var events) && events.ValueKind == JsonValueKind.Array
                ? [.. events.EnumerateArray().Select(Parse).OfType<PriorityEvent>()]
                : [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException)
        {
            return [];
        }
    }

    /// <summary>The newest change of one ask or quest still in force at <paramref name="now"/>, or null where none is.</summary>
    public static PriorityEvent? Standing(IReadOnlyList<PriorityEvent> events, string target, string id, DateTimeOffset now) =>
        events.LastOrDefault(each => each.Target == target && string.Equals(each.Id, id, StringComparison.OrdinalIgnoreCase) && (each.Until is null || each.Until > now));

    /// <summary>A quest's class at <paramref name="now"/>: its own, else its ask's, else normal.</summary>
    public static PriorityReading ClassOf(IReadOnlyList<PriorityEvent> events, string? quest, string? ask, DateTimeOffset now)
    {
        if (quest is { Length: > 0 } && Standing(events, Quest, quest, now) is { } own) return new PriorityReading(own.Class, Quest, own);
        if (ask is { Length: > 0 } && Standing(events, Ask, ask, now) is { } asked) return new PriorityReading(asked.Class, Ask, asked);
        return PriorityReading.None;
    }

    /// <summary>
    /// Keep one change, or say why not: a class none of the four, no why, an urgent class with no end after <paramref name="now"/>, or
    /// one more urgent than its workspace's cap. Null where it is kept.
    /// </summary>
    /// <exception cref="IOException">Another writer held the file's keeping lock for the whole of its wait.</exception>
    public static string? Set(string home, PriorityEvent change, DateTimeOffset now, PriorityRules rules)
    {
        if (PriorityClasses.Parse(change.Class) is not { } @class) return $"a class is urgent, high, normal or low — `{change.Class}` is none of them.";
        if (string.IsNullOrWhiteSpace(change.Why)) return "a class is set with why: say it with --why.";
        if (change.Target is not (Ask or Quest) || string.IsNullOrWhiteSpace(change.Id)) return "a class is set on an ask or a quest.";
        if ((@class == PriorityClasses.Urgent && change.Until is null) || (change.Until is { } until && until <= now))
        {
            return "an urgent class needs an end after now: give it with --until.";
        }

        var file = PathOf(home);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        using var held = KeepLock.Take(file);
        var events = Read(home);
        if (@class == PriorityClasses.Urgent && Holding(events, change, now) is { Count: var count } holding && count >= rules.UrgentCap)
        {
            var said = string.Join(", ", holding.Select(each => $"{each.Target} #{each.Id} until {Stamp(each.Until!.Value)}"));
            return $"the workspace `{change.Workspace}` holds {count} urgent at once, its cap of {rules.UrgentCap}: {said}. Lower one, or wait for one to end.";
        }

        AtomicFile.WriteText(file, Serialize([.. events, change with { Class = @class, Why = change.Why.Trim() }]));
        return null;
    }

    /// <summary>The asks and quests of the change's workspace urgent now, the change's own target apart.</summary>
    private static List<PriorityEvent> Holding(IReadOnlyList<PriorityEvent> events, PriorityEvent change, DateTimeOffset now) =>
        [.. events
            .Where(each => string.Equals(each.Workspace, change.Workspace, StringComparison.OrdinalIgnoreCase))
            .Select(each => (each.Target, Id: each.Id.ToLowerInvariant()))
            .Distinct()
            .Where(target => !(target.Target == change.Target && string.Equals(target.Id, change.Id, StringComparison.OrdinalIgnoreCase)))
            .Select(target => Standing(events, target.Target, target.Id, now))
            .OfType<PriorityEvent>()
            .Where(each => each.Class == PriorityClasses.Urgent)];

    /// <summary>A change as a wait line says where it comes from: <c>set by the person: the fix</c>.</summary>
    public static string Said(PriorityEvent change) => $"set by {change.By}: {change.Why}";

    internal static string Stamp(DateTimeOffset at) => at.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static PriorityEvent? Parse(JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.Object) return null;
        string? Text(string name) => raw.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        DateTimeOffset? At(string name) => raw.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && value.TryGetDateTimeOffset(out var at) ? at : null;
        return At("at") is { } when && Text("target") is { } target && Text("id") is { } id && Text("class") is { } @class
            ? new PriorityEvent(when, target, id, Text("workspace") ?? "", @class, Text("why") ?? "", Text("by") ?? "") { Until = At("until") }
            : null;
    }

    private static string Serialize(IReadOnlyList<PriorityEvent> events)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema", 1);
            writer.WriteStartArray("events");
            foreach (var each in events)
            {
                writer.WriteStartObject();
                writer.WriteString("at", each.At.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                writer.WriteString("target", each.Target);
                writer.WriteString("id", each.Id);
                writer.WriteString("workspace", each.Workspace);
                writer.WriteString("class", each.Class);
                writer.WriteString("why", each.Why);
                writer.WriteString("by", each.By);
                if (each.Until is { } until) writer.WriteString("until", until.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray()).ReplaceLineEndings("\n") + "\n";
    }
}
