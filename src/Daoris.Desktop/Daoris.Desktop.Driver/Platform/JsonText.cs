using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The string a JSON field or node holds, declared where the clock is so that <see cref="Moments"/> names nothing of Store (FOUNDATION1 mod-ii):
/// <see cref="Json.Text(JsonElement, string)"/> and <see cref="Json.Text(JsonNode?)"/> are these readers, spelled where every other reader of JSON finds them.
/// </summary>
/// <remarks>Absent (null) for a value of the wrong kind, and for an element that is not an object at all: <c>TryGetProperty</c> throws on one.</remarks>
public static class JsonText
{
    /// <summary>The string a field holds, empty included; null when missing, not a string, or the element not an object.</summary>
    public static string? Of(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>The string a node holds; null when it is missing or not a string.</summary>
    public static string? Of(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
}
