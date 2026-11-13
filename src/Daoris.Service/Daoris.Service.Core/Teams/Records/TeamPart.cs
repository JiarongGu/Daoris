namespace Daoris.Knowledge;

/// <summary>
/// One part of an ask's plan: a quest in a repository and lane, with what a person sees working when it is done. Its <see cref="Id"/> is
/// stable across revisions, and the quest a revision publishes for it is the one the first publish made.
/// </summary>
/// <param name="Id">Stable across revisions: letters, digits, <c>.</c>, <c>_</c>, <c>:</c> and <c>-</c>.</param>
/// <param name="Repository">The repository that does the work.</param>
/// <param name="Title">One line: what is wanted. Kept as first published.</param>
/// <param name="Body">Why, with the evidence; the quest's words.</param>
/// <param name="Outcome">What a person sees working, in one sentence.</param>
public sealed record TeamPart(string Id, string Repository, string Title, string Body, string Outcome)
{
    /// <summary>The lanes of the repository it is addressed to; empty for the whole repository.</summary>
    public IReadOnlyList<string> Lanes { get; init; } = [];

    /// <summary>The part a split made this one from, or null for a part of the ask's own plan.</summary>
    public string? Parent { get; init; }

    /// <summary><c>design</c> or <c>exact</c> as a quest's weight, or null.</summary>
    public string? Weight { get; init; }

    /// <summary>The ask's requirements this part serves, each quoted from the person's words.</summary>
    public IReadOnlyList<QuestRequirement> Requirements { get; init; } = [];

    /// <summary>Path globs the part will change.</summary>
    public IReadOnlyList<string> Claims { get; init; } = [];

    /// <summary>Contracts it depends on, each a <c>file:range</c>.</summary>
    public IReadOnlyList<string> Reads { get; init; } = [];

    /// <summary>One line per point it proves, each naming its check or test.</summary>
    public IReadOnlyList<string> Proof { get; init; } = [];

    /// <summary>What the scout read for it.</summary>
    public PartPack? Pack { get; init; }
}
