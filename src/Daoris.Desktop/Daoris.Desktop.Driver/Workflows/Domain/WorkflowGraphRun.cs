using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A graph node's option read as a step's field (FABRIC-CUTOVER1e): a node keeps each option as its JSON text, and a step reads text, a
/// switch or a list of names, so the person's part (<see cref="Involvement.StepsOf(NodeGraph)"/>) and a graph run's drawing read an option
/// as its step would.
/// </summary>
public static class NodeOptions
{
    /// <summary>An option's text read as a field: text, a switch, a list of names, null; anything else (a number, an object) its JSON text.</summary>
    public static object? Value(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            var value = document.RootElement;
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Array when value.EnumerateArray().All(each => each.ValueKind == JsonValueKind.String) =>
                    (IReadOnlyList<string>)[.. value.EnumerateArray().Select(each => each.GetString()!)],
                _ => value.GetRawText(),
            };
        }
        catch (JsonException)
        {
            return text;
        }
    }

    /// <summary>A node's option as text, or null where it names none or names no text.</summary>
    public static string? Text(NodeDeclaration node, string name) =>
        node.Options.TryGetValue(name, out var text) ? Value(text) as string : null;
}
