namespace Daoris.Driver;

/// <summary>
/// Daoris's own example plugins, carried by the install as offers (PLUG9 d, D103): folders in the install's
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
///
/// <para>The use case (STRUCT3a): the folders are read through <see cref="PluginFolders"/>, each manifest by the
/// catalogue's own rules, and a README's needs by <see cref="PluginOffer.NeedsIn"/>.</para>
/// </remarks>
public static class PluginOffers
{
    /// <summary>The offers folder's place in an install, from its root. Twin: the CLI's <c>OFFERS_DIR</c>, the publish's <c>PLUGIN_OFFERS</c>.</summary>
    public static readonly IReadOnlyList<string> Layout = ["app", "plugin-offers"];

    /// <summary>The heading in an offer's README whose bullets say what it needs. Twin: the CLI's <c>NEEDS_HEADING</c>.</summary>
    public const string NeedsHeading = PluginOffer.NeedsHeading;

    /// <summary>The offers in the home's Daoris folder (<see cref="DaorisHome.FolderOf"/>, HOMELAYOUT4): its <c>app/plugin-offers/</c>.</summary>
    public static string BesideHome(string home) =>
        Path.Combine([DaorisHome.FolderOf(PluginFolders.WholePath(home)).Folder, .. Layout]);

    /// <summary>The offers beside a running application, whose folder is the install's <c>app/</c>.</summary>
    public static string BesideApplication(string baseDirectory) =>
        Path.Combine(Path.TrimEndingDirectorySeparator(PluginFolders.WholePath(baseDirectory)), Layout[^1]);

    /// <summary>The offers an application sees: beside it where it has them, else beside the home.</summary>
    public static string FolderFor(string home, string baseDirectory) =>
        PluginFolders.There(BesideApplication(baseDirectory)) ? BesideApplication(baseDirectory) : BesideHome(home);

    /// <summary>The offers in a folder, by folder name; an unsound one is listed with why, as the catalogue lists a plugin.</summary>
    public static IReadOnlyList<PluginOffer> Load(string? folder, string home, IEnumerable<string> reservedHarnesses)
    {
        if (folder is null || PluginFolders.WithManifests(folder) is not { } offered) return [];
        var reserved = reservedHarnesses.ToList();
        var offers = new List<PluginOffer>();
        foreach (var (name, path, manifestPath) in offered)
        {
            var (manifest, problem) = PluginCatalog.ReadAsWritten(name, manifestPath);
            problem ??= PluginCatalog.RefusedByThisBuild(manifest, reserved);
            offers.Add(new PluginOffer(
                name, path, manifest, problem, Needs(path),
                PluginFolders.HasFile(DaorisHome.Entry(home, HomeEntry.Plugins, name, PluginCatalog.ManifestName))));
        }

        return offers;
    }

    /// <summary>
    /// What an installed plugin is against the offer of its own id (LANDPIN3): <see cref="PluginShipped"/> where it is older than the
    /// one this install ships, or the same version with files that differ; null where it needs no word, and where there is no
    /// sound offer of its id.
    /// </summary>
    public static PluginShipped? ShippedOf(string installFolder, PluginManifest installed, PluginOffer? offer) =>
        offer is { Problem: null } && string.Equals(offer.Id, installed.Id, StringComparison.OrdinalIgnoreCase)
            ? PluginShipped.Judge(
                installed.Version,
                offer.Manifest.Version,
                () => PluginFolders.SameFiles(installFolder, offer.Folder),
                PluginFolders.TextIfThere(Path.Combine(offer.Folder, PluginCatalog.ManifestName)) is { } text ? PluginShipped.ChangesIn(text) : null)
            : null;

    /// <summary>What a plugin needs on the machine, in its README's words (<see cref="PluginOffer.NeedsIn"/>); none without one.</summary>
    public static IReadOnlyList<string> Needs(string folder) =>
        PluginFolders.TextIfThere(Path.Combine(folder, "README.md")) is { } readme ? PluginOffer.NeedsIn(readme) : [];
}
