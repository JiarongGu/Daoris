using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// One act's receipt: the round of a plugin's tool that answered it (its id, the session and the tool), the fact it names, its state
/// (<c>reached</c>, <c>waiting</c> or <c>failed</c>), its words, page and credit, and the role it waits on; journaled on a run, the step,
/// attempt and epoch it is taken in.
/// </summary>
public sealed record StepActRow(
    string Id, string Session, string Tool, string Fact, string State, string? Message, string? Url, string? By, string? WaitsOn, DateTimeOffset At)
{
    public string? Node { get; init; }

    public int? Attempt { get; init; }

    public int? Epoch { get; init; }
}

/// <summary>
/// A session's rounds of its plugin tools that answered a receipt, <c>sessions/&lt;session&gt;.step-acts.json</c>, written by the session's
/// tools server alone (<see cref="PluginToolCall"/>) and read by the run's pass, which journals them (<c>StepActs</c>). The file keeps its
/// newest rows; a row that does not read is not an act. The rows' file shape is the run's journal's too (<see cref="Read"/>, <see cref="Write"/>).
/// </summary>
public static class PluginActRounds
{
    public const string SessionSuffix = ".step-acts.json";

    /// <summary>The newest rows a file keeps.</summary>
    public const int Kept = 200;

    private static string SessionPath(string home, string session) => DaorisHome.Entry(home, HomeEntry.Sessions, session + SessionSuffix);

    /// <summary>Keeps a round's receipt for the session; a receipt with no fact or state, or a session that is no local one, keeps nothing.</summary>
    public static void Append(string home, string session, string id, string tool, JsonElement receipt, DateTimeOffset at)
    {
        if (!SessionEventText.IsLocalId(session) || receipt.ValueKind != JsonValueKind.Object) return;
        if (Text(receipt, "fact") is not { Length: > 0 } fact || Text(receipt, "state") is not { Length: > 0 } state) return;
        var row = new StepActRow(id, session, tool, fact, state, Text(receipt, "message"), Text(receipt, "url"), Text(receipt, "by"), Text(receipt, "waitsOn"), at);
        Write(SessionPath(home, session), [.. Rounds(home, session), row]);
    }

    /// <summary>The session's rounds that answered a receipt, oldest first.</summary>
    public static IReadOnlyList<StepActRow> Rounds(string home, string session) =>
        SessionEventText.IsLocalId(session) ? Read(SessionPath(home, session)) : [];

    /// <summary>The rows a file of this shape holds, oldest first; none where it is missing or does not read.</summary>
    public static IReadOnlyList<StepActRow> Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return [];
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            if (!document.RootElement.TryGetProperty("acts", out var acts) || acts.ValueKind != JsonValueKind.Array) return [];
            return [.. acts.EnumerateArray().Select(RowOf).OfType<StepActRow>()];
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static StepActRow? RowOf(JsonElement row)
    {
        if (row.ValueKind != JsonValueKind.Object || Text(row, "id") is not { } id || Text(row, "session") is not { } session || Text(row, "tool") is not { } tool
            || Text(row, "fact") is not { } fact || Text(row, "state") is not { } state || Moments.Read(Text(row, "at")) is not { } at)
        {
            return null;
        }

        return new StepActRow(id, session, tool, fact, state, Text(row, "message"), Text(row, "url"), Text(row, "by"), Text(row, "waitsOn"), at)
        {
            Node = Text(row, "node"), Attempt = Int(row, "attempt"), Epoch = Int(row, "epoch"),
        };
    }

    /// <summary>Writes the file whole, its newest rows kept.</summary>
    public static void Write(string path, IReadOnlyList<StepActRow> rows) => JsonFile.Write(path, writer =>
    {
        writer.WriteStartObject();
        writer.WriteStartArray("acts");
        foreach (var row in rows.TakeLast(Kept))
        {
            writer.WriteStartObject();
            writer.WriteString("id", row.Id);
            writer.WriteString("session", row.Session);
            writer.WriteString("tool", row.Tool);
            writer.WriteString("fact", row.Fact);
            writer.WriteString("state", row.State);
            foreach (var (name, value) in new[] { ("message", row.Message), ("url", row.Url), ("by", row.By), ("waitsOn", row.WaitsOn), ("node", row.Node) })
            {
                if (value is not null) writer.WriteString(name, value);
            }

            if (row.Attempt is { } attempt) writer.WriteNumber("attempt", attempt);
            if (row.Epoch is { } epoch) writer.WriteNumber("epoch", epoch);
            writer.WriteString("at", Moments.Write(row.At));
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    });
}
