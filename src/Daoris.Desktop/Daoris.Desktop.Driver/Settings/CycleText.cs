using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// A cycle as <c>driver.json</c> keeps it (FABRIC-GRAPH1; FOUNDATION1 mod-iii): a graph's JSON, compact, every value as written. The
/// settings' own reading of the text of a cycle, which the graph homes' <see cref="NodeGraphHomes.Compact(string)"/> answers through.
/// </summary>
public static class CycleText
{
    private static readonly JsonWriterOptions CompactWriting = new() { Indented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>
    /// <paramref name="json"/> as the config keeps it: compact, its keys in the order written, its text unescaped where JSON allows, so the
    /// file says what the person wrote. Throws <see cref="JsonException"/> where it is not JSON.
    /// </summary>
    public static string Compact(string json)
    {
        using var document = JsonDocument.Parse(json);
        return Compact(document.RootElement);
    }

    /// <summary>A cycle the config keeps as JSON text, written as JSON in the file's own layout.</summary>
    public static void Write(Utf8JsonWriter writer, string name, string cycle)
    {
        using var document = JsonDocument.Parse(cycle);
        writer.WritePropertyName(name);
        document.RootElement.WriteTo(writer);
    }

    /// <summary><paramref name="element"/>'s JSON, compact.</summary>
    public static string Compact(JsonElement element)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, CompactWriting)) element.WriteTo(writer);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }
}
