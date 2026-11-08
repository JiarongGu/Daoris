using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A word waiting on a run's record as the file spells it. The person's names no author; a supervisor's names its role, its decision and
/// the attempt it was released on, and the person's that took one's place names the decision it replaced.
/// </summary>
internal static class NodeWordFiles
{
    public static void Write(Utf8JsonWriter writer, NodeWord word)
    {
        writer.WriteStartObject();
        writer.WriteString("node", word.Node);
        writer.WriteString("act", word.Act);
        if (word.Words is { } words) writer.WriteString("words", words);
        writer.WriteString("at", Moments.Write(word.At));
        if (word.By is { } by)
        {
            writer.WriteStartObject("by");
            writer.WriteString("role", by.Role);
            writer.WriteString("decision", by.Decision);
            writer.WriteEndObject();
        }

        if (word.Attempt is { } attempt) writer.WriteNumber("attempt", attempt);
        if (word.Supersedes is { } supersedes) writer.WriteString("supersedes", supersedes);
        writer.WriteEndObject();
    }

    /// <summary>The word <paramref name="each"/> is, or null where it is not whole or its act is not one this build applies.</summary>
    public static NodeWord? Read(JsonElement each)
    {
        if (each.ValueKind != JsonValueKind.Object || Text(each, "node") is not { } node || Text(each, "act") is not { } act
            || !NodeWordActs.All.Contains(act, StringComparer.Ordinal) || Moments.Read(each, "at") is not { } at
            || (each.TryGetProperty("words", out var said) && said.ValueKind != JsonValueKind.String)
            || (each.TryGetProperty("supersedes", out var replaced) && replaced.ValueKind != JsonValueKind.String))
        {
            return null;
        }

        NodeWordBy? by = null;
        if (each.TryGetProperty("by", out var author))
        {
            if (author.ValueKind != JsonValueKind.Object || Text(author, "role") is not { } role || Text(author, "decision") is not { } decision) return null;
            by = new NodeWordBy(role, decision);
        }

        int? attempt = null;
        if (each.TryGetProperty("attempt", out var number))
        {
            if (number.ValueKind != JsonValueKind.Number || !number.TryGetInt32(out var value)) return null;
            attempt = value;
        }

        return new NodeWord(node, act, Text(each, "words"), at) { By = by, Attempt = attempt, Supersedes = Text(each, "supersedes") };
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
