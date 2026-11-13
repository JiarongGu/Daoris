namespace Daoris.Knowledge;

/// <summary>One thing a repository releases, as the lead read it from that repository's declaration: what it provides and consumes, and the blob of the declaration it read.</summary>
/// <param name="Ref">The repository and the id its declaration gives it.</param>
/// <param name="Declared">The declaration file's place and blob; the edges the declarations give cite it.</param>
public sealed record DeliveryDeployable(DeliveryRef Ref, DeliveryEvidence Declared)
{
    /// <summary>What it provides, by resource.</summary>
    public IReadOnlyList<DeliveryUse> Provides { get; init; } = [];

    /// <summary>What it consumes, by resource.</summary>
    public IReadOnlyList<DeliveryUse> Consumes { get; init; } = [];
}
