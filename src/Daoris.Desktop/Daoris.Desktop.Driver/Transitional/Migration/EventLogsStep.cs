using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// The sessions' event logs in the shape this build reads: the receipt <c>answered</c> an older build wrote for the turn that took a
/// word is the <c>ended</c> receipt; a <c>chat.told-routing</c> note, which the standing block replaced, is taken out; and the
/// <c>map</c>, <c>look</c>, <c>indexes</c> and <c>standing</c> sections of what a session is handed are the one standing block they became,
/// their characters summed. Only a log that holds one of them is read whole and written again.
/// </summary>
public sealed class EventLogsStep : IHomeMigrationStep
{
    private static readonly string[] Sections = ["map", "look", "indexes", "standing"];
    private static readonly string[] Signs = ["\"answered\"", "chat.told-routing", .. Sections.Select(name => $"\"name\":\"{name}\"")];
    private static readonly JsonSerializerOptions Spelled = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public string Name => "event-logs";

    public void Run(HomeMigrationRun run)
    {
        var folder = DaorisHome.Entry(run.Home, HomeEntry.Sessions);
        if (!Directory.Exists(folder)) return;
        foreach (var path in Directory.EnumerateFiles(folder, "*.events.jsonl").Order(StringComparer.Ordinal))
        {
            var whole = AtomicFile.ReadText(path);
            if (!Signs.Any(sign => whole.Contains(sign, StringComparison.Ordinal))) continue;
            var text = new StringBuilder();
            var changed = false;
            using var lines = new StringReader(whole);
            for (var line = lines.ReadLine(); line is not null; line = lines.ReadLine())
            {
                var next = Signs.Any(sign => line.Contains(sign, StringComparison.Ordinal)) ? Converted(line) : line;
                changed |= !ReferenceEquals(next, line);
                if (next is not null) text.Append(next).Append('\n');
            }

            if (changed) run.Write(path, text.ToString());
        }
    }

    /// <summary>The line as this build writes it, the same line where nothing in it is old, or null for a note that is taken out.</summary>
    internal static string? Converted(string line)
    {
        JsonObject root;
        try
        {
            if (JsonNode.Parse(line) is not JsonObject read) return line;
            root = read;
        }
        catch (JsonException)
        {
            return line;
        }

        if (root["parts"] is JsonArray parts && parts.OfType<JsonObject>().Any(part => HomeMigrationRun.Text(part["code"]) == "chat.told-routing")) return null;
        var changed = false;
        if (HomeMigrationRun.Text(root["kind"]) == SessionEventKind.Receipt && HomeMigrationRun.Text(root["receipt"]) == "answered")
        {
            root["receipt"] = Receipts.Ended;
            changed = true;
        }

        if (root["account"]?["sections"] is JsonArray sections && Block(sections)) changed = true;
        return changed ? root.ToJsonString(Spelled) : line;
    }

    /// <summary>The four sections made the one block where the first stood, a section not handed left out; whether any is there.</summary>
    private static bool Block(JsonArray sections)
    {
        var old = sections.OfType<JsonObject>().Where(section => HomeMigrationRun.Text(section["name"]) is { } name && Sections.Contains(name, StringComparer.Ordinal)).ToList();
        if (old.Count == 0) return false;
        var handed = old.Where(section => section["none"] is null).ToList();
        var at = sections.IndexOf(old[0]);
        foreach (var section in old) sections.Remove(section);
        if (handed.Count == 0) return true;
        var block = new JsonObject
        {
            ["name"] = HandedSections.Block,
            ["source"] = handed[0]["source"]?.DeepClone(),
            ["said"] = "the standing block",
            ["chars"] = handed.Sum(section => section["chars"] is JsonValue chars && chars.TryGetValue<int>(out var n) ? n : 0),
        };
        sections.Insert(Math.Min(at, sections.Count), block);
        return true;
    }
}
