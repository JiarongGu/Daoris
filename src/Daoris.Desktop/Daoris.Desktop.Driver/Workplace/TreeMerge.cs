namespace Daoris.Driver;

/// <summary>
/// What came of asking to merge a session tree into the canonical line. A refusal is an
/// ANSWER — the checkout is busy, there is nothing to merge, the branches conflict — and each names
/// what the person would do about it.
/// </summary>
public sealed record TreeMerge(bool Merged, string Message)
{
    /// <summary>A refusal's code where the merge door has one: <c>uncommitted</c>, which the Accept carries its session on for.</summary>
    public string? Refusal { get; init; }

    /// <summary>The checkout's status (<see cref="LandTries.Fingerprint"/>) a <see cref="LandingCode.RootDirty"/> refusal read.</summary>
    public string? RootStatus { get; init; }
}

