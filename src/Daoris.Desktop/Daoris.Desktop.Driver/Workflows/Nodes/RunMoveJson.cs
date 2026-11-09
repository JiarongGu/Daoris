using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A run's own moves as its record writes and reads them, under <c>moves</c>, written only where the run made one.</summary>
internal static class RunMoveJson
{
    public static void Write(Utf8JsonWriter writer, IReadOnlyList<RunMove> moves)
    {
        if (moves.Count == 0) return;
        writer.WriteStartArray("moves");
        foreach (var move in moves)
        {
            writer.WriteStartObject();
            writer.WriteString("ref", move.Ref);
            writer.WriteString("from", move.From);
            writer.WriteString("to", move.To);
            writer.WriteString("node", move.Node);
            writer.WriteNumber("attempt", move.Attempt);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    /// <summary>The record's moves, none where it names none; null where one does not read whole, which refuses the record.</summary>
    public static IReadOnlyList<RunMove>? Read(JsonElement root)
    {
        if (!root.TryGetProperty("moves", out var kept)) return [];
        if (kept.ValueKind != JsonValueKind.Array) return null;
        var moves = new List<RunMove>();
        foreach (var each in kept.EnumerateArray())
        {
            if (each.ValueKind != JsonValueKind.Object || Text(each, "ref") is not { } reference || Text(each, "from") is not { } from
                || Text(each, "to") is not { } to || Text(each, "node") is not { } node
                || !each.TryGetProperty("attempt", out var number) || number.ValueKind != JsonValueKind.Number || !number.TryGetInt32(out var attempt))
            {
                return null;
            }

            moves.Add(new RunMove(reference, from, to, node, attempt));
        }

        return moves;
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
