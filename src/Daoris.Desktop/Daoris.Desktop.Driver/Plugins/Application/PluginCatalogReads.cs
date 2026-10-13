namespace Daoris.Driver;

/// <summary>
/// The catalogue read from the home (D64): the plugins under its `plugins/`, each manifest read through the module's
/// Infrastructure (<see cref="PluginFolders"/>) and judged by <see cref="PluginCatalog"/>'s own rules.
/// </summary>
/// <remarks>
/// <para><b>Named where its callers always named it</b> (STRUCT3a, D166's note): <c>PluginCatalog.Load</c>,
/// <c>ReadFolder</c>, <c>ReadAsWritten</c>, <c>Reserved</c> and <c>RefusedByThisBuild</c> are static members of a C# 14
/// extension block on the Domain's <see cref="PluginCatalog"/>, declared here in the Application. The catalogue is a value
/// and its rules are pure; reading it is this layer's use of the home, and what this build refuses is this build's word.
/// The structure gate reads each member as this file's, so a Domain that called one would be refused.</para>
/// </remarks>
public static class PluginCatalogReads
{
    extension(PluginCatalog)
    {
        /// <summary>
        /// The harness names this build runs, which no plugin may declare (PLUGINRESERVE1, D64 rule 3): the built set's
        /// <see cref="AdapterSet.Reserved"/>, each door and each agent a door runs as, `codex` among them since CODEXACCT1 made it
        /// a holder rather than a door. Twin: the CLI's <c>reservedHarnesses()</c>; both suites hold their list to the driver
        /// suite's <c>fixtures/reserved-harnesses.json</c>.
        /// </summary>
        public static IReadOnlyList<string> Reserved => AdapterSet.Built().Reserved;

        /// <summary>
        /// Read the home's plugins.
        /// </summary>
        /// <param name="home">The Daoris home (D63).</param>
        /// <param name="reservedHarnesses">
        /// Harness names reserved beside <c>Reserved</c>, which is reserved whatever is handed: a set a test builds of its own
        /// adapters, say. A plugin declaring one is refused naming both.
        /// </param>
        public static PluginCatalog Load(string home, IEnumerable<string>? reservedHarnesses = null)
        {
            var root = Path.Combine(home, PluginCatalog.Folder);
            if (PluginFolders.WithManifests(root) is not { } folders) return new([]);

            var disabled = new HashSet<string>(PluginState.Load(home).Disabled, StringComparer.OrdinalIgnoreCase);
            var reserved = ReservedWith(reservedHarnesses);
            var read = folders.Select(folder =>
            {
                var (manifest, problem) = Read(folder.Name, folder.Path, folder.Manifest);
                return (folder.Path, manifest, problem);
            });

            return PluginCatalog.Judge(root, read.ToList(), disabled, manifest => Refused(manifest, reserved));
        }

        /// <summary>
        /// What in a manifest this build refuses, whatever else is installed: a harness it already carries,
        /// or a server under the knowledge host's name. Case-blind, as every name here is. The catalogue and
        /// the driver's add ask this one question (PLUG9), as the CLI's twin asks `refusedByThisBuild`.
        /// </summary>
        /// <param name="reservedHarnesses">Harness names reserved beside <c>Reserved</c>, which is reserved whatever is handed.</param>
        public static string? RefusedByThisBuild(PluginManifest manifest, IEnumerable<string> reservedHarnesses) =>
            Refused(manifest, ReservedWith(reservedHarnesses));

        /// <summary>
        /// One manifest read by the catalogue's own rules, from any folder, its placeholders left as written
        /// (PLUG9): what the driver's add reads before it copies, and what an Ask Daoris card shows, so the
        /// person sees `${plugin}` where the manifest says it and never a machine path.
        /// </summary>
        /// <param name="id">The id it must carry: the name of the folder it is, or will be, installed as.</param>
        public static (PluginManifest Manifest, string? Problem) ReadAsWritten(string id, string manifestPath) =>
            Read(id, folder: null, manifestPath);

        /// <summary>
        /// A manifest in a folder anywhere — a plugins repository's, before anything is installed (PLUG8) —
        /// read by the catalogue's own rules, with the manifest's id taken as the folder's name the way
        /// `daoris plugin add` reads a source folder: installing names the folder by the id, so a source
        /// folder may be called anything.
        /// </summary>
        /// <param name="data">
        /// What <see cref="PluginCatalog.DataPlaceholder"/> expands to. Absent, it is the install layout's `.data/` beside
        /// the folder, which for a folder outside the home is somewhere nobody named.
        /// </param>
        public static (PluginManifest Manifest, string? Problem) ReadFolder(string folder, string? data = null)
        {
            var path = Path.Combine(folder, PluginCatalog.ManifestName);
            var (text, error) = PluginFolders.ReadText(path);
            var name = (text is null ? null : PluginCatalog.IdIn(text)) ?? PluginFolders.NameOf(folder);
            return text is null
                ? PluginCatalog.Unreadable(name, error!)
                : PluginCatalog.Parse(name, text, each => PluginFolders.Expand(each, folder, data));
        }
    }

    // The build's own names are reserved whatever a caller hands (PLUGINRESERVE1): callers hand a set's doors, and a door list
    // alone misses an agent a door runs as, which let a plugin declare `codex` here while the CLI refused it.
    private static HashSet<string> ReservedWith(IEnumerable<string>? handed) =>
        new(PluginCatalog.Reserved.Concat(handed ?? []), StringComparer.OrdinalIgnoreCase);

    private static string? Refused(PluginManifest manifest, HashSet<string> reserved)
    {
        if (manifest.Harnesses.FirstOrDefault(harness => reserved.Contains(harness.Name)) is { } carried)
        {
            return $"declares agent `{carried.Name}`, which this build already carries — "
                + "a plugin adds an agent and never replaces one.";
        }

        if (manifest.Servers.FirstOrDefault(server =>
                string.Equals(server.Name, KnowledgeConnector.ServerName, StringComparison.OrdinalIgnoreCase)) is { } own)
        {
            return $"declares server `{own.Name}`, which is Daoris's own knowledge host — "
                + "a plugin hands a session servers beside it, never in its place.";
        }

        return null;
    }

    /// <param name="folder">The install folder the placeholders expand to, or null to leave them as written.</param>
    private static (PluginManifest Manifest, string? Problem) Read(string folderName, string? folder, string path, string? data = null)
    {
        var (text, error) = PluginFolders.ReadText(path);
        if (text is null) return PluginCatalog.Unreadable(folderName, error!);
        return PluginCatalog.Parse(folderName, text, folder is null ? null : each => PluginFolders.Expand(each, folder, data));
    }
}
