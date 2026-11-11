namespace Daoris.Driver;

/// <summary>One pass's reads beyond the tips: git's for the observed built-ins, the gates', and what the line holds.</summary>
internal sealed record PassReads(PassGit Git, PassGates Gates, Func<WorkUnit, string, CancellationToken, Task<HoldContent?>> Content)
{
    /// <summary>The look's read of whether a checkout's line holds a commit whole (<see cref="NodeRuntimeLook.OnLine"/>).</summary>
    public Func<string, string, string, string, CancellationToken, Task<bool?>> OnLine { get; init; } = (_, _, _, _, _) => Task.FromResult<bool?>(null);
}
