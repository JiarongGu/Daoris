using System.Xml.Linq;

namespace Daoris.Driver;

/// <summary>What a package file says it is, read before anything is extracted (the distribution design §5.7 step 1).</summary>
/// <param name="Package">Its package id: the publisher's, global on a package source, never the plugin's id.</param>
/// <param name="Version">Its version, which must be its plugin's (§5.1).</param>
/// <param name="ApiVersion">The plugin API it speaks: the major number of its <c>DaorisPlugin</c> type's version.</param>
/// <param name="Sha512">The SHA-512 of the whole file in standard base64, as a NuGet catalog leaf's <c>packageHash</c> is.</param>
public sealed record PluginPackageRead(string Package, string Version, int ApiVersion, string Sha512);

/// <summary>A package installed: its plugin's manifest as written, and the record of where it came from.</summary>
public sealed record PluginPackageInstalled(PluginManifest Manifest, PluginPackageOrigin Origin);

/// <summary>A refusal of a package as it stands; the caller says whether anything was installed.</summary>
internal sealed class PluginPackageRefusal(string message) : Exception(message);

/// <summary>
/// A Daoris plugin package's words judged (PLUGDIST1a, D120; the distribution design §5.1 and §5.7 step 1): its one nuspec
/// at the root, the nuspec's id, version, type and dependencies, and every entry under <see cref="Root"/>, each refused by
/// name, before anything is written. Pure (STRUCT3a): the file is opened, hashed and read by
/// <see cref="PluginPackageFile"/>, which hands its entries' names and its nuspec here.
/// </summary>
internal static class PluginPackageRules
{
    /// <summary>The package type a Daoris plugin is (D120 §4). NuGet compares type names without case.</summary>
    public const string Type = "DaorisPlugin";

    /// <summary>Where a package keeps its plugin folder.</summary>
    public const string Root = "plugin/";

    /// <summary>A package file's extension.</summary>
    public const string Extension = ".nupkg";

    /// <summary>How a package is named in a refusal: its id and version.</summary>
    public static string Said(string package, string version) => $"package `{package}` {version}";

    /// <summary>The place, among a package's entries, of its one nuspec at the root.</summary>
    /// <param name="names">Each entry's name as the zip writes it.</param>
    /// <param name="file">The package file's own name, for the refusal.</param>
    /// <exception cref="PluginPackageRefusal">No nuspec at its root, or more than one.</exception>
    public static int Nuspec(IReadOnlyList<string> names, string file)
    {
        var nuspecs = names
            .Select((name, at) => (At: at, Name: name.Replace('\\', '/')))
            .Where(entry => !entry.Name.Contains('/') && entry.Name.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (nuspecs.Count == 0) throw new PluginPackageRefusal($"{file} is not a package: it holds no `.nuspec` at its root.");
        if (nuspecs.Count > 1) throw new PluginPackageRefusal($"{file} is not a package: it holds {nuspecs.Count} `.nuspec` files at its root, and a package holds one.");
        return nuspecs[0].At;
    }

    /// <summary>
    /// §5.1 and §5.7 step 1, once the nuspec is read: what the package says it is, then every entry under <see cref="Root"/>,
    /// its path inside the plugin folder in <c>/</c> form and whether it is a folder, by its place among the entries.
    /// </summary>
    /// <param name="nuspecName">The nuspec's name, for a refusal.</param>
    /// <param name="names">Each entry's name as the zip writes it.</param>
    /// <param name="file">The package file's own name, for a refusal.</param>
    /// <exception cref="PluginPackageRefusal">What the package is not, by name.</exception>
    public static (PluginPackageRead Read, IReadOnlyList<(int Entry, string Path, bool Folder)> Parts) Judge(
        XDocument document, string nuspecName, IReadOnlyList<string> names, string file, string sha512)
    {
        var metadata = Child(document.Root, "metadata")
            ?? throw new PluginPackageRefusal($"{file}'s `{nuspecName}` has no `metadata` — a nuspec names its package there.");
        var id = Child(metadata, "id")?.Value.Trim() ?? "";
        if (!PluginSource.IsPackageId(id)) throw new PluginPackageRefusal($"{file}'s id `{id}` is not a package id.");
        var version = Child(metadata, "version")?.Value.Trim() ?? "";
        if (!PluginSource.IsPackageVersion(version)) throw new PluginPackageRefusal($"package `{id}`'s version `{version}` is not a package version.");
        var said = Said(id, version);

        // The type names what the package is; a package without it is not a plugin, whatever its name says.
        var types = (Child(metadata, "packageTypes")?.Elements() ?? [])
            .Where(element => element.Name.LocalName == "packageType")
            .Select(element => (Name: element.Attribute("name")?.Value ?? "", Version: element.Attribute("version")?.Value))
            .ToList();
        var own = types.Where(type => string.Equals(type.Name, Type, StringComparison.OrdinalIgnoreCase)).ToList();
        if (own.Count == 0)
        {
            throw new PluginPackageRefusal(types.Count == 0
                ? $"{said} is not a Daoris plugin: it declares no package type, which NuGet reads as a library (`Dependency`), and a plugin's is `{Type}`."
                : $"{said} is not a Daoris plugin: it is of the type {Names(types.Select(type => type.Name))}, and a plugin's is `{Type}`.");
        }

        var others = types.Where(type => !string.Equals(type.Name, Type, StringComparison.OrdinalIgnoreCase)).Select(type => type.Name).ToList();
        if (others.Count > 0) throw new PluginPackageRefusal($"{said} is of the type `{Type}` and {Names(others)} — a Daoris plugin package is of that type alone.");

        // The type's version is the plugin API (D120 §4), read before anything is extracted: D64's rule, both numbers named.
        var typeVersion = own[0].Version;
        if (string.IsNullOrWhiteSpace(typeVersion))
        {
            throw new PluginPackageRefusal($"{said} names no version for its type `{Type}`, which is the plugin API it speaks, like `{PluginCatalog.ApiVersion}.0`.");
        }

        if (!Version.TryParse(typeVersion, out var api))
        {
            throw new PluginPackageRefusal($"{said}: its type's version `{typeVersion}` is not a version like `1.0`, whose first number is the plugin API it speaks.");
        }

        if (api.Major > PluginCatalog.ApiVersion)
        {
            throw new PluginPackageRefusal($"{said} needs plugin API {api.Major}, and this build speaks {PluginCatalog.ApiVersion} — "
                + $"update Daoris, or use a plugin written for {PluginCatalog.ApiVersion}.");
        }

        if (api.Major < 1) throw new PluginPackageRefusal($"{said}: its type's version `{typeVersion}` names no plugin API — its first number is the API, like `1.0`.");

        // No dependencies (§5.1): a plugin carries everything it runs, and Daoris installs nothing else.
        var dependencies = (Child(metadata, "dependencies")?.Descendants() ?? [])
            .Where(element => element.Name.LocalName == "dependency")
            .Select(element => element.Attribute("id")?.Value ?? "")
            .ToList();
        if (dependencies.Count > 0)
        {
            throw new PluginPackageRefusal($"{said} declares dependencies ({Names(dependencies)}) — a plugin carries everything it runs, "
                + "and Daoris installs nothing else.");
        }

        // The plugin/ guard: every part judged before one is written.
        var parts = new List<(int Entry, string Path, bool Folder)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var at = 0; at < names.Count; at++)
        {
            var name = names[at];
            // As NuGet reads a part's name: unescaped, then either slash a separator.
            var read = Uri.UnescapeDataString(name.Replace('\\', '/')).Replace('\\', '/');
            if (!read.StartsWith(Root, StringComparison.Ordinal)) continue;
            var inside = read[Root.Length..];
            if (inside.Length == 0) continue;
            var folder = inside.EndsWith('/');
            var path = folder ? inside.TrimEnd('/') : inside;
            if (!Tools.IsInside(path))
            {
                throw new PluginPackageRefusal($"{said} holds `{name}`, which would land outside its plugin's folder — the whole package is refused.");
            }

            // An escape can spell any character, NUL among them, which no path holds.
            if (path.Any(char.IsControl))
            {
                throw new PluginPackageRefusal($"{said} holds `{name}`, a name with a control character, which no folder holds — the whole package is refused.");
            }

            if (!folder && !seen.Add(path))
            {
                throw new PluginPackageRefusal($"{said} holds `{name}` twice, as this machine compares names — the whole package is refused.");
            }

            parts.Add((at, path, folder));
        }

        if (!parts.Any(part => !part.Folder && part.Path == PluginCatalog.ManifestName))
        {
            throw new PluginPackageRefusal($"{said} holds no `{Root}{PluginCatalog.ManifestName}` — a plugin package keeps its plugin folder under `{Root}`.");
        }

        return (new PluginPackageRead(id, version, api.Major, sha512), parts);
    }

    private static XElement? Child(XElement? element, string name) =>
        element?.Elements().FirstOrDefault(child => child.Name.LocalName == name);

    private static string Names(IEnumerable<string> names) => string.Join(" and ", names.Select(each => $"`{each}`"));
}
