namespace Daoris.Driver;

/// <summary>
/// What a driven session's run reaches of the driver that runs it: its stages, the hold that runs its process and the protocol door it
/// drives. The driver implements it; a stage names only these, never the driver, so it lives in its own subdomain and the driver holds
/// the order and the wiring alone.
/// </summary>
internal interface IRunWiring
{
    /// <summary>The driver's home, configuration, service, records, harnesses and the sessions it runs.</summary>
    LoopContext Context { get; }

    /// <summary>A continuation's words, what holds them, and a native run's go-on with what the person said while it worked.</summary>
    DriverContinuations GoOns { get; }

    /// <summary>The watcher's view of the running sessions, which a run's stream stamps and its waits tell.</summary>
    SessionWatch Watch { get; }

    /// <summary>The driver's configuration file, read again where a running session's permission prompts may have changed; null where none is kept.</summary>
    string? ConfigPath { get; }

    /// <summary>The clock a transient retry's waits are on.</summary>
    TimeProvider RetryClock { get; }

    /// <summary>What a session's process reaches of the driver: its lineage, its stream and its spawn.</summary>
    ITransportHost Transport { get; }

    /// <summary>How often a session waiting on its own background work has its quest read again.</summary>
    TimeSpan BackgroundLook { get; }

    /// <summary>The role the run's work step names, which every walk of the start takes in place of the work's; null for the work's.</summary>
    Task<string?> StepRoleAsync(QuestView quest, string? workspace, CancellationToken ct);

    /// <summary>The machine's role catalogue as a start's report reads it; null where it does not read.</summary>
    RoleCatalog? CatalogueForReport();

    /// <summary>The order the role taking a quest's work walks, with the fence, or the hand-over's agent, a caller names.</summary>
    IReadOnlyList<ActorEffective> QuestOrder(
        Consideration start, IReadOnlyList<string>? fence = null, string? handed = null, string? role = null);

    /// <summary>The walk of the role's order for a start, with each choice it passed and why.</summary>
    Task<SettledSelection> WalkBuildPassesAsync(
        Consideration start, IReadOnlyList<RepoView> registry, CancellationToken ct, bool counts = true,
        (ActorEffective Entry, string? Account)? refused = null, long except = 0, string? role = null);

    /// <summary>Whether a continuation's own conversation resumes now or waits for its account.</summary>
    LineCourse DecideResume(PriorSession record, string questStatus, ResumeChoice resume);

    /// <summary>Whether a line a limit cut off resumes on its own account, now ready again.</summary>
    Task<LineCourse> DecideLimitOwnAsync(PriorSession limited, ResumeChoice own, CancellationToken ct);

    /// <summary>The run bound to its workflow as it starts, once its tree is open; a bind not made, or a binding that does not read, holds.</summary>
    Task<WorkflowBind> BindAtStartAsync(QuestView quest, string? workspace, CancellationToken ct);

    /// <summary>Told a graph run that ended and whose chain came back to a start, so the runtime re-arms for it; null where nothing listens.</summary>
    Action<string>? RunReturned { get; }

    /// <summary>The desk a local set-up step's tab opens on; null where no shell carries one.</summary>
    ReviewDesk? Reviews { get; }

    /// <summary>A move of a line from one choice to another, counted against its role's bound.</summary>
    void CountMove(string line, string role, string condition, ActorEffective from, string? to);

    /// <summary>The knowledge server a session is offered, located per run; null where this machine has no connector.</summary>
    AcpMcpServer? Connector(string sessionId, IReadOnlyDictionary<string, string>? scope, string? proc);
}
