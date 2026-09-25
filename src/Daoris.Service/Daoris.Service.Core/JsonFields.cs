using System.Text;
using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// JSON read and written by hand, as every store and wire here does: nothing may quietly stop working
/// under AOT. Each of them had written these helpers for itself (REV3 CLEAN1).
/// </summary>
/// <remarks>
/// Every reader answers <i>absent</i> for a value of the wrong kind rather than throwing, and that
/// includes an element that is not an object at all. <c>TryGetProperty</c> throws on one, so a caller
/// that forgot to check first would turn a malformed document into an exception. Only one of the
/// copies this replaced checked.
/// </remarks>
internal static class JsonFields
{
    /// <summary>The document's root when it is an object; null when it is not JSON, or not an object.</summary>
    public static JsonElement? ParseObject(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object ? document.RootElement.Clone() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string? Text(JsonElement element, string name) =>
        Field(element, name, JsonValueKind.String) is { } value ? value.GetString() : null;

    public static long? Number(JsonElement element, string name) =>
        Field(element, name, JsonValueKind.Number) is { } value && value.TryGetInt64(out var number) ? number : null;

    /// <summary>
    /// The array's items, read now: a caller may still be inside the <c>using</c> of the document they
    /// came from, and an enumerator read after it closes throws.
    /// </summary>
    public static IReadOnlyList<JsonElement> Items(JsonElement element, string name) =>
        Field(element, name, JsonValueKind.Array) is { } value ? value.EnumerateArray().ToList() : [];

    /// <summary>What <paramref name="write"/> writes, as text.</summary>
    public static string Written(Action<Utf8JsonWriter> write)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) write(writer);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static JsonElement? Field(JsonElement element, string name, JsonValueKind kind) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == kind
            ? value
            : null;
}
