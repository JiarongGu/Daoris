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
///
/// <para><b>A room per workspace</b> (ASKSCOPE1b, D158's ASKSCOPE1 note point 4). A conversation runs in the room of the
/// workspace its record keeps: <c>default</c>'s is <see cref="Folder"/>, the whole machine as before, and a named workspace's is a
/// folder of its own beside it (<see cref="PathOf(string, string?)"/>), written from the facts projected to that workspace
/// (<see cref="HelpMachine.Within"/>). Two running conversations in two workspaces never write one room.</para>
/// </remarks>
public static class HelpRoom
{
    /// <summary>The folder under the home that is the room: <c>default</c>'s, the whole machine's.</summary>
    public const string Folder = "help";

    /// <summary>
    /// The longest a named workspace's part of its room's folder name is spelled out (ASKSCOPE1b); a longer one keeps its first
    /// <see cref="NameKept"/> characters and a digest of the whole name, so a folder's name stays well inside what a file system holds.
    /// </summary>
    private const int NameSpelled = 48;

    private const int NameKept = 32;

    /// <summary>
    /// The "repository" its sessions are recorded in: a colon is in no folder name, so no registered
    /// repository is ever called this.
    /// </summary>
    /// <remarks>A twin (`.claude/knowledge/twins.md`): the service's <c>SessionLedger.HelpRepository</c>
    /// and the page's <c>HELP_REPOSITORY</c> spell it too, and each side's test holds the spelling.</remarks>
    public const string Repository = "daoris:help";

    /// <summary>
    /// The mode its session asks for on the protocol door in restricted access (HELPMODE1a1, D183): the agent's own asking mode,
    /// whatever it started in, where the agent offers it. Full access asks the harness's mode that asks nothing instead
    /// (<see cref="HelpAccess"/>), only by the person's choice.
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

    /// <summary><c>default</c>'s room, the whole machine's: <see cref="Folder"/> under the home.</summary>
    public static string PathOf(string home) => DaorisHome.Entry(home, HomeEntry.HelpRoom);

    /// <summary>
    /// The room of <paramref name="workspace"/> (ASKSCOPE1b): <c>default</c>'s, or none named, is <see cref="PathOf(string)"/>, so
    /// every conversation started before keeps the folder its harness keeps it by; a named workspace's is a folder of its own beside
    /// it, <see cref="FolderOf"/>.
    /// </summary>
    public static string PathOf(string home, string? workspace) =>
        ScopeOf(workspace) is { } named ? Path.Combine(Path.GetDirectoryName(PathOf(home))!, FolderOf(named)) : PathOf(home);

    /// <summary>
    /// The workspace a room is scoped to (ASKSCOPE1b): null for <c>default</c> or none named, whose room is the whole machine; else
    /// the name, trimmed.
    /// </summary>
    public static string? ScopeOf(string? workspace)
    {
        var circle = RemoteTarget.Workspace(workspace);
        return string.Equals(circle, RemoteTarget.DefaultWorkspace, StringComparison.OrdinalIgnoreCase) ? null : circle;
    }

    /// <summary>
    /// A named workspace's room folder (ASKSCOPE1b): <c>help-</c> and the name as one workspace is one name, whatever its letters'
    /// case (D48), its ASCII letters and digits kept, lowercase, and every other character spelled by its UTF-8 bytes, each
    /// <c>_</c> and two hex digits. One name is one folder and two names are two, and none is <see cref="Folder"/>: no character
    /// is kept that a file system or the harness's own naming of a folder would change. A name spelled longer than
    /// <see cref="NameSpelled"/> keeps its first <see cref="NameKept"/> characters, <c>-</c> and a digest of the whole name, a
    /// <c>-</c> no shorter one holds.
    /// </summary>
    internal static string FolderOf(string workspace)
    {
        var spelled = new StringBuilder();
        Span<byte> bytes = stackalloc byte[4];
        foreach (var rune in workspace.Trim().ToUpperInvariant().EnumerateRunes())
        {
            if (rune.IsAscii && char.IsAsciiLetterOrDigit((char)rune.Value))
            {
                spelled.Append(char.ToLowerInvariant((char)rune.Value));
                continue;
            }

            var length = rune.EncodeToUtf8(bytes);
            foreach (var each in bytes[..length]) spelled.Append('_').Append(each.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }

        var name = spelled.ToString();
        if (name.Length > NameSpelled)
        {
            var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(workspace.Trim().ToUpperInvariant())));
            name = $"{name[..NameKept]}-{digest[..16].ToLowerInvariant()}";
        }

        return $"{Folder}-{name}";
    }

    /// <summary>
    /// The workspace whose room a kept conversation goes on in (ASKSCOPE1b): its record's, save one whose record says it ran in
    /// <c>default</c>'s room (<paramref name="tree"/>, <see cref="PathOf(string)"/>), started there before each workspace had its own,
    /// which goes on there, its harness keeping it by that folder; null is <c>default</c>'s.
    /// </summary>
    public static string? GoesOnIn(string home, string? workspace, string? tree) =>
        tree is { Length: > 0 } && Same(tree, PathOf(home)) ? null : ScopeOf(workspace);

    private static bool Same(string one, string other)
    {
        try
        {
            return string.Equals(
                Path.TrimEndingDirectorySeparator(Path.GetFullPath(one)), Path.TrimEndingDirectorySeparator(Path.GetFullPath(other)),
                StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// Whether the room of <paramref name="workspace"/> was ever written (HELPSTUCK1): its <c>AGENTS.md</c> is there, so a start whose
    /// reading of the machine does not answer in time can start on it as last written.
    /// </summary>
    public static bool Written(string home, string? workspace = null) => File.Exists(Path.Combine(PathOf(home, workspace), "AGENTS.md"));

    /// <summary>
    /// Write the room from the machine as it stands, and answer where it is: the room of the workspace it is scoped to
    /// (<see cref="HelpMachine.Workspace"/>, ASKSCOPE1b), <c>default</c>'s where it is the whole machine.
    /// </summary>
    public static string Prepare(string home, HelpMachine machine)
    {
        var room = PathOf(home, machine.Workspace);
        Directory.CreateDirectory(Path.Combine(room, ".claude"));
        // HELPMODE1c2a: the housekeeping granted, HELPMODE1c3's chosen settings and HELPMODE1c4's checkouts turned off, as the record
        // holds them as this start writes its room.
        var access = ConfiguredAccess.Read(home);
        AtomicFile.WriteText(Path.Combine(room, "AGENTS.md"), Render(machine with
        {
            Housekeeping = access.Housekeeping, Settings = access.Settings, CheckoutsOff = access.CheckoutsOff,
        }));
        // Two harnesses read AGENTS.md; Claude Code reads CLAUDE.md — the canon's own shape (D59).
        AtomicFile.WriteText(Path.Combine(room, "CLAUDE.md"), "@AGENTS.md\n");
        AtomicFile.WriteText(Path.Combine(room, ".claude", "settings.json"), Settings());
        Said[Path.GetFullPath(room)] = new RoomSaid(Path.GetFullPath(home), machine.Workspace, machine.NotReadYet.Any);
        return room;
    }

    // What each room this process wrote last under each home said was not read yet (PARALLEL1a), by its folder, with its workspace
    // (ASKSCOPE1b): the catch-up writes only such a room again, from its own workspace's facts. A room another process wrote, or one
    // written before this process, is written again at its next start.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, RoomSaid> Said = new(StringComparer.OrdinalIgnoreCase);

    private sealed record RoomSaid(string Home, string? Workspace, bool NotReadYet);

    /// <summary>
    /// Whether any room this process wrote last under <paramref name="home"/> said something was not read yet (PARALLEL1a), so it
    /// is written again as the facts land (<see cref="HelpRoomCatchUp"/>).
    /// </summary>
    public static bool SaidNotReadYet(string home) => Unread(home).Count > 0;

    /// <summary>Whether the room of <paramref name="workspace"/> this process wrote last under <paramref name="home"/> said so.</summary>
    public static bool SaidNotReadYet(string home, string? workspace) =>
        Said.GetValueOrDefault(Path.GetFullPath(PathOf(home, workspace)))?.NotReadYet ?? false;

    /// <summary>
    /// The workspaces whose room this process wrote last under <paramref name="home"/> said something was not read yet (ASKSCOPE1b),
    /// each the one its room is scoped to, null for <c>default</c>'s: what the catch-up writes again, each from its own facts.
    /// </summary>
    internal static IReadOnlyList<string?> Unread(string home)
    {
        var full = Path.GetFullPath(home);
        return [.. Said.Values.Where(said => said.NotReadYet && string.Equals(said.Home, full, StringComparison.OrdinalIgnoreCase)).Select(said => said.Workspace)];
    }

    /// <summary>How many asks wait for the person's answer (HELP1a): those the host answered <c>Proposed</c>.</summary>
    public static int AsksWaiting(IEnumerable<AskView> standing) => standing.Count(ask => ask.State == "Proposed");

    /// <summary>
    /// What its session is handed at spawn (PERM1): the room's allows and a read of the files its
    /// conversation keeps (CONV4c) — never the person's own allows, which are for work in a repository —
    /// and every deny the person wrote, which still wins.
    /// </summary>
    /// <param name="reads">
    /// The checkouts it may read (D107, the readable ones the person left on): with any, the connector's <c>checkout_read</c>, whose
    /// every call the driver judges (HELPMODE1c4, <see cref="HelpCheckouts"/>). Never a <c>Read</c> rule or a git prefix on a checkout,
    /// which held no agent that takes no rules; anything else is asked in its mode, and refused (D52).
    /// </param>
    /// <param name="plugins">
    /// The tools of the servers plugins hand it that it may call, as the person approved them (HELPPLUG1a, D170 point 3 and its
    /// note): <see cref="HelpGrantSet.Allow"/>, each a server's tool and nothing else. The person's denies still win.
    /// </param>
    /// <param name="asked">
    /// The writes of an approved server the person did not accept (<see cref="HelpGrantSet.Ask"/>): asked, which its asking mode
    /// refuses, so a write is never made silently (D89).
    /// </param>
    /// <param name="workspace">
    /// The workspace its tools are bound to (ASKSCOPE1c, <see cref="HelpCircle"/>), whose denies the person wrote it is handed;
    /// null is <c>default</c>'s, the whole machine's room, as before.
    /// </param>
    /// <param name="housekeeping">
    /// The housekeeping operations the person granted it (HELPMODE1c2a, <see cref="ConfiguredAccess"/>), each one's connector tool
    /// allowed beside the room's own; none by default. The tool's call is judged again by the driver, so this list is no ceiling
    /// for an agent that takes no rules.
    /// </param>
    /// <param name="settings">The chosen settings the person granted it (HELPMODE1c3), each one's connector tool allowed the same way.</param>
    public static RuleLists Rules(
        PermissionFile file, string kept, IReadOnlyList<HelpRead>? reads = null, IReadOnlyList<string>? plugins = null,
        IReadOnlyList<string>? asked = null, string? workspace = null, IReadOnlyList<string>? housekeeping = null,
        IReadOnlyList<string>? settings = null)
    {
        var composed = PermissionRules.Compose(file, workspace, repository: null);
        return new RuleLists(
            [
                .. Allowed,
                .. (housekeeping ?? []).Select(operation => $"mcp__{KnowledgeConnector.ServerName}__{ConfiguredAccess.ToolOf(operation)}"),
                .. (settings ?? []).Select(setting => $"mcp__{KnowledgeConnector.ServerName}__{ConfiguredAccess.SettingToolOf(setting)}"),
                PermissionRules.ReadRule(kept),
                .. reads is { Count: > 0 } ? [$"mcp__{KnowledgeConnector.ServerName}__{HelpCheckouts.Tool}"] : Array.Empty<string>(),
                .. plugins ?? [],
            ],
            [.. asked ?? []],
            composed.Deny);
    }

    /// <summary>
    /// What its session is handed in full access (HELPMODE1a1, D183 point 1): no allow-list and nothing asked, since its harness's
    /// mode asks nothing; every deny the person's rules compose, the shipped no-push among them while it is on, which still wins;
    /// those of the workspace its tools are bound to (ASKSCOPE1c), <c>default</c>'s where null.
    /// </summary>
    public static RuleLists FullRules(PermissionFile file, string? workspace = null) =>
        new([], [], PermissionRules.Compose(file, workspace, repository: null).Deny);

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
    /// <param name="history">What each workspace keeps of finished work (HIST1f-b), as <c>HISTORY_PLAN</c> reads it; null where none was read.</param>
    /// <param name="workflows">The named workflows under the home (WORKFLOW1h-b, <see cref="WorkflowStore.List"/>), which name the version each repository follows; null where none were read.</param>
    /// <param name="grants">The person's approvals of what plugins hand it (<see cref="HelpGrants.Read"/>, HELPPLUG1a); none read, none named.</param>
    /// <param name="notReadYet">What the facts the judges keep had not read yet (PARALLEL1a, <see cref="HelpFacts.Read"/>); none by default.</param>
    /// <param name="workspace">
    /// The workspace whose room it is (ASKSCOPE1b): its facts projected to it before any section counts or lists them
    /// (<see cref="HelpRoomScope"/>); <c>default</c> or none is the whole machine, as before.
    /// </param>
    public static HelpMachine Describe(
        DriverConfig config, Snapshot snapshot, IReadOnlyList<RepositoryLine> lines,
        IReadOnlyList<HarnessReport> roster, Func<string, string?> product, int asks,
        IReadOnlyList<AskView>? standing = null, PluginCatalog? plugins = null, IReadOnlyList<LandedBranch>? landed = null,
        IReadOnlyList<PluginOffer>? offers = null, IReadOnlyList<ParkedQuest>? parked = null, HelpBrowserFacts? browser = null,
        IReadOnlyList<HeldQuest>? held = null, SessionLook? sessions = null, IReadOnlyList<HistoryReading>? history = null,
        IReadOnlyList<WorkflowFileFound>? workflows = null, HelpGrantSet? grants = null,
        IReadOnlyList<WorkMilestone>? work = null, IReadOnlyList<InventoryUnread>? workUnread = null, IReadOnlyList<InventoryEntry>? workRecovery = null,
        HelpNotReadYet? notReadYet = null, string? workspace = null)
    {
        var sources = new HelpMachineSources(
            config, snapshot, lines, roster, product, asks, standing ?? [], plugins ?? PluginCatalog.None, landed ?? [], offers ?? [],
            parked ?? [], browser)
        {
            Held = held ?? [], Sessions = sessions, History = history, Workflows = workflows, Grants = grants,
            Work = work, WorkUnread = workUnread ?? [], WorkRecovery = workRecovery ?? [], NotReadYet = notReadYet ?? HelpNotReadYet.None,
        };
        return Describe(sources, ScopeOf(workspace));
    }

    /// <summary>
    /// The machine from the whole machine's <paramref name="whole"/>, as the room of <paramref name="workspace"/> says it: its facts
    /// projected to it first (ASKSCOPE1b), the whole machine where null. The machine keeps <paramref name="whole"/>, so another
    /// workspace's room is described from the same facts (<see cref="HelpMachine.Within"/>).
    /// </summary>
    internal static HelpMachine Describe(HelpMachineSources whole, string? workspace)
    {
        var sources = workspace is null ? whole : HelpRoomScope.Project(whole, workspace);
        return HelpRoomSections.All.Aggregate(
            new HelpMachine { Workspace = workspace, Whole = whole }, (machine, section) => section.Describe(machine, sources));
    }

    public static string Render(HelpMachine machine) =>
        string.Concat(HelpRoomSections.All.Select(section => section.Render(machine)));

    /// <summary>The allow-list, as the harness reads a project's settings.</summary>
    private static string Settings() => JsonFile.Serialize(writer =>
    {
        writer.WriteStartObject();
        writer.WriteStartObject("permissions");
        writer.WriteStartArray("allow");
        foreach (var rule in Allowed) writer.WriteStringValue(rule);
        writer.WriteEndArray();
        writer.WriteEndObject();
        writer.WriteEndObject();
    });
}

/// <summary>
/// What the room says this machine holds now: names and states, never a key, and a path only for a checkout
/// the helper may read (D107).
/// </summary>
/// <remarks>MOD6: each section says its facts in its own file; PART4 keeps the facts in this one declaration, under a comment per section.</remarks>
public sealed record HelpMachine
{
    // The intro's, which the may-do section reads too.

    /// <summary>
    /// The access mode its conversation runs in (HELPMODE1a1, D183 point 3), as the driver's file said when the room was written:
    /// one of <see cref="HelpModes"/>. The room is written again at each start, so it says the mode that start was placed under.
    /// </summary>
    public string Mode { get; init; } = HelpModes.Restricted;

    /// <summary>
    /// The housekeeping operations the person granted it (HELPMODE1c2a, <see cref="ConfiguredAccess"/>), read from the record as the
    /// room is written at a start (<see cref="HelpRoom.Prepare"/>); none where none is chosen, as a machine described by hand has.
    /// </summary>
    public IReadOnlyList<string> Housekeeping { get; init; } = [];

    /// <summary>
    /// The chosen settings the person granted it (HELPMODE1c3, <see cref="ConfiguredAccess.Settings"/>), read as
    /// <see cref="Housekeeping"/> is; none where none is chosen.
    /// </summary>
    public IReadOnlyList<string> Settings { get; init; } = [];

    /// <summary>
    /// The checkouts the person turned off for it (HELPMODE1c4, <see cref="ConfiguredAccess.CheckoutsOff"/>), read as
    /// <see cref="Housekeeping"/> is: the room lists the readable ones less these, and names these apart.
    /// </summary>
    public IReadOnlyList<string> CheckoutsOff { get; init; } = [];

    // The room's scope (ASKSCOPE1b).

    /// <summary>
    /// The workspace whose room this is, its facts projected to it (D158's ASKSCOPE1 note point 4); null for <c>default</c>'s room,
    /// the whole machine.
    /// </summary>
    public string? Workspace { get; init; }

    /// <summary>The whole machine's facts it was described from, so another workspace's room is described from the same; null where it was built by hand.</summary>
    internal HelpMachineSources? Whole { get; init; }

    /// <summary>
    /// The room of <paramref name="workspace"/> (ASKSCOPE1b): this machine's facts projected to it before any count or limit, said
    /// again by every section; itself for <c>default</c>, or none named, whose room is the whole machine. One built by hand, with no
    /// facts to project, is only named for the workspace; one altered with <c>with</c> after it was described is described again
    /// from its facts, so the alteration is not kept.
    /// </summary>
    public HelpMachine Within(string? workspace)
    {
        var scope = HelpRoom.ScopeOf(workspace);
        if (string.Equals(scope, Workspace, StringComparison.OrdinalIgnoreCase)) return this;
        return Whole is { } whole ? HelpRoom.Describe(whole, scope) : this with { Workspace = scope };
    }

    // The agents section's.

    public IReadOnlyList<HelpAgent> Agents { get; init; } = [];

    // The asks section's.

    /// <summary>The asks not closed, newest first (HELP6).</summary>
    public IReadOnlyList<HelpAsk> OpenAsks { get; init; } = [];

    // The landing section's.

    /// <summary>The plugins a branch rule may name here, by id (HELP8, D100).</summary>
    public IReadOnlyList<string> LandingPlugins { get; init; } = [];

    // The machine-now section's.

    /// <summary>The harness quests run on.</summary>
    public string? Adapter { get; init; }

    /// <summary>The harness asks are answered on, or null for none (INT4b).</summary>
    public string? Intake { get; init; }

    /// <summary>The harness Ask Daoris runs on (D89).</summary>
    public string? Helper { get; init; }

    public int Cap { get; init; }

    /// <summary>
    /// The sessions waiting on the person (ENTRY1f2), as the Sessions list's <i>Waiting on you</i> holds them less Ask Daoris's
    /// own, by id, so a go names one the room was shown; the room's count is theirs.
    /// </summary>
    public IReadOnlyList<HelpWaitingSession> Waiting { get; init; } = [];

    /// <summary>This machine's sessions by the Sessions list's groups, and the ended ones by id (SESSUX1h3); null where none was read.</summary>
    public HelpSessions? Sessions { get; init; }

    /// <summary>How many asks wait for the person's answer.</summary>
    public int Asks { get; init; }

    /// <summary>Every plugin installed here, sound or not, in the catalogue's order (PLUG9).</summary>
    public IReadOnlyList<HelpPlugin> Plugins { get; init; } = [];

    /// <summary>The branches this machine's landings made and recorded, in the order they landed (WSR5b).</summary>
    public IReadOnlyList<HelpLanded> Landed { get; init; } = [];

    /// <summary>The quests the loop's last tick parked by their failed sessions, as the room was written (HELP10).</summary>
    public IReadOnlyList<ParkedQuest> Parked { get; init; } = [];

    /// <summary>The quests the loop's last look held by the person's stop, as the room was written (SESSUX1b).</summary>
    public IReadOnlyList<HeldQuest> Held { get; init; } = [];

    /// <summary>
    /// The asks and quests paused on this machine, as <c>driver.json</c> keeps them (PAUSE1f-b, D132 §7.4): asks first, each
    /// by its id, so a resume names one the room was shown.
    /// </summary>
    public IReadOnlyList<PausedBy> Paused { get; init; } = [];

    /// <summary>Daoris's browser as its files hold it (HELP10), or null where the desktop read none.</summary>
    public HelpBrowser? Browser { get; init; }

    /// <summary>
    /// What each workspace keeps of finished work, what a clear would take now and what stays by the word that keeps it
    /// (HIST1f-b, design §2.4): counts and bytes, never a path or a title, so a clear names a workspace that keeps something.
    /// Null where none was read.
    /// </summary>
    public IReadOnlyList<HistoryReading>? History { get; init; }

    /// <summary>
    /// What the facts the judges keep had not read yet when the room was written (PARALLEL1a, D181 point 1): each said <i>not read
    /// yet</i> where its section says it, and the room written again as they land.
    /// </summary>
    public HelpNotReadYet NotReadYet { get; init; } = HelpNotReadYet.None;

    // The may-do section's.

    /// <summary>The checkouts it may read, by name; a path here is one reading across allows, and no other.</summary>
    public IReadOnlyList<HelpRead> Reads { get; init; } = [];

    /// <summary>The servers plugins hand it, as the person approved them, each with the tools it may call there (HELPPLUG1a), in catalogue order.</summary>
    public IReadOnlyList<HelpPluginServer> PluginServers { get; init; } = [];

    /// <summary>The servers plugins offer it that wait for the person's approval, so it is handed none of them.</summary>
    public IReadOnlyList<HelpPluginServer> PluginServersWaiting { get; init; } = [];

    /// <summary>
    /// The servers the install's newer copy of a plugin installed here would offer it, which the installed copy does not
    /// (HELPPLUG1c): its update first, then the person's approval.
    /// </summary>
    public IReadOnlyList<HelpPluginServerOffered> PluginServersOffered { get; init; } = [];

    /// <summary>The skills plugins hand it (HELPPLUG1b, D170 point 4), as the harness names them, <c>plugin:skill</c>.</summary>
    public IReadOnlyList<string> PluginSkills { get; init; } = [];

    // The work section's.

    /// <summary>Each unit of work's milestone as the records say it (FABRIC-MILESTONE1b); null where none was read.</summary>
    public IReadOnlyList<WorkMilestone>? Work { get; init; }

    /// <summary>What the milestones could not read, each in a clause (a repository with no checkout here).</summary>
    public IReadOnlyList<string> WorkUnread { get; init; } = [];

    /// <summary>Daoris's own recoveries no unit names (RECOVERYASK1, <see cref="WorkInventory.Recoveries"/>): a folder moved aside, a recovery ref.</summary>
    public IReadOnlyList<InventoryEntry> WorkRecovery { get; init; } = [];

    // The own-plugins section's.

    /// <summary>The install's own plugins not installed here and sound, which an add may name by id (PLUG9 d).</summary>
    public IReadOnlyList<HelpOffer> Offers { get; init; } = [];

    // The workspaces section's.

    public IReadOnlyList<HelpRepository> Repositories { get; init; } = [];

    /// <summary>The kinds of work each workspace declares (WORKFLOW1h-b, the workflow design §8.4), by workspace.</summary>
    public IReadOnlyList<HelpWorkflowKind> WorkflowKinds { get; init; } = [];
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

    /// <summary>One look at the sessions as the Sessions list takes it (SESSUX1h3); null where none was read.</summary>
    public SessionLook? Sessions { get; init; }

    /// <summary>What each workspace keeps of finished work (HIST1f-b, design §2.4); null where none was read.</summary>
    public IReadOnlyList<HistoryReading>? History { get; init; }

    /// <summary>The named workflows under the home (WORKFLOW1h-b, the workflow design §8.4); null where none were read.</summary>
    public IReadOnlyList<WorkflowFileFound>? Workflows { get; init; }

    /// <summary>The person's approvals of what plugins hand Ask Daoris (HELPPLUG1a's tightening); null where none were read.</summary>
    public HelpGrantSet? Grants { get; init; }

    /// <summary>Each unit of work's milestone (FABRIC-MILESTONE1b); null where none was read.</summary>
    public IReadOnlyList<WorkMilestone>? Work { get; init; }

    /// <summary>What the milestones could not read, each by its repository (FACTSPLIT1a), which a workspace's room keeps by.</summary>
    public IReadOnlyList<InventoryUnread> WorkUnread { get; init; } = [];

    /// <summary>Daoris's own recoveries no unit names (RECOVERYASK1).</summary>
    public IReadOnlyList<InventoryEntry> WorkRecovery { get; init; } = [];

    /// <summary>What the facts the judges keep had not read yet (PARALLEL1a).</summary>
    public HelpNotReadYet NotReadYet { get; init; } = HelpNotReadYet.None;

    /// <summary>The workspace these are projected to (ASKSCOPE1b, <see cref="HelpRoomScope"/>); null for the whole machine.</summary>
    public string? Workspace { get; init; }

    /// <summary>
    /// Every repository registered here, every workspace's, where these are projected (ASKSCOPE1b): what the checkouts it may read
    /// are judged from. Those are what its spawn is granted (D107), so the room lists what its rules allow (D158's ASKSCOPE1b note,
    /// point 5). Null for the whole machine, whose <see cref="HelpMachineSources.Snapshot"/> holds them all.
    /// </summary>
    public IReadOnlyList<RepoView>? Registry { get; init; }
}
