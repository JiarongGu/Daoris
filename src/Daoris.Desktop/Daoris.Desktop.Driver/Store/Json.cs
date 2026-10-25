using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The one set of field readers every store and wire in the driver reads JSON with, by hand and without reflection
/// (DEDUP1a): about forty private copies had each written their own, and about twenty-five threw on an element that was
/// not an object.
/// </summary>
/// <remarks>
/// Every reader answers <i>absent</i> for a value of the wrong kind rather than throwing, and that includes an element
/// that is not an object at all: <c>TryGetProperty</c> throws on one, so a caller that forgot to check first would turn a
/// malformed document into an exception. Where a caller treats an empty string as missing it reads
/// <see cref="NonEmpty(JsonElement, string)"/>; where it keeps one, <see cref="Text(JsonElement, string)"/>.
/// </remarks>
public static class Json
{
    /// <summary>The string a field holds, empty included; null when missing, not a string, or the element not an object.</summary>
    public static string? Text(JsonElement element, string name) => JsonText.Of(element, name);

    /// <summary>As <see cref="Text(JsonElement, string)"/>, with an empty string read as missing.</summary>
    public static string? NonEmpty(JsonElement element, string name) =>
        Text(element, name) is { Length: > 0 } text ? text : null;

    public static int? Int(JsonElement element, string name) =>
        Field(element, name, JsonValueKind.Number) is { } value && value.TryGetInt32(out var number) ? number : null;

    public static long? Long(JsonElement element, string name) =>
        Field(element, name, JsonValueKind.Number) is { } value && value.TryGetInt64(out var number) ? number : null;

    /// <summary>A number as a double; null when it is missing, not a number, or not finite.</summary>
    public static double? Number(JsonElement element, string name) =>
        Field(element, name, JsonValueKind.Number) is { } value && value.TryGetDouble(out var number) && double.IsFinite(number)
            ? number
            : null;

    /// <summary>True only for a boolean <c>true</c>.</summary>
    public static bool Flag(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    /// <summary>The strings of an array field; non-strings are dropped, and so are empty ones unless <paramref name="keepEmpty"/>.</summary>
    public static IReadOnlyList<string> Strings(JsonElement element, string name, bool keepEmpty = false)
    {
        if (Field(element, name, JsonValueKind.Array) is not { } array) return [];

        var items = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } text && (keepEmpty || text.Length > 0)) items.Add(text);
        }

        return items;
    }

    /// <summary>
    /// The objects of an array field, cloned: they outlive the <c>using</c> of the document they came from, which an
    /// enumerator read after it closes would not.
    /// </summary>
    public static IReadOnlyList<JsonElement> Items(JsonElement element, string name) =>
        Field(element, name, JsonValueKind.Array) is { } array
            ? [.. array.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.Object).Select(item => item.Clone())]
            : [];

    /// <summary>The string a node holds; null when it is missing or not a string.</summary>
    public static string? Text(JsonNode? node) => JsonText.Of(node);

    /// <summary>The string a node holds, trimmed; null when it is blank or not a string.</summary>
    public static string? Trimmed(JsonNode? node) =>
        Text(node) is { } text && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;

    /// <summary>The non-blank strings of an array node, each trimmed.</summary>
    public static IReadOnlyList<string> TrimmedStrings(JsonNode? node) =>
        node is JsonArray items ? [.. items.Select(Trimmed).OfType<string>()] : [];

    public static string? Text(JsonObject entry, string name) => Text(entry[name]);

    /// <summary>As <see cref="Text(JsonObject, string)"/>, with an empty string read as missing.</summary>
    public static string? NonEmpty(JsonObject entry, string name) => Text(entry[name]) is { Length: > 0 } text ? text : null;

    /// <summary>True only for a boolean <c>true</c>.</summary>
    public static bool Flag(JsonObject entry, string name) =>
        entry[name] is JsonValue value && value.TryGetValue<bool>(out var flag) && flag;

    private static JsonElement? Field(JsonElement element, string name, JsonValueKind kind) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == kind
            ? value
            : null;
}
