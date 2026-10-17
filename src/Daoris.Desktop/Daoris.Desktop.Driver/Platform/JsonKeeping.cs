using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// An edit written over the file it was read from (FABRIC-GRAPH1's review, finding 1; FIX-LOG 2026-10-17). A writer that rebuilt
/// <c>driver.json</c> from the fields it knows deleted every setting it did not, so an edit at Setup or from Ask Daoris erased what a
/// newer build, the terminal or the person had put there. Three texts make the edit instead: the file as it stands, the file as its reader
/// understood it written again, and the edited choices written. What the edit did not change is written as the file has it; what it
/// changed is written as the edit has it, an object the three hold alike merged key by key, so a rule's own unknown keys stay; a key
/// the edit adds goes after the one before it in the writer's order. The CLI's twin is <c>jsonkeep.ts</c> (<c>keepEdits</c>), and
/// both are held to <c>test/fixtures/driver-config-keeps.json</c>.
/// </summary>
public static class JsonKeeping
{
    private static readonly JsonDocument Nothing = JsonDocument.Parse("{}");

    /// <summary>
    /// <paramref name="edited"/> written over <paramref name="original"/>, where <paramref name="read"/> is the file as its reader understood
    /// it when it read it, written by the same writer as <paramref name="edited"/>. No edit is <paramref name="original"/>'s own bytes.
    /// <paramref name="original"/> is the file as it stands when the edit is written, which may be another writer's since the read (the
    /// re-check's finding 1): what that writer changed and this edit did not stays as it is. Its lines end as its first line does (finding 3:
    /// an edit wrote LF over a file in CRLF).
    /// </summary>
    public static string Kept(string original, string read, string edited)
    {
        if (read == edited) return original;
        using var file = JsonDocument.Parse(original);
        using var understood = JsonDocument.Parse(read);
        using var now = JsonDocument.Parse(edited);
        var text = file.RootElement.ValueKind != JsonValueKind.Object
            ? edited
            : JsonFile.Serialize(writer => Write(writer, Plan(file.RootElement, understood.RootElement, now.RootElement, top: true)));
        var end = original.IndexOf('\n');
        return end > 0 && original[end - 1] == '\r' ? text.Replace("\n", "\r\n", StringComparison.Ordinal) : text;
    }

    /// <summary>
    /// The first key written twice in one object of <paramref name="text"/>, a JSON text, in the order the second is written; or null (the
    /// re-check's finding 3; the CLI's <c>twice</c>). An edit cannot tell which of the two it changes: this writer kept both and edited
    /// each, the CLI's kept the last, so neither edits a file holding one.
    /// </summary>
    public static string? Twice(string text)
    {
        using var document = JsonDocument.Parse(text);
        return Twice(document.RootElement);
    }

    private static string? Twice(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (Twice(item) is { } inner) return inner;
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!seen.Add(property.Name)) return property.Name;
                if (Twice(property.Value) is { } inner) return inner;
            }
        }

        return null;
    }

    /// <summary>
    /// <paramref name="text"/> read as JavaScript's <c>JSON.parse</c> reads it, where a key is written twice in one object: once, in its
    /// first place, with its last value (the re-check's finding 3: the work cycles' kinds read the first here and the last in the CLI);
    /// else <paramref name="text"/> itself.
    /// </summary>
    public static string Once(string text)
    {
        using var document = JsonDocument.Parse(text);
        return Twice(document.RootElement) is null ? text : JsonFile.Serialize(writer => WriteOnce(writer, document.RootElement));
    }

    private static void WriteOnce(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray()) WriteOnce(writer, item);
            writer.WriteEndArray();
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            var last = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (var property in element.EnumerateObject())
            {
                if (!last.ContainsKey(property.Name)) order.Add(property.Name);
                last[property.Name] = property.Value;
            }

            writer.WriteStartObject();
            foreach (var name in order)
            {
                writer.WritePropertyName(name);
                WriteOnce(writer, last[name]);
            }

            writer.WriteEndObject();
        }
        else element.WriteTo(writer);
    }

    private static void Write(Utf8JsonWriter writer, List<(string Name, Action<Utf8JsonWriter> Value)> plan)
    {
        writer.WriteStartObject();
        foreach (var (name, value) in plan)
        {
            writer.WritePropertyName(name);
            value(writer);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// What one object becomes, key by key in the file's order: a key neither its reader nor the edit holds is the file's, kept; one the
    /// edit took goes, except that a map emptied at the top keeps the entries its reader never read; one its reader did not read and the
    /// edit set is the edit's; one the edit left as read is the file's, as written; an object the edit changed is merged; else the edit's.
    /// </summary>
    private static List<(string Name, Action<Utf8JsonWriter> Value)> Plan(JsonElement file, JsonElement read, JsonElement edited, bool top)
    {
        var plan = new List<(string Name, Action<Utf8JsonWriter> Value)>();
        foreach (var property in file.EnumerateObject())
        {
            var kept = property.Value;
            var wasRead = read.TryGetProperty(property.Name, out var was);
            var isEdited = edited.TryGetProperty(property.Name, out var now);
            if (!wasRead && !isEdited) plan.Add((property.Name, kept.WriteTo));
            else if (!isEdited)
            {
                if (top && kept.ValueKind == JsonValueKind.Object && was.ValueKind == JsonValueKind.Object)
                {
                    var unread = Plan(kept, was, Nothing.RootElement, top: false);
                    if (unread.Count > 0) plan.Add((property.Name, writer => Write(writer, unread)));
                }
            }
            else if (!wasRead) plan.Add((property.Name, now.WriteTo));
            else if (was.GetRawText() == now.GetRawText()) plan.Add((property.Name, kept.WriteTo));
            else if (kept.ValueKind == JsonValueKind.Object && was.ValueKind == JsonValueKind.Object && now.ValueKind == JsonValueKind.Object)
            {
                var merged = Plan(kept, was, now, top: false);
                plan.Add((property.Name, writer => Write(writer, merged)));
            }
            else plan.Add((property.Name, now.WriteTo));
        }

        // A key the edit adds goes after the edit's key before it, or first; a default the file never wrote stays unwritten.
        var at = 0;
        foreach (var property in edited.EnumerateObject())
        {
            var index = plan.FindIndex(each => each.Name == property.Name);
            if (index >= 0)
            {
                at = index + 1;
                continue;
            }

            if (file.TryGetProperty(property.Name, out _)) continue;
            if (read.TryGetProperty(property.Name, out var was) && was.GetRawText() == property.Value.GetRawText()) continue;
            plan.Insert(at++, (property.Name, property.Value.WriteTo));
        }

        return plan;
    }
}
