using System.Text.Json;
using static Daoris.Driver.Json;
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
/// <para><b>The rules are pure, the reading goes through <see cref="PluginFolders"/></b> (STRUCT3a, FLAT1b-i): a manifest's
/// text is parsed and the catalogue judged from what was read; <c>Load</c>, <c>ReadFolder</c> and <c>ReadAsWritten</c> read
/// the home through that helper, which does the I/O, so this file names none itself.</para>
///
/// <para>The CLI's <c>plugins.ts</c> was this class's twin and read the same folder by the same rules, until it left with
/// <c>daoris plugin</c>'s TypeScript (PUSH1f2f, D194): <c>daoris-driver plugin</c> reads through here. Three fields have readers of
/// their own, each held by its table: the icon
/// (<see cref="PluginIcon"/>) and the tools (<see cref="PluginTools"/>), whose problems never refuse a plugin, and the steps
/// with their resources (<see cref="PluginSteps"/>, FABRIC-UNIT1d), whose problems do.</para>
/// </remarks>
public sealed class PluginCatalog
{
    /// <summary>The plugin API this build speaks. Raised only when a plugin written for the new shape cannot work on the old one.</summary>
    public const int ApiVersion = 1;

    public const string ManifestName = "plugin.json";

    /// <summary>Where a plugin keeps what survives an update: `plugins/.data/&lt;id&gt;`, under the home's <see cref="HomeEntry.Plugins"/>.</summary>
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
    /// first by the driver, and a plugin may not declare its name. Since HELPPLUG1a, the ones declared for sessions.
    /// </summary>
    public IReadOnlyList<AcpMcpServer> Servers => ServersTo(PluginServerTo.Sessions);

    /// <summary>
    /// The servers the contributing plugins declare for Ask Daoris (HELPPLUG1a, D170 point 3), approved or not: what reaches it,
    /// and the rules for their tools, are the person's approvals' (<see cref="HelpGrants"/>).
    /// </summary>
    public IReadOnlyList<AcpMcpServer> HelpServers => ServersTo(PluginServerTo.Help);

    /// <summary>The servers the contributing plugins hand <paramref name="to"/>, in catalogue order, as the protocol door carries one.</summary>
    public IReadOnlyList<AcpMcpServer> ServersTo(string to) => Contributing
        .SelectMany(p => p.Manifest.Servers)
        .Where(s => s.HandedTo(to))
        .Select(s => new AcpMcpServer(s.Name, s.Command[0], s.Command.Skip(1).ToList(), s.Environment))
        .ToList();

    /// <summary>Whether a name has a plugin id's shape: lowercase letters, digits, dots and dashes, never leading with a dot or a dash.</summary>
    public static bool IsId(string id) => IdShape.IsMatch(id);

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
        var root = DaorisHome.Entry(home, HomeEntry.Plugins);
        if (PluginFolders.WithManifests(root) is not { } folders) return new([]);

        var disabled = new HashSet<string>(PluginState.Load(home).Disabled, StringComparer.OrdinalIgnoreCase);
        var reserved = ReservedWith(reservedHarnesses);
        var read = folders.Select(folder =>
        {
            var (manifest, problem) = Read(folder.Name, folder.Path, folder.Manifest);
            return (folder.Path, manifest, problem);
        });

        return Judge(root, read.ToList(), disabled, manifest => Refused(manifest, reserved));
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
    /// What <see cref="DataPlaceholder"/> expands to. Absent, it is the install layout's `.data/` beside
    /// the folder, which for a folder outside the home is somewhere nobody named.
    /// </param>
    public static (PluginManifest Manifest, string? Problem) ReadFolder(string folder, string? data = null)
    {
        var path = Path.Combine(folder, ManifestName);
        var (text, error) = PluginFolders.ReadText(path);
        var name = (text is null ? null : IdIn(text)) ?? PluginFolders.NameOf(folder);
        return text is null
            ? Unreadable(name, error!)
            : Parse(name, text, each => PluginFolders.Expand(each, folder, data));
    }

    // The build's own names are reserved whatever a caller hands (PLUGINRESERVE1): callers hand a set's doors, and a door list
    // alone misses an agent a door runs as, which let a plugin declare `codex` here while the CLI refused it.
    private static HashSet<string> ReservedWith(IEnumerable<string>? handed) =>
        new(Reserved.Concat(handed ?? []), StringComparer.OrdinalIgnoreCase);

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
        if (text is null) return Unreadable(folderName, error!);
        return Parse(folderName, text, folder is null ? null : each => PluginFolders.Expand(each, folder, data));
    }

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
            // (PLUGTOOL1a), not a step or a resource (FABRIC-UNIT1d). Its icon stays: it is how the person recognises the plugin the
            // sentence is about, never something the plugin contributes (D140 §3.1).
            if (problem is not null)
            {
                manifest = manifest with { Harnesses = [], Hooks = null, Servers = [], Skills = [], Steps = [], Resources = [], Tools = [], ToolsProblem = null };
            }

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
                        Maker: Text(row, "maker")?.Trim() is { Length: > 0 } maker ? maker : null,
                        // OWNSPACE1c (D175): only JSON true says the agent's own home is nobody's; the CLI reads it the same.
                        OwnHome: row.ValueKind == JsonValueKind.Object
                            && row.TryGetProperty("ownHome", out var ownHome) && ownHome.ValueKind == JsonValueKind.True));
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

                    // HELPPLUG1a (D170 point 3): who it is handed to, sessions where it says none, and the tools Ask Daoris may
                    // call, only where it is handed to Ask Daoris. Twin: `plugins.ts`, the same rules and words in the same order.
                    var to = DestinationsOf(row, $"server `{name}`", out var toProblem);
                    if (toProblem is not null) return (PluginManifest.Empty(id), toProblem);
                    var allow = ToolsOf(row, "allow");
                    if (allow is null)
                    {
                        return (PluginManifest.Empty(id),
                            $"server `{name}`'s `allow` must list the tools Ask Daoris may call, once each, by name: letters, digits, `_` and `-`.");
                    }

                    // HELPPLUG1a's tightening: the tools that change something outside Daoris, never allowed silently.
                    var writes = ToolsOf(row, "writes");
                    if (writes is null)
                    {
                        return (PluginManifest.Empty(id),
                            $"server `{name}`'s `writes` must list the tools that change something outside Daoris, once each, by name: letters, digits, `_` and `-`.");
                    }

                    if ((allow.Count > 0 || writes.Count > 0) && !to.Contains(PluginServerTo.Help, StringComparer.Ordinal))
                    {
                        return (PluginManifest.Empty(id), $"server `{name}` names tools for Ask Daoris but is not handed to it: its `to` must name `help`.");
                    }

                    if (allow.FirstOrDefault(tool => writes.Contains(tool, StringComparer.Ordinal)) is { } both)
                    {
                        return (PluginManifest.Empty(id),
                            $"server `{name}` names `{both}` in both `allow` and `writes`: a tool it hands Ask Daoris reads, or writes, never both.");
                    }

                    servers.Add(new PluginServer(name, command, environment) { To = to, Allow = allow, Writes = writes });
                }
            }

            // HELPPLUG1b (D170 point 4): the skills it hands, each a folder `skills/<name>` beside the manifest, and who to. Twin:
            // `plugins.ts`, the same rules and words in the same order; a problem refuses the plugin, as a server's does.
            var skills = new List<PluginSkill>();
            if (root.TryGetProperty("skills", out var carried))
            {
                if (carried.ValueKind != JsonValueKind.Array) return (PluginManifest.Empty(id), "`skills` must be an array.");
                foreach (var row in carried.EnumerateArray())
                {
                    var name = row.ValueKind == JsonValueKind.Object && row.TryGetProperty("name", out var named) && named.ValueKind == JsonValueKind.String
                        ? named.GetString()!
                        : null;
                    if (name is null || !PluginSkill.NameShape.IsMatch(name))
                    {
                        return (PluginManifest.Empty(id),
                            "a declared skill needs a `name` — lowercase letters, digits and dashes; its folder is `skills/<name>` beside the manifest.");
                    }

                    if (skills.Any(skill => skill.Name == name)) return (PluginManifest.Empty(id), $"skill `{name}` is declared twice.");
                    var to = DestinationsOf(row, $"skill `{name}`", out var toProblem);
                    if (toProblem is not null) return (PluginManifest.Empty(id), toProblem);
                    skills.Add(new PluginSkill(name) { To = to });
                }
            }

            // FABRIC-UNIT1d (§D9): its steps, each the one node declaration a built-in is, and the resources they create. Twin:
            // `steps.ts`, the same rules and words in the same order; a problem refuses the plugin, as a skill's does.
            var declaredSteps = PluginSteps.Read(id, root);
            if (declaredSteps.Problem is not null) return (PluginManifest.Empty(id), declaredSteps.Problem);

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
                Skills = skills,
                Steps = declaredSteps.Steps,
                Resources = declaredSteps.Resources,
                Tools = tools,
                ToolsProblem = toolsProblem,
            }, null);
        }
    }

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

    /// <summary>
    /// A tool's name as Ask Daoris's rules may name it (HELPPLUG1a): letters, digits, <c>_</c> and <c>-</c>, as the harness names
    /// a tool, so a rule made of one is a server's tool and never a command or a path. Twin: the CLI's <c>TOOL_NAME</c>.
    /// </summary>
    private static readonly Regex ToolName = new(@"^[A-Za-z0-9_-]{1,64}\z", RegexOptions.CultureInvariant);

    /// <summary>
    /// A server's or a skill's <c>to</c> (HELPPLUG1a, HELPPLUG1b): absent is sessions alone; otherwise a list naming each
    /// destination once, at least one, and none but <see cref="PluginServerTo.All"/>.
    /// </summary>
    /// <param name="said">The declaration as its problem names it: "server `x`" or "skill `x`".</param>
    private static IReadOnlyList<string> DestinationsOf(JsonElement declared, string said, out string? problem)
    {
        problem = null;
        if (!declared.TryGetProperty("to", out _)) return [PluginServerTo.Sessions];
        var to = Strings(declared, "to", text => text);
        if (to is not { Count: > 0 } || to.Distinct(StringComparer.Ordinal).Count() != to.Count
            || to.Any(each => !PluginServerTo.All.Contains(each, StringComparer.Ordinal)))
        {
            problem = $"{said}'s `to` must list who it is handed to, once each: `sessions`, `help` (Ask Daoris) or both.";
            return [];
        }

        return to;
    }

    /// <summary>A server's <c>allow</c> or <c>writes</c> (HELPPLUG1a): absent is none; otherwise tool names, each once; null where it breaks that.</summary>
    private static IReadOnlyList<string>? ToolsOf(JsonElement server, string field)
    {
        if (!server.TryGetProperty(field, out _)) return [];
        var tools = Strings(server, field, text => text);
        return tools is null || tools.Distinct(StringComparer.Ordinal).Count() != tools.Count || tools.Any(tool => !ToolName.IsMatch(tool))
            ? null
            : tools;
    }
}
