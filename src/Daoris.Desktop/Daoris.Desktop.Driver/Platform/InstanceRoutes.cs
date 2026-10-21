namespace Daoris.Driver;

/// <summary>
/// How the running Daoris's pipe treats a route (RPC1a's census, the terminal RPC design §4.2), as a verb reads it before it looks
/// for the instance: a twin of the modules' <c>PipeClass</c>, which the driver library cannot see, held to it route by route by
/// <c>PipeCensusTests</c>. A wire's words, added to and never renamed.
/// </summary>
public enum RouteClass
{
    /// <summary>A read, or a press D156 §3 leaves to an agent or to the driver: the instance runs it as asked.</summary>
    Open,

    /// <summary>
    /// A press D156 §3 makes the person's alone: sent to the instance that answers, which shows it in its window and runs it on the
    /// person's Confirm (RPC1c), the verb saying it waits; a verb with no client path yet acts alone, as it did.
    /// </summary>
    Person,

    /// <summary>What only a window does: no terminal verb sends it.</summary>
    Window,
}

/// <summary>A route a terminal verb sends over the pipe, with the class the census gives it (RPC1b).</summary>
/// <param name="Module">The module, as the kit's dispatcher names it.</param>
/// <param name="Route">The route, as its module's table names it.</param>
/// <param name="Class">The census's class, held to it by <c>PipeCensusTests</c>.</param>
public sealed record InstanceRoute(string Module, string Route, RouteClass Class);

/// <summary>
/// The open routes the host's verbs send over the pipe (RPCOPEN1, RPC1e, the terminal RPC design §7.1): the reads, which go through the
/// instance where one answers and alone otherwise, and the two presses the census leaves open, <c>SYNC_NOW</c> and
/// <c>REGISTRY_REFRESH</c>. Held to the census route by route by <c>PipeCensusTests</c>, which also names each verb left off the pipe
/// with its reason (<see cref="Verbs"/>).
/// </summary>
public static class OpenRoutes
{
    private static InstanceRoute Open(string route) => new(TreesRoutes.DriverModule, route, RouteClass.Open);

    /// <summary><c>trees clean</c>'s list: every session branch here with what it holds.</summary>
    public static InstanceRoute SweepPlan { get; } = Open("SWEEP_PLAN");

    /// <summary><c>trees sync</c>'s list: what bringing the repositories up to date would change.</summary>
    public static InstanceRoute TreesSyncPlan { get; } = Open("TREES_SYNC_PLAN");

    /// <summary><c>trees land &lt;session&gt; --plan</c>: where accepting the session would put its work.</summary>
    public static InstanceRoute Landing { get; } = Open("LANDING");

    /// <summary><c>trees land --entry &lt;id&gt; --plan</c>.</summary>
    public static InstanceRoute EntryLanding { get; } = Open("ENTRY_LANDING");

    /// <summary><c>trees land --ask &lt;id&gt; --plan</c>.</summary>
    public static InstanceRoute AskLanding { get; } = Open("ASK_LANDING");

    /// <summary><c>trees hand &lt;session|branch&gt; --plan</c>.</summary>
    public static InstanceRoute HandoffPlan { get; } = Open("HANDOFF_PLAN");

    /// <summary><c>sessions</c>' list.</summary>
    public static InstanceRoute SessionGroups { get; } = Open("SESSION_GROUPS");

    /// <summary><c>sync</c>: the tick's own pass for one circle.</summary>
    public static InstanceRoute SyncNow { get; } = Open("SYNC_NOW");

    /// <summary><c>register</c>: one repository followed from its line.</summary>
    public static InstanceRoute RegistryRefresh { get; } = Open("REGISTRY_REFRESH");

    /// <summary><c>space free --yes</c>'s follow (KEEP1e): the disk space read each second, its <c>run</c> until it ends.</summary>
    public static InstanceRoute SystemSpace { get; } = Open("SYSTEM_SPACE");

    /// <summary>
    /// Each verb of RPCOPEN1's scope, in the verb's words: the route it sends where the instance answers, or null with the reason it
    /// does not go through the pipe (yet).
    /// </summary>
    public static IReadOnlyList<(string Verb, InstanceRoute? Route, string Reason)> Verbs { get; } =
    [
        ("trees clean", SweepPlan, "the list; `--yes` is the person's press and acts alone until its client path is built"),
        ("trees sync", TreesSyncPlan, "the list; `--yes` is the person's press and acts alone until its client path is built"),
        ("trees land <session> --plan", Landing, "the plan; the press is the person's and acts alone until its client path is built"),
        ("trees land --entry <id> --plan", EntryLanding, "the plan; the press is the person's and acts alone until its client path is built"),
        ("trees land --ask <id> --plan", AskLanding, "the plan; the press is the person's and acts alone until its client path is built"),
        ("trees hand <session|branch> --plan", HandoffPlan, "the plan; the press is the person's and acts alone until its client path is built"),
        ("sessions", SessionGroups, "the list"),
        ("sync", SyncNow, "an open press (D156 §3: sync)"),
        ("register", RegistryRefresh, "an open press (D156 §3: refresh)"),
        ("space free --yes", SystemSpace, "the run its press started, read until it ends (KEEP1e)"),
        // The other open reads of RPC1e: each is read alone beside an instance, with the same words, until its route answers them as
        // `said` as the six above do (RPCOPEN1 built the shape, and the reads that follow it name no new one).
        ("trace", null, "alone: its words are built from the sources the host reads itself; TRACE answers the page's shape only"),
        ("git branches", null, "alone: its words are built from the registry's checkouts the host reads itself; GIT_BRANCHES answers the page's shape only"),
        ("workflow", null, "alone: WORKFLOW_RUN answers the page's shape only, and `workflow keep` writes its run"),
        ("help list", null, "alone: HELP_CONVERSATIONS answers the page's shape only"),
        ("history", null, "alone: HISTORY_PLAN answers the page's shape only, and its clear is a person's press"),
        ("plugins show", null, "alone: PLUGIN_READ answers the page's shape only"),
    ];
}

/// <summary>
/// The person's presses the host's verbs send over the pipe (RPC1c, D156 §4.2, the terminal RPC design §4.2 and §7.2): each goes to
/// the instance that answers, which shows it in its window naming this terminal, and runs on the person's Confirm alone; the verb says
/// it waits, then says the confirmed press's answer in its own words, or the person's refusal or the two minutes' end, exit 1. Where
/// no instance runs, the verb acts alone, as it did. Held to the census route by route by <c>PipeCensusTests</c>.
/// </summary>
/// <remarks>
/// The first, <c>sessions archive</c> and <c>unarchive</c>, prove the door; the other sessions verbs (RPC1d, built the same way), the
/// host's presses (RPC1e) and the CLI's edits (RPC1f) follow it, each with its words from its route's answer (<c>said</c>), held to
/// the alone path's.
/// </remarks>
public static class PersonPresses
{
    /// <summary><c>sessions archive &lt;id&gt;…</c> and <c>unarchive &lt;id&gt;…</c>: this machine's marks, the person's close.</summary>
    public static InstanceRoute SessionArchive { get; } = new(TreesRoutes.DriverModule, "SESSION_ARCHIVE", RouteClass.Person);

    private static InstanceRoute Person(string route) => new(TreesRoutes.DriverModule, route, RouteClass.Person);

    /// <summary><c>sessions stop &lt;id&gt;</c>: a session stopped.</summary>
    public static InstanceRoute SessionStop { get; } = Person("STOP_SESSION");

    /// <summary><c>sessions stop &lt;id&gt; --turn</c>: the keeping stop, a session's turn stopped so what waits goes next (COWORK1a).</summary>
    public static InstanceRoute SessionStopTurn { get; } = Person("CANCEL_TURN");

    /// <summary><c>sessions finish</c> and <c>decline</c>: a parked session's resolution.</summary>
    public static InstanceRoute SessionResolve { get; } = Person("RESOLVE_SESSION");

    /// <summary><c>sessions say</c>: the person's words to a session.</summary>
    public static InstanceRoute SessionSay { get; } = Person("SESSION_INPUT");

    /// <summary><c>sessions take-back</c> and <c>reword</c>: a word waiting on a session, taken back or reworded.</summary>
    public static InstanceRoute SessionQueueEdit { get; } = Person("SESSION_QUEUE_EDIT");

    /// <summary><c>sessions delete &lt;id&gt;</c>: a conversation's record deleted.</summary>
    public static InstanceRoute SessionDelete { get; } = Person("SESSION_DELETE");

    /// <summary><c>sessions go-on-new &lt;id&gt;</c>: words a resume holds go on in a new session.</summary>
    public static InstanceRoute SessionGoOnNew { get; } = Person("SESSION_GO_ON_NEW");

    /// <summary><c>sessions start-from &lt;id&gt;</c>: a conversation started with the words a session could not go on with.</summary>
    public static InstanceRoute SessionStartFrom { get; } = Person("SESSION_START_FROM");

    /// <summary><c>sessions hand-over &lt;id&gt;</c>: a conversation handed to another agent.</summary>
    public static InstanceRoute SessionHandOver { get; } = Person("SESSION_HAND_OVER");

    /// <summary><c>sessions helper &lt;id&gt; "…"</c>: the person's ask of a helper.</summary>
    public static InstanceRoute SessionHelperAsk { get; } = Person("SESSION_HELPER_ASK");

    /// <summary><c>sessions helper --approve &lt;ask&gt;</c>: the yes to an agent's ask of a helper.</summary>
    public static InstanceRoute SessionHelperApprove { get; } = Person("SESSION_HELPER_APPROVE");

    /// <summary><c>sessions helper --decline &lt;ask&gt;</c>: the no to an agent's ask of a helper.</summary>
    public static InstanceRoute SessionHelperDecline { get; } = Person("SESSION_HELPER_DECLINE");

    /// <summary><c>sessions hand-over &lt;id&gt; --approve</c>: the yes to an agent's ask to hand its session over (MIXAGENT1c).</summary>
    public static InstanceRoute SessionHandOverApprove { get; } = Person("SESSION_HAND_OVER_APPROVE");

    /// <summary><c>sessions hand-over &lt;id&gt; --decline</c>: the no to an agent's ask to hand its session over (MIXAGENT1c).</summary>
    public static InstanceRoute SessionHandOverDecline { get; } = Person("SESSION_HAND_OVER_DECLINE");

    /// <summary>
    /// <c>space free --yes</c> (KEEP1e, the cleanup design §3.3): a run removing what the person's list showed, their delete. The list is
    /// the same route's <c>plan</c> form, open (a <c>PipeRule</c> in the census), which the verb reads alone since a list runs anywhere.
    /// </summary>
    public static InstanceRoute SpaceFree { get; } = Person("SYSTEM_SPACE_FREE");

    /// <summary>Each person's press a verb sends over the pipe, in the verb's words, with its route.</summary>
    public static IReadOnlyList<(string Verb, InstanceRoute Route)> Presses { get; } =
    [
        ("space free --yes", SpaceFree),
        ("sessions archive <id>…", SessionArchive),
        ("sessions unarchive <id>…", SessionArchive),
        ("sessions stop <id>", SessionStop),
        ("sessions stop <id> --turn", SessionStopTurn),
        ("sessions finish <id>", SessionResolve),
        ("sessions decline <id>", SessionResolve),
        ("sessions say <id> \"…\"", SessionSay),
        ("sessions take-back <id> <word>", SessionQueueEdit),
        ("sessions reword <id> <word> \"…\"", SessionQueueEdit),
        ("sessions delete <id>", SessionDelete),
        ("sessions go-on-new <id>", SessionGoOnNew),
        ("sessions start-from <id>", SessionStartFrom),
        ("sessions hand-over <id>", SessionHandOver),
        ("sessions hand-over <id> --approve", SessionHandOverApprove),
        ("sessions hand-over <id> --decline", SessionHandOverDecline),
        ("sessions helper <id> \"…\"", SessionHelperAsk),
        ("sessions helper --approve <ask>", SessionHelperApprove),
        ("sessions helper --decline <ask>", SessionHelperDecline),
    ];
}

/// <summary>
/// The routes the <c>trees</c> verbs' presses are twins of (RPC1b, the terminal RPC design §1.2 and §7.2): each verb acts alone, as it
/// does today, until its client path is built.
/// </summary>
/// <remarks>
/// 🔴 Every one is the person's (D171's RPC1a note: a landing is the person's yes, a discard and a sweep their deletes, and
/// <c>TREES_SYNC</c> moves their lines and branches). RPC1c built the window's confirmation of a person's press, which
/// <see cref="PersonPresses"/>' verbs use; a trees verb that becomes a client sends its route through
/// <see cref="InstanceDoor.PressAsync"/> as they do, and the three land presses send <c>follow: true</c> (RPC1b5), so the lines they
/// move are followed as the alone path follows them. Its words come from the route's answer, held to the alone path's case for case
/// (§7.3): each route answers them as <c>said</c>, which <see cref="TreesSaid.Words"/> prints and <see cref="TreesWords"/> builds for
/// the alone path and the route alike (RPC1b3).
/// </remarks>
public static class TreesRoutes
{
    /// <summary>The driver's module, as the desktop's <c>DriverModule.Name</c> spells it.</summary>
    public const string DriverModule = "DAORIS.DRIVER";

    /// <summary><c>trees land &lt;session&gt;</c>: the review's Accept.</summary>
    public static InstanceRoute Land { get; } = new(DriverModule, PressesTold.Land, RouteClass.Person);

    /// <summary><c>trees land --entry &lt;id&gt;</c>: a line of work's one Accept.</summary>
    public static InstanceRoute LandEntry { get; } = new(DriverModule, PressesTold.LandEntry, RouteClass.Person);

    /// <summary><c>trees land --ask &lt;id&gt;</c>: an ask's one Accept.</summary>
    public static InstanceRoute LandAsk { get; } = new(DriverModule, PressesTold.LandAsk, RouteClass.Person);

    /// <summary><c>trees remove &lt;path&gt;</c>: a tree discarded.</summary>
    public static InstanceRoute DiscardTree { get; } = new(DriverModule, PressesTold.DiscardTree, RouteClass.Person);

    /// <summary><c>trees remove &lt;session|branch&gt;</c>: a failed or superseded attempt's branch discarded (LAND3).</summary>
    public static InstanceRoute DiscardBranch { get; } = new(DriverModule, PressesTold.DiscardBranch, RouteClass.Person);

    /// <summary><c>trees clean --yes</c>: the clean-up's press.</summary>
    public static InstanceRoute Sweep { get; } = new(DriverModule, PressesTold.Sweep, RouteClass.Person);

    /// <summary><c>trees sync --yes</c>: bringing the repositories up to date.</summary>
    public static InstanceRoute Sync { get; } = new(DriverModule, PressesTold.TreesSync, RouteClass.Person);

    /// <summary><c>trees hand &lt;session|branch&gt;</c>: a landed branch handed to a plugin.</summary>
    public static InstanceRoute HandOff { get; } = new(DriverModule, PressesTold.HandOff, RouteClass.Person);

    /// <summary>Each press of the <c>trees</c> verbs, in the verb's words, with its route.</summary>
    public static IReadOnlyList<(string Verb, InstanceRoute Route)> Presses { get; } =
    [
        ("trees land <session>", Land),
        ("trees land --entry <id>", LandEntry),
        ("trees land --ask <id>", LandAsk),
        ("trees remove <path>", DiscardTree),
        ("trees remove <session|branch>", DiscardBranch),
        ("trees clean --yes", Sweep),
        ("trees sync --yes", Sync),
        ("trees hand <session|branch>", HandOff),
    ];
}
