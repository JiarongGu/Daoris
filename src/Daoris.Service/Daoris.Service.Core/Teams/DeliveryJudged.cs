namespace Daoris.Knowledge;

/// <summary>What the delivery graph's judge made of a plan's deployables: its release orders, the questions only a person can settle, and what it found.</summary>
/// <param name="Refusal">Why the deployables have no shape to keep, or null; a flag is never a refusal.</param>
public sealed record DeliveryJudged(
    string? Refusal, IReadOnlyList<DeliveryEdge> Edges, IReadOnlyList<DeliveryQuestion> Questions, IReadOnlyList<TeamFlag> Flags)
{
    /// <summary>A plan with no deployables: nothing to order, nothing to ask.</summary>
    public static DeliveryJudged None { get; } = new(null, [], [], []);
}
