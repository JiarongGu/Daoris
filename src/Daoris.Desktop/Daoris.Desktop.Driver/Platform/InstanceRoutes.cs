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
/// The routes the <c>trees</c> verbs' presses are twins of (RPC1b, the terminal RPC design §1.2 and §7.2): each verb sends its
/// route over the pipe once the census opens it, and until then acts alone, as it does today.
/// </summary>
/// <remarks>
/// 🔴 Every one is the person's (D171's RPC1a note: a landing is the person's yes, a discard and a sweep their deletes, and
/// <c>TREES_SYNC</c> moves their lines and branches), so each waits for RPC1c, which confirms a person's press in the window. A
/// route the census opens fails <c>PipeCensusTests</c> here, naming the verb that then owes its client path: its words from the
/// route's answer, held to the alone path's case for case (§7.3).
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
