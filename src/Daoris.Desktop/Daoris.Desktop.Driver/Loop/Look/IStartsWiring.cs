namespace Daoris.Driver;

/// <summary>
/// What the look's plan and its starts reach of the driver beyond <see cref="ILookStagesWiring"/>: how a start and an intake are run,
/// what the watch wants of the loop, and the catalogue a report names an agent by.
/// </summary>
internal interface IStartsWiring : ILookStagesWiring
{
    /// <summary>
    /// What completes once something else wants the loop: a start still preparing then runs on beside the look. Null runs each in
    /// the look.
    /// </summary>
    Func<CancellationToken, Task>? Wanted { get; }

    /// <summary>A test's in-process stand-in for one quest's start; null runs <see cref="RunAsync"/>.</summary>
    Func<Consideration, Action, CancellationToken, Task<StartRun>>? Runner { get; }

    /// <summary>One quest's start, from its holds to its record's conclusion.</summary>
    Task<StartRun> RunAsync(
        Consideration start, IReadOnlyList<RepoView> registry, List<TrustHold> untrusted, Action onOpened, FreshTree fresh, CancellationToken ct,
        string? movedOff = null, (string Agent, string Model)? movedOffModel = null, string? movedFrom = null);

    /// <summary>One intake run in its room.</summary>
    Task<StartRun> RunIntakeAsync(AskView ask, List<TrustHold> untrusted, Action onOpened, CancellationToken ct);

    /// <summary>The quests whose start resumed their limited record's own conversation, by quest.</summary>
    System.Collections.Concurrent.ConcurrentDictionary<string, string> LimitResumed { get; }

    /// <summary>The catalogue for the look's report of a start, which never faults the look: null where the store will not read.</summary>
    RoleCatalog? CatalogueForReport();
}
