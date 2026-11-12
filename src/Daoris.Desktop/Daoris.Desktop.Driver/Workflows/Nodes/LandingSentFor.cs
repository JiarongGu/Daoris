namespace Daoris.Driver;

/// <summary>
/// The landing the call of a step after <c>handed</c> names, told to its plugin while the attempt still waits though the run has gone back
/// past that landing since: the plugin is then not stopped as idle under an attempt it is still working, and its answer reaches the run's
/// record, kept as a stale attempt's outcome.
/// </summary>
internal static class LandingSentFor
{
    /// <summary>The landed branch at the tip <paramref name="attempt"/>'s call names, under its unit; null where the run reads no such landing.</summary>
    public static WatchedUnit? Frame(GovernedUnit governed, NodeAttempt attempt)
    {
        var scope = governed.Scope!;
        var unit = scope.Branch?.Id == attempt.Call.Unit ? scope.Branch : scope.Branches.FirstOrDefault(each => each.Id == attempt.Call.Unit);
        var landing = unit?.Records.Landings.LastOrDefault(each => string.Equals(each.Tip, attempt.Call.SubjectRevision, StringComparison.OrdinalIgnoreCase));
        if (unit is null || landing is null) return null;
        return new WatchedUnit(governed.Watched.Repository, governed.Watched.Workspace, scope.Root, landing.Branch, landing.Tip, landing.Line, landing.PullRequest, [])
        {
            Unit = unit.Id, Identity = scope.Identity, Landing = true,
        };
    }
}
