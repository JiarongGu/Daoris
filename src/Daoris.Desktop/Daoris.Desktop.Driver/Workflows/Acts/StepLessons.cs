using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// What a step's acts came to, proposed into the repository's delivery knowledge entry the step names (the long loop): a step settled by
/// its receipts whose act did not reach at once (refused, failed, waiting on another role) and reached after, its failure and what reached
/// it, from one run and step.
/// </summary>
public sealed record StepLesson(string Repository, string Entry, string Run, string Node, string Words, DateTimeOffset At)
{
    /// <summary>The run whose hand-off carried it to its holder, or null while none has.</summary>
    public string? Carried { get; init; }

    /// <summary>The values a repository held for steps its workflow's new version holds no more, moved into the entry; null for a run's lesson.</summary>
    public MovedValues? Moved { get; init; }

    /// <summary>When the entry, in the repository's checkout, first held each moved value; null while it does not.</summary>
    public DateTimeOffset? Landed { get; init; }
}

/// <summary>A repository's values for the nodes of <paramref name="Workflow"/> its version <paramref name="Version"/> holds no more: by node, each option's JSON.</summary>
public sealed record MovedValues(string Workflow, int Version, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Values);

/// <summary>
/// The long loop's proposals, <c>workflows/acts/proposed.json</c>, written by the pass alone: proposed as a step its receipts settle settles
/// (<see cref="StepActs"/>), and carried once by the next hand-off of a step naming the same entry in the same repository
/// (<see cref="StepHandOffs"/>), whose holder, the repository's own agent, amends the entry with what still holds. A repository's values
/// moved into the entry (<see cref="ValuesMoves"/>) are carried by every such hand-off until the entry holds them. A proposal, never the
/// procedure: the procedure stays the entry, and the repository's own change decides it.
/// </summary>
public static class StepLessons
{
    /// <summary>The newest proposals the file keeps.</summary>
    public const int Kept = 100;

    public static string PathOf(string home) => Path.Combine(DaorisHome.Entry(home, HomeEntry.Workflows, "acts"), "proposed.json");

    /// <summary>
    /// What <paramref name="rows"/> came to for <paramref name="node"/>: each fact it maps whose newest receipt reached after one that did not,
    /// with each such receipt and the one that reached; null where every fact reached at once, or one has not reached.
    /// </summary>
    public static string? Of(NodeDeclaration node, IEnumerable<StepActRow> rows)
    {
        var listed = rows.ToList();
        var parts = new List<string>();
        foreach (var fact in StepReceipts.Mapped(node).Values.Distinct(StringComparer.Ordinal))
        {
            var of = listed.Where(row => row.Fact == fact).ToList();
            if (of.Count == 0 || of[^1] is not { State: StepReceipts.Reached } reached) continue;
            var before = of.Take(of.Count - 1).Where(row => row.State != StepReceipts.Reached).ToList();
            if (before.Count == 0) continue;
            parts.Add($"`{fact}` was {string.Join("; then ", before.Select(row => $"{row.State} ({row.Message ?? row.Url ?? fact})"))}; it reached after: {reached.Message ?? reached.Url ?? fact}.");
        }

        return parts.Count == 0 ? null : string.Join(" ", parts);
    }

    /// <summary>Keeps <paramref name="lesson"/>, once per run and step.</summary>
    public static bool Propose(string home, StepLesson lesson)
    {
        var kept = Read(home);
        if (kept.Any(each => each.Run == lesson.Run && each.Node == lesson.Node)) return false;
        Write(home, [.. kept, lesson]);
        return true;
    }

    /// <summary>
    /// The proposals for <paramref name="entry"/> of <paramref name="repository"/> from a run other than <paramref name="run"/>: a run's no
    /// hand-off carried yet, and moved values the entry does not hold yet.
    /// </summary>
    public static IReadOnlyList<StepLesson> Open(string home, string repository, string entry, string run) =>
        [.. Read(home).Where(each => (each.Moved is null ? each.Carried is null : each.Landed is null)
            && string.Equals(each.Repository, repository, StringComparison.OrdinalIgnoreCase) && each.Entry == entry && each.Run != run)];

    /// <summary>Marks <paramref name="lessons"/> carried by <paramref name="run"/>'s hand-off.</summary>
    public static void Carry(string home, IReadOnlyList<StepLesson> lessons, string run)
    {
        if (lessons.Count == 0) return;
        Write(home, [.. Read(home).Select(each => each.Carried is null && lessons.Any(lesson => lesson.Run == each.Run && lesson.Node == each.Node)
            ? each with { Carried = run }
            : each)]);
    }

    /// <summary>The hand-off's words for the proposals it carries; none where it carries none.</summary>
    public static string Words(string entry, IReadOnlyList<StepLesson> lessons) => lessons.Count == 0
        ? ""
        : $" Proposed for `{entry}` and not in it yet: {string.Join(" ", lessons.Select(each => each.Moved is null ? $"run #{each.Run}'s `{each.Node}`: {each.Words}" : each.Words))} "
          + $"Write into the entry what still holds (`.claude/knowledge/{entry}.md`, in your repository's own change), so later runs read it there.";

    /// <summary>The proposals kept, oldest first; none where the file is missing or does not read.</summary>
    public static IReadOnlyList<StepLesson> Read(string home)
    {
        var path = PathOf(home);
        try
        {
            if (!File.Exists(path)) return [];
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            if (!document.RootElement.TryGetProperty("proposed", out var rows) || rows.ValueKind != JsonValueKind.Array) return [];
            return [.. rows.EnumerateArray().Select(RowOf).OfType<StepLesson>()];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static StepLesson? RowOf(JsonElement row) =>
        row.ValueKind == JsonValueKind.Object && Text(row, "repository") is { } repository && Text(row, "entry") is { } entry && Text(row, "run") is { } run
        && Text(row, "node") is { } node && Text(row, "words") is { } words && Moments.Read(Text(row, "at")) is { } at
            ? new StepLesson(repository, entry, run, node, words, at) { Carried = Text(row, "carried"), Moved = MovedOf(row), Landed = Moments.Read(Text(row, "landed")) }
            : null;

    /// <summary>A row's moved values: its workflow, version and each node's options as JSON; null where it moves none or they do not read.</summary>
    private static MovedValues? MovedOf(JsonElement row)
    {
        if (!row.TryGetProperty("moved", out var moved) || moved.ValueKind != JsonValueKind.Object || Text(moved, "workflow") is not { } workflow
            || !moved.TryGetProperty("version", out var version) || !version.TryGetInt32(out var number)
            || !moved.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new MovedValues(workflow, number, values.EnumerateObject().Where(node => node.Value.ValueKind == JsonValueKind.Object).ToDictionary(
            node => node.Name,
            node => (IReadOnlyDictionary<string, string>)node.Value.EnumerateObject().ToDictionary(option => option.Name, option => option.Value.GetRawText(), StringComparer.Ordinal),
            StringComparer.Ordinal));
    }

    private static void WriteMoved(Utf8JsonWriter writer, MovedValues moved)
    {
        writer.WriteStartObject("moved");
        writer.WriteString("workflow", moved.Workflow);
        writer.WriteNumber("version", moved.Version);
        writer.WriteStartObject("values");
        foreach (var (node, options) in moved.Values.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteStartObject(node);
            foreach (var (name, json) in options.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                writer.WritePropertyName(name);
                writer.WriteRawValue(json);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    /// <summary>The proposals written whole, the newest <see cref="Kept"/>: by the pass alone.</summary>
    internal static void Write(string home, IReadOnlyList<StepLesson> rows) => JsonFile.Write(PathOf(home), writer =>
    {
        writer.WriteStartObject();
        writer.WriteStartArray("proposed");
        foreach (var row in rows.TakeLast(Kept))
        {
            writer.WriteStartObject();
            writer.WriteString("repository", row.Repository);
            writer.WriteString("entry", row.Entry);
            writer.WriteString("run", row.Run);
            writer.WriteString("node", row.Node);
            writer.WriteString("words", row.Words);
            if (row.Carried is not null) writer.WriteString("carried", row.Carried);
            if (row.Moved is not null) WriteMoved(writer, row.Moved);
            if (row.Landed is { } landed) writer.WriteString("landed", Moments.Write(landed));
            writer.WriteString("at", Moments.Write(row.At));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    });
}
