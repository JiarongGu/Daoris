namespace Daoris.Driver;

/// <summary>
/// What came of asking to merge a session tree into the canonical line (D51 rule 6). A refusal is an
/// ANSWER — the checkout is busy, there is nothing to merge, the branches conflict — and each names
/// what the person would do about it.
/// </summary>
public sealed record TreeMerge(bool Merged, string Message)
{
    /// <summary>A refusal's code where the merge door has one: <c>uncommitted</c>, which the Accept carries its session on for.</summary>
    public string? Refusal { get; init; }
}

