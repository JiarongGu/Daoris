namespace Daoris.Driver;

/// <summary>
/// Whether a run's <c>tidy</c> ended a session branch's part: a landing of its repository, standing or a trace, whose chain the tidy ended at the
/// landing's tip names the branch among its parts. Its pull request merged, so the work is on the line and the run is over for that branch's tree,
/// though the run goes on past the tidy and its record does not end settled. The tidy itself leaves a branch a tree has checked out.
/// </summary>
internal static class TidiedParts
{
    /// <summary>Whether <paramref name="landings"/> hold a tidy that ended <paramref name="entry"/>'s branch.</summary>
    public static bool Ended(IReadOnlyList<LandedBranch> landings, GrownBranch entry) =>
        landings.Any(landing => string.Equals(landing.Repository, entry.Repository, StringComparison.OrdinalIgnoreCase)
                                && landing.Tidied is { } tidied && string.Equals(tidied.Tip, landing.Tip, StringComparison.OrdinalIgnoreCase)
                                && tidied.Parts.Any(part => string.Equals(part.Branch, entry.Branch, StringComparison.Ordinal)));
}
