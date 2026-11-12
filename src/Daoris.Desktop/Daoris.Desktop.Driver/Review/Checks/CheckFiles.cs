using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// The broker's files per check under <c>&lt;home&gt;/checks</c>: <c>&lt;id&gt;.ask.json</c> (written by the service's connector or by
/// Daoris's reading of a hand-back), <c>&lt;id&gt;.state.json</c> and <c>&lt;id&gt;.log</c> (the driver's alone). The file is the contract; the
/// service's <c>CheckBox</c> shares no code with this.
/// </summary>
public sealed class CheckFiles(string home)
{
    private string Folder => DaorisHome.Entry(home, HomeEntry.Checks);

    public string PathOf(string id, string kind) => DaorisHome.Entry(home, HomeEntry.Checks, $"{id}.{kind}");

    /// <summary>The log a run writes to.</summary>
    public string LogOf(string id) => PathOf(id, "log");

    /// <summary>Every ask on file, oldest first; one unreadable or of another shape is not an ask here.</summary>
    public IReadOnlyList<CheckAsk> Asks()
    {
        if (!Directory.Exists(Folder)) return [];
        var asks = new List<CheckAsk>();
        foreach (var path in Directory.EnumerateFiles(Folder, "*.ask.json"))
        {
            if (Parse(path) is not { } root) continue;
            using (root)
            {
                var element = root.RootElement;
                var id = Text(element, "id") ?? Path.GetFileName(path)[..^".ask.json".Length];
                if (Text(element, "session") is not { Length: > 0 } session) continue;
                IReadOnlyList<string>? command = element.TryGetProperty("command", out var words) && words.ValueKind == JsonValueKind.Array
                    ? [.. words.EnumerateArray().Where(word => word.ValueKind == JsonValueKind.String).Select(word => word.GetString()!)]
                    : null;
                asks.Add(new CheckAsk(
                    id, session, Text(element, "gate") is { Length: > 0 } gate ? gate : null, command is { Count: > 0 } ? command : null,
                    Text(element, "by") == CheckAsk.ByDaoris ? CheckAsk.ByDaoris : CheckAsk.ByAgent,
                    Moments.Read(element, "at") ?? File.GetLastWriteTimeUtc(path)));
            }
        }

        return [.. asks.OrderBy(ask => ask.At).ThenBy(ask => ask.Id, StringComparer.Ordinal)];
    }

    public void WriteAsk(CheckAsk ask) => JsonFile.Write(PathOf(ask.Id, "ask.json"), writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("id", ask.Id);
        writer.WriteString("session", ask.Session);
        if (ask.Gate is not null) writer.WriteString("gate", ask.Gate);
        if (ask.Command is { } command)
        {
            writer.WriteStartArray("command");
            foreach (var word in command) writer.WriteStringValue(word);
            writer.WriteEndArray();
        }

        writer.WriteString("by", ask.By);
        writer.WriteString("at", Moments.Write(ask.At));
        writer.WriteEndObject();
    });

    /// <summary>One check's state, or null where the driver wrote none or it is unreadable.</summary>
    public CheckState? ReadState(string id)
    {
        if (Parse(PathOf(id, "state.json")) is not { } root) return null;
        using (root)
        {
            var element = root.RootElement;
            if (Text(element, "state") is not { } state) return null;
            return new CheckState(Text(element, "id") ?? id, state, Moments.Read(element, "at") ?? DateTimeOffset.UtcNow)
            {
                Exit = element.TryGetProperty("exit", out var exit) && exit.ValueKind == JsonValueKind.Number && exit.TryGetInt32(out var code) ? code : null,
                Tail = Text(element, "tail"),
                Tip = Text(element, "tip"),
                Handed = Text(element, "handed"),
            };
        }
    }

    public void WriteState(CheckState state) => JsonFile.Write(PathOf(state.Id, "state.json"), writer =>
    {
        writer.WriteStartObject();
        writer.WriteString("id", state.Id);
        writer.WriteString("state", state.State);
        writer.WriteString("at", Moments.Write(state.At));
        if (state.Exit is { } exit) writer.WriteNumber("exit", exit);
        if (state.Tail is not null) writer.WriteString("tail", state.Tail);
        if (state.Tip is not null) writer.WriteString("tip", state.Tip);
        if (state.Handed is not null) writer.WriteString("handed", state.Handed);
        writer.WriteEndObject();
    });

    private static JsonDocument? Parse(string path)
    {
        try
        {
            return File.Exists(path) ? JsonDocument.Parse(AtomicFile.ReadText(path)) : null;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
