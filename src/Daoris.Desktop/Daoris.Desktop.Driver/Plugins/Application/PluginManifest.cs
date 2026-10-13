namespace Daoris.Driver;

/// <summary>
/// One configuration of the ACP door a plugin declares (D64 §3) — the adapter seam's own fields, as a
/// file. A fifth harness arrives this way rather than as Daoris code.
/// </summary>
/// <param name="Name">What `driver.json` and the roster call it. Refused if this build carries it.</param>
/// <param name="Command">The default command, `${plugin}` already expanded; the machine's `commands` row outranks it.</param>
/// <param name="Posture">D37 in this harness's own vocabulary, or null for a wire that carries none (ACP3).</param>
/// <param name="ProfileVariable">The environment variable that makes a directory an account (D49 §4).</param>
/// <param name="Product">
/// What a person calls the tool it runs, as the plugin says (XAGENT1b, D155 point 4): trimmed, and null where it says none, says
/// a blank or says something that is not text. Never a reason to refuse the plugin.
/// </param>
/// <param name="Maker">
/// Who makes that tool, as the plugin says, read as <paramref name="Product"/> is: the plugin's word, by which its agent may count
/// as another maker's for a second opinion (the second-agent design §3.1). Null is a maker not declared, which is never taken
/// as independent.
/// </param>
public sealed record PluginHarness(
    string Name,
    IReadOnlyList<string> Command,
    string? Posture = null,
    string? ProfileVariable = null,
    string? Package = null,
    IReadOnlyList<string>? Install = null,
    IReadOnlyList<string>? VersionArguments = null,
    string? AccountOf = null,
    string? Product = null,
    string? Maker = null);

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
