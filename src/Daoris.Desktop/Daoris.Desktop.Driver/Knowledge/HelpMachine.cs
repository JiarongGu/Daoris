namespace Daoris.Driver;

/// <summary>
/// What the room says this machine holds now: names and states, never a key, and a path only for a checkout
/// the helper may read.
/// </summary>
/// <remarks>Each section says its facts in its own file; this one declaration keeps the facts, under a comment per section.</remarks>
public sealed record HelpMachine
{
    // The intro's, which the may-do section reads too.

    /// <summary>
    /// The access mode its conversation runs in, as the driver's file said when the room is written:
    /// one of <see cref="HelpModeNames"/>. The room is written again at each start, so it says the mode that start is placed under.
    /// </summary>
    public string Mode { get; init; } = HelpModeNames.Restricted;

    /// <summary>
    /// The housekeeping operations the person granted it (<see cref="ConfiguredAccess"/>), read from the record as the
    /// room is written at a start (<see cref="HelpRoom.Prepare"/>); none where none is chosen, as a machine described by hand has.
    /// </summary>
    public IReadOnlyList<string> Housekeeping { get; init; } = [];

    /// <summary>
    /// The chosen settings the person granted it (<see cref="ConfiguredAccess.Settings"/>), read as
    /// <see cref="Housekeeping"/> is; none where none is chosen.
    /// </summary>
    public IReadOnlyList<string> Settings { get; init; } = [];

    /// <summary>
    /// The checkouts the person turned off for it (<see cref="ConfiguredAccess.CheckoutsOff"/>), read as
    /// <see cref="Housekeeping"/> is: the room lists the readable ones less these, and names these apart.
    /// </summary>
    public IReadOnlyList<string> CheckoutsOff { get; init; } = [];

    // The room's scope.

    /// <summary>
    /// The workspace whose room this is, its facts projected to it; null for <c>default</c>'s room,
    /// the whole machine.
    /// </summary>
    public string? Workspace { get; init; }

    /// <summary>The whole machine's facts it is described from, so another workspace's room is described from the same; null where it is built by hand.</summary>
    internal IHelpMachineSources? Whole { get; init; }

    /// <summary>
    /// The room of <paramref name="workspace"/>: this machine's facts projected to it before any count or limit, said
    /// again by every section; itself for <c>default</c>, or none named, whose room is the whole machine. One built by hand, with no
    /// facts to project, is only named for the workspace; one altered with <c>with</c> after it is described is described again
    /// from its facts, so the alteration is not kept.
    /// </summary>
    public HelpMachine Within(string? workspace)
    {
        var scope = KnowledgeHelp.Current.ScopeOf(workspace);
        if (Focus is null && string.Equals(scope, Workspace, StringComparison.OrdinalIgnoreCase)) return this;
        return Whole is { } whole ? whole.Describe(scope) : this with { Workspace = scope, Focus = null, Home = scope };
    }

    /// <summary>
    /// The room of <paramref name="workspace"/>, its conversation's home, written for <paramref name="focus"/>: the machine's facts
    /// projected to the focus's workspace, or kept whole for this machine, in the home's folder (<see cref="Home"/>). Null is
    /// <see cref="Within(string?)"/>, the home's own focus.
    /// </summary>
    public HelpMachine Within(string? workspace, HelpFocus? focus)
    {
        if (focus is null) return Within(workspace);
        var home = KnowledgeHelp.Current.ScopeOf(workspace);
        if (focus.Equals(Focus) && string.Equals(home, Home, StringComparison.OrdinalIgnoreCase)) return this;
        return Whole is { } whole
            ? whole.Describe(home, focus)
            : this with { Workspace = KnowledgeHelp.Current.ScopeOf(focus.Workspace), Focus = focus, Home = home };
    }

    /// <summary>The place its conversation is about, which the room is written for; null for its home's own (<see cref="HelpFocus.Home"/>).</summary>
    public HelpFocus? Focus { get; init; }

    /// <summary>
    /// The workspace whose room folder it is written in, its conversation's home: <see cref="Workspace"/> unless a focus elsewhere
    /// projects its facts to another; null for <c>default</c>'s.
    /// </summary>
    public string? Home
    {
        get => _homeSet ? _home : Workspace;
        init
        {
            _home = value;
            _homeSet = true;
        }
    }

    private readonly string? _home;
    private readonly bool _homeSet;

    // The agents section's.

    public IReadOnlyList<HelpAgent> Agents { get; init; } = [];

    // The roles section's.

    /// <summary>
    /// The working roles with their choices, policy and last seven days, read from the home as the room is written
    /// (<see cref="HelpRoom.Prepare"/>); none for a machine described by hand.
    /// </summary>
    public IReadOnlyList<HelpRole> Roles { get; init; } = [];

    /// <summary>
    /// The supervisor's rules, one line per place holding some, in the terminal's words, read as the room is written
    /// (<see cref="HelpRoom.Prepare"/>); none for a machine described by hand.
    /// </summary>
    public IReadOnlyList<string> Rules { get; init; } = [];

    // The asks section's.

    /// <summary>The asks not closed, newest first.</summary>
    public IReadOnlyList<HelpAsk> OpenAsks { get; init; } = [];

    // The landing section's.

    /// <summary>The plugins a branch rule may name here, by id.</summary>
    public IReadOnlyList<string> LandingPlugins { get; init; } = [];

    // The machine-now section's.

    /// <summary>The harness quests run on.</summary>
    public string? Adapter { get; init; }

    /// <summary>The harness asks are answered on, or null for none.</summary>
    public string? Intake { get; init; }

    /// <summary>The harness Ask Daoris runs on.</summary>
    public string? Helper { get; init; }

    public int Cap { get; init; }

    /// <summary>How many minutes a session may say nothing and run nothing before Daoris looks in on it (<see cref="QuietSetting"/>).</summary>
    public int QuietMinutes { get; init; } = (int)QuietSetting.Default;

    /// <summary>
    /// The sessions waiting on the person, as the Sessions list's <i>Waiting on you</i> holds them less Ask Daoris's
    /// own, by id, so a go names one the room is shown; the room's count is theirs.
    /// </summary>
    public IReadOnlyList<HelpWaitingSession> Waiting { get; init; } = [];

    /// <summary>This machine's sessions by the Sessions list's groups, and the ended ones by id; null where none is read.</summary>
    public HelpSessions? Sessions { get; init; }

    /// <summary>How many asks wait for the person's answer.</summary>
    public int Asks { get; init; }

    /// <summary>Every plugin installed here, sound or not, in the catalogue's order.</summary>
    public IReadOnlyList<HelpPlugin> Plugins { get; init; } = [];

    /// <summary>The branches this machine's landings made and recorded, in the order they landed.</summary>
    public IReadOnlyList<HelpLanded> Landed { get; init; } = [];

    /// <summary>The quests the loop's last tick parked by their failed sessions, as the room is written.</summary>
    public IReadOnlyList<IKnowledgeParkedQuest> Parked { get; init; } = [];

    /// <summary>The quests the loop's last look held by the person's stop, as the room is written.</summary>
    public IReadOnlyList<IKnowledgeHeldQuest> Held { get; init; } = [];

    /// <summary>
    /// The asks and quests paused on this machine, as <c>driver.json</c> keeps them: asks first, each
    /// by its id, so a resume names one the room is shown.
    /// </summary>
    public IReadOnlyList<PausedBy> Paused { get; init; } = [];

    /// <summary>Daoris's browser as its files hold it, or null where the desktop read none.</summary>
    public HelpBrowser? Browser { get; init; }

    /// <summary>
    /// What each workspace keeps of finished work, what a clear would take now and what stays by the word that keeps it:
    /// counts and bytes, never a path or a title, so a clear names a workspace that keeps something.
    /// Null where none is read.
    /// </summary>
    public IReadOnlyList<HistoryReading>? History { get; init; }

    /// <summary>
    /// What the facts the judges keep had not read yet when the room is written: each said <i>not read
    /// yet</i> where its section says it, and the room written again as they land.
    /// </summary>
    public HelpNotReadYet NotReadYet { get; init; } = HelpNotReadYet.None;

    // The may-do section's.

    /// <summary>The checkouts it may read, by name; a path here is one reading across allows, and no other.</summary>
    public IReadOnlyList<HelpRead> Reads { get; init; } = [];

    /// <summary>The servers plugins hand it, as the person approved them, each with the tools it may call there, in catalogue order.</summary>
    public IReadOnlyList<HelpPluginServer> PluginServers { get; init; } = [];

    /// <summary>The servers plugins offer it that wait for the person's approval, so it is handed none of them.</summary>
    public IReadOnlyList<HelpPluginServer> PluginServersWaiting { get; init; } = [];

    /// <summary>
    /// The servers the install's newer copy of a plugin installed here would offer it, which the installed copy does not:
    /// its update first, then the person's approval.
    /// </summary>
    public IReadOnlyList<HelpPluginServerOffered> PluginServersOffered { get; init; } = [];

    /// <summary>The skills plugins hand it, as the harness names them, <c>plugin:skill</c>.</summary>
    public IReadOnlyList<string> PluginSkills { get; init; } = [];

    // The work section's.

    /// <summary>Each unit of work's milestone as the records say it; null where none is read.</summary>
    public IReadOnlyList<IKnowledgeMilestone>? Work { get; init; }

    /// <summary>What the milestones could not read, each in a clause (a repository with no checkout here).</summary>
    public IReadOnlyList<string> WorkUnread { get; init; } = [];

    /// <summary>Daoris's own recoveries no unit names (<see cref="WorkInventory.Recoveries"/>): a folder moved aside, a recovery ref.</summary>
    public IReadOnlyList<InventoryEntry> WorkRecovery { get; init; } = [];

    // The exchanges' section's.

    /// <summary>What each line waits on, as the exchanges' one read found it; null where it is not read.</summary>
    public HelpWaits? Waits { get; init; }

    // The own-plugins section's.

    /// <summary>The install's own plugins not installed here and sound, which an add may name by id.</summary>
    public IReadOnlyList<HelpOffer> Offers { get; init; } = [];

    // The workspaces section's.

    public IReadOnlyList<HelpRepository> Repositories { get; init; } = [];

    /// <summary>The kinds of work each workspace declares, by workspace.</summary>
    public IReadOnlyList<HelpWorkflowKind> WorkflowKinds { get; init; } = [];
}
