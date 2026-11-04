namespace Daoris.Driver;

/// <summary>One pass's reads beyond the tips: git's for the observed built-ins, D157's gates, and what the line holds.</summary>
internal sealed record PassReads(PassGit Git, PassGates Gates, Func<WorkUnit, string, CancellationToken, Task<HoldContent?>> Content);
