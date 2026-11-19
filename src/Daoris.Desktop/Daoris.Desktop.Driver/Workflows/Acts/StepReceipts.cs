using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What a <c>task</c> step its holder works by acts declares: its <c>receipts</c> option maps each evidence name to the fact a tool's receipt
/// names (<c>{"release":"release-created","ended":"stage-ended"}</c>), and its <c>knowledge</c> option names the repository's delivery
/// knowledge entry that holds its procedure (which target, which acts, what settles each). A step mapping receipts is settled by them alone,
/// never by its holder's words. Pure.
/// </summary>
public static class StepReceipts
{
    /// <summary>The option mapping evidence names to the facts receipts name.</summary>
    public const string Option = "receipts";

    /// <summary>The option naming the repository's delivery knowledge entry.</summary>
    public const string KnowledgeOption = "knowledge";

    /// <summary>A holder's settle of a step its receipts settle: refused, the receipts' alone.</summary>
    public const string ByReceipt = "by-receipt";

    /// <summary>A receipt's state for a fact made or read so.</summary>
    public const string Reached = "reached";

    /// <summary>The facts <paramref name="node"/>'s receipts give, by the evidence name each gives; none where it maps none or its option does not read.</summary>
    public static IReadOnlyDictionary<string, string> Mapped(NodeDeclaration node) => Map(node, Option);

    /// <summary>The names <paramref name="node"/>'s <paramref name="option"/> maps to facts, each trimmed; none where it maps none or does not read.</summary>
    internal static IReadOnlyDictionary<string, string> Map(NodeDeclaration node, string option)
    {
        if (!node.Options.TryGetValue(option, out var text)) return new Dictionary<string, string>();
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind != JsonValueKind.Object
                ? new Dictionary<string, string>()
                : document.RootElement.EnumerateObject()
                    .Where(each => each.Value.ValueKind == JsonValueKind.String && each.Value.GetString()!.Trim().Length > 0 && each.Name.Trim().Length > 0)
                    .ToDictionary(each => each.Name.Trim(), each => each.Value.GetString()!.Trim(), StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    /// <summary>The delivery knowledge entry <paramref name="node"/> names, or null.</summary>
    public static string? Knowledge(NodeDeclaration node)
    {
        if (!node.Options.TryGetValue(KnowledgeOption, out var text)) return null;
        try
        {
            using var document = JsonDocument.Parse(text);
            return document.RootElement.ValueKind == JsonValueKind.String && document.RootElement.GetString()!.Trim() is { Length: > 0 } name ? name : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Whether a receipt naming <paramref name="fact"/> gives <paramref name="node"/> evidence.</summary>
    public static bool Takes(NodeDeclaration node, string fact) => Mapped(node).Values.Contains(fact, StringComparer.Ordinal);

    /// <summary>
    /// The evidence <paramref name="rows"/> give <paramref name="node"/>, each name the newest receipt of its fact, where each is reached; null
    /// while one is missing or its newest receipt is not reached (waiting, or failed).
    /// </summary>
    public static IReadOnlyDictionary<string, string>? Settled(NodeDeclaration node, IEnumerable<StepActRow> rows)
    {
        var mapped = Mapped(node);
        if (mapped.Count == 0) return null;
        var listed = rows.ToList();
        var evidence = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, fact) in mapped)
        {
            if (listed.LastOrDefault(row => row.Fact == fact) is not { State: Reached } newest) return null;
            evidence[name] = newest.Message ?? newest.Url ?? fact;
        }

        return evidence;
    }

    /// <summary>The hand-off's words for the step's procedure: the entry it names, where it names one.</summary>
    public static string KnowledgeWords(NodeDeclaration node) => Knowledge(node) is { } entry
        ? $" Its procedure is the repository's delivery knowledge `{entry}`: read it (`knowledge_get`, or `.claude/knowledge/{entry}.md`) and follow it."
        : "";

    /// <summary>The hand-off's words for how a step its receipts settle is settled.</summary>
    public static string Words(NodeDeclaration node)
    {
        var mapped = Mapped(node);
        var facts = string.Join(", ", mapped.Values.Distinct(StringComparer.Ordinal).Select(fact => $"`{fact}`"));
        return $" Take its acts with your plugin's tools: each round's receipt is journaled on this step, and reached receipts {facts} settle it "
               + $"({string.Join(", ", mapped.Keys.Select(name => $"`{name}`"))}), so `{StepSettles.Tool}` takes none of it.";
    }
}
