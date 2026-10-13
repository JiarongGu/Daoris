using System.Text;
using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// The helpers' three files per ask under <c>&lt;home&gt;/helpers</c> (MIXAGENT2a-i, D163 point 2): read as the service writes
/// them and written as the service reads them. The file is the contract; the service's <c>HelperBox</c> and this share no code,
/// and each side's tests hold the same shapes.
/// </summary>
/// <remarks>The records are the Domain's (<see cref="HelperAsk"/>, <see cref="HelperState"/>, <see cref="HelperAnswer"/>; STRUCT3h).</remarks>
public sealed class HelperFiles(string home)
{
    public const string Folder = "helpers";

    // Indented, and a plus sign and a quote's neighbours left as written: the files are read by people and by the service's box.
    private static readonly JsonWriterOptions Options = new() { Indented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public string PathOf(string id, string kind) => Path.Combine(home, Folder, $"{id}.{kind}.json");

    /// <summary>Every ask on file, oldest first; one unreadable or of another shape is not an ask here.</summary>
    public IReadOnlyList<HelperAsk> Asks()
    {
        var folder = Path.Combine(home, Folder);
        if (!Directory.Exists(folder)) return [];
        var asks = new List<HelperAsk>();
        foreach (var path in Directory.EnumerateFiles(folder, "*.ask.json"))
        {
            if (Parse(path) is not { } root) continue;
            using (root)
            {
                var element = root.RootElement;
                var name = Path.GetFileName(path);
                var id = Text(element, "id") ?? name[..^".ask.json".Length];
                if (Text(element, "asker") is not { Length: > 0 } asker || Text(element, "question") is not { Length: > 0 } question) continue;
                var by = Text(element, "askedBy") == HelperAsk.ByPerson ? HelperAsk.ByPerson : HelperAsk.ByAgent;
                var at = DateTimeOffset.TryParse(Text(element, "at"), out var when) ? when : File.GetLastWriteTimeUtc(path);
                asks.Add(new HelperAsk(id, asker, question, Text(element, "agent") is { Length: > 0 } agent ? agent : null, by, at));
            }
        }

        return [.. asks.OrderBy(ask => ask.At).ThenBy(ask => ask.Id, StringComparer.Ordinal)];
    }

    /// <summary>One ask's state, or null where the driver wrote none or it is unreadable.</summary>
    public HelperState? ReadState(string id)
    {
        if (Parse(PathOf(id, "state")) is not { } root) return null;
        using (root)
        {
            var element = root.RootElement;
            if (Text(element, "state") is not { } state || !HelperStates.All.Contains(state)) return null;
            return new HelperState(
                Text(element, "id") ?? id, state, Text(element, "code"), Text(element, "helper"),
                DateTimeOffset.TryParse(Text(element, "at"), out var at) ? at : DateTimeOffset.UtcNow)
            {
                LastWords = element.TryGetProperty("lastWords", out var last) && last.ValueKind == JsonValueKind.True,
                Handed = Text(element, "handed"),
                Agent = Text(element, "agent"),
                Words = Text(element, "words"),
            };
        }
    }

    /// <summary>Keep one ask's state whole, atomically: the driver is its only writer.</summary>
    public void WriteState(HelperState state)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, Options))
        {
            writer.WriteStartObject();
            writer.WriteString("id", state.Id);
            writer.WriteString("state", state.State);
            if (state.Code is null) writer.WriteNull("code");
            else writer.WriteString("code", state.Code);
            if (state.Helper is null) writer.WriteNull("helper");
            else writer.WriteString("helper", state.Helper);
            writer.WriteString("at", state.At.ToString("O"));
            if (state.LastWords) writer.WriteBoolean("lastWords", true);
            if (state.Handed is not null) writer.WriteString("handed", state.Handed);
            if (state.Agent is not null) writer.WriteString("agent", state.Agent);
            if (state.Words is not null) writer.WriteString("words", state.Words);
            writer.WriteEndObject();
        }

        Directory.CreateDirectory(Path.Combine(home, Folder));
        AtomicFile.WriteText(PathOf(state.Id, "state"), Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n");
    }

    /// <summary>The helper's answer to an ask, or null where it said none or the file is unreadable.</summary>
    public HelperAnswer? ReadAnswer(string id)
    {
        if (Parse(PathOf(id, "answer")) is not { } root) return null;
        using (root)
        {
            var element = root.RootElement;
            return Text(element, "answer") is { Length: > 0 } answer && Text(element, "helper") is { } helper
                ? new HelperAnswer(Text(element, "id") ?? id, helper, answer, DateTimeOffset.TryParse(Text(element, "at"), out var at) ? at : DateTimeOffset.UtcNow)
                : null;
        }
    }

    /// <summary>
    /// The ask a person's terminal writes, in the shape <c>helper_ask</c> writes (MIXAGENT2a-ii): with <c>askedBy: person</c>,
    /// atomically. Returns its id.
    /// </summary>
    public string WriteAsk(string asker, string question, string? agent, DateTimeOffset at)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, Options))
        {
            writer.WriteStartObject();
            writer.WriteString("id", id);
            writer.WriteString("asker", asker);
            writer.WriteString("question", question);
            if (agent is null) writer.WriteNull("agent");
            else writer.WriteString("agent", agent);
            writer.WriteString("askedBy", HelperAsk.ByPerson);
            writer.WriteString("at", at.ToString("O"));
            writer.WriteEndObject();
        }

        Directory.CreateDirectory(Path.Combine(home, Folder));
        AtomicFile.WriteText(PathOf(id, "ask"), Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n");
        return id;
    }

    private static JsonDocument? Parse(string path)
    {
        try
        {
            return File.Exists(path) ? JsonDocument.Parse(File.ReadAllText(path)) : null;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
