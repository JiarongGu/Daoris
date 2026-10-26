namespace Daoris.Driver;

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
    /// one of <see cref="HelpModeNames"/>. The room is written again at each start, so it says the mode that start was placed under.
    /// </summary>
    public string Mode { get; init; } = HelpModeNames.Restricted;

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
    internal IHelpMachineSources? Whole { get; init; }

    /// <summary>
    /// The room of <paramref name="workspace"/> (ASKSCOPE1b): this machine's facts projected to it before any count or limit, said
    /// again by every section; itself for <c>default</c>, or none named, whose room is the whole machine. One built by hand, with no
    /// facts to project, is only named for the workspace; one altered with <c>with</c> after it was described is described again
    /// from its facts, so the alteration is not kept.
    /// </summary>
    public HelpMachine Within(string? workspace)
    {
        var scope = KnowledgeHelp.Current.ScopeOf(workspace);
        if (string.Equals(scope, Workspace, StringComparison.OrdinalIgnoreCase)) return this;
        return Whole is { } whole ? whole.Describe(scope) : this with { Workspace = scope };
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
    public IReadOnlyList<IKnowledgeParkedQuest> Parked { get; init; } = [];

    /// <summary>The quests the loop's last look held by the person's stop, as the room was written (SESSUX1b).</summary>
    public IReadOnlyList<IKnowledgeHeldQuest> Held { get; init; } = [];

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
    public IReadOnlyList<IKnowledgeMilestone>? Work { get; init; }

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
