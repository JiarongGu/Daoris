using System.Text.Json;

namespace Daoris.Desktop;

/// <summary>A press's payload as a view reads it: a field's text, a list of words, a read's rows.</summary>
internal static class PressPayload
{
    /// <summary>The field's trimmed text, empty where the payload has none.</summary>
    public static string Text(JsonElement? payload, string name) =>
        payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()!.Trim()
            : "";

    /// <summary>The field as sent, never trimmed: null where it is missing, null or not text. A route that takes text exactly is judged on this.</summary>
    public static string? Raw(JsonElement? payload, string name) =>
        payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    /// <summary>The field's strings, each trimmed with a leading <c>#</c> dropped, none repeated, none empty.</summary>
    public static List<string> Words(JsonElement? payload, string name) =>
        payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? [.. value.EnumerateArray()
                .Where(each => each.ValueKind == JsonValueKind.String)
                .Select(each => each.GetString()!.Trim().TrimStart('#').Trim())
                .Where(word => word.Length > 0)
                .Distinct(StringComparer.Ordinal)]
            : [];

    /// <summary>A field as text, empty where it is missing or not text.</summary>
    public static string Field(JsonElement row, string name) =>
        row.ValueKind == JsonValueKind.Object && row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString()! : "";

    /// <summary>A field as a flag, false where it is missing.</summary>
    public static bool Flag(JsonElement row, string name) =>
        row.ValueKind == JsonValueKind.Object && row.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    /// <summary>Whether a read's field is an object, as a pause is where one is held.</summary>
    public static bool Held(JsonElement read, string name) =>
        read.ValueKind == JsonValueKind.Object && read.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object;

    /// <summary>A read's array field's elements; none where it is missing or not an array.</summary>
    public static IEnumerable<JsonElement> Rows(JsonElement read, string name) =>
        read.ValueKind == JsonValueKind.Object && read.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray()
            : [];
}
