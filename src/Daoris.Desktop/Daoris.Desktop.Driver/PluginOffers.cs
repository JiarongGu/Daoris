using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// One of Daoris's own plugins the install offers (PLUG9 d, D102), read as the catalogue reads a plugin,
/// its placeholders as written.
/// </summary>
/// <param name="Problem">Why it could not be installed as it stands, in the catalogue's words; null when sound.</param>
/// <param name="Needs">What it needs on the machine: its README's own requirement lines.</param>
/// <param name="Installed">Whether a plugin of this id is installed here already.</param>
public sealed record PluginOffer(
    string Id, string Folder, PluginManifest Manifest, string? Problem, IReadOnlyList<string> Needs, bool Installed);

/// <summary>
/// Daoris's own example plugins, carried by the install as offers (PLUG9 d, D102): folders in the install's
/// <c>app/plugin-offers/</c>, which the publish lays out, never under the home's <c>plugins/</c>, so none
/// runs until a person installs it with the same copy <c>daoris plugin add</c> makes.
/// </summary>
/// <remarks>
/// <para><b>Where they are.</b> The application's own install first — the folder beside the running
/// executable, as the HTTP host is found beside the shell — and else the one beside the home, which an
/// install makes its <c>data/</c>. The CLI on a terminal has only the home, so it looks beside it; in an
/// install the two are one folder.</para>
///
/// <para>A twin of the CLI's <c>readOffers</c> and <c>readNeeds</c> in <c>plugins.ts</c>: the same folder, the
/// same reading, the same requirement lines, and each side's tests hold the same table.</para>
/// </remarks>
public static partial class PluginOffers
{
    /// <summary>The offers folder's place in an install, from its root. Twin: the CLI's <c>OFFERS_DIR</c>, the publish's <c>PLUGIN_OFFERS</c>.</summary>
    public static readonly IReadOnlyList<string> Layout = ["app", "plugin-offers"];

    /// <summary>The heading in an offer's README whose bullets say what it needs. Twin: the CLI's <c>NEEDS_HEADING</c>.</summary>
    public const string NeedsHeading = "## What it needs";

    /// <summary>The offers beside the home: its sibling <c>app/plugin-offers/</c>.</summary>
    public static string BesideHome(string home)
    {
        var full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(home));
        return Path.Combine([Path.GetDirectoryName(full) ?? full, .. Layout]);
    }

    /// <summary>The offers beside a running application, whose folder is the install's <c>app/</c>.</summary>
    public static string BesideApplication(string baseDirectory) =>
        Path.Combine(Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDirectory)), Layout[^1]);

    /// <summary>The offers an application sees: beside it where it has them, else beside the home.</summary>
    public static string FolderFor(string home, string baseDirectory) =>
        Directory.Exists(BesideApplication(baseDirectory)) ? BesideApplication(baseDirectory) : BesideHome(home);

    /// <summary>The offers in a folder, by folder name; an unsound one is listed with why, as the catalogue lists a plugin.</summary>
    public static IReadOnlyList<PluginOffer> Load(string? folder, string home, IEnumerable<string> reservedHarnesses)
    {
        if (folder is null || !Directory.Exists(folder)) return [];
        var reserved = reservedHarnesses.ToList();
        var offers = new List<PluginOffer>();
        foreach (var offered in Directory.EnumerateDirectories(folder).OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            var name = Path.GetFileName(offered)!;
            if (name.StartsWith('.')) continue;
            var manifestPath = Path.Combine(offered, PluginCatalog.ManifestName);
            if (!File.Exists(manifestPath)) continue;

            var (manifest, problem) = PluginCatalog.ReadAsWritten(name, manifestPath);
            problem ??= PluginCatalog.RefusedByThisBuild(manifest, reserved);
            offers.Add(new PluginOffer(
                name, offered, manifest, problem, Needs(offered),
                File.Exists(Path.Combine(home, PluginCatalog.Folder, name, PluginCatalog.ManifestName))));
        }

        return offers;
    }

    /// <summary>
    /// What a plugin needs on the machine, in its README's words: the bullets under <see cref="NeedsHeading"/>,
    /// a wrapped bullet joined into one line. Emphasis is dropped, since neither door draws it; code is kept.
    /// </summary>
    public static IReadOnlyList<string> Needs(string folder)
    {
        var path = Path.Combine(folder, "README.md");
        if (!File.Exists(path)) return [];
        var lines = File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, line => line.TrimEnd() == NeedsHeading);
        if (start < 0) return [];

        var needs = new List<string>();
        foreach (var raw in lines.Skip(start + 1))
        {
            if (raw.StartsWith('#')) break;
            var line = raw.Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal)) needs.Add(line[2..].Trim());
            else if (needs.Count > 0 && char.IsWhiteSpace(raw[0])) needs[^1] += $" {line}";
        }

        return [.. needs.Select(need => Bold().Replace(need, "$1"))];
    }

    [GeneratedRegex(@"\*\*([^*]+)\*\*")]
    private static partial Regex Bold();
}
