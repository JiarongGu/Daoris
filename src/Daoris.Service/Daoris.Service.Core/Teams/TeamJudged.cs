namespace Daoris.Knowledge;

/// <summary>What the judge made of a plan: its parts with the refused claims stripped, its edges with the added orders, and what it found.</summary>
/// <param name="Refusal">Why the plan has no shape to save, or null; a flag is never a refusal.</param>
public sealed record TeamJudged(
    string? Refusal, IReadOnlyList<TeamPart> Parts, IReadOnlyList<TeamEdge> Edges, IReadOnlyList<TeamFlag> Flags);
