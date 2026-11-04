namespace Daoris.Driver;

/// <summary>
/// What a look's pass may reach of the driver that runs it, each read when the pass runs. The driver implements it; a pass names
/// only these, never the driver, so a pass lives in its own subdomain and the driver holds the order and the wiring alone.
/// </summary>
/// <remarks>
/// A member whose type sits above Work orders in the driver's layers goes on a wiring interface of the module that needs it,
/// declared in that module's subdomain and extending this one, as the landings' <see cref="ILandingWiring"/> does.
/// </remarks>
internal interface ILookWiring
{
    string Home { get; }

    DriverConfig Config { get; }

    ServiceClient Service { get; }

    /// <summary>The remote sync, or null where this machine syncs with no remote.</summary>
    RemoteSyncSet? Sync { get; }

    /// <summary>The sessions this driver runs, and the work it runs beside its looks.</summary>
    RunningSessions Runs { get; }

    SessionProcesses Processes { get; }

    /// <summary>The sessions' event records.</summary>
    SessionEvents Events { get; }

    /// <summary>The watcher's look at the running sessions: its steps this look, each said as a line classed by <paramref name="said"/>.</summary>
    IReadOnlyList<string> LookIn(LookSaid said);

    /// <summary>Each line the look says, classed where it is made.</summary>
    LookSaid Lines { get; }

    /// <summary>Where the look's passes time their spans, beside the look's own.</summary>
    LookStages Stages { get; }

    /// <summary>
    /// Whether the look leaves beside it the work no part of the look waits on: the watch's. A one-look run and a gate's
    /// run-until-idle do that work in the look, as each reads one look whole.
    /// </summary>
    bool Beside { get; }

    /// <summary>A test's stand-in for stopping a session whose take lost; null stops its process.</summary>
    Func<string, Noted, bool>? Stops { get; }
}
