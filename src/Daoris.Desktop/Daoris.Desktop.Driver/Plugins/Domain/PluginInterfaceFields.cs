using System.Text.Json;

namespace Daoris.Driver;

/// <summary>The field readers an interface entry is read by: a text, a flag, authored words and a list of a closed set of words.</summary>
internal static class PluginInterfaceFields
{
    public static string? Text(JsonElement row, string name) =>
        row.ValueKind == JsonValueKind.Object && row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
        && value.GetString()!.Trim() is { Length: > 0 } text
            ? text
            : null;

    /// <summary>A flag as written, false where absent; null where it is not a flag.</summary>
    public static bool? Flag(JsonElement row, string name) =>
        !row.TryGetProperty(name, out var value) ? false
        : value.ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null };

    /// <summary>Authored words (D222), or <paramref name="unwritten"/> where absent; false where present and not words of that bound.</summary>
    public static bool Words(JsonElement row, string name, int most, bool oneLine, AuthoredText? unwritten, out AuthoredText? words)
    {
        words = unwritten;
        if (!row.TryGetProperty(name, out var declared)) return true;
        words = AuthoredText.Read(declared, most, oneLine);
        if (words is null || words.Texts.Any(text => text.Trim().Length == 0)) return false;
        return true;
    }

    /// <summary>
    /// A list of words of <paramref name="closed"/>, each once, or <paramref name="unwritten"/> where absent; null where it is no such
    /// list, or empty and <paramref name="atLeastOne"/>.
    /// </summary>
    public static IReadOnlyList<string>? Closed(JsonElement row, string name, IReadOnlyList<string> closed, IReadOnlyList<string> unwritten, bool atLeastOne)
    {
        if (!row.TryGetProperty(name, out var declared)) return unwritten;
        if (declared.ValueKind != JsonValueKind.Array) return null;
        var words = declared.EnumerateArray().Select(each => each.ValueKind == JsonValueKind.String ? each.GetString()! : "").ToList();
        if (words.Any(word => !closed.Contains(word, StringComparer.Ordinal)) || words.Distinct(StringComparer.Ordinal).Count() != words.Count) return null;
        return atLeastOne && words.Count == 0 ? null : words;
    }

    /// <summary>A closed set as a sentence names it: <c>`a`, `b`</c>.</summary>
    public static string Listed(IEnumerable<string> words) => string.Join(", ", words.Select(word => $"`{word}`"));
}
