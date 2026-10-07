using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// One configuration of the ACP door a plugin declares (D64 §3) — the adapter seam's own fields, as a
/// file. A fifth harness arrives this way rather than as Daoris code.
/// </summary>
/// <param name="Name">What `driver.json` and the roster call it. Refused if this build carries it.</param>
/// <param name="Command">The default command, `${plugin}` already expanded; the machine's `commands` row outranks it.</param>
/// <param name="Posture">D37 in this harness's own vocabulary, or null for a wire that carries none (ACP3).</param>
/// <param name="ProfileVariable">The environment variable that makes a directory an account (D49 §4).</param>
public sealed record PluginHarness(
    string Name,
    IReadOnlyList<string> Command,
    string? Posture = null,
    string? ProfileVariable = null,
    string? Package = null,
    IReadOnlyList<string>? Install = null,
    IReadOnlyList<string>? VersionArguments = null,
    string? AccountOf = null);

/// <summary>What a plugin speaks (D64 §4): the process, and the points it listens on.</summary>
public sealed record PluginHooks(IReadOnlyList<string> Command, IReadOnlyList<string> Points);

/// <summary>
/// An MCP server a plugin hands to every session (D65 §1f) — a browser, a ticket system, whatever
/// the session should be able to reach. Beside the knowledge host, never instead of it.
/// </summary>
/// <param name="Name">What the agent calls it — tools arrive as `mcp__&lt;name&gt;__&lt;tool&gt;`. The knowledge host's name is refused.</param>
/// <param name="Command">The program and its arguments, `${plugin}` already expanded.</param>
/// <param name="Environment">What the server is started with, as declared.</param>
public sealed record PluginServer(
    string Name, IReadOnlyList<string> Command, IReadOnlyDictionary<string, string> Environment);

/// <summary>A plugin's manifest, as read — with everything a refused plugin would have contributed already removed.</summary>
/// <param name="Icon">
/// Its icon (PLUGUI2, D140 §3.1): a path inside its folder to an SVG or a PNG, as the manifest writes it, once the
/// manifest's rules hold. Null with none, and with one those rules refuse. <see cref="PluginIcon.Read"/> judges the file.
/// </param>
/// <param name="IconProblem">Why its declared icon is not drawn, by the manifest's rules; never a reason to refuse the plugin.</param>
public sealed record PluginManifest(
    string Id,
    int ApiVersion,
    string Name,
    string Version,
    string Description,
    IReadOnlyList<PluginHarness> Harnesses,
    PluginHooks? Hooks,
    IReadOnlyList<PluginServer> Servers,
    string? Icon = null,
    string? IconProblem = null)
{
    /// <summary>
    /// The tools its process runs (PLUGTOOL1a, D150 point 7; the UX6 design §7.2), each read or saying its first problem.
    /// None for a refused plugin. <see cref="PluginToolChecks"/> finds and checks them, only at a trial or a press.
    /// </summary>
    public IReadOnlyList<PluginTool> Tools { get; init; } = [];

    /// <summary>Why <c>tools</c> as a whole is not read, when it is not an array; never a reason to refuse the plugin.</summary>
    public string? ToolsProblem { get; init; }

    /// <summary>The manifest of a plugin nothing can be taken from: an id, and nothing else.</summary>
    public static PluginManifest Empty(string id) => new(id, PluginCatalog.ApiVersion, id, "", "", [], null, []);
}

/// <summary>
/// One plugin as the catalogue found it.
/// </summary>
/// <param name="Folder">Where it is installed — replaced wholesale by an update, so nothing of the plugin's own is written here.</param>
/// <param name="Data">What it keeps, beside the install and never touched by an update.</param>
/// <param name="Enabled">The person's word (`plugins.json`), independent of whether the plugin is sound.</param>
/// <param name="Problem">Why it contributes nothing, in a sentence — a version this build does not speak, a malformed manifest, a conflict. Null when sound.</param>
public sealed record PluginEntry(
    PluginManifest Manifest, string Folder, string Data, bool Enabled, string? Problem)
{
    /// <summary>Enabled and sound: what the host actually drives.</summary>
    public bool Contributes => Enabled && Problem is null;
}

/// <summary>
/// The plugins under the home's `plugins/`, read the way Yaorin's host reads its catalogue: the host
/// drives whatever is there and names no plugin (D64).
/// </summary>
/// <remarks>
/// <para><b>The version is read before anything else.</b> A manifest from a newer API is refused
/// naming both numbers and nothing of it is taken — the same rule `pack.json` has (PLUG1).</para>
///
/// <para><b>A broken plugin is a named problem, never a crash</b> — the loop that spends accounts does
/// not stop because somebody's manifest has a trailing comma. It is listed with its problem, so both
/// doors can say why it is missing rather than leaving a person to wonder.</para>
///
/// <para><b>A conflict is refused before anything loads, naming both sides.</b> A harness name this
/// build carries, or one an earlier plugin (by id) declared, makes the later plugin contribute nothing.
/// A plugin adds; it never replaces.</para>
///
/// <para>The CLI's <c>plugins.ts</c> is this class's twin and reads the same folder by the same rules;
/// they share no code and move together. Two fields have readers of their own, each held by its table: the icon
/// (<see cref="PluginIcon"/>) and the tools (<see cref="PluginTools"/>), whose problems never refuse a plugin.</para>
/// </remarks>
public sealed class PluginCatalog
{
    /// <summary>The plugin API this build speaks. Raised only when a plugin written for the new shape cannot work on the old one.</summary>
    public const int ApiVersion = 1;

    public const string ManifestName = "plugin.json";

    /// <summary>The folder under the home.</summary>
    public const string Folder = "plugins";

    /// <summary>Where a plugin keeps what survives an update: `plugins/.data/&lt;id&gt;`.</summary>
    public const string DataFolder = ".data";

    /// <summary>
    /// The host telling a plugin where it is: a plugin cannot work out its own folder, and one that
    /// guessed a relative path would resolve it against whatever directory happened to be current.
    /// </summary>
    public const string Placeholder = "${plugin}";

    /// <summary>
    /// The host telling a plugin where it KEEPS things (D77): its data folder, which an update never
    /// touches — a browser's signed-in profile, say, which otherwise lands under the user's profile,
    /// where nothing of Daoris's lives (D63). Twin: `plugins.ts`.
    /// </summary>
    public const string DataPlaceholder = "${data}";

    /// <summary>
    /// A plugin id's shape, which the CLI's <c>isPluginId</c> twins: it checks <see cref="IsId"/>, a manifest's id and a server's
    /// name. <c>\z</c>, since .NET's <c>$</c> also passes a final line break the CLI's refuses (CASEFOLD1e). Its one spelling in the
    /// driver (REFAC1): a landing rule's plugin, a plugin's tool id and the log's landing line ask <see cref="IsId"/>.
    /// </summary>
    private static readonly Regex IdShape = new(@"^[a-z0-9][a-z0-9.-]*\z", RegexOptions.CultureInvariant);

    /// <summary>Every plugin found, sound or not, in folder order by id.</summary>
    public IReadOnlyList<PluginEntry> Plugins { get; }

    /// <summary>The ones the host drives: enabled and sound.</summary>
    public IReadOnlyList<PluginEntry> Contributing { get; }

    /// <summary>
    /// What the set of contributing plugins IS — equal between two loads when nothing a plugin
    /// contributes has changed, so a loop can tell an edit between ticks from the same catalogue again.
    /// </summary>
    public string Signature { get; }

    /// <summary>A machine with no plugins: what a reader that was handed none judges against (HELP8).</summary>
    public static PluginCatalog None { get; } = new([]);

    private PluginCatalog(IReadOnlyList<PluginEntry> plugins)
    {
        Plugins = plugins;
        Contributing = plugins.Where(p => p.Contributes).ToList();
        Signature = string.Join("\n", Contributing.Select(p =>
            $"{p.Manifest.Id}\t{p.Manifest.Version}\t{p.Folder}\t"
            + string.Join(",", p.Manifest.Harnesses.Select(h => h.Name))
            + "\t" + (p.Manifest.Hooks is { } hooks ? string.Join(" ", hooks.Command) + "|" + string.Join(",", hooks.Points) : "")
            + "\t" + string.Join(",", p.Manifest.Servers.Select(s => s.Name))));
    }

    /// <summary>
    /// Every server the contributing plugins hand to a session, in catalogue order, as the protocol
    /// door carries one (ACP4). The knowledge host is not among them: it is Daoris's own, offered
    /// first by the driver, and a plugin may not declare its name.
    /// </summary>
    public IReadOnlyList<AcpMcpServer> Servers => Contributing
        .SelectMany(p => p.Manifest.Servers)
        .Select(s => new AcpMcpServer(s.Name, s.Command[0], s.Command.Skip(1).ToList(), s.Environment))
        .ToList();

    /// <summary>
    /// Read the home's plugins.
    /// </summary>
    /// <param name="home">The Daoris home (D63).</param>
    /// <param name="reservedHarnesses">The harness names this build carries — a plugin declaring one is refused naming both.</param>
    public static PluginCatalog Load(string home, IEnumerable<string>? reservedHarnesses = null)
    {
        var root = Path.Combine(home, Folder);
        if (!Directory.Exists(root)) return new([]);

        var disabled = new HashSet<string>(PluginState.Load(home).Disabled, StringComparer.OrdinalIgnoreCase);
        var reserved = new HashSet<string>(reservedHarnesses ?? [], StringComparer.OrdinalIgnoreCase);
        var declaredBy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var servedBy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<PluginEntry>();

        foreach (var folder in Directory.EnumerateDirectories(root).OrderBy(Path.GetFileName, StringComparer.Ordinal))
        {
            var folderName = Path.GetFileName(folder)!;
            // `.data/` and any other dot-folder is the catalogue's own, never a plugin.
            if (folderName.StartsWith('.')) continue;
            var manifestPath = Path.Combine(folder, ManifestName);
            if (!File.Exists(manifestPath)) continue;

            var (manifest, problem) = Read(folderName, folder, manifestPath);
            var enabled = !disabled.Contains(manifest.Id);

            if (problem is null && enabled)
            {
                // What this build refuses whatever else is installed, asked first, as the CLI's twin
                // and `daoris plugin add` ask it (PLUG9: the driver's add asks the same question).
                problem = RefusedByThisBuild(manifest, reserved);
                foreach (var harness in problem is null ? manifest.Harnesses : [])
                {
                    if (declaredBy.TryGetValue(harness.Name, out var other))
                    {
                        problem = $"declares agent `{harness.Name}`, which plugin `{other}` already declares — "
                            + "the first by id keeps it, and this plugin contributes nothing.";
                        break;
                    }
                }

                // A server's name is what the agent calls it, and two plugins claiming one would
                // give the session two tools under one name — the first by id keeps it.
                foreach (var server in problem is null ? manifest.Servers : [])
                {
                    if (servedBy.TryGetValue(server.Name, out var other))
                    {
                        problem = $"declares server `{server.Name}`, which plugin `{other}` already declares — "
                            + "the first by id keeps it, and this plugin contributes nothing.";
                        break;
                    }
                }

                if (problem is null)
                {
                    foreach (var harness in manifest.Harnesses) declaredBy[harness.Name] = manifest.Id;
                    foreach (var server in manifest.Servers) servedBy[server.Name] = manifest.Id;
                }
            }

            // Nothing of a refused plugin is taken — not a harness, not a hook, not a server, not a tool it would run
            // (PLUGTOOL1a). Its icon stays: it is how the person recognises the plugin the sentence is about, never something
            // the plugin contributes (D140 §3.1).
            if (problem is not null) manifest = manifest with { Harnesses = [], Hooks = null, Servers = [], Tools = [], ToolsProblem = null };

            entries.Add(new(manifest, folder, Path.Combine(root, DataFolder, manifest.Id), enabled, problem));
        }

        return new(entries);
    }

    /// <summary>
    /// What in a manifest this build refuses, whatever else is installed: a harness it already carries,
    /// or a server under the knowledge host's name. Case-blind, as every name here is. The catalogue and
    /// the driver's add ask this one question (PLUG9), as the CLI's twin asks `refusedByThisBuild`.
    /// </summary>
    public static string? RefusedByThisBuild(PluginManifest manifest, IEnumerable<string> reservedHarnesses)
    {
        var reserved = new HashSet<string>(reservedHarnesses, StringComparer.OrdinalIgnoreCase);
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

    /// <summary>
    /// One manifest read by the catalogue's own rules, from any folder, its placeholders left as written
    /// (PLUG9): what the driver's add reads before it copies, and what an Ask Daoris card shows, so the
    /// person sees `${plugin}` where the manifest says it and never a machine path.
    /// </summary>
    /// <param name="id">The id it must carry: the name of the folder it is, or will be, installed as.</param>
    public static (PluginManifest Manifest, string? Problem) ReadAsWritten(string id, string manifestPath) =>
        Read(id, folder: null, manifestPath);

    /// <summary>Whether a name has a plugin id's shape: lowercase letters, digits, dots and dashes, never leading with a dot or a dash.</summary>
    public static bool IsId(string id) => IdShape.IsMatch(id);

    /// <summary>
    /// A manifest in a folder anywhere — a plugins repository's, before anything is installed (PLUG8) —
    /// read by the catalogue's own rules, with the manifest's id taken as the folder's name the way
    /// `daoris plugin add` reads a source folder: installing names the folder by the id, so a source
    /// folder may be called anything.
    /// </summary>
    /// <param name="data">
    /// What <see cref="DataPlaceholder"/> expands to. Absent, it is the install layout's `.data/` beside
    /// the folder, which for a folder outside the home is somewhere nobody named.
    /// </param>
    public static (PluginManifest Manifest, string? Problem) ReadFolder(string folder, string? data = null)
    {
        var path = Path.Combine(folder, ManifestName);
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)));
        try
        {
            using var probe = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
            if (Text(probe.RootElement, "id") is { } id) name = id;
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            // Read below, which names what is wrong with it.
        }

        return Read(name, folder, path, data);
    }

    /// <param name="folder">The install folder the placeholders expand to, or null to leave them as written.</param>
    private static (PluginManifest Manifest, string? Problem) Read(string folderName, string? folder, string path, string? data = null)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip,
            });
        }
        catch (Exception error) when (error is JsonException or IOException or UnauthorizedAccessException)
        {
            return (PluginManifest.Empty(folderName), $"`{ManifestName}` could not be read: {error.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return (PluginManifest.Empty(folderName), $"`{ManifestName}` is not an object.");
            }

            var id = Text(root, "id") ?? folderName;

            // 🔴 The version before anything else: a plugin from a newer API is refused with both
            // numbers, and nothing below is read from it.
            var apiVersion = 1;
            if (root.TryGetProperty("apiVersion", out var version))
            {
                if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out apiVersion))
                {
                    return (PluginManifest.Empty(id),
                        "`apiVersion` must be an integer — this manifest is malformed, not old.");
                }

                if (apiVersion > ApiVersion)
                {
                    return (PluginManifest.Empty(id) with { ApiVersion = apiVersion },
                        $"needs plugin API {apiVersion}, and this build speaks {ApiVersion} — "
                        + $"update Daoris, or use a plugin written for {ApiVersion}.");
                }
            }

            if (!IdShape.IsMatch(id))
            {
                return (PluginManifest.Empty(folderName),
                    $"`id` must be lowercase letters, digits, dots and dashes — `{id}` is not.");
            }

            if (!string.Equals(id, folderName, StringComparison.Ordinal))
            {
                return (PluginManifest.Empty(id),
                    $"`id` is `{id}` but the folder is `{folderName}` — a plugin's folder is its id.");
            }

            var harnesses = new List<PluginHarness>();
            if (root.TryGetProperty("harnesses", out var declared))
            {
                if (declared.ValueKind != JsonValueKind.Array)
                {
                    return (PluginManifest.Empty(id), "`harnesses` must be an array.");
                }

                foreach (var row in declared.EnumerateArray())
                {
                    var name = Text(row, "name");
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        return (PluginManifest.Empty(id), "a declared agent needs a `name`.");
                    }

                    var command = Strings(row, "command", folder, data);
                    if (command is not { Count: > 0 })
                    {
                        return (PluginManifest.Empty(id), $"agent `{name}` needs a `command` — what to run.");
                    }

                    harnesses.Add(new PluginHarness(
                        name.Trim(),
                        command,
                        Posture: Text(row, "posture"),
                        ProfileVariable: Text(row, "profileVariable"),
                        Package: Text(row, "package"),
                        Install: Strings(row, "install", folder, data),
                        VersionArguments: Strings(row, "versionArguments", folder, data),
                        AccountOf: Text(row, "accountOf")));
                }
            }

            PluginHooks? hooks = null;
            if (root.TryGetProperty("hooks", out var spoken))
            {
                var command = spoken.ValueKind == JsonValueKind.Object ? Strings(spoken, "command", folder, data) : null;
                var points = spoken.ValueKind == JsonValueKind.Object ? Strings(spoken, "points", folder, data) : null;
                if (command is not { Count: > 0 } || points is not { Count: > 0 })
                {
                    return (PluginManifest.Empty(id), "`hooks` needs a `command` and the `points` it listens on.");
                }

                hooks = new PluginHooks(command, points);
            }

            var servers = new List<PluginServer>();
            if (root.TryGetProperty("servers", out var handed))
            {
                if (handed.ValueKind != JsonValueKind.Array)
                {
                    return (PluginManifest.Empty(id), "`servers` must be an array.");
                }

                foreach (var row in handed.EnumerateArray())
                {
                    var name = Text(row, "name")?.Trim();
                    if (string.IsNullOrWhiteSpace(name) || !IdShape.IsMatch(name))
                    {
                        return (PluginManifest.Empty(id),
                            "a declared server needs a `name` — lowercase letters, digits, dots and dashes; it is what the agent calls it.");
                    }

                    var command = Strings(row, "command", folder, data);
                    if (command is not { Count: > 0 })
                    {
                        return (PluginManifest.Empty(id), $"server `{name}` needs a `command` — what to run.");
                    }

                    var environment = new Dictionary<string, string>(StringComparer.Ordinal);
                    if (row.TryGetProperty("env", out var env))
                    {
                        if (env.ValueKind != JsonValueKind.Object)
                        {
                            return (PluginManifest.Empty(id), $"server `{name}`'s `env` must be an object of strings.");
                        }

                        foreach (var pair in env.EnumerateObject())
                        {
                            if (pair.Value.ValueKind != JsonValueKind.String)
                            {
                                return (PluginManifest.Empty(id), $"server `{name}`'s `env` must be an object of strings.");
                            }

                            environment[pair.Name] = Expand(pair.Value.GetString()!, folder, data);
                        }
                    }

                    servers.Add(new PluginServer(name, command, environment));
                }
            }

            // Its icon, last: an icon's problem is said and never refuses the plugin (D140 §3.1).
            var (icon, iconProblem) = root.TryGetProperty("icon", out var declaredIcon)
                ? PluginIcon.Declared(declaredIcon)
                : (null, null);

            // Its tools, after: a tool's problem is that tool's sentence and never refuses the plugin (D150 point 7).
            var (tools, toolsProblem) = root.TryGetProperty("tools", out var declaredTools)
                ? PluginTools.Read(declaredTools)
                : ([], null);

            return (new PluginManifest(
                id,
                apiVersion,
                Text(root, "name") ?? id,
                Text(root, "version") ?? "",
                Text(root, "description") ?? "",
                harnesses,
                hooks,
                servers,
                icon,
                iconProblem)
            {
                Tools = tools,
                ToolsProblem = toolsProblem,
            }, null);
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>A string array, with the plugin placeholder expanded to the install folder in every entry.</summary>
    private static IReadOnlyList<string>? Strings(JsonElement element, string name, string? folder, string? data)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = new List<string>();
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) return null;
            items.Add(Expand(item.GetString()!, folder, data));
        }

        return items;
    }

    /// <remarks>
    /// The data folder is the install folder's sibling under <see cref="DataFolder"/>, by the same name:
    /// a plugin's folder IS its id, so the two are one derivation and never disagree — unless a reader of
    /// a folder outside the home names where it keeps things (<see cref="ReadFolder"/>).
    /// </remarks>
    private static string Expand(string text, string? folder, string? data)
    {
        if (folder is null) return text;
        var plugin = text.Contains(Placeholder, StringComparison.Ordinal);
        var keeps = text.Contains(DataPlaceholder, StringComparison.Ordinal);
        if (!plugin && !keeps) return text;

        var install = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        var kept = data ?? Path.Combine(Path.GetDirectoryName(install)!, DataFolder, Path.GetFileName(install));
        return Path.GetFullPath(text
            .Replace(Placeholder, install, StringComparison.Ordinal)
            .Replace(DataPlaceholder, kept, StringComparison.Ordinal));
    }
}

/// <summary>
/// The machine's word about its plugins: which are disabled. A row, never a rename (D64 §5) — the
/// plugin stays where it is, its data stays where it is, and both doors show it present and off.
/// </summary>
public sealed record PluginState(IReadOnlyList<string> Disabled)
{
    public const string FileName = "plugins.json";

    public static string PathIn(string home) => System.IO.Path.Combine(home, FileName);

    public static PluginState Load(string home)
    {
        var path = PathIn(home);
        if (!File.Exists(path)) return new([]);

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var disabled = new List<string>();
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("disabled", out var rows)
                && rows.ValueKind == JsonValueKind.Array)
            {
                foreach (var row in rows.EnumerateArray())
                {
                    if (row.ValueKind == JsonValueKind.String && row.GetString() is { Length: > 0 } id) disabled.Add(id);
                }
            }

            return new(disabled);
        }
        catch (JsonException error)
        {
            // An unreadable state file disables nothing: the safe direction is the plugin the person
            // installed still running. An edit over it is refused rather than rewriting it whole.
            return new([]) { Problem = $"{FileName} could not be read ({error.Message})" };
        }
    }

    /// <summary>
    /// Why the file could not be read — null when it could, or when there was none. Reading it as
    /// "nothing disabled" keeps every installed plugin running, which is the safe direction for a READ;
    /// for an edit it would re-enable every plugin the person had switched off, so an edit is refused
    /// (REV3, and the CLI twin refuses the same edit).
    /// </summary>
    public string? Problem { get; init; }

    public static void Disable(string home, string id) =>
        Save(home, Editable(home).Disabled.Where(d => !Same(d, id)).Append(id).ToList());

    public static void Enable(string home, string id) =>
        Save(home, Editable(home).Disabled.Where(d => !Same(d, id)).ToList());

    /// <exception cref="DriverException">The file could not be read, so an edit would rewrite it whole.</exception>
    private static PluginState Editable(string home) =>
        Load(home) is { Problem: { } problem }
            ? throw new DriverException(
                $"{problem}, so this was not changed — writing it now would switch back on every plugin "
                + "it had switched off. Fix the file or remove it, then make the change again.")
            : Load(home);

    private static bool Same(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    /// <summary>Written beside and renamed over, like every file Daoris owns.</summary>
    private static void Save(string home, IReadOnlyList<string> disabled)
    {
        var path = PathIn(home);
        Directory.CreateDirectory(home);
        var json = JsonSerializer.Serialize(new { disabled = disabled.OrderBy(d => d, StringComparer.Ordinal) },
            new JsonSerializerOptions { WriteIndented = true });
        AtomicFile.WriteText(path, json.Replace("\r\n", "\n") + "\n");
    }
}

/// <summary>
/// A harness a plugin declared, riding the ACP door (D64 §3): the same door dsh and codex arrive by,
/// configured from a file rather than from Daoris code.
/// </summary>
/// <remarks>
/// <para>The posture is the manifest's, in the harness's own vocabulary, or null when its wire
/// carries none (ACP3) — never a guessed mode. The command resolves like every adapter's: the
/// machine's `commands` row first, then the declaration.</para>
///
/// <para>The toolchain rows are what the roster needs to show it beside the built-in harnesses: the
/// binary is the command's first word, so "is it installed" is asked of the thing that will run.</para>
/// </remarks>
public sealed class DeclaredAcpAdapter(PluginHarness harness, string plugin) : ISessionAdapter
{
    public string Name => harness.Name;

    /// <summary>The plugin it came from — what the roster shows beside it, and nothing a session record carries.</summary>
    public string Plugin => plugin;

    public SessionWire Wire => SessionWire.Acp;

    public string? AcpPosture => harness.Posture;

    public bool Interactive => true;

    public ProcessStartInfo Prepare(SessionTarget target, IReadOnlyList<string>? command)
    {
        var resolved = Resolve(command);
        return Spawning.InRoot(target, resolved[0], resolved.Skip(1), redirectInput: true);
    }

    public ProcessStartInfo PrepareChat(ChatTarget target, IReadOnlyList<string>? command)
    {
        var resolved = Resolve(command);
        return Spawning.ChatInRoot(target, resolved[0], resolved.Skip(1));
    }

    public HarnessToolchain? Toolchain => new(
        Binary: [harness.Command[0]],
        VersionArguments: harness.VersionArguments ?? [],
        ProfileVariable: harness.ProfileVariable,
        Install: harness.Install,
        Package: harness.Package,
        AccountOf: harness.AccountOf,
        // No version question declared: presence is asked of the file or PATH, never by running it.
        ProbeByPresence: harness.VersionArguments is null);

    private IReadOnlyList<string> Resolve(IReadOnlyList<string>? command) =>
        command is { Count: > 0 } ? command : harness.Command;
}

/// <summary>Whether a command would start, without starting it.</summary>
public static class CommandPresence
{
    public static bool Resolvable(string command) => Resolve(command) is not null;

    /// <summary>
    /// The file a command names — itself when it is a path, else the first match on <paramref name="path"/>
    /// (this process's own by default), with Windows's extensions tried — or null where there is none.
    /// </summary>
    /// <param name="startable">
    /// Only what Windows can START: npm puts an extensionless POSIX `npm` script beside `npm.cmd`, and
    /// the bare name found first is a file no Windows process can run.
    /// </param>
    public static string? Resolve(string command, string? path = null, bool startable = false)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;

        if (Path.IsPathRooted(command)
            || command.Contains(Path.DirectorySeparatorChar)
            || command.Contains(Path.AltDirectorySeparatorChar))
        {
            return File.Exists(command) ? command : null;
        }

        var pathExt = (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE;.CMD;.BAT;.COM")
            .Split(';', StringSplitOptions.RemoveEmptyEntries);
        IEnumerable<string> extensions = !OperatingSystem.IsWindows()
            ? [""]
            : startable ? pathExt : pathExt.Prepend("");
        var directories = (path ?? Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        foreach (var directory in directories)
        {
            foreach (var extension in extensions)
            {
                var candidate = Path.Combine(directory, command + extension);
                if (File.Exists(candidate)) return candidate;
            }
        }

        return null;
    }
}
