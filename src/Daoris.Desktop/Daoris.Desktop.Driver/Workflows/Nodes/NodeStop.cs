namespace Daoris.Driver;

/// <summary>
/// A problem's stop, by its identity: the sort (<see cref="ProblemSorts"/>), the stopped commit's patch id and the paths it could not settle,
/// by which the step that answers it counts its tries per stop.
/// </summary>
/// <param name="Sort">One of <see cref="ProblemSorts"/>.</param>
/// <param name="PatchId">The stopped commit's patch id; null for <see cref="ProblemSorts.Uncommitted"/>, which no commit carries.</param>
/// <param name="Paths">The conflicted or uncommitted paths, in the order the step met them.</param>
public sealed record NodeStop(string Sort, string? PatchId, IReadOnlyList<string> Paths)
{
    // A stop is its identity, so two read apart are the same stop where their paths are, whoever built the list.
    public bool Equals(NodeStop? other) =>
        other is not null && Sort == other.Sort && PatchId == other.PatchId && Paths.SequenceEqual(other.Paths, StringComparer.Ordinal);

    public override int GetHashCode() => HashCode.Combine(Sort, PatchId, string.Join('\n', Paths));
}
