using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// <c>harnesses.json</c> read into <see cref="HarnessSettings"/> and written back in the writer's layout, which the CLI's writer
/// shares as the file's contract.
/// </summary>
internal static class HarnessSettingsFile
{
    /// <summary>The sections this build reads and writes; anything else is <see cref="HarnessSettings.Kept"/>. Spelled as the CLI's are.</summary>
    private static readonly HashSet<string> Sections = new(StringComparer.Ordinal)
    {
        "defaults", "workspaces", "versions", "workspaceVersions", "rotation", "workspaceRotation", "rotationUse", "workspaceRotationUse",
        "pooled",
    };

    /// <summary>
    /// A missing file is a machine that named no profiles — not an error. Neither is an unreadable
    /// one: this is wiring, and absent wiring is the documented default (D21), so a hand-mangled file
    /// must never be what stops a driver coming up.
    /// </summary>
    public static HarnessSettings Load(string path)
    {
        var nothing = new HarnessSettings();
        if (!File.Exists(path)) return nothing with { Understood = Layout(nothing, asRead: true) };

        try
        {
            using var document = JsonDocument.Parse(AtomicFile.ReadText(path));
            // A file that is no object reads as none, and an edit over it is refused at the write, as the CLI refuses it.
            if (document.RootElement.ValueKind != JsonValueKind.Object) return nothing with { Understood = Layout(nothing, asRead: true), Found = true };

            var defaults = ReadMap(document.RootElement, "defaults");
            var workspaces = new Dictionary<string, IReadOnlyDictionary<string, string>>(
                StringComparer.OrdinalIgnoreCase);

            if (document.RootElement.TryGetProperty("workspaces", out var circles)
                && circles.ValueKind == JsonValueKind.Object)
            {
                foreach (var circle in circles.EnumerateObject())
                {
                    if (circle.Value.ValueKind != JsonValueKind.Object) continue;
                    workspaces[circle.Name] = ReadMap(circle.Value, null);
                }
            }

            var settings = new HarnessSettings(
                defaults, workspaces,
                ReadMap(document.RootElement, "versions"),
                ReadCircles(document.RootElement, "workspaceVersions"))
            {
                Rotation = ReadOrders(document.RootElement, "rotation"),
                WorkspaceRotation = ReadOrderCircles(document.RootElement, "workspaceRotation"),
                Uses = ReadUses(document.RootElement, "rotationUse"),
                WorkspaceUses = ReadUseCircles(document.RootElement, "workspaceRotationUse"),
                Pooled = PoolReceipts.Read(document.RootElement),
                Kept = [.. document.RootElement.EnumerateObject()
                    .Where(property => !Sections.Contains(property.Name))
                    .Select(property => KeyValuePair.Create(property.Name, property.Value.Clone()))],
                Found = true,
            };
            return settings with { Understood = Layout(settings, asRead: true) };
        }
        catch (Exception error)
            when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return new HarnessSettings { Problem = $"{Path.GetFileName(path)} could not be read ({error.Message})" };
        }
    }

    /// <summary><see cref="HarnessSettings.Save"/> for a writer already holding the file's <see cref="KeepLock"/>.</summary>
    public static void SaveHolding(HarnessSettings settings, string path)
    {
        if (settings.Problem is not null)
        {
            throw new DriverException(
                $"{settings.Problem}, so this edit was not written — it would have replaced every choice in it. "
                + "Fix the file or remove it, then make the change again.");
        }

        if (settings.Understood is { } understood) JsonKeeping.SaveHolding(path, settings.Found, understood, Layout(settings, asRead: false));
        // LF on every platform, as the CLI's writer writes it: both write this file, the same wiring as the same bytes.
        else JsonFile.Write(path, writer => Write(settings, writer, asRead: false));
    }

    /// <summary>The wiring in the writer's layout; <paramref name="asRead"/> writes each settings entry as the file held it.</summary>
    private static string Layout(HarnessSettings settings, bool asRead) => JsonFile.Serialize(writer => Write(settings, writer, asRead));

    private static void Write(HarnessSettings settings, Utf8JsonWriter writer, bool asRead)
    {
        writer.WriteStartObject();

        // What this build has no field for goes first, as the CLI's writer puts its `rest` first (TOOL4e).
        foreach (var (name, value) in settings.Kept)
        {
            writer.WritePropertyName(name);
            value.WriteTo(writer);
        }

        writer.WriteStartObject("defaults");
        foreach (var (harness, profile) in settings.Defaults.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            writer.WriteString(harness, profile);
        }

        writer.WriteEndObject();

        WriteCircles(writer, "workspaces", settings.Workspaces);

        // 🔴 The pins go out too, or this write DELETES what `daoris agent pin` put there.
        // Both artefacts write this one file, and a save that knew only about profiles would
        // compile, pass every profile test, and silently lose somebody's toolchain (TOOL2).
        writer.WriteStartObject("versions");
        foreach (var (harness, version) in settings.Versions.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            writer.WriteString(harness, version);
        }

        writer.WriteEndObject();
        WriteCircles(writer, "workspaceVersions", settings.WorkspaceVersions);

        // 🔴 The orders go out too (TOOL4e), or a screen edit DELETES what `daoris agent account order` wrote, the
        // way a save that knew only profiles once would have deleted the pins. Written only when set: absent is no
        // rotation, and a file that never chose an order should not start carrying an empty one.
        if (settings.Rotation.Any(order => order.Value.Count > 0))
        {
            writer.WriteStartObject("rotation");
            WriteOrders(writer, settings.Rotation);
            writer.WriteEndObject();
        }

        // TOOL6a: how each list is used goes out beside it, for the same reason the orders do.
        WriteUses(writer, "rotationUse", settings.Uses, asRead);

        if (settings.WorkspaceRotation.Any(circle => circle.Value.Any(order => order.Value.Count > 0)))
        {
            writer.WriteStartObject("workspaceRotation");
            foreach (var (workspace, orders) in settings.WorkspaceRotation.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                if (!orders.Any(order => order.Value.Count > 0)) continue;
                writer.WriteStartObject(workspace);
                WriteOrders(writer, orders);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        WriteUseCircles(writer, "workspaceRotationUse", settings.WorkspaceUses, asRead);
        PoolReceipts.Write(writer, settings.Pooled);

        writer.WriteEndObject();
    }

    /// <summary>One workspace → harness → value map, read from a named property.</summary>
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> ReadCircles(
        JsonElement root, string property)
    {
        var circles = new Dictionary<string, IReadOnlyDictionary<string, string>>(
            StringComparer.OrdinalIgnoreCase);

        if (root.TryGetProperty(property, out var element) && element.ValueKind == JsonValueKind.Object)
        {
            foreach (var circle in element.EnumerateObject())
            {
                if (circle.Value.ValueKind != JsonValueKind.Object) continue;
                circles[circle.Name] = ReadMap(circle.Value, null);
            }
        }

        return circles;
    }

    /// <summary>The write half of <see cref="ReadCircles"/>. An empty circle is not written at all.</summary>
    private static void WriteCircles(
        Utf8JsonWriter writer, string property,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> circles)
    {
        writer.WriteStartObject(property);
        foreach (var (workspace, map) in circles.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (map.Count == 0) continue;
            writer.WriteStartObject(workspace);
            foreach (var (harness, value) in map.OrderBy(e => e.Key, StringComparer.Ordinal))
            {
                writer.WriteString(harness, value);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// One agent → order map (TOOL4e): each list's names trimmed, a blank or a name that is not text skipped, a name
    /// written twice in any case read once where first written; a list that is not one, or names nobody, is none.
    /// </summary>
    private static IReadOnlyDictionary<string, IReadOnlyList<string>> ReadOrders(JsonElement parent, string? property)
    {
        var orders = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        var element = parent;
        if (property is not null
            && (!parent.TryGetProperty(property, out element) || element.ValueKind != JsonValueKind.Object))
        {
            return orders;
        }

        foreach (var entry in element.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.Array) continue;
            var order = new List<string>();
            foreach (var item in entry.Value.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && item.GetString()!.Trim() is { Length: > 0 } name
                    && !order.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    order.Add(name);
                }
            }

            if (order.Count > 0) orders[entry.Name] = order;
        }

        return orders;
    }

    /// <summary>One workspace → agent → order map, read as <see cref="ReadOrders"/> reads each; a workspace naming none is none.</summary>
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> ReadOrderCircles(
        JsonElement root, string property)
    {
        var circles = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(property, out var element) || element.ValueKind != JsonValueKind.Object) return circles;

        foreach (var circle in element.EnumerateObject())
        {
            if (circle.Value.ValueKind != JsonValueKind.Object) continue;
            var orders = ReadOrders(circle.Value, null);
            if (orders.Count > 0) circles[circle.Name] = orders;
        }

        return circles;
    }

    /// <summary>The write half of <see cref="ReadOrders"/>: agents in order of name, each list in the person's order.</summary>
    private static void WriteOrders(Utf8JsonWriter writer, IReadOnlyDictionary<string, IReadOnlyList<string>> orders)
    {
        foreach (var (agent, order) in orders.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            if (order.Count == 0) continue;
            writer.WriteStartArray(agent);
            foreach (var name in order) writer.WriteStringValue(name);
            writer.WriteEndArray();
        }
    }

    private static IReadOnlyDictionary<string, string> ReadMap(JsonElement parent, string? property)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var element = parent;
        if (property is not null
            && (!parent.TryGetProperty(property, out element) || element.ValueKind != JsonValueKind.Object))
        {
            return map;
        }

        foreach (var entry in element.EnumerateObject())
        {
            if (entry.Value.ValueKind == JsonValueKind.String
                && entry.Value.GetString() is { Length: > 0 } profile)
            {
                map[entry.Name] = profile;
            }
        }

        return map;
    }

    /// <summary>One agent → settings map: each entry an object kept whole; anything else, or an entry naming nothing, is none.</summary>
    private static IReadOnlyDictionary<string, UseEntry> ReadUses(JsonElement parent, string? property)
    {
        var uses = new Dictionary<string, UseEntry>(StringComparer.OrdinalIgnoreCase);
        var element = parent;
        if (property is not null && !parent.TryGetProperty(property, out element)) return uses;
        if (element.ValueKind != JsonValueKind.Object) return uses;

        foreach (var entry in element.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.Object) continue;
            List<KeyValuePair<string, JsonElement>> fields = [.. entry.Value.EnumerateObject()
                .Select(field => KeyValuePair.Create(field.Name, field.Value.Clone()))];
            if (fields.Count > 0) uses[entry.Name] = new UseEntry(fields);
        }

        return uses;
    }

    /// <summary>One workspace → agent → settings map, each read as <see cref="ReadUses"/> reads one; a workspace naming none is none.</summary>
    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, UseEntry>> ReadUseCircles(JsonElement root, string property)
    {
        var circles = new Dictionary<string, IReadOnlyDictionary<string, UseEntry>>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(property, out var element) || element.ValueKind != JsonValueKind.Object) return circles;

        foreach (var circle in element.EnumerateObject())
        {
            var uses = ReadUses(circle.Value, null);
            if (uses.Count > 0) circles[circle.Name] = uses;
        }

        return circles;
    }

    /// <summary>A settings section, written only where an entry is set: agents in order of name.</summary>
    private static void WriteUses(Utf8JsonWriter writer, string property, IReadOnlyDictionary<string, UseEntry> uses, bool asRead)
    {
        var written = Written(uses, asRead);
        if (written.Count == 0) return;
        writer.WriteStartObject(property);
        WriteEntries(writer, written, asRead);
        writer.WriteEndObject();
    }

    /// <summary>The workspaces' settings section, written only where one is set: workspaces in order of name.</summary>
    private static void WriteUseCircles(
        Utf8JsonWriter writer, string property, IReadOnlyDictionary<string, IReadOnlyDictionary<string, UseEntry>> circles, bool asRead)
    {
        var written = circles.OrderBy(e => e.Key, StringComparer.Ordinal)
            .Select(circle => (Workspace: circle.Key, Uses: Written(circle.Value, asRead)))
            .Where(circle => circle.Uses.Count > 0)
            .ToList();
        if (written.Count == 0) return;
        writer.WriteStartObject(property);
        foreach (var (workspace, uses) in written)
        {
            writer.WriteStartObject(workspace);
            WriteEntries(writer, uses, asRead);
            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <summary>
    /// Each agent's entry as written (<see cref="UseEntry.Written"/>), or as the file held it where <paramref name="asRead"/> says, its old
    /// and retired names included (KEEPALL1b): in order of name, one written as nothing left out.
    /// </summary>
    private static List<(string Agent, UseEntry Entry)> Written(IReadOnlyDictionary<string, UseEntry> uses, bool asRead) =>
        [.. uses.OrderBy(e => e.Key, StringComparer.Ordinal)
            .Select(entry => (Agent: entry.Key, Entry: asRead ? entry.Value : entry.Value.Written()))
            .Where(entry => !entry.Entry.IsEmpty)];

    private static void WriteEntries(Utf8JsonWriter writer, List<(string Agent, UseEntry Entry)> uses, bool asRead)
    {
        foreach (var (agent, entry) in uses)
        {
            writer.WritePropertyName(agent);
            if (asRead) entry.WriteAsRead(writer);
            else
            {
                writer.WriteStartObject();
                entry.WriteTo(writer);
                writer.WriteEndObject();
            }
        }
    }
}
