namespace Daoris.Driver;

/// <summary>A plugin's answer at a decision point (D64 §4): allow, or hold with the sentence a person reads.</summary>
public sealed record HookDecision(bool Allowed, string? Reason = null)
{
    public static readonly HookDecision Allow = new(true);

    public static HookDecision Hold(string reason) => new(false, reason);
}

/// <summary>
/// The points a plugin may listen on, named here once. A handshake naming one this build lacks is said once and never
/// asked there (PLUGHOOK1a, D148 point 8): a plugin that grew a point keeps the points this build has.
/// </summary>
/// <remarks>
/// <b>A point added</b> (STATEHUB1g's list) goes last in <see cref="All"/>, and in the same commit: its frame in
/// <see cref="HookFrames"/>; its call on <see cref="IHookChannel"/> (a default for the fakes), <see cref="HookPeer"/> and
/// <see cref="HookProcess"/>; its kit entry and sample (<see cref="PluginKit"/>), its <c>ANSWERS</c> entry in the kit's
/// <c>plugin.test.mjs.template</c> and its case in <see cref="PluginTrials"/>; where a failure there costs
/// (<c>PluginsCommand.Cost</c>); Ask Daoris's sentence (<c>HelpRoomMakingAPlugin.PointSaid</c>, its goldens rewritten); the rows
/// of <c>PluginKitTests</c> and the kit's list in the modules' <c>DriverModulePluginsTests</c>; the page's gloss
/// (<c>plugins/PluginKit.tsx</c>) where its kind's does not say when it speaks; and <c>twins.md</c>'s answer row.
/// </remarks>
public static class HookPoints
{
    /// <summary>A decision: one consideration the planner marked <i>Start</i>. The first hold in catalogue order ends the waterfall.</summary>
    public const string QuestConsider = "quest/consider";

    /// <summary>An observation: what a tick concluded. Contained — nothing a plugin says here changes anything.</summary>
    public const string SessionEnded = "session/ended";

    /// <summary>
    /// An act, after Daoris's own (WSR4, D100): a branch rule's landing has made its branch, and the plugin
    /// the rule names pushes it and opens the pull request for its platform. Spoken by the landing, never
    /// by the loop, and it can undo nothing: a failure here leaves the branch.
    /// </summary>
    public const string Land = "work/land";

    /// <summary>
    /// A query (PLUGHOOK1a, D148): a landed branch's pull request, asked of the plugin that pushed it, which only its platform
    /// knows. Asked only at an occasion that may remove something, and acted on only where git confirms the answer. An
    /// unanswered query is not known, never a default.
    /// </summary>
    public const string State = "work/state";

    /// <summary>
    /// An observation (STATEHUB1g, D169 point 9): what a press, or a terminal's verb, changed of what the driver holds, as the
    /// shell's bus told every window. Contained, as <see cref="SessionEnded"/> is, and told off the press's path: the press has
    /// answered before any plugin hears, and nothing a plugin says or fails to say holds, changes or refuses that answer.
    /// </summary>
    public const string StateChanged = "state/changed";

    /// <summary>
    /// Every point, in the order they were added: a point is additive, so a new one goes last (PLUGHOOK1a). The work wire's three joined
    /// with their kit entries (FABRIC-PERFORM1e), so the kit scaffolds, tests and tries them as it does the rest; who speaks them is still
    /// <see cref="Work"/>'s rule.
    /// </summary>
    public static readonly IReadOnlyList<string> All = [QuestConsider, SessionEnded, Land, State, StateChanged, Watch, Step, Perform];

    /// <summary>
    /// The points the driver loop's set speaks at — a plugin that speaks on none of them is not kept running beside it. The
    /// shell's loop tells <see cref="StateChanged"/> from its bus; a headless loop has no bus, so it starts such a plugin and
    /// tells it nothing (D169's STATEHUB1g note).
    /// </summary>
    public static readonly IReadOnlyList<string> Loop = [QuestConsider, SessionEnded, StateChanged];

    /// <summary>
    /// A loop point of the work wire (FABRIC-PERFORM1a, the fabric events design §C4): which units await the plugin's steps, the whole set
    /// each time it changes, with the process's generation and whether to send again what it knows (<see cref="WorkWatchFrame"/>).
    /// </summary>
    public const string Watch = "work/watch";

    /// <summary>
    /// An event (§C4): a JSON-RPC notification the plugin sends, never answered, read only from the process the lease's loop started for it
    /// (<see cref="WorkStepFrame"/>). Named in the manifest, so a person reads that the plugin reports steps.
    /// </summary>
    public const string Step = "work/step";

    /// <summary>A request (§D6): a performed step's attempt, acknowledged on receipt, its outcome arriving later at <see cref="Step"/>.</summary>
    public const string Perform = "work/perform";

    /// <summary>
    /// The work wire's points (FABRIC-PERFORM1a): spoken only by the loop that holds the home's lease, on a process it supervises
    /// (<see cref="HookSet"/>), never by a verb, a landing or a <c>--share</c> loop. Since FABRIC-PERFORM1e they are in <see cref="All"/> too,
    /// whose every point the kit scaffolds, answers in its test and lets a person try.
    /// </summary>
    public static readonly IReadOnlyList<string> Work = [Watch, Step, Perform];
}

/// <summary>
/// What <see cref="HookPoints.StateChanged"/> is told (STATEHUB1g): the shell's <c>STATE_CHANGED</c>, as every window hears it
/// (D169 point 3). What moved, never what it is now: no words, no answer, no machine path.
/// </summary>
/// <param name="Scopes">The families of state that moved, in the screen's words (<c>sessions</c>, <c>landing</c>, …).</param>
/// <param name="Sessions">The sessions the change was about, or null where it was not one session's: any.</param>
public sealed record StateChangedFrame(IReadOnlyList<string> Scopes, IReadOnlyList<string>? Sessions);

/// <summary>
/// What each point is sent, built in one place (PLUG8): the loop and the landing send these, and the
/// plugin kit's sample frames are built by the same functions, so a sample cannot say what the driver
/// does not. Every name is spelled here rather than left to a serializer's policy.
/// </summary>
public static class HookFrames
{
    /// <summary><see cref="HookPoints.QuestConsider"/>: one planned start.</summary>
    public static object Consider(Consideration start) => new
    {
        quest = new { id = start.Quest.Id, title = start.Quest.Title, from = start.Quest.From, to = start.Quest.To },
        repository = start.Quest.To,
        workspace = start.Workspace,
        root = start.Root,
    };

    /// <summary><see cref="HookPoints.SessionEnded"/>: what a tick concluded.</summary>
    public static object Ended(SessionEnded ended) => new
    {
        session = ended.Session,
        quest = ended.Quest,
        repository = ended.Repository,
        state = ended.State,
        adapter = ended.Adapter,
        account = ended.Account,
        byPerson = ended.ByPerson,
        note = ended.Note,
    };

    /// <summary>
    /// <see cref="HookPoints.Land"/>: the branch a landing just made (D100), or moved on (LAND2c): the pull request already open
    /// from it, which a plugin grows rather than opening a second, who accepted the work, and the commit judged, which it pushes
    /// (LANDPIN2).
    /// </summary>
    public static object Land(LandingFrame frame) => new
    {
        repository = frame.Repository,
        workspace = frame.Workspace,
        root = frame.Root,
        branch = frame.Branch,
        @base = frame.Base,
        title = frame.Title,
        quest = frame.Quest is { } quest ? new { id = quest, title = frame.Title } : null,
        session = frame.Session,
        commits = frame.Commits.Select(commit => new { sha = commit.Sha, subject = commit.Subject }).ToArray(),
        pullRequest = frame.PullRequest,
        acceptedBy = frame.AcceptedBy,
        tip = frame.Tip,
        origin = frame.Origin,
    };

    /// <summary>
    /// <see cref="HookPoints.State"/>: a landed branch, asked about (PLUGHOOK1a, design §2.2): where it is, the line, and the pull
    /// request and the commit the record holds, so a plugin finds the pull request by its address, else by its source branch; and
    /// <c>origin</c>, as the land frame carries it (GITAUTH1b).
    /// </summary>
    public static object State(StateFrame frame) => new
    {
        repository = frame.Repository,
        workspace = frame.Workspace,
        root = frame.Root,
        branch = frame.Branch,
        line = frame.Line,
        pullRequest = frame.PullRequest,
        pushedTip = frame.PushedTip,
        origin = frame.Origin,
    };

    /// <summary>
    /// <see cref="HookPoints.StateChanged"/>: what moved (STATEHUB1g), the event's own two fields, <c>sessions</c> null where the
    /// change was about no one session.
    /// </summary>
    public static object Changed(StateChangedFrame frame) => new
    {
        scopes = frame.Scopes.ToArray(),
        sessions = frame.Sessions?.ToArray(),
    };
}
