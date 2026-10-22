using System.Globalization;
using System.Text;
using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// An opinion's delivery file (XAGENT1e), <c>&lt;home&gt;/opinions/&lt;id&gt;/delivered.json</c> (<see cref="OpinionDeliveries"/>): read
/// without trusting its shape, written whole, atomically, as BOM-less UTF-8 with LF.
/// </summary>
/// <remarks>The use case decides what is kept; this reads and writes it.</remarks>
internal static class OpinionDeliveryFiles
{
    /// <summary>What <paramref name="path"/> keeps; <see cref="OpinionDelivered.None"/> where it is missing or does not read.</summary>
    public static OpinionDelivered Read(string path)
    {
        if (!File.Exists(path)) return OpinionDelivered.None;
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return OpinionDelivered.None;
            return new OpinionDelivered(Text(root, "person"), Moment(root, "personAt"), Reading(root))
            {
                Recheck = Text(root, "recheck"),
                RecheckWhy = Text(root, "recheckWhy"),
            };
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return OpinionDelivered.None;
        }
    }

    /// <summary><paramref name="delivered"/> written to <paramref name="path"/>, its folder made where it is not; a write that fails is lost.</summary>
    public static void Write(string path, OpinionDelivered delivered)
    {
        try
        {
            JsonFile.Write(path, writer => Compose(writer, delivered));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // Lost: the next look reads the host and the record again, and keeps it then.
        }
    }

    private static void Compose(Utf8JsonWriter writer, OpinionDelivered delivered)
    {
        writer.WriteStartObject();
        if (delivered.Person is { } person)
        {
            writer.WriteString("person", person);
            if (delivered.PersonAt is { } at) writer.WriteString("personAt", OpinionViews.Moment(at));
        }

        if (delivered.Answered is { } reading)
        {
            writer.WriteStartObject("answered");
            writer.WriteString("session", reading.Session);
            if (reading.Tip is not null) writer.WriteString("tip", reading.Tip);
            writer.WriteString("at", OpinionViews.Moment(reading.At));
            writer.WriteStartArray("findings");
            foreach (var finding in reading.Findings)
            {
                writer.WriteStartObject();
                writer.WriteNumber("finding", finding.Finding);
                writer.WriteString("weight", finding.Weight);
                writer.WriteString("counts", finding.Counts);
                if (finding.Said is not null) writer.WriteString("said", finding.Said);
                if (finding.Commit is not null) writer.WriteString("commit", finding.Commit);
                if (finding.Fix is not null) writer.WriteString("fix", finding.Fix);
                if (finding.Why is not null) writer.WriteString("why", finding.Why);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        if (delivered.Recheck is not null) writer.WriteString("recheck", delivered.Recheck);
        if (delivered.RecheckWhy is not null) writer.WriteString("recheckWhy", delivered.RecheckWhy);
        writer.WriteEndObject();
    }

    private static OpinionReading? Reading(JsonElement root)
    {
        if (!root.TryGetProperty("answered", out var answered) || answered.ValueKind != JsonValueKind.Object
            || Text(answered, "session") is not { } session || Moment(answered, "at") is not { } at)
        {
            return null;
        }

        var findings = new List<OpinionAnswerRead>();
        if (answered.TryGetProperty("findings", out var rows) && rows.ValueKind == JsonValueKind.Array)
        {
            foreach (var row in rows.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty("finding", out var number) || number.ValueKind != JsonValueKind.Number
                    || !number.TryGetInt32(out var finding) || Text(row, "weight") is not { } weight || Text(row, "counts") is not { } counts)
                {
                    continue;
                }

                findings.Add(new OpinionAnswerRead(finding, weight, counts)
                {
                    Said = Text(row, "said"), Commit = Text(row, "commit"), Fix = Text(row, "fix"), Why = Text(row, "why"),
                });
            }
        }

        return new OpinionReading(session, Text(answered, "tip"), at, findings);
    }

    private static DateTimeOffset? Moment(JsonElement element, string name) => Moments.Read(Text(element, name));
}
