namespace Daoris.Driver;

/// <summary>
/// A plugin's page (PLUGUI1d, D119 §3.2 and §4.1): what it declares, as written; each point's kind, wait and whether the
/// running process listens; a landing plugin's rules and readiness; its source and whether an update waits; when it
/// was installed; its data folder counted up to a bound; the tests its folder carries; and its health. One reader for
/// both doors: the page's <c>PLUGIN</c> and <c>daoris-driver plugins show</c>.
/// </summary>
/// <remarks>
/// <para>🔴 <b>An environment variable's value never leaves the reader</b> (D119 §4.1): a server's <c>env</c> is
/// answered by name. Nothing here answers the raw manifest.</para>
///
/// <para><b>A refused plugin is read as written</b> and marked not taken; one whose manifest does not read answers
/// its sentence and nothing it declares. The catalogue takes nothing of either (<c>PluginCatalog.Load</c>).</para>
///
/// <para><b>Its health is the loop's record where the caller runs the loop</b>, and the machine log's last word
/// where it does not (D119 §2): the shell hands its <see cref="PluginHealth"/>, a terminal hands none.</para>
///
/// <para>The use case (STRUCT3a): the page's records are the Domain's, the folders it counts and walks
/// are <see cref="PluginFolders"/>'.</para>
/// </remarks>
public static class PluginPage
{
    /// <summary>How many entries of a data folder are counted before the count stops and says so.</summary>
    public const int CountBound = 10_000;

    /// <summary>How long a data folder is counted before the count stops and says so.</summary>
    public static readonly TimeSpan CountTime = TimeSpan.FromSeconds(2);

    /// <summary>The sentence for an id this machine holds no plugin under: the page's gone state, and the terminal's refusal.</summary>
    public static string Unknown(string id) => $"no plugin `{id}` on this machine — `daoris plugin list` shows what there is.";

    /// <param name="health">The loop's record, where the caller runs the loop; null reads the machine log under the home.</param>
    /// <param name="offers">The install's offers folder, where an offer's update is found; null finds it as the shell does.</param>
    /// <exception cref="DriverException">This machine holds no such plugin.</exception>
    public static PluginPageRead Read(string home, string id, PluginHealth? health = null, string? offers = null)
    {
        var (entry, manifest, catalog) = Installed(home, id);
        var plugin = entry.Manifest.Id;
        // HELPPLUG1a's tightening: what it offers Ask Daoris, with the person's approval as it stands.
        var grants = HelpGrants.Read(home, catalog).All.Where(grant => string.Equals(grant.Plugin, plugin, StringComparison.OrdinalIgnoreCase)).ToList();
        PageServer Granted(PageServer page, PluginServer server)
        {
            if (!server.HandedTo(PluginServerTo.Help)) return page;
            var grant = grants.FirstOrDefault(each => string.Equals(each.Server.Name, server.Name, StringComparison.Ordinal));
            return page with
            {
                Help = grant?.State ?? (HelpGrants.Refusal(server) is null ? HelpGrants.Waits : HelpGrants.Refused),
                Changed = grant?.Changed ?? false,
                Accepted = grant?.Accepted ?? [],
                Refusal = grant?.Refusal ?? HelpGrants.Refusal(server),
            };
        }

        var said = health is not null ? health.Of(entry) : PluginHealth.FromLog(entry, home);

        // A process runs now when the loop's is up; with none, whether a point is listened on has no answer.
        var up = said.Listening.Count > 0;
        var points = (manifest.Hooks?.Points ?? []).Select(point => PluginKit.Find(point) is { } kit
                ? new PagePoint(point, kit.Kind, (long)kit.Patience.TotalMilliseconds, up ? said.Listening.Contains(point, StringComparer.Ordinal) : null)
                : new PagePoint(point, null, null, up ? said.Listening.Contains(point, StringComparer.Ordinal) : null))
            .ToList();

        return new PluginPageRead(
            plugin, manifest.Name, manifest.Version, manifest.Description, entry.Enabled, entry.Problem, entry.Problem is null,
            entry.Folder, PluginFolders.MadeAt(entry.Folder), said, health is not null ? "loop" : "log",
            manifest.Hooks is { } hooks ? new PageHook(hooks.Command, hooks.Points) : null,
            points,
            [.. manifest.Harnesses.Select(harness => new PageAgent(
                harness.Name, harness.Command, "protocol", harness.Posture, harness.ProfileVariable, harness.Package, harness.Install))],
            [.. manifest.Servers.Select(server => Granted(new PageServer(
                server.Name, server.Command,
                // Names only: the value is read for nothing but whether it names the browser.
                [.. server.Environment.Keys.Order(StringComparer.Ordinal)],
                server.Command.Any(DrivesBrowser) || server.Environment.Values.Any(DrivesBrowser))
            {
                To = server.To, Allow = server.Allow, Writes = server.Writes,
            }, server))],
            manifest.Hooks?.Points.Contains(HookPoints.Land, StringComparer.Ordinal) == true ? Landing(home, plugin, catalog) : null,
            Source(home, entry, offers),
            Count(entry.Data, CountBound, CountTime),
            PluginFolders.Tests(entry.Folder, CountBound))
        {
            // FABRIC-UNIT1e (§D9): its steps and what they create, as written.
            Steps = manifest.Steps,
            Resources = manifest.Resources,
        };
    }

    /// <summary>
    /// An installed plugin by its id: the catalogue's entry, its manifest as written (placeholders left as they are,
    /// read even where the catalogue refused it), and the catalogue it was found in.
    /// </summary>
    /// <exception cref="DriverException">This machine holds no such plugin.</exception>
    internal static (PluginEntry Entry, PluginManifest Manifest, PluginCatalog Catalog) Installed(string home, string id)
    {
        var catalog = PluginCatalog.Load(home, AdapterSet.Built().Names);
        var entry = catalog.Plugins.FirstOrDefault(p => string.Equals(p.Manifest.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new DriverException(Unknown(id));
        var (written, _) = PluginCatalog.ReadAsWritten(entry.Manifest.Id, Path.Combine(entry.Folder, PluginCatalog.ManifestName));
        return (entry, written, catalog);
    }

    /// <summary>
    /// A folder's files and bytes, counted up to <paramref name="bound"/> entries or for <paramref name="time"/>,
    /// whichever comes first, with <see cref="PageData.More"/> set past either (<see cref="PluginFolders.Count"/>).
    /// </summary>
    internal static PageData Count(string folder, int bound, TimeSpan time) => PluginFolders.Count(folder, bound, time);

    private static bool DrivesBrowser(string text) => text.Contains(InAppBrowserServers.Placeholder, StringComparison.Ordinal);

    /// <summary>The rules in <c>driver.json</c> that name the plugin, a repository's before a workspace's, and whether it can land work here.</summary>
    private static PageLanding Landing(string home, string plugin, PluginCatalog catalog)
    {
        var config = DriverConfig.Load(DriverConfig.ResolvePath(home));
        IEnumerable<PageRule> Naming(IReadOnlyDictionary<string, LandingRule> rules, string scope) => rules
            .Where(pair => string.Equals(pair.Value.Plugin, plugin, StringComparison.OrdinalIgnoreCase))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => new PageRule(scope, pair.Key, pair.Value.Pattern));
        return new PageLanding(
            [.. Naming(config.Landings, LandingSource.Repository), .. Naming(config.WorkspaceLandings, LandingSource.Workspace)],
            LandingRules.PluginProblem(plugin, catalog));
    }

    /// <summary>Where it came from, and what an update would do: <c>waits</c> with what changes, <c>current</c>, or the refusal.</summary>
    private static PageSource Source(string home, PluginEntry entry, string? offers)
    {
        var (source, problem) = PluginSource.Read(entry.Folder);
        if (problem is not null) return new("unread", null, null, problem, null, [], null);
        if (source is null) return new("none", null, null, null, null, [], null);

        var (plan, refusal) = PluginInstall.PlanUpdate(
            home, entry.Manifest.Id, AdapterSet.Built().Names, offers ?? PluginOffers.FolderFor(home, AppContext.BaseDirectory));
        return new(
            source.Offer is not null ? "offer" : "folder", source.Folder, source.Offer, null,
            plan is null ? null : plan.Changes.Count > 0 ? "waits" : "current",
            plan?.Changes ?? [],
            refusal);
    }
}
