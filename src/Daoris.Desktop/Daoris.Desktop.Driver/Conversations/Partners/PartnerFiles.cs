using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// A piece's box under <c>&lt;home&gt;/data/partners</c> (COAGENT1b, D199 point 2; the co-agent design §7.1): <c>&lt;id&gt;.ask.json</c>
/// written once, <c>&lt;id&gt;.state.json</c> the driver's alone, <c>&lt;id&gt;.answer.json</c>, <c>&lt;id&gt;.facts.json</c> and
/// <c>&lt;id&gt;.delivery.json</c>. Each is written whole, to a temporary name and renamed (<see cref="JsonFile"/>). The service's
/// <c>PartnerBox</c>, when it comes, shares no code with this and holds the same shapes (<c>twins.md</c>).
/// </summary>
public sealed class PartnerFiles(string home)
{
    public string PathOf(string id, string kind) => DaorisHome.Entry(home, HomeEntry.Partners, $"{id}.{kind}.json");

    /// <summary>Whether a box file exists but cannot be read, which a restart reads as <see cref="PartnerStates.Uncertain"/>.</summary>
    public bool Unreadable(string id, string kind)
    {
        var path = PathOf(id, kind);
        if (!File.Exists(path)) return false;
        using var root = Parse(path);
        return root is null;
    }

    /// <summary>Every ask on file, oldest first; one unreadable or of another shape is not an ask here.</summary>
    public IReadOnlyList<PartnerAsk> Asks()
    {
        var folder = DaorisHome.Entry(home, HomeEntry.Partners);
        if (!Directory.Exists(folder)) return [];
        var asks = new List<PartnerAsk>();
        foreach (var path in Directory.EnumerateFiles(folder, "*.ask.json"))
        {
            if (ReadAsk(Path.GetFileName(path)[..^".ask.json".Length]) is { } ask) asks.Add(ask);
        }

        return [.. asks.OrderBy(ask => ask.At).ThenBy(ask => ask.Id, StringComparer.Ordinal)];
    }

    /// <summary>
    /// Every piece on file with its lead and where it stands, as the common admission counts them (<see cref="PartnerAdmission"/>): an
    /// ask with no state is <see cref="PartnerStates.Asked"/>, one whose state cannot be read <see cref="PartnerStates.Uncertain"/>, which
    /// stays open until the person's act.
    /// </summary>
    public IReadOnlyList<PieceReservation> Reservations() =>
        [.. Asks().Select(ask => new PieceReservation(
            ask.Id, ask.Asker,
            ReadState(ask.Id)?.State ?? (Unreadable(ask.Id, "state") ? PartnerStates.Uncertain : PartnerStates.Asked)))];

    public PartnerAsk? ReadAsk(string id)
    {
        if (Parse(PathOf(id, "ask")) is not { } root) return null;
        using (root)
        {
            var element = root.RootElement;
            if (Text(element, "asker") is not { Length: > 0 } asker || Text(element, "piece") is not { Length: > 0 } piece
                || Text(element, "base") is not { Length: > 0 } @base)
            {
                return null;
            }

            return new PartnerAsk(
                Text(element, "id") ?? id, asker, piece, Strings(element, "paths"), Text(element, "agent") is { Length: > 0 } agent ? agent : null,
                @base, Strings(element, "uncommitted"), Text(element, "askedBy") == PartnerAsk.ByAgent ? PartnerAsk.ByAgent : PartnerAsk.ByPerson,
                Moments.Read(element, "at") ?? DateTimeOffset.UtcNow);
        }
    }

    /// <summary>Write an ask once: an id already on file is never written again (the ask is never changed).</summary>
    /// <returns>False where the id was already on file.</returns>
    public bool WriteAsk(PartnerAsk ask)
    {
        if (File.Exists(PathOf(ask.Id, "ask"))) return false;
        JsonFile.Write(PathOf(ask.Id, "ask"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("id", ask.Id);
            writer.WriteString("asker", ask.Asker);
            writer.WriteString("piece", ask.Piece);
            WriteStrings(writer, "paths", ask.Paths);
            if (ask.Agent is null) writer.WriteNull("agent");
            else writer.WriteString("agent", ask.Agent);
            writer.WriteString("base", ask.Base);
            WriteStrings(writer, "uncommitted", ask.Uncommitted);
            writer.WriteString("askedBy", ask.AskedBy);
            writer.WriteString("at", Moments.Write(ask.At));
            writer.WriteEndObject();
        });
        return true;
    }

    /// <summary>One piece's state, or null where none was written or it is unreadable (<see cref="Unreadable"/> tells which).</summary>
    public PartnerState? ReadState(string id)
    {
        if (Parse(PathOf(id, "state")) is not { } root) return null;
        using (root)
        {
            var element = root.RootElement;
            if (Text(element, "state") is not { } state || !PartnerStates.All.Contains(state)) return null;
            return new PartnerState(Text(element, "id") ?? id, state, Text(element, "code"), Moments.Read(element, "at") ?? DateTimeOffset.UtcNow)
            {
                Copy = Text(element, "copy"),
                Partner = Text(element, "partner"),
                Agent = Text(element, "agent"),
                Account = Text(element, "account"),
                Job = Text(element, "job"),
                End = Text(element, "end"),
                Kept = Moments.Read(element, "kept"),
                Commit = Text(element, "commit"),
                CopyRemoved = element.TryGetProperty("copyRemoved", out var removed) && removed.ValueKind == JsonValueKind.True,
                TakenBy = Text(element, "takenBy"),
            };
        }
    }

    /// <summary>Keep one piece's state whole, atomically: the driver is its only writer.</summary>
    public void WriteState(PartnerState state)
    {
        JsonFile.Write(PathOf(state.Id, "state"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("id", state.Id);
            writer.WriteString("state", state.State);
            if (state.Code is null) writer.WriteNull("code");
            else writer.WriteString("code", state.Code);
            Optional(writer, "copy", state.Copy);
            Optional(writer, "partner", state.Partner);
            Optional(writer, "agent", state.Agent);
            Optional(writer, "account", state.Account);
            Optional(writer, "job", state.Job);
            Optional(writer, "end", state.End);
            if (state.Kept is { } kept) writer.WriteString("kept", Moments.Write(kept));
            Optional(writer, "commit", state.Commit);
            if (state.CopyRemoved) writer.WriteBoolean("copyRemoved", true);
            Optional(writer, "takenBy", state.TakenBy);
            writer.WriteString("at", Moments.Write(state.At));
            writer.WriteEndObject();
        });
    }

    /// <summary>The partner's answer, or null where it gave none or the file is unreadable.</summary>
    public PartnerAnswer? ReadAnswer(string id)
    {
        if (Parse(PathOf(id, "answer")) is not { } root) return null;
        using (root)
        {
            var element = root.RootElement;
            return Text(element, "summary") is { Length: > 0 } summary
                ? new PartnerAnswer(Text(element, "id") ?? id, summary, Text(element, "left"), Moments.Read(element, "at") ?? DateTimeOffset.UtcNow)
                {
                    Partner = Text(element, "partner"),
                    LastWords = element.TryGetProperty("lastWords", out var last) && last.ValueKind == JsonValueKind.True,
                }
                : null;
        }
    }

    /// <summary>
    /// Keep the partner's last words as its answer, where it gave none through <c>partner_answer</c> (the service's <c>PartnerBox</c> writes
    /// that one). Never over an answer on file: the tool's answer stands.
    /// </summary>
    public bool WriteAnswer(PartnerAnswer answer)
    {
        if (File.Exists(PathOf(answer.Id, "answer"))) return false;
        JsonFile.Write(PathOf(answer.Id, "answer"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("id", answer.Id);
            Optional(writer, "partner", answer.Partner);
            writer.WriteString("summary", answer.Summary);
            if (answer.Left is null) writer.WriteNull("left");
            else writer.WriteString("left", answer.Left);
            if (answer.LastWords) writer.WriteBoolean("lastWords", true);
            writer.WriteString("at", Moments.Write(answer.At));
            writer.WriteEndObject();
        });
        return true;
    }

    public void WriteFacts(PartnerFacts facts)
    {
        JsonFile.Write(PathOf(facts.Id, "facts"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("id", facts.Id);
            writer.WriteString("commit", facts.Commit);
            WriteStrings(writer, "files", facts.Files);
            WriteStrings(writer, "beyond", facts.Beyond);
            writer.WriteStartArray("leftOut");
            foreach (var left in facts.LeftOut)
            {
                writer.WriteStartObject();
                writer.WriteString("path", left.Path);
                writer.WriteString("why", left.Why);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteString("at", Moments.Write(facts.At));
            writer.WriteEndObject();
        });
    }

    public PartnerFacts? ReadFacts(string id)
    {
        if (Parse(PathOf(id, "facts")) is not { } root) return null;
        using (root)
        {
            var element = root.RootElement;
            if (Text(element, "commit") is not { Length: > 0 } commit) return null;
            var left = new List<PieceLeftOut>();
            if (element.TryGetProperty("leftOut", out var leaving) && leaving.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in leaving.EnumerateArray())
                {
                    if (Text(entry, "path") is { } path && Text(entry, "why") is { } why) left.Add(new(path, why));
                }
            }

            return new PartnerFacts(
                Text(element, "id") ?? id, commit, Strings(element, "files"), Strings(element, "beyond"), left,
                Moments.Read(element, "at") ?? DateTimeOffset.UtcNow);
        }
    }

    /// <summary>The piece's one delivery receipt, or null where none was claimed yet or it is unreadable.</summary>
    public PartnerReceipt? ReadReceipt(string id)
    {
        if (Parse(PathOf(id, "delivery")) is not { } root) return null;
        using (root)
        {
            var element = root.RootElement;
            return Text(element, "to") is { Length: > 0 } to && Text(element, "key") is { Length: > 0 } key
                && Text(element, "outcome") is { } outcome && PartnerDelivery.All.Contains(outcome)
                ? new PartnerReceipt(Text(element, "id") ?? id, to, key, outcome, Moments.Read(element, "at") ?? DateTimeOffset.UtcNow)
                : null;
        }
    }

    /// <summary>Keep the receipt whole, written before anything is handed.</summary>
    public void WriteReceipt(PartnerReceipt receipt)
    {
        JsonFile.Write(PathOf(receipt.Id, "delivery"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("id", receipt.Id);
            writer.WriteString("to", receipt.To);
            writer.WriteString("key", receipt.Key);
            writer.WriteString("outcome", receipt.Outcome);
            writer.WriteString("at", Moments.Write(receipt.At));
            writer.WriteEndObject();
        });
    }

    private static void Optional(Utf8JsonWriter writer, string name, string? value)
    {
        if (value is not null) writer.WriteString(name, value);
    }

    private static void WriteStrings(Utf8JsonWriter writer, string name, IEnumerable<string> values)
    {
        writer.WriteStartArray(name);
        foreach (var value in values) writer.WriteStringValue(value);
        writer.WriteEndArray();
    }

    private static IReadOnlyList<string> Strings(JsonElement element, string name) =>
        element.TryGetProperty(name, out var list) && list.ValueKind == JsonValueKind.Array
            ? [.. list.EnumerateArray().Where(each => each.ValueKind == JsonValueKind.String).Select(each => each.GetString()!)]
            : [];

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
}
