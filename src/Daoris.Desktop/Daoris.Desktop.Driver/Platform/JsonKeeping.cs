using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// An edit written over the file it was read from (FABRIC-GRAPH1's review, finding 1; FIX-LOG 2026-10-17). A writer that rebuilt
/// <c>driver.json</c> from the fields it knows deleted every setting it did not, so an edit at Setup or from Ask Daoris erased what a
/// newer build, the terminal or the person had put there. Three texts make the edit instead: the file as it stands, the file as its reader
/// understood it written again, and the edited choices written. What the edit did not change is written as the file has it; what it
/// changed is written as the edit has it, an object the three hold alike merged key by key, so a rule's own unknown keys stay; a key
/// the edit adds goes after the one before it in the writer's order. The CLI's twin is <c>jsonkeep.ts</c> (<c>keepEdits</c>), and
/// both are held to <c>test/fixtures/driver-config-keeps.json</c>.
/// <para>🔴 An edit is the set of key paths it changed (a value set, a key added, a key removed), from the text read and the edited choices,
/// and making it touches those paths alone (the second re-check, finding 1): merged over the file as it stands, an object another writer
/// had taken out came back whole with every key the edit never touched, and one another writer had made a string was overwritten by the
/// stale edited object. An object missing now is made again with only the edit's own keys under it; one that is no longer an object
/// refuses the edit (<see cref="JsonEditRefusedException"/>); a key the edit took that is already gone is nothing.</para>
/// </summary>
public static class JsonKeeping
{
    private static readonly JsonDocument Nothing = JsonDocument.Parse("{}");

    /// <summary>
    /// <paramref name="edited"/> written over <paramref name="original"/>, where <paramref name="read"/> is the file as its reader understood
    /// it when it read it, written by the same writer as <paramref name="edited"/>. No edit is <paramref name="original"/>'s own bytes.
    /// <paramref name="original"/> is the file as it stands when the edit is written, which may be another writer's since the read (the
    /// re-check's finding 1): what that writer changed and this edit did not stays as it is. Its lines end as its first line does (finding 3:
    /// an edit wrote LF over a file in CRLF).
    /// </summary>
    /// <exception cref="JsonEditRefusedException">A path the edit changes lies under a value that is no longer an object.</exception>
    public static string Kept(string original, string read, string edited)
    {
        if (read == edited) return original;
        using var file = JsonDocument.Parse(original);
        using var understood = JsonDocument.Parse(read);
        using var now = JsonDocument.Parse(edited);
        if (file.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new JsonEditRefusedException(Changed(understood.RootElement, now.RootElement, "") ?? "", "");
        }

        var plan = Plan(file.RootElement, understood.RootElement, now.RootElement, "", top: true);
        var text = JsonFile.Serialize(writer => Write(writer, plan));
        var end = original.IndexOf('\n');
        return end > 0 && original[end - 1] == '\r' ? text.Replace("\n", "\r\n", StringComparison.Ordinal) : text;
    }

    /// <summary>
    /// An edit of a home file made onto the file as it stands, and written (KEEPALL1): every file a door edits keeps what its writer does
    /// not own, as <c>driver.json</c> does through <see cref="DriverConfig.Save"/>. <paramref name="understood"/> is the file as its reader
    /// understood it when it read it, and <paramref name="edited"/> the edited choices, both written by one writer; only the key paths that
    /// differ between them are made, under <see cref="Kept"/>'s rules, onto the file read again here. No edit writes nothing.
    /// <paramref name="found"/> says a file was there at the read: where there was none then and is none now, <paramref name="edited"/> is
    /// written whole in the writer's own layout, since there is nothing to keep. The CLI's twin is <c>jsonkeep.ts</c>' <c>writeKeeping</c>.
    /// </summary>
    /// <exception cref="DriverException">
    /// The file there now does not read, holds a key twice in one object, or holds no object where this edit changes something inside one;
    /// nothing was written.
    /// </exception>
    public static void Save(string path, bool found, string understood, string edited)
    {
        if (understood == edited) return;
        var name = Path.GetFileName(path);
        string? current = null;
        try
        {
            if (File.Exists(path)) current = File.ReadAllText(path);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            throw new DriverException($"{name} could not be read ({error.Message.TrimEnd('.')}), so this edit was not written. {Remedy}");
        }

        string text;
        if (current is null && !found) text = edited;
        else
        {
            current ??= "{}\n";
            try
            {
                using var _ = JsonDocument.Parse(current);
            }
            catch (JsonException error)
            {
                throw new DriverException(
                    $"{name} is not readable JSON now ({error.Message}), so this edit was not written: a file that does not read is never overwritten. {Remedy}");
            }

            if (Twice(current) is { } key)
            {
                throw new DriverException($"{name} holds the key `{key}` twice in one object, so an edit cannot tell which it changes; this edit was not written. {Remedy}");
            }

            try
            {
                text = Kept(current, understood, edited);
            }
            catch (JsonEditRefusedException refused)
            {
                throw new DriverException($"{name} has changed under this edit: {refused.Message}, so this edit was not written. {Remedy}");
            }
        }

        var folder = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        AtomicFile.WriteText(path, text);
    }

    private const string Remedy = "Fix it, or delete it to start from nothing, then make the change again.";

    /// <summary>
    /// The first key written twice in one object of <paramref name="text"/>, a JSON text, in the order the second is written; or null (the
    /// re-check's finding 3; the CLI's <c>twice</c>). An edit cannot tell which of the two it changes: this writer kept both and edited
    /// each, the CLI's kept the last, so neither edits a file holding one.
    /// </summary>
    public static string? Twice(string text)
    {
        using var document = JsonDocument.Parse(text);
        return Twice(document.RootElement);
    }

    private static string? Twice(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (Twice(item) is { } inner) return inner;
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!seen.Add(property.Name)) return property.Name;
                if (Twice(property.Value) is { } inner) return inner;
            }
        }

        return null;
    }

    /// <summary>
    /// <paramref name="text"/> read as JavaScript's <c>JSON.parse</c> reads it, where a key is written twice in one object: once, in its
    /// first place, with its last value (the re-check's finding 3: the work cycles' kinds read the first here and the last in the CLI);
    /// else <paramref name="text"/> itself.
    /// </summary>
    public static string Once(string text)
    {
        using var document = JsonDocument.Parse(text);
        return Twice(document.RootElement) is null ? text : JsonFile.Serialize(writer => WriteOnce(writer, document.RootElement));
    }

    private static void WriteOnce(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray()) WriteOnce(writer, item);
            writer.WriteEndArray();
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            var last = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            var order = new List<string>();
            foreach (var property in element.EnumerateObject())
            {
                if (!last.ContainsKey(property.Name)) order.Add(property.Name);
                last[property.Name] = property.Value;
            }

            writer.WriteStartObject();
            foreach (var name in order)
            {
                writer.WritePropertyName(name);
                WriteOnce(writer, last[name]);
            }

            writer.WriteEndObject();
        }
        else element.WriteTo(writer);
    }

    private static void Write(Utf8JsonWriter writer, List<(string Name, Action<Utf8JsonWriter> Value)> plan)
    {
        writer.WriteStartObject();
        foreach (var (name, value) in plan)
        {
            writer.WritePropertyName(name);
            value(writer);
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// What one object of the file as it stands becomes, key by key in its order, <paramref name="where"/> being its path: a key neither
    /// its reader nor the edit holds is the file's, kept; one the edit took goes, except that a map emptied at the top loses only the
    /// entries its reader read; one its reader did not read and the edit set is the edit's, a map merged into the file's key by key and
    /// refused where the file's is no longer an object; one the edit left as read is the file's, as
    /// written; an object the edit changed is merged, refused where the file's is no longer an object; else the edit's. A key the edit
    /// set that the file no longer holds is added, an object with only the edit's own keys under it (the second re-check, finding 1).
    /// </summary>
    private static List<(string Name, Action<Utf8JsonWriter> Value)> Plan(JsonElement file, JsonElement read, JsonElement edited, string where, bool top)
    {
        var plan = new List<(string Name, Action<Utf8JsonWriter> Value)>();
        foreach (var property in file.EnumerateObject())
        {
            var kept = property.Value;
            var path = Join(where, property.Name);
            var wasRead = read.TryGetProperty(property.Name, out var was);
            var isEdited = edited.TryGetProperty(property.Name, out var now);
            if (!wasRead && !isEdited) plan.Add((property.Name, kept.WriteTo));
            else if (!isEdited)
            {
                // The writer leaves an emptied map out, so at the top its edit took the entries its reader read, not the map.
                if (!top || was.ValueKind != JsonValueKind.Object || Changed(was, Nothing.RootElement, path) is not { } first) continue;
                if (kept.ValueKind != JsonValueKind.Object) throw new JsonEditRefusedException(first, path);
                var unread = Plan(kept, was, Nothing.RootElement, path, top: false);
                if (unread.Count > 0) plan.Add((property.Name, writer => Write(writer, unread)));
            }
            else if (!wasRead && now.ValueKind == JsonValueKind.Object)
            {
                // A map its reader never saw is no whole value: its keys are the edit's paths, made into what another writer wrote
                // meanwhile (the re-check of round 3: written whole, it deleted that writer's entries, or overwrote its string).
                if (kept.ValueKind != JsonValueKind.Object) throw new JsonEditRefusedException(Changed(Nothing.RootElement, now, path) ?? path, path);
                var merged = Plan(kept, Nothing.RootElement, now, path, top: false);
                plan.Add((property.Name, writer => Write(writer, merged)));
            }
            else if (!wasRead) plan.Add((property.Name, now.WriteTo));
            else if (was.GetRawText() == now.GetRawText()) plan.Add((property.Name, kept.WriteTo));
            else if (was.ValueKind == JsonValueKind.Object && now.ValueKind == JsonValueKind.Object)
            {
                if (kept.ValueKind != JsonValueKind.Object) throw new JsonEditRefusedException(Changed(was, now, path) ?? path, path);
                var merged = Plan(kept, was, now, path, top: false);
                plan.Add((property.Name, writer => Write(writer, merged)));
            }
            else plan.Add((property.Name, now.WriteTo));
        }

        // A key the edit adds goes after the edit's key before it, or first; a default the file never wrote stays unwritten, and a key
        // another writer took that this edit left as read stays out.
        var at = 0;
        foreach (var property in edited.EnumerateObject())
        {
            var index = plan.FindIndex(each => each.Name == property.Name);
            if (index >= 0)
            {
                at = index + 1;
                continue;
            }

            if (file.TryGetProperty(property.Name, out _)) continue;
            if (!read.TryGetProperty(property.Name, out var was)) plan.Insert(at++, (property.Name, property.Value.WriteTo));
            else if (was.GetRawText() == property.Value.GetRawText()) continue;
            else if (was.ValueKind == JsonValueKind.Object && property.Value.ValueKind == JsonValueKind.Object)
            {
                // Taken out since the read: made again with only the edit's own keys under it, never the ones it left as read.
                var made = Plan(Nothing.RootElement, was, property.Value, Join(where, property.Name), top: false);
                if (made.Count > 0) plan.Insert(at++, (property.Name, writer => Write(writer, made)));
            }
            else plan.Insert(at++, (property.Name, property.Value.WriteTo));
        }

        return plan;
    }

    /// <summary>
    /// The first key path under <paramref name="where"/> the edit from <paramref name="was"/> to <paramref name="now"/> changes, in the
    /// edit's order and then the keys it took; or null where it changes none. What a refusal names.
    /// </summary>
    private static string? Changed(JsonElement was, JsonElement now, string where)
    {
        foreach (var property in now.EnumerateObject())
        {
            var path = Join(where, property.Name);
            if (!was.TryGetProperty(property.Name, out var before)) return path;
            if (before.GetRawText() == property.Value.GetRawText()) continue;
            return before.ValueKind == JsonValueKind.Object && property.Value.ValueKind == JsonValueKind.Object
                ? Changed(before, property.Value, path) ?? path
                : path;
        }

        foreach (var property in was.EnumerateObject())
        {
            if (!now.TryGetProperty(property.Name, out _)) return Join(where, property.Name);
        }

        return null;
    }

    private static string Join(string where, string name) => where.Length == 0 ? name : $"{where}.{name}";
}

/// <summary>
/// An edit refused, nothing written: it changes <see cref="EditPath"/>, and <see cref="Holder"/>, the value that path lies under, is no
/// longer an object in the file as it stands (FABRIC-GRAPH1's second re-check, finding 1). Another writer made it a string, a number or
/// an array since this one read it; writing the stale object over it would undo that writer's change. The CLI's twin is
/// <c>EditRefused</c> in <c>jsonkeep.ts</c>.
/// </summary>
public sealed class JsonEditRefusedException(string editPath, string holder)
    : Exception(holder.Length == 0
        ? $"this edit changes `{editPath}`, and the file is no longer a JSON object"
        : $"this edit changes `{editPath}`, and `{holder}` is no longer an object there")
{
    /// <summary>The key path the edit changes, its names joined by dots.</summary>
    public string EditPath { get; } = editPath;

    /// <summary>The path of the value it lies under that is no longer an object; empty for the file's top.</summary>
    public string Holder { get; } = holder;
}
