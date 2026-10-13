using System.Text.Json;
using System.Text.RegularExpressions;

namespace Daoris.Driver;

/// <summary>
/// The plugins under the home's `plugins/`, judged the way Yaorin's host judges its catalogue: the host
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
/// <para><b>The rules are this class's, the reading is not</b> (STRUCT3a): a manifest's text is parsed and the
/// catalogue judged here, from what was read; <c>PluginCatalog.Load</c>, <c>ReadFolder</c> and <c>ReadAsWritten</c>
/// read the home through the module's Infrastructure, and are declared in the Application's
/// <see cref="PluginCatalogReads"/>, where its callers name them as they always did.</para>
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

    /// <summary>How a manifest's text is parsed: as written by hand, a trailing comma and a comment forgiven.</summary>
    private static readonly JsonDocumentOptions Forgiving = new()
    {
        AllowTrailingCommas = true,
        CommentHandling = JsonCommentHandling.Skip,
    };

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

    internal PluginCatalog(IReadOnlyList<PluginEntry> plugins)
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

    /// <summary>Whether a name has a plugin id's shape: lowercase letters, digits, dots and dashes, never leading with a dot or a dash.</summary>
    public static bool IsId(string id) => IdShape.IsMatch(id);

    /// <summary>
    /// The catalogue of what was read under <paramref name="root"/>, in the order read: each plugin's folder, its manifest and
    /// the problem its reading found. A plugin the person switched off is listed and judged no further; one this build refuses,
    /// or one declaring an agent or a server an earlier plugin (by id) declared, contributes nothing.
    /// </summary>
    /// <param name="root">The home's `plugins/`, under which each plugin's data folder is `.data/&lt;id&gt;`.</param>
    /// <param name="disabled">The ids switched off, compared as the set compares them.</param>
    /// <param name="refusedByThisBuild">What this build refuses in a manifest whatever else is installed, or null.</param>
    internal static PluginCatalog Judge(
        string root,
        IEnumerable<(string Folder, PluginManifest Manifest, string? Problem)> read,
        IReadOnlySet<string> disabled,
        Func<PluginManifest, string?> refusedByThisBuild)
    {
        var declaredBy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var servedBy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<PluginEntry>();

        foreach (var (folder, written, found) in read)
        {
            var manifest = written;
            var problem = found;
            var enabled = !disabled.Contains(manifest.Id);

            if (problem is null && enabled)
            {
                // What this build refuses whatever else is installed, asked first, as the CLI's twin
                // and `daoris plugin add` ask it (PLUG9: the driver's add asks the same question).
                problem = refusedByThisBuild(manifest);
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

    /// <summary>A manifest that could not be read at all, from the file or as JSON: its folder's name and the reason.</summary>
    internal static (PluginManifest Manifest, string? Problem) Unreadable(string folderName, string reason) =>
        (PluginManifest.Empty(folderName), $"`{ManifestName}` could not be read: {reason}");

    /// <summary>
    /// The id a manifest's text names, as written: null where it is not JSON, not an object or names no id in text. An id of
    /// another shape is returned as it is, for the rules to refuse by name.
    /// </summary>
    internal static string? IdIn(string text)
    {
        try
        {
            using var probe = JsonDocument.Parse(text, Forgiving);
            return Text(probe.RootElement, "id");
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// One manifest's text, parsed by the catalogue's rules: the version before anything else, then the id held to the folder
    /// it is (or will be) installed as, then each thing it declares. A problem names what is wrong and takes nothing.
    /// </summary>
    /// <param name="folderName">The id it must carry: the name of the folder it is, or will be, installed as.</param>
    /// <param name="expand">
    /// What each command word and environment value is expanded with, <see cref="Placeholder"/> and
    /// <see cref="DataPlaceholder"/> to the folders on this machine; absent, they are left as written.
    /// </param>
    internal static (PluginManifest Manifest, string? Problem) Parse(string folderName, string text, Func<string, string>? expand = null)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(text, Forgiving);
        }
        catch (JsonException error)
        {
            return Unreadable(folderName, error.Message);
        }

        var expanded = expand ?? (each => each);
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

                    var command = Strings(row, "command", expanded);
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
                        Install: Strings(row, "install", expanded),
                        VersionArguments: Strings(row, "versionArguments", expanded),
                        AccountOf: Text(row, "accountOf"),
                        // XAGENT1b: the plugin's word for its tool and its maker; a manifest written before says neither.
                        Product: Text(row, "product")?.Trim() is { Length: > 0 } product ? product : null,
                        Maker: Text(row, "maker")?.Trim() is { Length: > 0 } maker ? maker : null));
                }
            }

            PluginHooks? hooks = null;
            if (root.TryGetProperty("hooks", out var spoken))
            {
                var command = spoken.ValueKind == JsonValueKind.Object ? Strings(spoken, "command", expanded) : null;
                var points = spoken.ValueKind == JsonValueKind.Object ? Strings(spoken, "points", expanded) : null;
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

                    var command = Strings(row, "command", expanded);
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

                            environment[pair.Name] = expanded(pair.Value.GetString()!);
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

    /// <summary>A string array, each entry expanded as <paramref name="expand"/> says.</summary>
    private static IReadOnlyList<string>? Strings(JsonElement element, string name, Func<string, string> expand)
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
            items.Add(expand(item.GetString()!));
        }

        return items;
    }
}
