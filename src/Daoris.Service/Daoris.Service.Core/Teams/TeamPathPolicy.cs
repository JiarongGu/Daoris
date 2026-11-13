namespace Daoris.Knowledge;

/// <summary>
/// The paths of one repository no worker changes: the records its manifest declares and the outputs a tool generates. The integrator writes
/// both, so a claim on one is stripped from its part and said on the plan.
/// </summary>
/// <param name="Records">Globs of the records <c>daoris.json</c> declares: the backlog, the archive, the changelog, the fix log, the areas.</param>
/// <param name="Generated">Globs of generated outputs.</param>
public sealed record TeamPathPolicy(IReadOnlyList<string> Records, IReadOnlyList<string> Generated)
{
    /// <summary>A policy that strips nothing.</summary>
    public static TeamPathPolicy None { get; } = new([], []);
}
