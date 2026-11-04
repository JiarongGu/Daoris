namespace Daoris.Driver;

/// <summary>The service's answers and the roster, as the machine's topic judged them (PARALLEL1a).</summary>
/// <param name="Snapshot">The registry, the quests and the active sessions.</param>
/// <param name="Standing">The asks the host answered.</param>
/// <param name="Roster">The harnesses as the roster reports them.</param>
public sealed record HelpMachineWhole(IKnowledgeSnapshot Snapshot, IReadOnlyList<AskView> Standing, IReadOnlyList<HarnessReport> Roster);

/// <summary>A checkout's guess at its line as git said it (<see cref="LineGuesses"/>); null where git names none.</summary>
public sealed record HelpLineGuess(string? Branch);

/// <summary>
/// Where each unit of work has got to (FABRIC-MILESTONE1b), what could not be read, each by its repository (FACTSPLIT1a), and
/// Daoris's own recoveries no unit names: one repository's part, or the parts merged.
/// </summary>
public sealed record HelpWork(IReadOnlyList<IKnowledgeMilestone> Units, IReadOnlyList<InventoryUnread> Unread, IReadOnlyList<InventoryEntry> Recovery);

/// <summary>What one part of the work reads (FACTSPLIT1a, <see cref="HelpFacts.PartOf"/>), from the service's answers.</summary>
/// <param name="Repository">The repository whose part it is; null for the rest's (<see cref="HelpFacts.NoCheckout"/>).</param>
/// <param name="Roots">The checkout whose refs it reads: the repository's own; none for the rest.</param>
/// <param name="Checkouts">Every repository with a checkout here, which the rest's holds none of.</param>
/// <param name="InUse">The trees sessions still running or waiting hold.</param>
public sealed record HelpWorkPart(
    string? Repository, IReadOnlyDictionary<string, string> Roots, IReadOnlySet<string> Checkouts, IReadOnlySet<string> InUse)
{
    /// <summary>The trees this part's repositories' sessions recorded (OVERVIEWNOISE1b), from the sessions' look; none before it.</summary>
    public IReadOnlyList<RecordedTree> Recorded { get; init; } = [];

    /// <summary>
    /// The quests the service says are closed among those this part's landings name (LANDRETIRE1 part 2, <see cref="LandedStanding.ClosedAsync"/>),
    /// read by the module's judge, which holds the service; none read, none is closed. The facts' deadline judges the part again, so a quest closing
    /// or a day passing reaches the page within it, with no wake of its own.
    /// </summary>
    public IReadOnlySet<string> ClosedQuests { get; init; } = new HashSet<string>();

    /// <summary>Whether a repository the trees or the records name is this part's.</summary>
    public bool Holds(string repository) => Repository is { } own
        ? string.Equals(own, repository, StringComparison.OrdinalIgnoreCase)
        : !Checkouts.Contains(repository);
}

/// <summary>What one part of the clean-up's list reads (PUSH1c4a, <see cref="HelpFacts.SweepPartOf"/>), from the service's answers.</summary>
/// <param name="Repository">The repository whose part it is, as the registry names it.</param>
/// <param name="Workspace">The workspace the registry names it in, which its line is read under.</param>
/// <param name="Root">Its checkout here.</param>
/// <param name="InUse">The trees sessions still running or waiting hold, kept whatever they hold.</param>
public sealed record HelpSweepPart(string Repository, string Workspace, string Root, IReadOnlySet<string> InUse)
{
    /// <summary>The quests the service says are closed among those its landings name (LANDRETIRE1 part 2), as <see cref="HelpWorkPart.ClosedQuests"/> reads them.</summary>
    public IReadOnlySet<string> ClosedQuests { get; init; } = new HashSet<string>();
}
