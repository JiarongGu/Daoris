using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// What this machine keeps beside a session's transcript for its words to go on (MSG1b, MSG1g, ANSWER1a), each
/// <c>&lt;home&gt;/sessions/&lt;session&gt;.&lt;kind&gt;.json</c>: a mark (<see cref="GoOnMarks"/>), the person's choice of a new
/// session (<see cref="NewSessionChoices"/>) and the harness's conversation id (<see cref="HarnessConversations"/>), read
/// without trusting their shape and written whole, atomically, as BOM-less UTF-8 with LF.
/// </summary>
/// <remarks>
/// STRUCT3h, D166's note: the stores decide what is kept and whether a failed write is lost; this
/// reads and writes it. A file that does not read is nothing kept; a write that fails throws.
/// </remarks>
internal static class GoOnFiles
{
    /// <summary>A mark written to <paramref name="path"/>, its folder made where it is not.</summary>
    public static void WriteMark(string path, IReadOnlyList<string> said, string why, DateTimeOffset at)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("said");
            foreach (var id in said) writer.WriteStringValue(id);
            writer.WriteEndArray();
            writer.WriteString("why", why);
            writer.WriteString("at", at.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        }

        // LF on every platform, as every file under the home is written.
        AtomicFile.WriteText(path, Encoding.UTF8.GetString(buffer.ToArray()).Replace("\r\n", "\n") + "\n");
    }

    /// <summary>The mark at <paramref name="path"/>, or null for none or a file that does not read.</summary>
    public static GoOnMark? ReadMark(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("said", out var said) || said.ValueKind != JsonValueKind.Array
                || !root.TryGetProperty("why", out var why) || why.ValueKind != JsonValueKind.String || why.GetString() is not { Length: > 0 } code)
            {
                return null;
            }

            var at = root.TryGetProperty("at", out var when) && when.ValueKind == JsonValueKind.String
                     && DateTimeOffset.TryParse(when.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : DateTimeOffset.MinValue;
            return new GoOnMark(
                [.. said.EnumerateArray().Where(id => id.ValueKind == JsonValueKind.String).Select(id => id.GetString()!)], code, at);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>A choice written to <paramref name="path"/>, its folder made where it is not.</summary>
    public static void WriteChoice(string path, IReadOnlyList<string> said, DateTimeOffset at)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("said");
            foreach (var id in said) writer.WriteStringValue(id);
            writer.WriteEndArray();
            writer.WriteString("at", at.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        }

        // LF on every platform, as every file under the home is written.
        AtomicFile.WriteText(path, Encoding.UTF8.GetString(buffer.ToArray()).Replace("\r\n", "\n") + "\n");
    }

    /// <summary>The choice at <paramref name="path"/>, or null for none or a file that does not read.</summary>
    public static NewSessionChoice? ReadChoice(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("said", out var said) || said.ValueKind != JsonValueKind.Array
                || !root.TryGetProperty("at", out var when) || when.ValueKind != JsonValueKind.String
                || !DateTimeOffset.TryParse(when.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var at))
            {
                return null;
            }

            return new NewSessionChoice([.. said.EnumerateArray().Where(id => id.ValueKind == JsonValueKind.String).Select(id => id.GetString()!)], at);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>A conversation id written to <paramref name="path"/>, its folder made where it is not.</summary>
    public static void WriteConversation(string path, string adapter, string conversation)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteString("adapter", adapter);
            writer.WriteString("conversation", conversation);
            writer.WriteEndObject();
        }

        // LF on every platform, as every file under the home is written: the writer's own line end is the platform's.
        var text = Encoding.UTF8.GetString(buffer.ToArray()).Replace("\r\n", "\n") + "\n";
        AtomicFile.WriteText(path, text);
    }

    /// <summary>The conversation id at <paramref name="path"/>, or null for none or a file that does not read.</summary>
    public static HarnessConversation? ReadConversation(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return null;
            return Json.Text(root, "adapter")?.Trim() is { Length: > 0 } adapter
                   && Json.Text(root, "conversation")?.Trim() is { Length: > 0 } conversation
                ? new HarnessConversation(adapter, conversation)
                : null;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The file at <paramref name="path"/> gone; nothing to take, or a file that will not go, is no failure.</summary>
    public static void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Left behind: what it names is judged again where it is read.
        }
    }
}
