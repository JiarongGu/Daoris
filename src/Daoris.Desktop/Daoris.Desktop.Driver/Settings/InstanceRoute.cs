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

    /// <summary>
    /// A role's act (SUPERVISOR1a-ii, D208): sent only by its holder's verb, with a proof by the holder key over the hello's nonce, and
    /// judged by the instance with no card; refused where not covered, never done alone.
    /// </summary>
    Role,
}

/// <summary>
/// A route a terminal verb sends over the pipe, with the class the census gives it (RPC1b). The route's vocabulary is the settings'
/// (FOUNDATION1 mod-iii): the settings' own presses name it, and the instance's door, which sends it, reads it from here.
/// </summary>
/// <param name="Module">The module, as the kit's dispatcher names it.</param>
/// <param name="Route">The route, as its module's table names it.</param>
/// <param name="Class">The census's class, held to it by <c>PipeCensusTests</c>.</param>
public sealed record InstanceRoute(string Module, string Route, RouteClass Class)
{
    /// <summary>The driver's module, as the desktop's <c>DriverModule.Name</c> spells it.</summary>
    public const string DriverModule = "DAORIS.DRIVER";
}
