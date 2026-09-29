using System.Text.Json;

namespace Daoris.Knowledge;

/// <summary>
/// The folder scan, demoted from an authority to a bootstrap (D48 §3): it reads a folder's
/// subdirectories and <em>proposes</em> registrations for them.
/// </summary>
/// <remarks>
/// <para>A scan is a fine way to find repositories and a terrible way to decide which ones count. What
/// it does not say governs as much as what it says — a repository renamed on disk went on being served
/// for weeks (FIX-LOG 2026-09-19) — and one root folder cannot express membership that does not follow
/// disk layout. So the scan became a verb: someone runs it, sees what it found, and the registry keeps
/// the result.</para>
///
/// <para><b>It proposes no workspace.</b> A scan knows nothing about circles, and an import that said
/// `default` would re-point every wired repository the moment someone re-imported a folder. Unstated
/// is preserved on upsert, which is exactly what makes re-importing safe.</para>
/// </remarks>
public static class RegistryImport
{
    /// <summary>Every immediate subdirectory of a folder, read as a registration proposal.</summary>
    public static IReadOnlyList<Registration> Propose(string folder)
    {
        if (!Directory.Exists(folder)) return [];

        var proposed = new List<Registration>();
        foreach (var directory in Directory.GetDirectories(folder).OrderBy(d => d, StringComparer.Ordinal))
        {
            var name = new DirectoryInfo(directory).Name;
            var manifest = Path.Combine(directory, "daoris.json");

            proposed.Add(File.Exists(manifest)
                ? ReadManifest(name, manifest, directory)
                // Present in the family, not adopted. Worth proposing rather than hiding: "who could I
                // ask, and who cannot be asked yet" is the same question, and a silent omission reads
                // as the repository not existing.
                : new Registration(name, false, null, [], [], [], Entries: 0, Root: directory));
        }

        return proposed;
    }

    private static Registration ReadManifest(string name, string manifest, string directory)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifest));
            var root = document.RootElement;
            var domain = root.TryGetProperty("domain", out var d) && d.ValueKind == JsonValueKind.Object
                ? d
                : (JsonElement?)null;
            var remote = root.TryGetProperty("remote", out var m) && m.ValueKind == JsonValueKind.Object
                ? m
                : (JsonElement?)null;
            var joined = remote is not null && Bool(remote.Value, "join");

            return new Registration(
                name,
                Adopted: true,
                Summary: domain is null ? null : String(domain.Value, "summary"),
                Owns: domain is null ? [] : Strings(domain.Value, "owns"),
                Accepts: domain is null ? [] : Strings(domain.Value, "accepts"),
                Packs: Strings(root, "packs"),
                Entries: 0,
                Root: directory,
                Joined: joined,
                // Knowledge feeds only from a joined repository (D47 §4). The CLI refuses this
                // manifest; an import reads manifests the CLI never validated, so it narrows too.
                SharesKnowledge: joined && remote is not null && Bool(remote.Value, "knowledge"),
                // What it says it uses (D91), read by the one rule every door applies.
                Uses: Declared.Uses(domain is null ? [] : Strings(domain.Value, "uses"), name));
        }
        // JSON of the wrong shape (an array, a string) throws InvalidOperationException from the element
        // reads, and is as broken as JSON that will not parse (REV3).
        catch (Exception error) when (error is JsonException or InvalidOperationException)
        {
            // A manifest that will not parse is the repository's own problem and its own tooling will
            // say so. Here it means only that we cannot read the declaration — which is not a reason to
            // drop the repository off the map.
            return new Registration(name, true, null, [], [], [], Entries: 0, Root: directory);
        }
    }

    private static bool Bool(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string? String(JsonElement element, string name) =>
        JsonFields.Text(element, name) is { Length: > 0 } text ? text : null;

    private static IReadOnlyList<string> Strings(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.Array) return [];

        var items = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { Length: > 0 } text) items.Add(text);
        }

        return items;
    }
}
