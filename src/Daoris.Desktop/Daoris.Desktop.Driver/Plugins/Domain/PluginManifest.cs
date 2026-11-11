namespace Daoris.Driver;

/// <summary>
/// One configuration of the ACP door a plugin declares — the adapter seam's own fields, as a
/// file. A fifth harness arrives this way rather than as Daoris code.
/// </summary>
/// <param name="Name">What `driver.json` and the roster call it. Refused if this build carries it.</param>
/// <param name="Command">The default command, `${plugin}` already expanded; the machine's `commands` row outranks it.</param>
/// <param name="Posture">The harness's own word for its ordinary posture, or null for a wire that carries none.</param>
/// <param name="ProfileVariable">The environment variable that makes a directory an account.</param>
/// <param name="Product">
/// What a person calls the tool it runs, as the plugin says: trimmed, and null where it says none, says
/// a blank or says something that is not text. Never a reason to refuse the plugin.
/// </param>
/// <param name="Maker">
/// Who makes that tool, as the plugin says, read as <paramref name="Product"/> is: the plugin's word, by which its agent may count
/// as another maker's for a second opinion. Null is a maker not declared, which is never taken
/// as independent.
/// </param>
/// <param name="OwnHome">
/// The plugin's word that its agent's own home is nobody's, a sandboxed tool: the agent that names a
/// <paramref name="ProfileVariable"/> otherwise always takes a Daoris account. Only JSON <c>true</c> says it; never a reason to
/// refuse the plugin. A built-in harness never reads it. Twin: the CLI's <c>ownHome</c>.
/// </param>
/// <param name="FullPosture">
/// The harness's own mode that asks nothing, as <c>posture</c> names its ordinary one: the word
/// a full-access start hands it, where a start would otherwise be refused naming the agent. Trimmed; null where none is declared.
/// </param>
/// <param name="AskingPosture">
/// The harness's own mode that asks the person: the word a start under the asking posture hands it, so a request it holds
/// reaches the person; null where none is declared, and such a start is refused naming the agent.
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
    string? Maker = null,
    bool OwnHome = false,
    string? FullPosture = null,
    string? AskingPosture = null);

/// <summary>
/// What a plugin speaks: the process, and the points it listens on; and whether its process is handed Daoris's own browser's DevTools
/// address (<see cref="PluginBrowser"/>) to make its calls from the person's signed-in page.
/// </summary>
public sealed record PluginHooks(IReadOnlyList<string> Command, IReadOnlyList<string> Points, bool Browser = false);

/// <summary>
/// An MCP server a plugin hands to sessions — a browser, a ticket system, whatever the session should be able to
/// reach — and, where it says so, to Ask Daoris. Beside the knowledge host, never instead of it.
/// </summary>
/// <param name="Name">What the agent calls it — tools arrive as `mcp__&lt;name&gt;__&lt;tool&gt;`. The knowledge host's name is refused.</param>
/// <param name="Command">The program and its arguments, `${plugin}` already expanded.</param>
/// <param name="Environment">What the server is started with, as declared.</param>
public sealed record PluginServer(
    string Name, IReadOnlyList<string> Command, IReadOnlyDictionary<string, string> Environment)
{
    /// <summary>
    /// Who it is handed to: <see cref="PluginServerTo.Sessions"/>, <see cref="PluginServerTo.Help"/> or both, as
    /// declared; absent is sessions alone. Twin: the CLI's <c>PluginServer.to</c>.
    /// </summary>
    public IReadOnlyList<string> To { get; init; } = [PluginServerTo.Sessions];

    /// <summary>
    /// The tools of it that read, which Ask Daoris may call once the person approves them; none
    /// where it declares none. Only a server handed to Ask Daoris declares any. Twin: the CLI's <c>PluginServer.allow</c>.
    /// </summary>
    public IReadOnlyList<string> Allow { get; init; } = [];

    /// <summary>
    /// The tools of it that change something outside Daoris: never allowed silently, only as writes the
    /// person accepted (<see cref="HelpGrants"/>). A tool is in <see cref="Allow"/> or here, never both. Twin: <c>PluginServer.writes</c>.
    /// </summary>
    public IReadOnlyList<string> Writes { get; init; } = [];

    /// <summary>Whether it is handed to <paramref name="to"/>.</summary>
    public bool HandedTo(string to) => To.Contains(to, StringComparer.Ordinal);
}

/// <summary>
/// A skill a plugin hands: its folder <c>skills/&lt;name&gt;</c> beside the manifest, holding a
/// <c>SKILL.md</c>, and who it is handed to, as a server's <c>to</c> says it. Handed at spawn (<see cref="SpawnSkills"/>), never
/// into a repository. Twin: the CLI's <c>PluginSkill</c>.
/// </summary>
/// <param name="Name">Its folder's name, as a harness names a skill: lowercase letters, digits and dashes.</param>
public sealed record PluginSkill(string Name)
{
    /// <summary>A skill's name: lowercase letters, digits and dashes, at most 64. Twin: the CLI's <c>SKILL_NAME</c>.</summary>
    public static readonly System.Text.RegularExpressions.Regex NameShape =
        new(@"^[a-z0-9][a-z0-9-]{0,63}\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>Who it is handed to: <c>sessions</c>, <c>help</c> or both; absent in the manifest is sessions.</summary>
    public IReadOnlyList<string> To { get; init; } = [PluginServerTo.Sessions];

    /// <summary>Whether it is handed to <paramref name="to"/>.</summary>
    public bool HandedTo(string to) => To.Contains(to, StringComparer.Ordinal);
}


/// <summary>A plugin's manifest, as read — with everything a refused plugin would have contributed already removed.</summary>
/// <param name="Icon">
/// Its icon: a path inside its folder to an SVG or a PNG, as the manifest writes it, once the
/// manifest's rules hold. Null with none, and with one those rules refuse. <c>PluginIcon.Read</c> judges the file.
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
    /// The tools its process runs, each read or saying its first problem.
    /// None for a refused plugin. <see cref="PluginToolChecks"/> finds and checks them, only at a trial or a press.
    /// </summary>
    public IReadOnlyList<PluginTool> Tools { get; init; } = [];

    /// <summary>Why <c>tools</c> as a whole is not read, when it is not an array; never a reason to refuse the plugin.</summary>
    public string? ToolsProblem { get; init; }

    /// <summary>The skills it hands sessions or Ask Daoris; none for a refused plugin.</summary>
    public IReadOnlyList<PluginSkill> Skills { get; init; } = [];

    /// <summary>
    /// The steps it declares: each a node of its own kind, <c>&lt;id&gt;/&lt;step&gt;</c>,
    /// read into the one <see cref="NodeDeclaration"/> a built-in is and judged by its <see cref="NodeDeclarations.Problem"/>. None for a
    /// refused plugin, or one that declares none. <see cref="PluginSteps"/> reads them. Twin: the CLI's <c>steps</c>.
    /// </summary>
    public IReadOnlyList<NodeDeclaration> Steps { get; init; } = [];

    /// <summary>
    /// What its steps create that a unit's cleanup must account for, each by schema, with its role and who cleans it. None for a
    /// refused plugin, or one that declares none. Twin: the CLI's <c>resources</c>.
    /// </summary>
    public IReadOnlyList<PluginResource> Resources { get; init; } = [];

    /// <summary>The systems it adapts, each reached only through its console (<see cref="PluginSystems"/>); none for a refused plugin.</summary>
    public IReadOnlyList<PluginSystem> Systems { get; init; } = [];

    /// <summary>The interfaces it implements, each reached only as declared (<see cref="PluginInterfaces"/>); none for a refused plugin.</summary>
    public PluginInterfacesDeclared Interfaces { get; init; } = PluginInterfacesDeclared.None;

    /// <summary>The old ids it declares, whose records Daoris moves to it once (<see cref="PluginRenames"/>); none for a refused plugin.</summary>
    public IReadOnlyList<string> Formerly { get; init; } = [];

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
    PluginManifest Manifest, string Folder, string Data, bool Enabled, string? Problem) : IWorkplacePluginEntry
{
    string IWorkplacePluginEntry.Id => Manifest.Id;

    IReadOnlyList<string> IWorkplacePluginEntry.Points => Manifest.Hooks?.Points ?? [];

    /// <summary>Enabled and sound: what the host actually drives.</summary>
    public bool Contributes => Enabled && Problem is null;
}
