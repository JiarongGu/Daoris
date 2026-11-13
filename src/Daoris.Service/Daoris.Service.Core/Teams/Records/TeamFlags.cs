namespace Daoris.Knowledge;

/// <summary>The kinds of <see cref="TeamFlag"/>.</summary>
public static class TeamFlags
{
    /// <summary>An edge names a part no plan of the ask holds.</summary>
    public const string MissingReference = "missing-reference";

    /// <summary>The edges run in a circle.</summary>
    public const string Cycle = "cycle";

    /// <summary>A claim reached a record the integrator writes; the claim was stripped.</summary>
    public const string ClaimRecord = "claim-record";

    /// <summary>A claim reached a generated output; the claim was stripped.</summary>
    public const string ClaimGenerated = "claim-generated";

    /// <summary>Two parallel parts claim one path; an <c>orders</c> edge was added.</summary>
    public const string ClaimOverlap = "claim-overlap";
}
