namespace Daoris.Driver;

/// <summary>
/// The points a plugin may listen on, named here once. A handshake naming one this build lacks is said once and never
/// asked there: a plugin that grew a point keeps the points this build has.
/// </summary>
/// <remarks>
/// <b>A point added</b> goes last in <see cref="All"/>, and in the same commit: its frame in
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
    /// A query: a landed branch's pull request, asked of the plugin that pushed it, which only its platform
    /// knows. Asked only at an occasion that may remove something, and acted on only where git confirms the answer. An
    /// unanswered query is not known, never a default.
    /// </summary>
    public const string State = "work/state";

    /// <summary>
    /// An observation: what a press, or a terminal's verb, changed of what the driver holds, as the
    /// shell's bus told every window. Contained, as <see cref="SessionEnded"/> is, and told off the press's path: the press has
    /// answered before any plugin hears, and nothing a plugin says or fails to say holds, changes or refuses that answer.
    /// </summary>
    public const string StateChanged = "state/changed";

    /// <summary>
    /// Every point, in the order they were added: a point is additive, so a new one goes last. The work wire's three and the system
    /// points joined with their kit entries, so the kit scaffolds, tests and tries them as it does the rest; who speaks them is still
    /// <see cref="Work"/>'s rule and <see cref="System"/>'s.
    /// </summary>
    public static readonly IReadOnlyList<string> All = [QuestConsider, SessionEnded, State, StateChanged, Watch, Step, Perform, Event, PlanRead, ApplyPerform];

    /// <summary>
    /// The points the driver loop's set speaks at — a plugin that speaks on none of them is not kept running beside it. The
    /// shell's loop tells <see cref="StateChanged"/> from its bus; a headless loop has no bus, so it starts such a plugin and
    /// tells it nothing.
    /// </summary>
    public static readonly IReadOnlyList<string> Loop = [QuestConsider, SessionEnded, StateChanged];

    /// <summary>
    /// A loop point of the work wire: which units await the plugin's steps, the whole set
    /// each time it changes, with the process's generation and whether to send again what it knows (<see cref="WorkWatchFrame"/>).
    /// </summary>
    public const string Watch = "work/watch";

    /// <summary>
    /// An event: a JSON-RPC notification the plugin sends, never answered, read only from the process the lease's loop started for it
    /// (<see cref="WorkStepFrame"/>). Named in the manifest, so a person reads that the plugin reports steps.
    /// </summary>
    public const string Step = "work/step";

    /// <summary>A request: a performed step's attempt, acknowledged on receipt, its outcome arriving later at <see cref="Step"/>.</summary>
    public const string Perform = "work/perform";

    /// <summary>
    /// The work wire's points: spoken only by the loop that holds the home's lease, on a process it supervises
    /// (<see cref="HookSet"/>), never by a verb, a landing or a <c>--share</c> loop. They are in <see cref="All"/> too,
    /// whose every point the kit scaffolds, answers in its test and lets a person try.
    /// </summary>
    public static readonly IReadOnlyList<string> Work = [Watch, Step, Perform];

    /// <summary>
    /// An observation: what moved in the work cycle, as ids and closed codes, told only by the loop
    /// that holds the home's lease (<see cref="WorkEventPoint"/>), each frame's answer awaited before the next and never read.
    /// </summary>
    public const string Event = "work/event";

    /// <summary>A system adapter's request: intended changes answered as typed operations with their preconditions (<see cref="SystemWire"/>).</summary>
    public const string PlanRead = "plan/read";

    /// <summary>A system adapter's request: a reviewed change set applied under its preconditions, answered applied, stale or failed.</summary>
    public const string ApplyPerform = "apply/perform";

    /// <summary>Asked of a plugin declaring <c>systems</c> by a plan and an apply alone, never by the loop's set; in <see cref="All"/> too, whose every point the kit scaffolds and tries.</summary>
    public static readonly IReadOnlyList<string> System = [PlanRead, ApplyPerform];
}

/// <summary>
/// Who a plugin's server is handed to, as a manifest's <c>to</c> spells it. Twin: the CLI's
/// <c>SERVER_TO</c>.
/// </summary>
public static class PluginServerTo
{
    /// <summary>Every session in a repository: driven, a conversation, an intake.</summary>
    public const string Sessions = "sessions";

    /// <summary>Ask Daoris, in its room.</summary>
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
