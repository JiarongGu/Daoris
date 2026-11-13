namespace Daoris.Knowledge;

/// <summary>
/// One revision of an ask's plan, or of one part's split of itself: appended whole and never edited, the next revision a new record.
/// </summary>
/// <param name="Ask">The ask it plans.</param>
/// <param name="Scope">The part this plan splits, or null for the ask's own plan.</param>
/// <param name="Revision">From 1, within the scope.</param>
/// <param name="By">The session that wrote it, or null for a person's hand.</param>
/// <param name="Why">One paragraph: why these parts, and what changed since the last revision.</param>
/// <param name="At">When it is written.</param>
public sealed record TeamPlan(string Ask, string? Scope, int Revision, string? By, string Why, DateTimeOffset At)
{
    /// <summary>The line's commit the plan reads, where the writer names one.</summary>
    public string? Base { get; init; }

    /// <summary>The parts, each stripped of the claims the judge refused.</summary>
    public IReadOnlyList<TeamPart> Parts { get; init; } = [];

    /// <summary>The edges written, then those the judge added.</summary>
    public IReadOnlyList<TeamEdge> Edges { get; init; } = [];

    /// <summary>What each repository releases, as each declaration reads at its blob; empty for a plan that ends at its landing.</summary>
    public IReadOnlyList<DeliveryDeployable> Deployables { get; init; } = [];

    /// <summary>The release orders between the deployables, each with its reason, its source and its evidence.</summary>
    public IReadOnlyList<DeliveryEdge> Releases { get; init; } = [];

    /// <summary>What only a person can settle, asked once each.</summary>
    public IReadOnlyList<DeliveryQuestion> Questions { get; init; } = [];

    /// <summary>What the judge found.</summary>
    public IReadOnlyList<TeamFlag> Flags { get; init; } = [];

    /// <summary>How deep its parts are: 1 for the ask's own plan, one more for each split below.</summary>
    public int Depth { get; init; } = 1;
}
