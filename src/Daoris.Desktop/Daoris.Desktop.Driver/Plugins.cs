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
public sealed record PluginManifest(
    string Id,
    int ApiVersion,
    string Name,
    string Version,
    string Description,
    IReadOnlyList<PluginHarness> Harnesses,
    PluginHooks? Hooks,
    IReadOnlyList<PluginServer> Servers)
{
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
/// they share no code and move together.</para>
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

    private static readonly Regex IdShape = new("^[a-z0-9][a-z0-9.-]*$", RegexOptions.CultureInvariant);

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
                foreach (var harness in manifest.Harnesses)
                {
                    if (reserved.Contains(harness.Name))
                    {
                        problem = $"declares harness `{harness.Name}`, which this build already carries — "
                            + "a plugin adds a harness and never replaces one.";
                        break;
                    }

                    if (declaredBy.TryGetValue(harness.Name, out var other))
                    {
                        problem = $"declares harness `{harness.Name}`, which plugin `{other}` already declares — "
                            + "the first by id keeps it, and this plugin contributes nothing.";
                        break;
                    }
                }

                // A server's name is what the agent calls it, and two plugins claiming one would
                // give the session two tools under one name — the first by id keeps it. The
                // knowledge host's name is Daoris's own and is refused outright.
                foreach (var server in problem is null ? manifest.Servers : [])
                {
                    if (string.Equals(server.Name, KnowledgeConnector.ServerName, StringComparison.OrdinalIgnoreCase))
                    {
                        problem = $"declares server `{server.Name}`, which is Daoris's own knowledge host — "
                            + "a plugin hands a session servers beside it, never in its place.";
                        break;
                    }

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

            // Nothing of a refused plugin is taken — not a harness, not a hook, not a server.
            if (problem is not null) manifest = manifest with { Harnesses = [], Hooks = null, Servers = [] };

            entries.Add(new(manifest, folder, Path.Combine(root, DataFolder, manifest.Id), enabled, problem));
        }

        return new(entries);
    }

    private static (PluginManifest Manifest, string? Problem) Read(string folderName, string folder, string path)
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
                        return (PluginManifest.Empty(id), "a declared harness needs a `name`.");
                    }

                    var command = Strings(row, "command", folder);
                    if (command is not { Count: > 0 })
                    {
                        return (PluginManifest.Empty(id), $"harness `{name}` needs a `command` — what to run.");
                    }

                    harnesses.Add(new PluginHarness(
                        name.Trim(),
                        command,
                        Posture: Text(row, "posture"),
                        ProfileVariable: Text(row, "profileVariable"),
                        Package: Text(row, "package"),
                        Install: Strings(row, "install", folder),
                        VersionArguments: Strings(row, "versionArguments", folder),
                        AccountOf: Text(row, "accountOf")));
                }
            }

            PluginHooks? hooks = null;
            if (root.TryGetProperty("hooks", out var spoken))
            {
                var command = spoken.ValueKind == JsonValueKind.Object ? Strings(spoken, "command", folder) : null;
                var points = spoken.ValueKind == JsonValueKind.Object ? Strings(spoken, "points", folder) : null;
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

                    var command = Strings(row, "command", folder);
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

                            environment[pair.Name] = Expand(pair.Value.GetString()!, folder);
                        }
                    }

                    servers.Add(new PluginServer(name, command, environment));
                }
            }

            return (new PluginManifest(
                id,
                apiVersion,
                Text(root, "name") ?? id,
                Text(root, "version") ?? "",
                Text(root, "description") ?? "",
                harnesses,
                hooks,
                servers), null);
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object
        && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>A string array, with the plugin placeholder expanded to the install folder in every entry.</summary>
    private static IReadOnlyList<string>? Strings(JsonElement element, string name, string folder)
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
            items.Add(Expand(item.GetString()!, folder));
        }

        return items;
    }

    /// <remarks>
    /// The data folder is the install folder's sibling under <see cref="DataFolder"/>, by the same name:
    /// a plugin's folder IS its id, so the two are one derivation and never disagree.
    /// </remarks>
    private static string Expand(string text, string folder)
    {
        var plugin = text.Contains(Placeholder, StringComparison.Ordinal);
        var data = text.Contains(DataPlaceholder, StringComparison.Ordinal);
        if (!plugin && !data) return text;

        var install = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        var kept = Path.Combine(Path.GetDirectoryName(install)!, DataFolder, Path.GetFileName(install));
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
