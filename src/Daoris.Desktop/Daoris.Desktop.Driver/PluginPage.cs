using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// One declared point: its kind and how long the driver waits there (the kit's words), and whether the process that
/// runs now listens on it, or null where no process runs to say.
/// </summary>
public sealed record PagePoint(string Name, string? Kind, long? WaitMs, bool? Listening);

/// <summary>A plugin's hook as written: its command, `${plugin}` left as written, and the points it declares.</summary>
public sealed record PageHook(IReadOnlyList<string> Command, IReadOnlyList<string> Points);

/// <summary>An agent a plugin declares, as written: its way in is the protocol door, and its posture null where its wire names none (ACP3).</summary>
public sealed record PageAgent(
    string Name, IReadOnlyList<string> Command, string WayIn, string? Posture, string? ProfileVariable, string? Package,
    IReadOnlyList<string>? Install);

/// <summary>
/// A server a plugin hands every session, as written: its command, and its environment variables by NAME only
/// (D119 §4.1), since a value may be a key. Whether it drives Daoris's browser, which is withheld where no shell
/// answers (D78).
/// </summary>
public sealed record PageServer(string Name, IReadOnlyList<string> Command, IReadOnlyList<string> Environment, bool DrivesBrowser);

/// <summary>A landing rule that names the plugin: a repository's or a workspace's, and its pattern.</summary>
public sealed record PageRule(string Scope, string Name, string? Pattern);

/// <summary>A landing plugin's rules, and why it cannot land work here now, or null when it can (<see cref="LandingRules.PluginProblem"/>).</summary>
public sealed record PageLanding(IReadOnlyList<PageRule> Rules, string? Problem);

/// <summary>
/// Where it came from (D103): <c>folder</c>, <c>offer</c>, <c>none</c> or <c>unread</c>; whether an update
/// <c>waits</c> or it is <c>current</c> (null where it cannot be told), what would change, and an update's refusal.
/// </summary>
public sealed record PageSource(
    string Kind, string? Folder, string? Offer, string? Problem, string? Update, IReadOnlyList<PluginChange> Changes, string? Refusal);

/// <summary>What a plugin keeps: its data folder, whether it exists, its files and bytes counted up to a bound, and its newest change.</summary>
/// <param name="More">Whether the count stopped at its bound: the files and bytes are then at least this many.</param>
public sealed record PageData(string Folder, bool Exists, int Files, long Bytes, bool More, DateTimeOffset? Changed);

/// <summary>What <see cref="PluginPage.Read"/> answers for one installed plugin.</summary>
/// <param name="Taken">Whether the driver takes what it declares: false for a refused plugin, whose manifest is shown as written.</param>
/// <param name="HealthFrom"><c>loop</c> where the loop's own record answered, <c>log</c> where the machine log's last word did.</param>
/// <param name="Tests">The test files its install folder carries, relative, as <c>node --test</c> would find them.</param>
public sealed record PluginPageRead(
    string Id, string Name, string Version, string Description, bool Enabled, string? Problem, bool Taken, string Folder,
    DateTimeOffset? InstalledAt, PluginHealthState Health, string HealthFrom, PageHook? Hook, IReadOnlyList<PagePoint> Points,
    IReadOnlyList<PageAgent> Agents, IReadOnlyList<PageServer> Servers, PageLanding? Landing, PageSource Source, PageData Data,
    IReadOnlyList<string> Tests);

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
/// its sentence and nothing it declares. The catalogue takes nothing of either (<see cref="PluginCatalog.Load"/>).</para>
///
/// <para><b>Its health is the loop's record where the caller runs the loop</b>, and the machine log's last word
/// where it does not (D119 §2): the shell hands its <see cref="PluginHealth"/>, a terminal hands none.</para>
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
        var said = health is not null ? health.Of(entry) : PluginHealth.FromLog(entry, home);

        // A process runs now when the loop's is up; with none, whether a point is listened on has no answer.
        var up = said.Listening.Count > 0;
        var points = (manifest.Hooks?.Points ?? []).Select(point => PluginKit.Find(point) is { } kit
                ? new PagePoint(point, kit.Kind, (long)kit.Patience.TotalMilliseconds, up ? said.Listening.Contains(point, StringComparer.Ordinal) : null)
                : new PagePoint(point, null, null, up ? said.Listening.Contains(point, StringComparer.Ordinal) : null))
            .ToList();

        return new PluginPageRead(
            plugin, manifest.Name, manifest.Version, manifest.Description, entry.Enabled, entry.Problem, entry.Problem is null,
            entry.Folder, InstalledAt(entry.Folder), said, health is not null ? "loop" : "log",
            manifest.Hooks is { } hooks ? new PageHook(hooks.Command, hooks.Points) : null,
            points,
            [.. manifest.Harnesses.Select(harness => new PageAgent(
                harness.Name, harness.Command, "protocol", harness.Posture, harness.ProfileVariable, harness.Package, harness.Install))],
            [.. manifest.Servers.Select(server => new PageServer(
                server.Name, server.Command,
                // Names only: the value is read for nothing but whether it names the browser.
                [.. server.Environment.Keys.Order(StringComparer.Ordinal)],
                server.Command.Any(DrivesBrowser) || server.Environment.Values.Any(DrivesBrowser)))],
            manifest.Hooks?.Points.Contains(HookPoints.Land, StringComparer.Ordinal) == true ? Landing(home, plugin, catalog) : null,
            Source(home, entry, offers),
            Count(entry.Data, CountBound, CountTime),
            Tests(entry.Folder));
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
    /// whichever comes first, with <see cref="PageData.More"/> set past either. A link is counted and never followed.
    /// </summary>
    internal static PageData Count(string folder, int bound, TimeSpan time)
    {
        if (!Directory.Exists(folder)) return new(folder, false, 0, 0, false, null);

        var clock = Stopwatch.StartNew();
        var files = 0;
        var bytes = 0L;
        var entries = 0;
        DateTimeOffset? changed = null;
        var waiting = new Stack<DirectoryInfo>([new DirectoryInfo(folder)]);
        while (waiting.Count > 0)
        {
            IEnumerable<FileSystemInfo> inside;
            try
            {
                inside = waiting.Pop().EnumerateFileSystemInfos();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var item in inside)
            {
                if (entries >= bound || clock.Elapsed > time) return new(folder, true, files, bytes, true, changed);
                entries++;
                var written = new DateTimeOffset(item.LastWriteTimeUtc, TimeSpan.Zero);
                if (changed is null || written > changed) changed = written;
                if (item is FileInfo file)
                {
                    files++;
                    bytes += file.Length;
                }
                else if (item is DirectoryInfo directory && !directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    waiting.Push(directory);
                }
            }
        }

        return new(folder, true, files, bytes, false, changed);
    }

    /// <summary>When its install folder was made: an install and an update each make it whole (D103).</summary>
    private static DateTimeOffset? InstalledAt(string folder)
    {
        try
        {
            return Directory.Exists(folder) ? new DateTimeOffset(Directory.GetCreationTimeUtc(folder), TimeSpan.Zero) : null;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static bool DrivesBrowser(string text) => text.Contains(InAppBrowserServers.Placeholder, StringComparison.Ordinal);

    /// <summary>The rules in <c>driver.json</c> that name the plugin, a repository's before a workspace's, and whether it can land work here.</summary>
    private static PageLanding Landing(string home, string plugin, PluginCatalog catalog)
    {
        var config = DriverConfig.Load(Path.Combine(home, "driver.json"));
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

    /// <summary>
    /// The test files an install folder carries, as <c>node --test</c> finds them by default: <c>*.test.*</c>,
    /// <c>*-test.*</c>, <c>*_test.*</c>, <c>test-*.*</c>, <c>test.*</c>, and any script under a <c>test</c> folder,
    /// in JavaScript, never under <c>node_modules</c>. Bounded as a data folder's count is.
    /// </summary>
    private static IReadOnlyList<string> Tests(string folder)
    {
        if (!Directory.Exists(folder)) return [];
        var found = new List<string>();
        var entries = 0;
        var waiting = new Stack<(DirectoryInfo Directory, string Relative, bool UnderTest)>([(new DirectoryInfo(folder), "", false)]);
        while (waiting.Count > 0 && entries < CountBound)
        {
            var (directory, relative, underTest) = waiting.Pop();
            IEnumerable<FileSystemInfo> inside;
            try
            {
                inside = directory.EnumerateFileSystemInfos();
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (var item in inside)
            {
                if (++entries > CountBound) break;
                var path = relative.Length == 0 ? item.Name : $"{relative}/{item.Name}";
                if (item is DirectoryInfo inner)
                {
                    if (inner.Name is "node_modules" || inner.Name.StartsWith('.') || inner.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;
                    waiting.Push((inner, path, underTest || inner.Name == "test"));
                }
                else if (IsTest(item.Name, underTest))
                {
                    found.Add(path);
                }
            }
        }

        return [.. found.Order(StringComparer.Ordinal)];
    }

    private static bool IsTest(string name, bool underTest)
    {
        var extension = Path.GetExtension(name);
        if (extension is not (".js" or ".mjs" or ".cjs")) return false;
        if (underTest) return true;
        var stem = Path.GetFileNameWithoutExtension(name);
        return stem == "test" || stem.StartsWith("test-", StringComparison.Ordinal)
            || stem.EndsWith(".test", StringComparison.Ordinal) || stem.EndsWith("-test", StringComparison.Ordinal)
            || stem.EndsWith("_test", StringComparison.Ordinal);
    }
}
