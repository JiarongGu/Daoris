using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// One terminal's press waiting for the person, as its record keeps it across a restart (CONFIRMWAIT1a-ii, the confirm-wait design
/// §7.1): the confirmation's own id, which names the file; the terminal's id and the binding it was sent under; the module, route and
/// payload as sent; the grounds read at the ask; the asker as the system named it; when it was asked and first shown; whether it carries
/// a key; and how it stands (<see cref="Waiting"/>, <see cref="Begun"/>, or <see cref="Ended"/> with its answer). No card's fields and
/// no named values: those are made again from the payload, so a record cannot show one thing and run another.
/// </summary>
/// <param name="Payload">The payload's text as it came, its binding's fingerprint's own; null for a keyed press, which keeps none.</param>
/// <param name="Grounds">The grounds as read at the ask; none for a keyed press.</param>
/// <param name="Answer">The response envelope it ended with, for <see cref="Ended"/>.</param>
internal sealed record PipeWaitingRecord(
    string Confirmation, string Request, PipeBinding Binding, string Module, string Route, string? Payload,
    IReadOnlyList<PipeReading> Grounds, PipeAsker Asker, DateTimeOffset Asked, DateTimeOffset? ShownAt, bool Keyed, string State,
    string? Answer = null)
{
    /// <summary>The record's version: one a newer Daoris wrote is left for it, never asked here.</summary>
    public const int Version = 1;

    /// <summary>It waits for the person.</summary>
    public const string Waiting = "waiting";

    /// <summary>Confirmed, its grounds holding, and about to run: never asked again.</summary>
    public const string Begun = "begun";

    /// <summary>Ended otherwise, with its answer, before its journal line was written.</summary>
    public const string Ended = "ended";
}

/// <summary>A file of the records' folder as the restore reads it: its name, and its record, or why it gave none.</summary>
/// <param name="Newer">A newer Daoris's record, left in place.</param>
internal sealed record PipeWaitingFile(string Path, string Name, PipeWaitingRecord? Record, bool Newer);

/// <summary>
/// The records of a terminal's presses waiting for the person (CONFIRMWAIT1a-ii, the confirm-wait design §7.1): one file each under the
/// home's <c>data/pipe-waiting/</c> (<see cref="HomeEntry.PipeWaiting"/>), named by the confirmation's own id, never the terminal's,
/// which never reaches a path; written whole as BOM-less UTF-8 (<see cref="JsonFile"/>). A kept place of the home, not <c>run/</c>,
/// which D172 lets anyone clear while Daoris is closed; never a key in it (D172 point 3).
/// </summary>
internal sealed class PipeWaiting(string folder)
{
    /// <summary>Whether <paramref name="name"/> is a confirmation's id, 32 lower-case hexadecimal characters: no other file is a record.</summary>
    public static bool IsName(string name) => name.Length == 32 && name.All(each => each is >= '0' and <= '9' or >= 'a' and <= 'f');

    /// <summary><paramref name="record"/> written whole, replacing the one before: false where it could not be.</summary>
    public bool Write(PipeWaitingRecord record)
    {
        if (!IsName(record.Confirmation)) return false;
        try
        {
            JsonFile.Write(System.IO.Path.Combine(folder, record.Confirmation + ".json"), writer => Write(writer, record));
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>The record of <paramref name="confirmation"/> removed: false where it is still there.</summary>
    public bool Remove(string confirmation) => IsName(confirmation) && Delete(System.IO.Path.Combine(folder, confirmation + ".json"));

    /// <summary>A file of the folder removed: false where it is still there.</summary>
    public static bool Delete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Every file of the folder, each with its record or why it gave none; none where the folder is not there.</summary>
    public IReadOnlyList<PipeWaitingFile> Read()
    {
        string[] paths;
        try
        {
            paths = Directory.Exists(folder) ? Directory.GetFiles(folder) : [];
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        return [.. paths.Order(StringComparer.Ordinal).Select(path =>
        {
            var name = System.IO.Path.GetFileName(path);
            var id = name.EndsWith(".json", StringComparison.Ordinal) ? name[..^5] : name;
            var (record, newer) = Parse(path);
            return new PipeWaitingFile(path, id, record, newer);
        })];
    }

    private static (PipeWaitingRecord? Record, bool Newer) Parse(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return (null, false);
            if (!root.TryGetProperty("version", out var version) || !version.TryGetInt32(out var number)) return (null, false);
            if (number > PipeWaitingRecord.Version) return (null, true);
            if (Json.Text(root, "confirmation") is not { } confirmation || Json.Text(root, "request") is not { } request
                || Json.Text(root, "module") is not { } module || Json.Text(root, "route") is not { } route
                || !root.TryGetProperty("binding", out var binding) || binding.ValueKind != JsonValueKind.Object
                || Json.Text(binding, "request") is not { } bound || Json.Text(binding, "payload") is not { } fingerprint
                || Json.Text(binding, "key") is not { } key
                || Moments.Read(root, "asked") is not { } asked || Json.Text(root, "state") is not { } state)
            {
                return (null, false);
            }

            var grounds = new List<PipeReading>();
            if (root.TryGetProperty("grounds", out var read) && read.ValueKind == JsonValueKind.Array)
            {
                foreach (var each in read.EnumerateArray())
                {
                    if (each.ValueKind != JsonValueKind.Object || !Enum.TryParse<PipeGroundKind>(Json.Text(each, "kind"), ignoreCase: true, out var kind)
                        || Json.Text(each, "subject") is not { } subject || Json.Text(each, "value") is not { } value)
                    {
                        return (null, false);
                    }

                    grounds.Add(new PipeReading(kind, subject, value, Json.Text(each, "words")));
                }
            }

            var asker = root.TryGetProperty("asker", out var named) && named.ValueKind == JsonValueKind.Object
                ? new PipeAsker(Json.Text(named, "process"), named.TryGetProperty("pid", out var pid) && pid.TryGetInt32(out var id) ? id : null)
                : new PipeAsker(null, null);
            var answer = root.TryGetProperty("answer", out var answered) && answered.ValueKind == JsonValueKind.Object ? answered.GetRawText() : null;
            return (new PipeWaitingRecord(
                confirmation, request, new PipeBinding(bound, fingerprint, key), module, route, Json.Text(root, "payload"), grounds, asker, asked,
                Moments.Read(root, "shownAt"), root.TryGetProperty("keyed", out var keyed) && keyed.ValueKind == JsonValueKind.True, state, answer), false);
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return (null, false);
        }
    }

    private static void Write(Utf8JsonWriter writer, PipeWaitingRecord record)
    {
        writer.WriteStartObject();
        writer.WriteNumber("version", PipeWaitingRecord.Version);
        writer.WriteString("confirmation", record.Confirmation);
        writer.WriteString("request", record.Request);
        writer.WriteStartObject("binding");
        writer.WriteString("request", record.Binding.Request);
        writer.WriteString("payload", record.Binding.Payload);
        writer.WriteString("key", record.Binding.Key);
        writer.WriteEndObject();
        writer.WriteString("module", record.Module);
        writer.WriteString("route", record.Route);
        // A keyed press keeps no payload and no grounds: its key never reaches data/ (D172 point 3).
        if (!record.Keyed && record.Payload is { } payload) writer.WriteString("payload", payload);
        writer.WriteStartArray("grounds");
        foreach (var ground in record.Keyed ? [] : record.Grounds)
        {
            writer.WriteStartObject();
            writer.WriteString("kind", PipeGroundRow.Code(ground.Kind));
            writer.WriteString("subject", ground.Subject);
            writer.WriteString("value", ground.Value);
            if (ground.Words is { } words) writer.WriteString("words", words);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteStartObject("asker");
        if (record.Asker.Process is { } process) writer.WriteString("process", process);
        if (record.Asker.Pid is { } pid) writer.WriteNumber("pid", pid);
        writer.WriteEndObject();
        writer.WriteString("asked", Moments.Write(record.Asked));
        if (record.ShownAt is { } shown) writer.WriteString("shownAt", Moments.Write(shown));
        writer.WriteBoolean("keyed", record.Keyed);
        writer.WriteString("state", record.State);
        if (record.Answer is { } answer)
        {
            writer.WritePropertyName("answer");
            writer.WriteRawValue(answer);
        }

        writer.WriteEndObject();
    }
}
