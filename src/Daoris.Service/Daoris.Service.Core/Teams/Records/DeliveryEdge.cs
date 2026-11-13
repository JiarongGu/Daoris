namespace Daoris.Knowledge;

/// <summary>A release order between two deployables with its reason and the place the reason stands on. Its source says how much it is worth: only an edge with evidence is more than an order somebody gave.</summary>
/// <param name="Deployable">The deployable that is released second.</param>
/// <param name="On">The deployable it is released after.</param>
/// <param name="Why">The reason, in a sentence.</param>
/// <param name="Source">One of the constants of this record.</param>
/// <param name="Evidence">The place the reason stands on; null for <see cref="Policy"/> and <see cref="Person"/>.</param>
public sealed record DeliveryEdge(DeliveryRef Deployable, DeliveryRef On, string Why, string Source, DeliveryEvidence? Evidence)
{
    /// <summary>The person's reason for waiving this order, or null while it stands; a waived order releases nothing first and goes stale no more.</summary>
    public string? Waived { get; init; }

    /// <summary>A declaration: one deployable provides what another consumes as <c>mirror</c> or <c>reads</c>.</summary>
    public const string Declaration = "declaration";

    /// <summary>A release definition's stage condition.</summary>
    public const string StageCondition = "stage-condition";

    /// <summary>A contract revision the later deployable's part reads.</summary>
    public const string Contract = "contract";

    /// <summary>The lead's reading of code, citing its range.</summary>
    public const string CodeRead = "code-read";

    /// <summary>An order given with no evidence behind it.</summary>
    public const string Policy = "policy";

    /// <summary>An edge the person added, with a reason.</summary>
    public const string Person = "person";

    /// <summary>The sources a writer may give; <see cref="Declaration"/> is the judge's own.</summary>
    public static IReadOnlyList<string> Written { get; } = [StageCondition, Contract, CodeRead, Policy, Person];

    /// <summary>The sources that stand only on evidence.</summary>
    public static IReadOnlyList<string> Evidenced { get; } = [StageCondition, Contract, CodeRead];
}
