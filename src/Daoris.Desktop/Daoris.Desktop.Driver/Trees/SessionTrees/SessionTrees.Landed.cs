namespace Daoris.Driver;

/// <summary>
/// The clean-up of the branches landings made, as <see cref="LandedCleanups"/> (PART10l): built once here, with one-line public
/// calls for the page's list and `trees clean`. Its records and words are in <c>LandedCleanups.cs</c>, the proof it shares in
/// <see cref="LandedProofs"/>. PART10l-ii folds this part into the front.
/// </summary>
public sealed partial class SessionTrees
{
    private LandedCleanups? _cleanups;

    /// <summary>The clean-up's list and press, and each landed branch judged (D88, WSR5).</summary>
    private LandedCleanups Cleanups => _cleanups ??= new(_context, Asks, Content, Replays, this);

    /// <inheritdoc cref="LandedCleanups.CleanPlanAsync"/>
    public Task<SweepPlan> CleanPlanAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        CancellationToken ct = default) =>
        Cleanups.CleanPlanAsync(repositories, inUse, ct);

    /// <inheritdoc cref="LandedCleanups.CleanAsync"/>
    public Task<SweepDone> CleanAsync(
        IEnumerable<(string Repository, string? Workspace, string? Root)> repositories, IReadOnlySet<string> inUse,
        IReadOnlySet<string>? only = null, CancellationToken ct = default) =>
        Cleanups.CleanAsync(repositories, inUse, only, ct);
}
