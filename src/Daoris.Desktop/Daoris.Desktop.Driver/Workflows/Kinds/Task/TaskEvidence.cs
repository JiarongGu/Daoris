using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The evidence a <c>task</c> step's settle names (its <c>evidence</c> option, a list of names, as the config change's <c>version</c> and
/// <c>capture</c>): an agent's settle through its tool (<see cref="StepSettles"/>) gives each, and is refused naming those it leaves out; the
/// evidence is kept on the record in the settle's words. Pure.
/// </summary>
public static class TaskEvidence
{
    /// <summary>The option naming the evidence a settle gives.</summary>
    public const string Option = "evidence";

    /// <summary>The names <paramref name="node"/>'s settle gives, in their order; none where it names none or its option does not read.</summary>
    public static IReadOnlyList<string> Required(NodeDeclaration node)
    {
        if (!node.Options.TryGetValue(Option, out var text)) return [];
        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            return root.ValueKind switch
            {
                JsonValueKind.String => root.GetString() is { Length: > 0 } one ? [one] : [],
                JsonValueKind.Array => [.. root.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)
                    .Where(each => each.Trim().Length > 0)],
                _ => [],
            };
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>The names <paramref name="node"/> requires that <paramref name="given"/> leaves out or leaves empty.</summary>
    public static IReadOnlyList<string> Missing(NodeDeclaration node, IReadOnlyDictionary<string, string> given) =>
        [.. Required(node).Where(name => !given.TryGetValue(name, out var value) || string.IsNullOrWhiteSpace(value))];

    /// <summary>The settle's words with its evidence after them, as the record keeps them: <c>words (version: v42; capture: …)</c>.</summary>
    public static string? Words(string? words, IReadOnlyDictionary<string, string> given)
    {
        var said = string.IsNullOrWhiteSpace(words) ? null : words.Trim();
        var named = given.Where(each => !string.IsNullOrWhiteSpace(each.Value)).Select(each => $"{each.Key.Trim()}: {each.Value.Trim()}").ToList();
        if (named.Count == 0) return said;
        var evidence = string.Join("; ", named);
        return said is null ? evidence : $"{said} ({evidence})";
    }
}
