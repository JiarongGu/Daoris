namespace Daoris.Driver;

/// <summary>
/// What one part's sessions' trees hold (FACTSPLIT1b): each tree judged, by <see cref="KnowledgeWorkOrders.Current.Normal"/>, as
/// <see cref="IKnowledgeSessionLook.Trees"/> keys them. A tree not here holds nothing to review.
/// </summary>
public sealed record HelpTrees(IReadOnlyDictionary<string, TreeWork> Trees)
{
    /// <summary>A part with no tree judged.</summary>
    public static HelpTrees None { get; } = new(new Dictionary<string, TreeWork>(StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// The trees this part asked (PUSH1c1), by <see cref="KnowledgeWorkOrders.Current.Normal"/>: one asked and not in <see cref="Trees"/> holds nothing
    /// to review, one not asked was never judged here, which a door reading the parts walks itself (<see cref="HelpFacts.WithTreesAsync"/>).
    /// </summary>
    public IReadOnlySet<string> Asked { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

/// <summary>A door's trees as the parts judged them (<see cref="HelpFacts.TreesJudged"/>, PUSH1c1).</summary>
/// <param name="Trees">The work of each tree a part asked and git spoke for, by <see cref="KnowledgeWorkOrders.Current.Normal"/>.</param>
/// <param name="NotJudged">The trees no part asked, as the door named them.</param>
public sealed record HelpTreesJudged(IReadOnlyDictionary<string, TreeWork> Trees, IReadOnlyList<string> NotJudged);

/// <summary>What one part of the trees reads (FACTSPLIT1b, <see cref="HelpFacts.TreesPartOf"/>): the look, and whose sessions are its.</summary>
/// <param name="Repository">The repository whose part it is; null for the rest's (<see cref="HelpFacts.NoCheckout"/>).</param>
/// <param name="Checkouts">Every repository with a checkout here, which the rest's holds none of.</param>
/// <param name="Look">The sessions' look without trees, as the facts hold it.</param>
public sealed record HelpTreesPart(string? Repository, IReadOnlySet<string> Checkouts, IKnowledgeSessionLook Look)
{
    /// <summary>Whether a repository a record names is this part's, by its registry name, without case.</summary>
    public bool Holds(string repository) => Repository is { } own
        ? string.Equals(own, repository, StringComparison.OrdinalIgnoreCase)
        : !Checkouts.Contains(repository);

    /// <summary>The sessions whose trees are this part's to judge: the look's records of its repositories, in every workspace.</summary>
    public IReadOnlyList<string> Sessions => [.. Look.Records.Where(record => Holds(record.Repository)).Select(record => record.Id)];
}
