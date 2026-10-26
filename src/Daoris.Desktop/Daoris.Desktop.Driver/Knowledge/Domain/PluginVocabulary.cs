namespace Daoris.Driver;

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
/// Who a plugin's server is handed to (HELPPLUG1a, D170 point 3), as a manifest's <c>to</c> spells it. Twin: the CLI's
/// <c>SERVER_TO</c>.
/// </summary>
public static class PluginServerTo
{
    /// <summary>Every session in a repository: driven, a conversation, an intake (INT1).</summary>
    public const string Sessions = "sessions";

    /// <summary>Ask Daoris, in its room (D170 point 3).</summary>
    public const string Help = "help";

    /// <summary>Each destination a manifest may name, in its order.</summary>
    public static readonly IReadOnlyList<string> All = [Sessions, Help];
}

/// <summary>The states of a plugin server approval as the room and its grants read them.</summary>
public static class HelpGrantStates
{
    public const string Approved = "approved";
    public const string Waits = "waits";
}
