namespace Daoris.Driver;

/// <summary>A check's attempt as the heavy executor takes it.</summary>
/// <param name="Run">The run whose check it is; its folder is under the home's <c>workflows/lines/</c>.</param>
/// <param name="Invocation">The attempt's invocation, which names its folder and its job.</param>
/// <param name="Repository">The repository's name, as what is said names it.</param>
/// <param name="Checkout">The repository's checkout Daoris knows: a runner starts there, and the commit is read there.</param>
/// <param name="Commit">The full commit the check judges.</param>
/// <param name="Accepted">The blob of <c>daoris.gates.json</c> the person accepted, <c>none</c> for this machine's mapping, or null where there is none.</param>
public sealed record VerifySend(string Run, string Invocation, string Repository, string Checkout, string Commit, string? Accepted)
{
    /// <summary>The gates the check names; null for the heavy gates, or the runner where the declaration names one.</summary>
    public IReadOnlyList<string>? Gates { get; init; }

    /// <summary>Where the check runs only the gates its batch's changed paths reach (<c>reach: paths</c>); null where every gate it names runs.</summary>
    public VerifyReach? Reach { get; init; }

    /// <summary>The check's bound, counted from both slots held.</summary>
    public TimeSpan Bound { get; init; } = HeavyExecutor.DefaultBound;

    /// <summary>The verification tree its direct gates run in; null for one under the lines folder, by the repository's slot key.</summary>
    public string? Tree { get; init; }
}
