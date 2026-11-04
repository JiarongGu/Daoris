using System.Text.Json.Nodes;

namespace Daoris.Desktop;

/// <summary>The readings of a slice's records that every row of <see cref="NeedsAPerson"/> shares: a field's text, a row's shell, a list of names.</summary>
internal static class AttentionJson
{
    /// <summary>A row of <paramref name="kind"/>, with <c>workspace</c> where it is judged from a record a window's scope filters.</summary>
    public static JsonObject Row(string kind, string? id, string? since, string? workspace = null)
    {
        var row = new JsonObject { ["kind"] = kind, ["id"] = id, ["since"] = since };
        if (workspace is not null) row["workspace"] = workspace;
        return row;
    }

    /// <summary>The workspace a record of the service's belongs to, as a window's scope reads it: <c>default</c> where it names none.</summary>
    public static string Scope(JsonObject record) => Text(record, "workspace") ?? "default";

    public static IEnumerable<JsonObject> Objects(JsonNode? node) => node is JsonArray items ? items.OfType<JsonObject>() : [];

    public static JsonArray Strings(IEnumerable<string?> values) => new([.. values.Select(value => (JsonNode?)value)]);

    /// <summary>Names said once each, in the order a person reads them, none empty.</summary>
    public static JsonArray Names(IEnumerable<string?> values) =>
        Strings(values.OfType<string>().Where(name => name.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase));

    public static string? Text(JsonObject? record, string field) =>
        record?[field] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    /// <summary>A field's text where it is a non-empty string: what the page's truthiness read.</summary>
    public static string? Truthy(JsonObject? record, string field) => Text(record, field) is { Length: > 0 } text ? text : null;

    /// <summary>A value as the page's template wrote it into an id: a string as itself, anything else as its JSON.</summary>
    public static string Part(JsonNode? node) => node switch
    {
        null => "undefined",
        JsonValue value when value.TryGetValue<string>(out var text) => text,
        _ => node.ToJsonString(),
    };
}
