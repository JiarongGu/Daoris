using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's room (HELP1a, D89): the folder under Daoris's home its conversation runs in, written
/// from the machine at every open — what Daoris is, which screen and which terminal command does each
/// thing (D50), and what this machine holds now.
/// </summary>
/// <remarks>
/// <para><b>Daoris's own directory</b>, the intake's rule (D65 §1b): the one kind of tree the driver may
/// write, since no one else owns it. Never a session tree, and never asked about by git — it is no
/// repository, and git asked about it walks UP.</para>
///
/// <para><b>It reads, and it advises.</b> Its allow-list reads the family and proposes, and what it is
/// handed at spawn reads the checkouts reading across allows (D107), and nothing else: over the protocol
/// door anything unlisted is refused by construction (D52). A change is the person's, on a screen or at a
/// terminal; a proposal is a card the person confirms (HELP1c, HELP6).</para>
///
/// <para><b>By section (MOD6).</b> What it says is <see cref="HelpRoomSections.All"/>, in order: each section a
/// file of its own under <c>Help/Room/</c> that says one thing and describes the facts it says, so a new
/// section is a file and a line there.</para>
/// </remarks>
public static class HelpRoom
{
    /// <summary>The folder under the home that is the room.</summary>
    public const string Folder = "help";

    /// <summary>
    /// The "repository" its sessions are recorded in: a colon is in no folder name, so no registered
    /// repository is ever called this.
    /// </summary>
    /// <remarks>A twin (`.claude/knowledge/twins.md`): the service's <c>SessionLedger.HelpRepository</c>
    /// and the page's <c>HELP_REPOSITORY</c> spell it too, and each side's test holds the spelling.</remarks>
    public const string Repository = "daoris:help";

    /// <summary>
    /// The mode its session asks for on the protocol door: the agent's own asking mode, whatever it
    /// started in, where the agent offers it.
    /// </summary>
    /// <remarks>
    /// 🔴 Since D81 a session runs in its harness's own <c>auto</c> mode, which judges each action
    /// itself — and the first real conversation ran shell commands under it, reading a checkout to
    /// research its answer. A helper that can run a command can run <c>daoris driver …</c>, a change
    /// nobody confirmed (D89). In this mode every tool off the room's allow-list asks, and over the
    /// protocol door every ask is refused by construction (D52). Never wider than the agent's default.
    /// </remarks>
    public const string Posture = "default";

    /// <summary>The tools the room allows: reading the family, and nothing that writes anywhere.</summary>
    public static readonly IReadOnlyList<string> Allowed =
    [
        $"mcp__{KnowledgeConnector.ServerName}__registry",
        $"mcp__{KnowledgeConnector.ServerName}__knowledge_search",
        $"mcp__{KnowledgeConnector.ServerName}__knowledge_get",
        $"mcp__{KnowledgeConnector.ServerName}__knowledge_repositories",
        $"mcp__{KnowledgeConnector.ServerName}__quest_list",
        // It proposes, and the person applies (HELP1c, D89): each kind's own tool, in the kinds' order. Never
        // `permission_propose`: PERM2 applies a narrowing at the tick with nobody's press, and every change
        // here is the person's.
        .. HelpProposalKinds.All.Select(kind => $"mcp__{KnowledgeConnector.ServerName}__{kind.Tool}"),
    ];

    public static string PathOf(string home) => Path.Combine(home, Folder);

    /// <summary>Write the room from the machine as it stands, and answer where it is.</summary>
    public static string Prepare(string home, HelpMachine machine)
    {
        var room = PathOf(home);
        Directory.CreateDirectory(Path.Combine(room, ".claude"));
        AtomicFile.WriteText(Path.Combine(room, "AGENTS.md"), Render(machine));
        // Two harnesses read AGENTS.md; Claude Code reads CLAUDE.md — the canon's own shape (D59).
        AtomicFile.WriteText(Path.Combine(room, "CLAUDE.md"), "@AGENTS.md\n");
        AtomicFile.WriteText(Path.Combine(room, ".claude", "settings.json"), Settings());
        return room;
    }

    /// <summary>
    /// What its session is handed at spawn (PERM1): the room's allows and a read of the files its
    /// conversation keeps (CONV4c) — never the person's own allows, which are for work in a repository —
    /// and every deny the person wrote, which still wins.
    /// </summary>
    /// <param name="reads">
    /// The checkouts reading across lets it read (D107): each a read and two read-only git commands by exact
    /// prefix, and nothing that writes. Anything else is asked in its mode, and refused (D52).
    /// </param>
    public static RuleLists Rules(PermissionFile file, string kept, IReadOnlyList<HelpRead>? reads = null)
    {
        var composed = PermissionRules.Compose(file, workspace: null, repository: null);
        return new RuleLists(
            [
                .. Allowed, PermissionRules.ReadRule(kept),
                .. (reads ?? []).SelectMany(read => AcrossRules.ReadOnlyRules(new AcrossCheckout(read.Name, read.Path))),
            ],
            [],
            composed.Deny);
    }

    /// <summary>
    /// The machine as the driver already holds it — its file, the registry, each repository's line, the
    /// roster — so the room says what the screens say. Names and states, and the root of a checkout only
    /// where reading across lets the helper read it (D107): no other root, no profile's home, no key.
    /// </summary>
    /// <remarks>Each section describes the facts it says, from the same sources (MOD6).</remarks>
    /// <param name="product">What a person calls a harness's tool, where its toolchain says.</param>
    /// <param name="asks">How many asks wait for the person's answer.</param>
    /// <param name="standing">The asks the host answered, listed by id where they are not closed (HELP6).</param>
    /// <param name="offers">The install's own plugins (PLUG9 d); the room lists the sound ones not installed here.</param>
    /// <param name="parked">The quests the loop's last tick parked by their failed sessions (HELP10), which a retry names.</param>
    /// <param name="browser">What Daoris's browser's files hold, as the desktop read them (HELP10); null where none was read.</param>
    /// <param name="held">The quests the loop's last look held by the person's stop (SESSUX1b), which a retry releases.</param>
    public static HelpMachine Describe(
        DriverConfig config, Snapshot snapshot, IReadOnlyList<RepositoryLine> lines,
        IReadOnlyList<HarnessReport> roster, Func<string, string?> product, int asks,
        IReadOnlyList<AskView>? standing = null, PluginCatalog? plugins = null, IReadOnlyList<LandedBranch>? landed = null,
        IReadOnlyList<PluginOffer>? offers = null, IReadOnlyList<ParkedQuest>? parked = null, HelpBrowserFacts? browser = null,
        IReadOnlyList<HeldQuest>? held = null)
    {
        var sources = new HelpMachineSources(
            config, snapshot, lines, roster, product, asks, standing ?? [], plugins ?? PluginCatalog.None, landed ?? [], offers ?? [],
            parked ?? [], browser) { Held = held ?? [] };
        return HelpRoomSections.All.Aggregate(new HelpMachine(), (machine, section) => section.Describe(machine, sources));
    }

    public static string Render(HelpMachine machine) =>
        string.Concat(HelpRoomSections.All.Select(section => section.Render(machine)));

    /// <summary>The allow-list, as the harness reads a project's settings.</summary>
    private static string Settings()
    {
        using var stream = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream, new System.Text.Json.JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartObject("permissions");
            writer.WriteStartArray("allow");
            foreach (var rule in Allowed) writer.WriteStringValue(rule);
            writer.WriteEndArray();
            writer.WriteEndObject();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray()).Replace("\r\n", "\n") + "\n";
    }
}

/// <summary>
/// What the room says this machine holds now: names and states, never a key, and a path only for a checkout
/// the helper may read (D107).
/// </summary>
/// <remarks>MOD6: each section declares, in its own file, the facts it says.</remarks>
public sealed partial record HelpMachine
{
}

/// <summary>
/// What the driver hands the room to describe the machine from (MOD6): its file, the registry, each
/// repository's line, the roster, and the records each section reads. Paths and keys are in here; what a
/// section keeps of them is names and states.
/// </summary>
/// <param name="Product">What a person calls a harness's tool, where its toolchain says.</param>
/// <param name="Asks">How many asks wait for the person's answer.</param>
/// <param name="Standing">The asks the host answered.</param>
/// <param name="Offers">The install's own plugins (PLUG9 d).</param>
/// <param name="Parked">The quests the loop's last tick parked by their failed sessions (HELP10).</param>
/// <param name="Browser">What Daoris's browser's files hold (HELP10), or null where the desktop read none.</param>
internal sealed record HelpMachineSources(
    DriverConfig Config, Snapshot Snapshot, IReadOnlyList<RepositoryLine> Lines, IReadOnlyList<HarnessReport> Roster,
    Func<string, string?> Product, int Asks, IReadOnlyList<AskView> Standing, PluginCatalog Plugins,
    IReadOnlyList<LandedBranch> Landed, IReadOnlyList<PluginOffer> Offers, IReadOnlyList<ParkedQuest> Parked,
    HelpBrowserFacts? Browser)
{
    /// <summary>The quests the loop's last look held by the person's stop (SESSUX1b).</summary>
    public IReadOnlyList<HeldQuest> Held { get; init; } = [];
}
