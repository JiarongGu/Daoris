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

    /// <summary>A press D156 §3 makes the person's alone: its verb acts alone until RPC1c confirms it in the window (§7.2).</summary>
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

    /// <summary>
    /// Each verb of RPCOPEN1's scope, in the verb's words: the route it sends where the instance answers, or null with the reason it
    /// does not go through the pipe (yet).
    /// </summary>
    public static IReadOnlyList<(string Verb, InstanceRoute? Route, string Reason)> Verbs { get; } =
    [
        ("trees clean", SweepPlan, "the list; `--yes` is the person's press and acts alone until RPC1c"),
        ("trees sync", TreesSyncPlan, "the list; `--yes` is the person's press and acts alone until RPC1c"),
        ("trees land <session> --plan", Landing, "the plan; the press is the person's and acts alone until RPC1c"),
        ("trees land --entry <id> --plan", EntryLanding, "the plan; the press is the person's and acts alone until RPC1c"),
        ("trees land --ask <id> --plan", AskLanding, "the plan; the press is the person's and acts alone until RPC1c"),
        ("trees hand <session|branch> --plan", HandoffPlan, "the plan; the press is the person's and acts alone until RPC1c"),
        ("sessions", SessionGroups, "the list"),
        ("sync", SyncNow, "an open press (D156 §3: sync)"),
        ("register", RegistryRefresh, "an open press (D156 §3: refresh)"),
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
/// The routes the <c>trees</c> verbs' presses are twins of (RPC1b, the terminal RPC design §1.2 and §7.2): each verb sends its
/// route over the pipe once the census opens it, and until then acts alone, as it does today.
/// </summary>
/// <remarks>
/// 🔴 Every one is the person's (D171's RPC1a note: a landing is the person's yes, a discard and a sweep their deletes, and
/// <c>TREES_SYNC</c> moves their lines and branches), so each waits for RPC1c, which confirms a person's press in the window. A
/// route the census opens fails <c>PipeCensusTests</c> here, naming the verb that then owes its client path: its words from the
/// route's answer, held to the alone path's case for case (§7.3). RPC1b3 built the words: each route answers them as <c>said</c>,
/// which <see cref="TreesSaid.Words"/> prints and <see cref="TreesWords"/> builds for the alone path and the route alike.
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
