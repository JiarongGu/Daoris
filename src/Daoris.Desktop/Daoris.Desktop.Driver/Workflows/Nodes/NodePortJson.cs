using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A port as a node record writes and reads it: its reference, and a value's producer, <c>onto</c> and stop where it carries them.</summary>
internal static class NodePortJson
{
    public static void Write(Utf8JsonWriter writer, NodePort port)
    {
        writer.WriteStartObject();
        writer.WriteString("schema", port.Schema);
        writer.WriteString("ref", port.Ref);
        if (port.Revision is { } revision) writer.WriteString("revision", revision);
        if (port.Acceptance is { } acceptance) writer.WriteString("acceptance", acceptance);
        if (port.Producer is { } producer)
        {
            writer.WriteStartObject("producer");
            writer.WriteString("node", producer.Node);
            writer.WriteNumber("attempt", producer.Attempt);
            writer.WriteEndObject();
        }

        if (port.Onto is { } onto) writer.WriteString("onto", onto);
        if (port.Stop is { } stop)
        {
            writer.WriteStartObject("stop");
            writer.WriteString("sort", stop.Sort);
            if (stop.PatchId is { } patch) writer.WriteString("patchId", patch);
            writer.WriteStartArray("paths");
            foreach (var path in stop.Paths) writer.WriteStringValue(path);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>The port, or null where it does not read whole: a producer, a stop or a field of the wrong shape refuses the record.</summary>
    public static NodePort? Read(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object || Text(element, "schema") is not { } schema || Text(element, "ref") is not { } reference) return null;
        NodeProducer? producer = null;
        if (element.TryGetProperty("producer", out var made))
        {
            if (made.ValueKind != JsonValueKind.Object || Text(made, "node") is not { } node
                || !made.TryGetProperty("attempt", out var number) || number.ValueKind != JsonValueKind.Number || !number.TryGetInt32(out var attempt))
            {
                return null;
            }

            producer = new NodeProducer(node, attempt);
        }

        NodeStop? stop = null;
        if (element.TryGetProperty("stop", out var stopped))
        {
            if (stopped.ValueKind != JsonValueKind.Object || Text(stopped, "sort") is not { } sort
                || !stopped.TryGetProperty("paths", out var paths) || paths.ValueKind != JsonValueKind.Array
                || paths.EnumerateArray().Any(path => path.ValueKind != JsonValueKind.String))
            {
                return null;
            }

            stop = new NodeStop(sort, Text(stopped, "patchId"), [.. paths.EnumerateArray().Select(path => path.GetString()!)]);
        }

        return new NodePort(schema, reference, Text(element, "revision"), Text(element, "acceptance"))
        {
            Producer = producer,
            Onto = Text(element, "onto"),
            Stop = stop,
        };
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
