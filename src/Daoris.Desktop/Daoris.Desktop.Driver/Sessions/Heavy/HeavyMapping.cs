using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// This machine's heavy gates for a repository that marks none of its own (HEAVYRUN1b2a, D180's HEAVYRUN1b2a note): <c>heavyGates</c>
/// in <c>driver.json</c>, by repository, each a list of gates in the order they run, each its name, its command and its folder under
/// the repository's root, heavy by being there. Read, edited and written here, the one place the terminal's <c>heavy</c>, a heavy run
/// and a driven start ask; the CLI's <c>driverconfig.ts</c> reads and keeps it the same way (<c>fixtures/heavy-gates.json</c>).
/// </summary>
/// <remarks>
/// <para><b>Never an edit to the repository.</b> A repository with no CI beside often declares no gates file, and the owner's machine
/// is where its sessions run their full suites at once; the mapping is the owner's, kept with this machine's other choices. A command
/// may bound its own workers (<c>npm test -- --maxWorkers=2</c>), which is where an undeclared repository's workers are bounded.</para>
///
/// <para><b>Used only where the repository's own file marks no heavy gate</b> (<see cref="HeavyGates.Resolve"/>): a repository that
/// declares its heavy gates has said what they are. Full access reads none of it, so the slot is never bypassed.</para>
///
/// <para><b>Read as a stored shape</b>: a list is read only where every gate of it reads, since running part of a sequence the owner
/// set, in another order, would be a guess; what a gate has no field for is passed over. Keyed as <c>access</c> is: the repository's
/// name, trimmed, matched in any case, a key keeping the spelling first written.</para>
/// </remarks>
public static class HeavyMapping
{
    /// <summary>The file's key.</summary>
    public const string Key = "heavyGates";

    private const string Scope = "this machine's heavy gates are set for a repository: name one.";
    private const string Shape = "each is `<name>=<command>`, in the order they run.";

    /// <summary>A repository's gates, its name trimmed and matched in any case; null where it has none here.</summary>
    public static IReadOnlyList<HeavyGate>? For(DriverConfig config, string repository) =>
        config.HeavyGates.TryGetValue(repository.Trim(), out var gates) ? gates : null;

    /// <summary>
    /// One repository's gates set, in the order given, or taken away with none or null, the config changed and nothing else: each gate
    /// read as the file is read (its name, command and folder trimmed, a blank folder none), its first problem refused.
    /// </summary>
    /// <exception cref="DriverException">The first problem, in the words every door says: no repository, a gate with no name or no
    /// command, a name twice, a folder not under the repository's root.</exception>
    public static DriverConfig Apply(DriverConfig config, string? repository, IReadOnlyList<HeavyGate>? gates)
    {
        var named = repository?.Trim() is { Length: > 0 } r ? r : throw new DriverException(Scope);
        var key = config.HeavyGates.Keys.FirstOrDefault(k => string.Equals(k, named, StringComparison.OrdinalIgnoreCase)) ?? named;
        var changed = new Dictionary<string, IReadOnlyList<HeavyGate>>(config.HeavyGates, StringComparer.OrdinalIgnoreCase);
        if (gates is not { Count: > 0 })
        {
            changed.Remove(key);
        }
        else
        {
            var kept = gates.Select(gate => Gate(gate.Name, gate.Run, gate.WorkingDirectory)).ToList();
            if (Problem(kept) is { } problem) throw new DriverException(problem);
            changed[key] = kept;
        }

        return config with { HeavyGates = changed };
    }

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
    private static HeavyGate Gate(string name, string run, string? folder) =>
        new(name.Trim(), run.Trim(), folder?.Trim() is { Length: > 0 } kept ? kept : null, Heavy: true);

    private static string? Text(JsonElement entry, string name) =>
        entry.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
