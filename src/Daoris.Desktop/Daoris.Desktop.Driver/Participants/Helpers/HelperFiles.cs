using System.Text;
using System.Text.Json;
using static Daoris.Driver.Json;

namespace Daoris.Driver;

/// <summary>
/// The helpers' three files per ask under <c>&lt;home&gt;/helpers</c>: read as the service writes
/// them and written as the service reads them. The file is the contract; the service's <c>HelperBox</c> and this share no code,
/// and each side's tests hold the same shapes. A fourth, <c>&lt;askId&gt;.capture.json</c>, is the driver's alone, as is a waiting session's
/// <c>&lt;session&gt;.back.json</c>, its move back to the person refused.
/// </summary>
/// <remarks>The records are the Domain's (<see cref="HelperAsk"/>, <see cref="HelperState"/>, <see cref="HelperAnswer"/>).</remarks>
public sealed class HelperFiles(string home)
{
    public string PathOf(string id, string kind) => DaorisHome.Entry(home, HomeEntry.Helpers, $"{id}.{kind}.json");

    /// <summary>Every ask on file, oldest first; one unreadable or of another shape is not an ask here.</summary>
    public IReadOnlyList<HelperAsk> Asks()
    {
        var folder = DaorisHome.Entry(home, HomeEntry.Helpers);
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
                var at = Moments.Read(element, "at") ?? File.GetLastWriteTimeUtc(path);
                asks.Add(new HelperAsk(id, asker, question, Text(element, "agent") is { Length: > 0 } agent ? agent : null, by, at)
                {
                    // An ask with no level is ordinary, today's delivery.
                    Urgent = element.TryGetProperty("urgent", out var urgent) && urgent.ValueKind == JsonValueKind.True,
                    // Absent, a first ask whose answer goes to the lead.
                    After = Text(element, "after") is { Length: > 0 } after ? after : null,
                    To = Text(element, "to") == HelperAsk.ToPerson ? HelperAsk.ToPerson : null,
                });
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
                Moments.Read(element, "at") ?? DateTimeOffset.UtcNow)
            {
                LastWords = element.TryGetProperty("lastWords", out var last) && last.ValueKind == JsonValueKind.True,
                Handed = Text(element, "handed"),
                Attempted = Moments.Read(element, "attempted"),
                Agent = Text(element, "agent"),
                Words = Text(element, "words"),
                Until = Moments.Read(element, "until"),
                Passed = Moments.Read(element, "passed"),
            };
        }
    }

    /// <summary>Keep one ask's state whole, atomically: the driver is its only writer.</summary>
    public void WriteState(HelperState state)
    {
        JsonFile.Write(PathOf(state.Id, "state"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("id", state.Id);
            writer.WriteString("state", state.State);
            if (state.Code is null) writer.WriteNull("code");
            else writer.WriteString("code", state.Code);
            if (state.Helper is null) writer.WriteNull("helper");
            else writer.WriteString("helper", state.Helper);
            writer.WriteString("at", Moments.Write(state.At));
            if (state.LastWords) writer.WriteBoolean("lastWords", true);
            if (state.Handed is not null) writer.WriteString("handed", state.Handed);
            if (state.Attempted is { } attempted) writer.WriteString("attempted", Moments.Write(attempted));
            if (state.Agent is not null) writer.WriteString("agent", state.Agent);
            if (state.Words is not null) writer.WriteString("words", state.Words);
            if (state.Until is { } until) writer.WriteString("until", Moments.Write(until));
            if (state.Passed is { } passed) writer.WriteString("passed", Moments.Write(passed));
            writer.WriteEndObject();
        });
    }

    /// <summary>
    /// The ask <paramref name="ask"/> asks again after, with its answer as kept: its answer file, or the last words its state
    /// keeps. Null for a first ask, or where the earlier one is gone or holds no answer.
    /// </summary>
    public HelperEarlier? EarlierOf(HelperAsk ask)
    {
        if (ask.After is not { } after || Asks().FirstOrDefault(each => each.Id == after && each.Asker == ask.Asker) is not { } earlier) return null;
        if (ReadAnswer(earlier.Id) is { } answer) return HelperEarlier.Of(earlier, answer.Answer, lastWords: false);
        return ReadState(earlier.Id) is { LastWords: true, Words: { Length: > 0 } words } ? HelperEarlier.Of(earlier, words, lastWords: true) : null;
    }

    /// <summary>The helper's answer to an ask, or null where it said none or the file is unreadable.</summary>
    public HelperAnswer? ReadAnswer(string id)
    {
        if (Parse(PathOf(id, "answer")) is not { } root) return null;
        using (root)
        {
            var element = root.RootElement;
            return Text(element, "answer") is { Length: > 0 } answer && Text(element, "helper") is { } helper
                ? new HelperAnswer(Text(element, "id") ?? id, helper, answer, Moments.Read(element, "at") ?? DateTimeOffset.UtcNow)
                : null;
        }
    }

    /// <summary>
    /// Keep the manifest of the work a helper is handed, <c>&lt;askId&gt;.capture.json</c>:
    /// the driver's alone, written before the helper starts, never its bytes. The service reads none of it.
    /// </summary>
    public void WriteCapture(string id, HelperCaptured capture)
    {
        JsonFile.Write(PathOf(id, "capture"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("id", capture.Id);
            writer.WriteString("commit", capture.Commit);
            writer.WriteString("at", Moments.Write(capture.At));
            writer.WriteStartArray("files");
            foreach (var file in capture.Files)
            {
                writer.WriteStartObject();
                writer.WriteString("path", file.Path);
                writer.WriteString("sha256", file.Hash);
                writer.WriteNumber("size", file.Size);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteStartArray("deleted");
            foreach (var path in capture.Deleted) writer.WriteStringValue(path);
            writer.WriteEndArray();
            writer.WriteStartArray("leftOut");
            foreach (var left in capture.LeftOut)
            {
                writer.WriteStartObject();
                writer.WriteString("path", left.Path);
                writer.WriteString("why", left.Why);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteBoolean("ignoredLeftOut", true);
            writer.WriteEndObject();
        });
    }

    /// <summary>The manifest of the work an ask's helper is handed, or null where none is kept or it is unreadable.</summary>
    public HelperCaptured? ReadCapture(string id)
    {
        if (Parse(PathOf(id, "capture")) is not { } root) return null;
        using (root)
        {
            var element = root.RootElement;
            if (Text(element, "id") is not { Length: > 0 } name || Text(element, "commit") is not { Length: > 0 } commit) return null;
            var files = new List<CapturedFile>();
            var deleted = new List<string>();
            var left = new List<CaptureLeftOut>();
            if (element.TryGetProperty("files", out var listed) && listed.ValueKind == JsonValueKind.Array)
            {
                foreach (var file in listed.EnumerateArray())
                {
                    if (Text(file, "path") is { } path && Text(file, "sha256") is { } hash)
                    {
                        files.Add(new CapturedFile(path, hash, file.TryGetProperty("size", out var size) && size.TryGetInt64(out var bytes) ? bytes : 0));
                    }
                }
            }

            if (element.TryGetProperty("deleted", out var gone) && gone.ValueKind == JsonValueKind.Array)
            {
                deleted.AddRange(gone.EnumerateArray().Where(path => path.ValueKind == JsonValueKind.String).Select(path => path.GetString()!));
            }

            if (element.TryGetProperty("leftOut", out var leaving) && leaving.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in leaving.EnumerateArray())
                {
                    if (Text(entry, "path") is { } path && Text(entry, "why") is { } why) left.Add(new CaptureLeftOut(path, why));
                }
            }

            return new HelperCaptured(name, commit, Moments.Read(element, "at") ?? DateTimeOffset.UtcNow, files, deleted, left);
        }
    }

    /// <summary>
    /// The ask a person's terminal writes, in the shape <c>helper_ask</c> writes: with <c>askedBy: person</c>,
    /// atomically. Returns its id. A follow-up names the ask it follows (<paramref name="after"/>) and an answer kept for the person says
    /// <c>to: person</c>, each after <c>askedBy</c> and only where set.
    /// </summary>
    public string WriteAsk(string asker, string question, string? agent, DateTimeOffset at, string? after = null, bool toPerson = false)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        JsonFile.Write(PathOf(id, "ask"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("id", id);
            writer.WriteString("asker", asker);
            writer.WriteString("question", question);
            if (agent is null) writer.WriteNull("agent");
            else writer.WriteString("agent", agent);
            writer.WriteString("askedBy", HelperAsk.ByPerson);
            if (after is not null) writer.WriteString("after", after);
            if (toPerson) writer.WriteString("to", HelperAsk.ToPerson);
            writer.WriteString("at", Moments.Write(at));
            writer.WriteEndObject();
        });
        return id;
    }

    /// <summary>
    /// Every refused move back to the person on file (<see cref="HelperBack"/>), oldest first; one unreadable or of another
    /// shape is none. The service reads none of them.
    /// </summary>
    public IReadOnlyList<HelperBack> Backs()
    {
        var folder = DaorisHome.Entry(home, HomeEntry.Helpers);
        if (!Directory.Exists(folder)) return [];
        var backs = new List<HelperBack>();
        foreach (var path in Directory.EnumerateFiles(folder, "*.back.json"))
        {
            if (Parse(path) is not { } root) continue;
            using (root)
            {
                var element = root.RootElement;
                if (Text(element, "session") is not { Length: > 0 } session || Text(element, "words") is not { } words) continue;
                backs.Add(new HelperBack(session, Text(element, "quest"), words, Moments.Read(element, "at") ?? File.GetLastWriteTimeUtc(path)));
            }
        }

        return [.. backs.OrderBy(back => back.At).ThenBy(back => back.Session, StringComparer.Ordinal)];
    }

    /// <summary>Keep a refused move back to the person whole, atomically: the driver is its only writer.</summary>
    public void WriteBack(HelperBack back)
    {
        JsonFile.Write(PathOf(back.Session, "back"), writer =>
        {
            writer.WriteStartObject();
            writer.WriteString("session", back.Session);
            if (back.Quest is null) writer.WriteNull("quest");
            else writer.WriteString("quest", back.Quest);
            writer.WriteString("words", back.Words);
            writer.WriteString("at", Moments.Write(back.At));
            writer.WriteEndObject();
        });
    }

    /// <summary>Let a refused move back go: it moved, or the session waits on nothing of the person's any more.</summary>
    public void ClearBack(string session)
    {
        try { File.Delete(PathOf(session, "back")); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

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
