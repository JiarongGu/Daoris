namespace Daoris.Driver;

/// <summary>One pass's reads beyond the tips: git's for the observed built-ins, the gates', and what the line holds.</summary>
internal sealed record PassReads(PassGit Git, PassGates Gates, Func<WorkUnit, string, CancellationToken, Task<HoldContent?>> Content)
{
    public Func<string, IReadOnlyCollection<string>, CancellationToken, Task<IReadOnlyDictionary<string, string>>> Heads { get; init; } =
        (_, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

    /// <summary>The look's read of the commits a checkout's line lacks of a tip (<see cref="NodeRuntimeLook.Lacks"/>).</summary>
    public Func<string, string, string, string, CancellationToken, Task<IReadOnlyList<LandingCommit>?>> Lacks { get; init; } =
        (_, _, _, _, _) => Task.FromResult<IReadOnlyList<LandingCommit>?>(null);
}
