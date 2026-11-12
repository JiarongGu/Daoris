using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>One check of a declaration: its name and the command it runs; <see cref="Was"/> is the command it ran before, where a changed check's command differs.</summary>
public sealed record DeclaredCheck(string Name, string Run, string? Was = null);

/// <summary>
/// What a line's <c>daoris.gates.json</c> changed against the one accepted, as a person reads it: the checks added, removed and
/// changed by name with the command each runs, and the other top-level parts of the file that changed (by their key).
/// </summary>
public sealed record DeclarationChange(
    IReadOnlyList<DeclaredCheck> Added, IReadOnlyList<DeclaredCheck> Removed, IReadOnlyList<DeclaredCheck> Changed, IReadOnlyList<string> Other)
{
    /// <summary>Nothing differs that runs: the same checks and the same parts.</summary>
    public bool IsEmpty => Added.Count == 0 && Removed.Count == 0 && Changed.Count == 0 && Other.Count == 0;

    private const string ChecksKey = "gates";

    /// <summary>
    /// The change from the declaration <paramref name="accepted"/> (the text of the accepted blob; null where none was accepted or it held
    /// none) to <paramref name="tip"/> (the line's), read from their JSON. A text that is not JSON declares no checks.
    /// </summary>
    public static DeclarationChange Between(string? accepted, string? tip)
    {
        var was = Parse(accepted);
        var now = Parse(tip);
        var before = Checks(was);
        var after = Checks(now);

        var added = after.Where(each => !before.ContainsKey(each.Key)).Select(each => Check(each.Key, each.Value)).ToList();
        var removed = before.Where(each => !after.ContainsKey(each.Key)).Select(each => Check(each.Key, each.Value)).ToList();
        var changed = after
            .Where(each => before.TryGetValue(each.Key, out var old) && !JsonNode.DeepEquals(old, each.Value))
            .Select(each =>
            {
                var run = Check(each.Key, each.Value);
                var old = Check(each.Key, before[each.Key]);
                return old.Run == run.Run ? run : run with { Was = old.Run };
            })
            .ToList();

        var keys = (was?.Select(each => each.Key) ?? []).Concat(now?.Select(each => each.Key) ?? []).Distinct(StringComparer.Ordinal)
            .Where(key => key != ChecksKey && !JsonNode.DeepEquals(was?[key], now?[key]))
            .Order(StringComparer.Ordinal)
            .ToList();
        return new DeclarationChange(added, removed, changed, keys);
    }

    /// <summary>
    /// The change of the line's declaration <paramref name="tip"/> against the blob <paramref name="accepted"/> (null or <c>none</c> for none), both read
    /// as git objects in the checkout at <paramref name="root"/>; null where no checkout reads.
    /// </summary>
    public static async Task<DeclarationChange?> AtAsync(string? root, string? accepted, string tip, CancellationToken ct)
    {
        if (root is null) return null;
        var was = accepted is null ? null : await IntegrationAcceptance.TextAsync(root, accepted, ct).ConfigureAwait(false);
        var now = await IntegrationAcceptance.TextAsync(root, tip, ct).ConfigureAwait(false);
        return Between(was, now);
    }

    /// <summary>The change as the window and the terminal read it: the checks by name with their commands, and the other parts' keys. No blob id.</summary>
    public object Wire() => new { Added, Removed, Changed, Other };

    private static DeclaredCheck Check(string name, JsonNode? node) => new(name, node?["run"]?.GetValue<string>() ?? "");

    private static JsonObject? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        try
        {
            return JsonNode.Parse(text, null, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true }) as JsonObject;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Dictionary<string, JsonNode?> Checks(JsonObject? declaration)
    {
        var checks = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        if (declaration?[ChecksKey] is not JsonArray array) return checks;
        foreach (var node in array)
        {
            if (node is JsonObject { } check && check["name"] is JsonValue name && name.TryGetValue<string>(out var text)) checks[text] = check;
        }

        return checks;
    }

    /// <summary>The change in plain words, one sentence: <c>added `a`, `b`; removed `c`; changed `d`</c>, and the other parts named; empty where nothing differs.</summary>
    public string Said()
    {
        var parts = new List<string>();
        if (Added.Count > 0) parts.Add($"added {Names(Added)}");
        if (Removed.Count > 0) parts.Add($"removed {Names(Removed)}");
        if (Changed.Count > 0) parts.Add($"changed {Names(Changed)}");
        if (Other.Count > 0) parts.Add($"changed its {string.Join(", ", Other.Select(key => $"`{key}`"))} part");
        return string.Join("; ", parts);
    }

    /// <summary>The sentence a line's own acceptance says: <c>checks changed on main: added `a`; they run from now on.</c></summary>
    public string Words(string line) => Said() is { Length: > 0 } said
        ? $"checks changed on {line}: {said}; they run from now on."
        : $"declaration changed on {line}; it is used from now on.";

    private static string Names(IEnumerable<DeclaredCheck> checks) => string.Join(", ", checks.Select(each => $"`{each.Name}`"));
}
