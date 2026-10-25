using System.Text.Json;

namespace Daoris.Driver;

/// <summary>A gate a checkout's <c>daoris.gates.json</c> declares, as a heavy run reads it (HEAVYRUN1a).</summary>
/// <param name="Name">Its name.</param>
/// <param name="Run">Its command line, run through the platform's shell.</param>
/// <param name="WorkingDirectory">Its <c>cwd</c>, under the checkout; null for the checkout itself.</param>
/// <param name="Heavy">Whether the repository marks it <c>"heavy": true</c>: one that runs only through the slot.</param>
public sealed record HeavyGate(string Name, string Run, string? WorkingDirectory, bool Heavy);

/// <summary>
/// This machine's heavy gates as <c>driver.json</c> keeps them, <c>heavyGates</c> (HEAVYRUN1b2a, D180's HEAVYRUN1b2a note;
/// FOUNDATION1 mod-iii): the key's shape, the sentences that say what does not read, and the reading and writing, by the settings' own
/// parser. What the mapping means, its edit and where it stands in a heavy run, stays with the mapping (<see cref="HeavyMapping"/>),
/// which answers through this for what it shares.
/// </summary>
public static class HeavyKey
{
    /// <summary>The file's key.</summary>
    public const string Key = "heavyGates";

    private const string Shape = "each is `<name>=<command>`, in the order they run.";

    /// <summary>The first problem of a list of gates, read as kept, in the words every door says; null for none.</summary>
    public static string? Problem(IReadOnlyList<HeavyGate> gates)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var gate in gates)
        {
            if (gate.Name.Length == 0) return $"a gate needs a name: {Shape}";
            if (gate.Run.Length == 0) return $"`{gate.Name}` needs the command it runs: {Shape}";
            if (!seen.Add(gate.Name)) return $"`{gate.Name}` is named twice: each gate is named once.";
            if (gate.WorkingDirectory is { } folder && !UnderRoot(folder))
            {
                return $"`{gate.Name}`'s folder, `{folder}`, is not under the repository's root: name it from there, with no `..`.";
            }
        }

        return null;
    }

    /// <summary>
    /// Whether a gate's folder is one under the repository's root, spelled alike on every platform: no <c>:</c> (a drive, a stream), not
    /// starting with <c>/</c> or <c>\</c> (rooted, a share), and no <c>..</c> part between either separator.
    /// </summary>
    public static bool UnderRoot(string folder) =>
        !folder.Contains(':', StringComparison.Ordinal)
        && !folder.StartsWith('/') && !folder.StartsWith('\\')
        && !folder.Split('/', '\\').Contains("..", StringComparer.Ordinal);

    /// <summary>
    /// The map as <c>driver.json</c> holds it: each repository's name trimmed, a blank one and a list not read passed over, a name written
    /// twice in any case read where first readable; none where the key is absent or no object.
    /// </summary>
    internal static IReadOnlyDictionary<string, IReadOnlyList<HeavyGate>> Map(JsonElement root)
    {
        var map = new Dictionary<string, IReadOnlyList<HeavyGate>>(StringComparer.OrdinalIgnoreCase);
        if (!root.TryGetProperty(Key, out var element) || element.ValueKind != JsonValueKind.Object) return map;

        foreach (var property in element.EnumerateObject())
        {
            var named = property.Name.Trim();
            if (named.Length == 0 || map.ContainsKey(named) || Kept(property.Value) is not { } gates) continue;
            map[named] = gates;
        }

        return map;
    }

    /// <summary>
    /// One repository's list as kept: a list of one gate or more, each an object whose <c>name</c> and <c>run</c> are text, whose
    /// <c>cwd</c> is absent, null or text, and the whole free of <see cref="Problem"/>; null where any of it does not read.
    /// </summary>
    internal static IReadOnlyList<HeavyGate>? Kept(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array) return null;
        var gates = new List<HeavyGate>();
        foreach (var entry in value.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object || Text(entry, "name") is not { } name || Text(entry, "run") is not { } run) return null;
            string? folder = null;
            if (entry.TryGetProperty("cwd", out var cwd) && cwd.ValueKind != JsonValueKind.Null)
            {
                if (cwd.ValueKind != JsonValueKind.String) return null;
                folder = cwd.GetString();
            }

            gates.Add(Gate(name, run, folder));
        }

        return gates.Count > 0 && Problem(gates) is null ? gates : null;
    }

    /// <summary>The map written as <c>driver.json</c> keeps it, only when set, repositories in ordinal order, each list in its own.</summary>
    internal static void WriteMap(Utf8JsonWriter writer, IReadOnlyDictionary<string, IReadOnlyList<HeavyGate>> map)
    {
        if (map.Count == 0) return;
        writer.WriteStartObject(Key);
        foreach (var (repository, gates) in map.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            writer.WriteStartArray(repository);
            foreach (var gate in gates)
            {
                writer.WriteStartObject();
                writer.WriteString("name", gate.Name);
                writer.WriteString("run", gate.Run);
                if (gate.WorkingDirectory is { } folder) writer.WriteString("cwd", folder);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    /// <summary>A gate as kept: trimmed, a blank folder none, heavy by being here.</summary>
    internal static HeavyGate Gate(string name, string run, string? folder) =>
        new(name.Trim(), run.Trim(), folder?.Trim() is { Length: > 0 } kept ? kept : null, Heavy: true);

    private static string? Text(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
