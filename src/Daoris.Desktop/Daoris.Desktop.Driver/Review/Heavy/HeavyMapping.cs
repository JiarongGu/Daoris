using System.Text.Json;

namespace Daoris.Driver;

/// <summary>
/// This machine's heavy gates for a repository that marks none of its own (HEAVYRUN1b2a, D180's HEAVYRUN1b2a note): <c>heavyGates</c>
/// in <c>driver.json</c>, by repository, each a list of gates in the order they run, each its name, its command and its folder under
/// the repository's root, heavy by being there. Read, edited and written here, the one place the terminal's <c>heavy</c>, a heavy run
/// and a driven start ask, held to <c>fixtures/heavy-gates.json</c> (the CLI's twin left in PUSH1f2b).
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
    public const string Key = HeavyKey.Key;

    private const string Scope = "this machine's heavy gates are set for a repository: name one.";

    /// <summary>A repository's heavy gates set in order, or taken away with none, through <see cref="Apply"/>.</summary>
    /// <exception cref="DriverException">The edit's refusal, nothing changed.</exception>
    public static DriverConfig WithHeavyGates(this DriverConfig config, string repository, IReadOnlyList<HeavyGate>? gates) => Apply(config, repository, gates);

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
            var kept = gates.Select(gate => HeavyKey.Gate(gate.Name, gate.Run, gate.WorkingDirectory)).ToList();
            if (HeavyKey.Problem(kept) is { } problem) throw new DriverException(problem);
            changed[key] = kept;
        }

        return config with { HeavyGates = changed };
    }

    /// <summary>The first problem of a list of gates, read as kept, in the words every door says; null for none (the settings' <see cref="HeavyKey.Problem"/>).</summary>
    public static string? Problem(IReadOnlyList<HeavyGate> gates) => HeavyKey.Problem(gates);

    /// <summary>Whether a gate's folder is one under the repository's root, spelled alike on every platform (the settings' <see cref="HeavyKey.UnderRoot"/>).</summary>
    public static bool UnderRoot(string folder) => HeavyKey.UnderRoot(folder);

    /// <summary>The map as <c>driver.json</c> holds it (the settings' <see cref="HeavyKey.Map"/>).</summary>
    internal static IReadOnlyDictionary<string, IReadOnlyList<HeavyGate>> Map(JsonElement root) => HeavyKey.Map(root);

    /// <summary>One repository's list as kept (the settings' <see cref="HeavyKey.Kept"/>).</summary>
    internal static IReadOnlyList<HeavyGate>? Kept(JsonElement value) => HeavyKey.Kept(value);

    /// <summary>The map written as <c>driver.json</c> keeps it (the settings' <see cref="HeavyKey.WriteMap"/>).</summary>
    internal static void WriteMap(Utf8JsonWriter writer, IReadOnlyDictionary<string, IReadOnlyList<HeavyGate>> map) => HeavyKey.WriteMap(writer, map);
}
