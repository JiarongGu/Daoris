using System.Text;

namespace Daoris.Driver;

/// <summary>
/// Ask Daoris's room: the folder under Daoris's home its conversation runs in, written
/// from the machine at every open — what Daoris is, which screen and which terminal command does each
/// thing, and what this machine holds now.
/// </summary>
/// <remarks>
/// <para><b>Daoris's own directory</b>, the intake's rule: the one kind of tree the driver may
/// write, since no one else owns it. Never a session tree, and never asked about by git — it is no
/// repository, and git asked about it walks UP.</para>
///
/// <para><b>It reads, and it advises.</b> Its allow-list reads the family and presses Daoris's verb, and what it is
/// handed at spawn reads the checkouts reading across allows, and nothing else: over the protocol
/// door anything unlisted is refused by construction. A change is the person's, on a screen or at a
/// terminal; a press waits on the card the person confirms.</para>
///
/// <para><b>By section.</b> What it says is <see cref="HelpRoomSections.All"/>, in order: each section a
/// file of its own under <c>Knowledge/Room/</c> that says one thing and describes the facts it says, so a new
/// section is a file and a line there.</para>
///
/// <para><b>A room per workspace</b>. A conversation runs in the room of the
/// workspace its record keeps: <c>default</c>'s is <see cref="Folder"/>, the whole machine, and a named workspace's is a
/// folder of its own beside it (<see cref="PathOf(string, string?)"/>), written from the facts projected to that workspace
/// (<see cref="HelpMachine.Within"/>). Two running conversations in two workspaces never write one room.</para>
/// </remarks>
public static class HelpRoom
{
    /// <summary>The folder under the home that is the room: <c>default</c>'s, the whole machine's.</summary>
    public const string Folder = KnowledgeRoomPaths.Folder;

    /// <summary>
    /// The "repository" its sessions are recorded in: a colon is in no folder name, so no registered
    /// repository is ever called this.
    /// </summary>
    /// <remarks>A twin (`.claude/knowledge/twins.md`): the service's <c>SessionLedger.HelpRepository</c>
    /// and the page's <c>HELP_REPOSITORY</c> spell it too, and each side's test holds the spelling.</remarks>
    public const string Repository = KnowledgeRoomIdentity.Repository;

    /// <summary>
    /// The mode its session asks for on the protocol door in restricted access: the agent's own asking mode,
    /// whatever it started in, where the agent offers it. Full access asks the harness's mode that asks nothing instead
    /// (<see cref="HelpAccess"/>), only by the person's choice.
    /// </summary>
    /// <remarks>
    /// 🔴 A session runs in its harness's own <c>auto</c> mode, which judges each action
    /// itself — and the first real conversation ran shell commands under it, reading a checkout to
    /// research its answer. A helper that can run a command can run <c>daoris …</c>, a change
    /// nobody confirmed. In this mode every tool off the room's allow-list asks, and over the
    /// protocol door every ask is refused by construction. Never wider than the agent's default.
    /// </remarks>
    public const string Posture = "default";

    /// <summary>Daoris's own verb in the forms the harness matches: unasked, since each change it presses waits on the person's card.</summary>
    public static readonly IReadOnlyList<string> Verb = ["Bash(daoris:*)", "PowerShell(daoris:*)"];

    /// <summary>The one write the room allows: a file under <see cref="KnowledgeRoomPaths.Drafts"/>.</summary>
    public const string DraftsRule = $"Edit(./{KnowledgeRoomPaths.Drafts}/**)";

    /// <summary>The tools the room allows: reading the family, Daoris's own verb and writing a draft in <see cref="KnowledgeRoomPaths.Drafts"/>; nothing else that changes the machine.</summary>
    public static readonly IReadOnlyList<string> Allowed =
    [
        .. Verb,
        DraftsRule,
        $"mcp__{KnowledgeConnector.ServerName}__registry",
        $"mcp__{KnowledgeConnector.ServerName}__knowledge_search",
        $"mcp__{KnowledgeConnector.ServerName}__knowledge_get",
        $"mcp__{KnowledgeConnector.ServerName}__knowledge_repositories",
        $"mcp__{KnowledgeConnector.ServerName}__quest_list",
    ];

    /// <summary><c>default</c>'s room, the whole machine's: <see cref="Folder"/> under the home.</summary>
    public static string PathOf(string home) => KnowledgeRoomPaths.PathOf(home);

    /// <summary>
    /// The room of <paramref name="workspace"/>: <c>default</c>'s, or none named, is <see cref="PathOf(string)"/>, so
    /// every conversation started before keeps the folder its harness keeps it by; a named workspace's is a folder of its own beside
    /// it, <see cref="FolderOf"/>.
    /// </summary>
    public static string PathOf(string home, string? workspace) => KnowledgeRoomPaths.PathOf(home, workspace);

    /// <summary>
    /// The workspace a room is scoped to: null for <c>default</c> or none named, whose room is the whole machine; else
    /// the name, trimmed.
    /// </summary>
    public static string? ScopeOf(string? workspace) => KnowledgeRoomPaths.ScopeOf(workspace);

    /// <summary>A named workspace's room folder, using Knowledge's shared encoding.</summary>
    internal static string FolderOf(string workspace) => KnowledgeRoomPaths.FolderOf(workspace);

    /// <summary>
    /// The workspace whose room a kept conversation goes on in: its record's, save one whose record says it ran in
    /// <c>default</c>'s room (<paramref name="tree"/>, <see cref="PathOf(string)"/>),
    /// which goes on there, its harness keeping it by that folder; null is <c>default</c>'s.
    /// </summary>
    public static string? GoesOnIn(string home, string? workspace, string? tree) => KnowledgeRoomPaths.GoesOnIn(home, workspace, tree);

    /// <summary>
    /// Whether the room of <paramref name="workspace"/> has ever been written: its <c>AGENTS.md</c> is there, so a start whose
    /// reading of the machine does not answer in time can start on it as last written.
    /// </summary>
    public static bool Written(string home, string? workspace = null) => File.Exists(Path.Combine(PathOf(home, workspace), "AGENTS.md"));

    /// <summary>
    /// Write the room from the machine as it stands, and answer where it is: the room of its conversation's home workspace
    /// (<see cref="HelpMachine.Home"/>), <c>default</c>'s where it is the whole machine. Every conversation of that workspace reads it, so
    /// it is written for the home alone, whatever focus the machine was described for: each run is told its own (<see cref="HelpFocusTold"/>).
    /// </summary>
    public static string Prepare(string home, HelpMachine machine)
    {
        var room = PathOf(home, machine.Home);
        var own = machine.Focus is null ? machine : machine.Within(machine.Home);
        lock (HelpRoomWrites.Of(room)) return Write(home, room, own);
    }

    private static string Write(string home, string room, HelpMachine machine)
    {
        Directory.CreateDirectory(Path.Combine(room, ".claude"));
        Directory.CreateDirectory(Path.Combine(room, KnowledgeRoomPaths.Drafts));
        // The families of acts granted and the checkouts turned off, as the record holds them as this start writes its room.
        var access = ConfiguredAccess.Read(home);
        AtomicFile.WriteText(Path.Combine(room, "AGENTS.md"), Render(machine with
        {
            Acts = access.Acts, CheckoutsOff = access.CheckoutsOff,
            // The roles' policies and weeks are the home's records, read as the room is written like the access.
            Roles = HelpRoleFacts.Read(home),
            Rules = HelpRuleFacts.Read(home),
        }));
        // Two harnesses read AGENTS.md; Claude Code reads CLAUDE.md — the canon's own shape.
        AtomicFile.WriteText(Path.Combine(room, "CLAUDE.md"), "@AGENTS.md\n");
        AtomicFile.WriteText(Path.Combine(room, ".claude", "settings.json"), Settings());
        Said[Path.GetFullPath(room)] = new RoomSaid(Path.GetFullPath(home), machine.Home, machine.NotReadYet.Any);
        return room;
    }

    // What each room this process wrote last under each home said is not read yet, by its folder, with its workspace:
    // the catch-up writes only such a room again, from its own workspace's facts. A room another process wrote, or one
    // written before this process, is written again at its next start.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, RoomSaid> Said = new(StringComparer.OrdinalIgnoreCase);

    private sealed record RoomSaid(string Home, string? Workspace, bool NotReadYet);

    /// <summary>
    /// Whether any room this process wrote last under <paramref name="home"/> said something is not read yet, so it
    /// is written again as the facts land (<see cref="HelpRoomCatchUp"/>).
    /// </summary>
    public static bool SaidNotReadYet(string home) => Unread(home).Count > 0;

    /// <summary>Whether the room of <paramref name="workspace"/> this process wrote last under <paramref name="home"/> said so.</summary>
    public static bool SaidNotReadYet(string home, string? workspace) =>
        Said.GetValueOrDefault(Path.GetFullPath(PathOf(home, workspace)))?.NotReadYet ?? false;

    /// <summary>
    /// The workspaces whose room this process wrote last under <paramref name="home"/> said something is not read yet,
    /// each the one its room is scoped to, null for <c>default</c>'s: what the catch-up writes again, each from its own facts.
    /// </summary>
    internal static IReadOnlyList<string?> Unread(string home)
    {
        var full = Path.GetFullPath(home);
        return [.. Said.Values.Where(said => said.NotReadYet && string.Equals(said.Home, full, StringComparison.OrdinalIgnoreCase)).Select(said => said.Workspace)];
    }

    /// <summary>How many asks wait for the person's answer: those the host answered <c>Proposed</c>.</summary>
    public static int AsksWaiting(IEnumerable<AskView> standing) => standing.Count(ask => ask.State == "Proposed");

    /// <summary>
    /// What its session is handed at spawn: the room's allows and a read of the files its
    /// conversation keeps — never the person's own allows, which are for work in a repository —
    /// and every deny the person wrote, which still wins.
    /// </summary>
    /// <param name="reads">
    /// The checkouts it may read (the readable ones the person left on): with any, the connector's <c>checkout_read</c>, whose
    /// every call the driver judges (<see cref="HelpCheckouts"/>). Never a <c>Read</c> rule or a git prefix on a checkout,
    /// which held no agent that takes no rules; anything else is asked in its mode, and refused.
    /// </param>
    /// <param name="plugins">
    /// The tools of the servers plugins hand it that it may call, as the person approved them:
    /// <see cref="HelpGrantSet.Allow"/>, each a server's tool and nothing else. The person's denies still win.
    /// </param>
    /// <param name="asked">
    /// The writes of an approved server the person did not accept (<see cref="HelpGrantSet.Ask"/>): asked, which its asking mode
    /// refuses, so a write is never made silently.
    /// </param>
    /// <param name="workspace">
    /// The workspace its tools are bound to (<see cref="HelpCircle"/>), whose denies the person wrote it is handed;
    /// null is <c>default</c>'s, the whole machine's room.
    /// </param>
    public static RuleLists Rules(
        PermissionFile file, string kept, IReadOnlyList<HelpRead>? reads = null, IReadOnlyList<string>? plugins = null,
        IReadOnlyList<string>? asked = null, string? workspace = null)
    {
        var composed = PermissionRules.Compose(file, workspace, repository: null);
        return new RuleLists(
            [
                .. Allowed,
                PermissionRules.ReadRule(kept),
                .. reads is { Count: > 0 } ? [$"mcp__{KnowledgeConnector.ServerName}__{HelpCheckouts.Tool}"] : Array.Empty<string>(),
                .. plugins ?? [],
            ],
            [.. asked ?? []],
            composed.Deny);
    }

    /// <summary>
    /// What its session is handed in full access: no allow-list and nothing asked, since its harness's
    /// mode asks nothing; every deny the person's rules compose, the shipped no-push among them, which still wins;
    /// those of the workspace its tools are bound to, <c>default</c>'s where null.
    /// </summary>
    public static RuleLists FullRules(PermissionFile file, string? workspace = null) =>
        new([], [], PermissionRules.Compose(file, workspace, repository: null).Deny);

    /// <summary>
    /// The machine as the driver already holds it — its file, the registry, each repository's line, the
    /// roster — so the room says what the screens say. Names and states, and the root of a checkout only
    /// where reading across lets the helper read it: no other root, no profile's home, no key.
    /// </summary>
    /// <remarks>Each section describes the facts it says, from the same sources.</remarks>
    /// <param name="facts">The agents as their registrations declare them (<see cref="IAgentFacts"/>), which say what a person calls a harness's tool; none names none.</param>
    /// <param name="asks">How many asks wait for the person's answer.</param>
    /// <param name="standing">The asks the host answered, listed by id where they are not closed.</param>
    /// <param name="offers">The install's own plugins; the room lists the sound ones not installed here.</param>
    /// <param name="parked">The quests the loop's last tick parked by their failed sessions, which a retry names.</param>
    /// <param name="browser">What Daoris's browser's files hold, as the desktop read them; null where none is read.</param>
    /// <param name="held">The quests the loop's last look held by the person's stop, which a retry releases.</param>
    /// <param name="history">What each workspace keeps of finished work, as <c>HISTORY_PLAN</c> reads it; null where none is read.</param>
    /// <param name="workflows">The named workflows under the home (<see cref="WorkflowStore.List"/>), which name the version each repository follows; null where none were read.</param>
    /// <param name="grants">The person's approvals of what plugins hand it (<see cref="HelpGrants.Read"/>); none read, none named.</param>
    /// <param name="notReadYet">What the facts the judges keep had not read yet (<see cref="HelpFacts.Read"/>); none by default.</param>
    /// <param name="workspace">
    /// The workspace whose room it is: its facts projected to it before any section counts or lists them
    /// (<see cref="HelpRoomScope"/>); <c>default</c> or none is the whole machine.
    /// </param>
    public static HelpMachine Describe(
        DriverConfig config, IKnowledgeSnapshot snapshot, IReadOnlyList<RepositoryLine> lines,
        IReadOnlyList<HarnessReport> roster, int asks,
        IReadOnlyList<AskView>? standing = null, PluginCatalog? plugins = null, IReadOnlyList<LandedBranch>? landed = null,
        IReadOnlyList<PluginOffer>? offers = null, IReadOnlyList<ParkedQuest>? parked = null, HelpBrowserFacts? browser = null,
        IReadOnlyList<HeldQuest>? held = null, IKnowledgeSessionLook? sessions = null, IReadOnlyList<HistoryReading>? history = null,
        IReadOnlyList<WorkflowFileFound>? workflows = null, HelpGrantSet? grants = null,
        IReadOnlyList<IKnowledgeMilestone>? work = null, IReadOnlyList<InventoryUnread>? workUnread = null, IReadOnlyList<InventoryEntry>? workRecovery = null,
        HelpNotReadYet? notReadYet = null, string? workspace = null, HelpWaits? waits = null, IAgentFacts? facts = null)
    {
        var sources = new HelpMachineSources(
            config, snapshot, lines, roster, facts, asks, standing ?? [], plugins ?? PluginCatalog.None, landed ?? [], offers ?? [],
            parked ?? [], browser)
        {
            Held = held ?? [], Sessions = sessions, History = history, Workflows = workflows, Grants = grants,
            Work = work, WorkUnread = workUnread ?? [], WorkRecovery = workRecovery ?? [], NotReadYet = notReadYet ?? HelpNotReadYet.None,
            Waits = waits,
        };
        return Describe(sources, ScopeOf(workspace));
    }

    /// <summary>
    /// The machine from the whole machine's <paramref name="whole"/>, as the room of <paramref name="workspace"/> says it: its facts
    /// projected to it first, the whole machine where null. The machine keeps <paramref name="whole"/>, so another
    /// workspace's room is described from the same facts (<see cref="HelpMachine.Within"/>).
    /// </summary>
    /// <param name="focus">
    /// The place its conversation is about, the facts projected to its workspace and kept whole for this machine, the room still the
    /// home's (<see cref="HelpMachine.Home"/>); null is the home's own.
    /// </param>
    internal static HelpMachine Describe(HelpMachineSources whole, string? workspace, HelpFocus? focus = null)
    {
        var place = focus ?? (workspace is null ? HelpFocus.Machine : HelpFocus.OfWorkspace(workspace));
        var sources = HelpRoomScope.Project(whole, place);
        return HelpRoomSections.All.Aggregate(
            new HelpMachine { Workspace = sources.Workspace, Home = workspace, Focus = focus, Whole = whole },
            (machine, section) => section.Describe(machine, sources));
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
/// What the driver hands the room to describe the machine from: its file, the registry, each
/// repository's line, the roster, and the records each section reads. Paths and keys are in here; what a
/// section keeps of them is names and states.
/// </summary>
/// <param name="Product">What a person calls a harness's tool, where its toolchain says.</param>
/// <param name="Asks">How many asks wait for the person's answer.</param>
/// <param name="Standing">The asks the host answered.</param>
/// <param name="Offers">The install's own plugins.</param>
/// <param name="Parked">The quests the loop's last tick parked by their failed sessions.</param>
/// <param name="Browser">What Daoris's browser's files hold, or null where the desktop read none.</param>
internal sealed record HelpMachineSources(
    DriverConfig Config, IKnowledgeSnapshot Snapshot, IReadOnlyList<RepositoryLine> Lines, IReadOnlyList<HarnessReport> Roster,
    IAgentFacts? Facts, int Asks, IReadOnlyList<AskView> Standing, PluginCatalog Plugins,
    IReadOnlyList<LandedBranch> Landed, IReadOnlyList<PluginOffer> Offers, IReadOnlyList<ParkedQuest> Parked,
    HelpBrowserFacts? Browser) : IHelpMachineSources
{
    HelpMachine IHelpMachineSources.Describe(IHelpRoomSection section, HelpMachine machine) => HelpRoomDescriptions.Describe(section, machine, this);
    HelpMachine IHelpMachineSources.Describe(string? workspace) => HelpRoom.Describe(this, workspace);
    HelpMachine IHelpMachineSources.Describe(string? home, HelpFocus focus) => HelpRoom.Describe(this, home, focus);

    /// <summary>The quests the loop's last look held by the person's stop.</summary>
    public IReadOnlyList<HeldQuest> Held { get; init; } = [];

    /// <summary>One look at the sessions as the Sessions list takes it; null where none is read.</summary>
    public IKnowledgeSessionLook? Sessions { get; init; }

    /// <summary>What each workspace keeps of finished work; null where none is read.</summary>
    public IReadOnlyList<HistoryReading>? History { get; init; }

    /// <summary>The named workflows under the home; null where none were read.</summary>
    public IReadOnlyList<IKnowledgeWorkflowFile>? Workflows { get; init; }

    /// <summary>The person's approvals of what plugins hand Ask Daoris; null where none were read.</summary>
    public HelpGrantSet? Grants { get; init; }

    /// <summary>Each unit of work's milestone; null where none is read.</summary>
    public IReadOnlyList<IKnowledgeMilestone>? Work { get; init; }

    /// <summary>What the milestones could not read, each by its repository, which a workspace's room keeps by.</summary>
    public IReadOnlyList<InventoryUnread> WorkUnread { get; init; } = [];

    /// <summary>Daoris's own recoveries no unit names.</summary>
    public IReadOnlyList<InventoryEntry> WorkRecovery { get; init; } = [];

    /// <summary>What the facts the judges keep had not read yet.</summary>
    public HelpNotReadYet NotReadYet { get; init; } = HelpNotReadYet.None;

    /// <summary>What each line waits on, as the exchanges' one read found it; null where it is not read.</summary>
    public HelpWaits? Waits { get; init; }

    /// <summary>The workspace these are projected to (<see cref="HelpRoomScope"/>); null for the whole machine.</summary>
    public string? Workspace { get; init; }

    /// <summary>
    /// Every repository registered here, every workspace's, where these are projected: what the checkouts it may read
    /// are judged from. Those are what its spawn is granted, so the room lists what its rules allow.
    /// Null for the whole machine, whose <see cref="HelpMachineSources.Snapshot"/> holds them all.
    /// </summary>
    public IReadOnlyList<RepoView>? Registry { get; init; }
}
