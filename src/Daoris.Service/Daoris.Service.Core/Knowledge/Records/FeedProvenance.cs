namespace Daoris.Knowledge;

/// <summary>
/// Which point in a repository's history its fed knowledge came from (D48 §6).
/// </summary>
/// <remarks>
/// <para>Two checkouts of one repository are two points in its history, and the remote must decide
/// which one speaks. This is what lets it: the driver stamps it from git, and the deployment keeps it
/// beside the entries so the next feed can be compared rather than simply believed.</para>
///
/// <para>It is also `claims-need-checks` applied to the index itself. The remote's copy of a
/// repository's knowledge is a <i>claim about a commit</i>; served provenance names the commit, so
/// staleness is something a person can see instead of something they must assume.</para>
/// </remarks>
/// <param name="Commit">The full SHA — short forms collide, and this is an identity.</param>
/// <param name="CommittedAt">Commit time, not feed time: the ordering must be the history's.</param>
/// <param name="Branch">Which line it came from — judged against the repository's declared default.</param>
/// <param name="Origin">Which machine fed it, from the key that carried it. Null where nobody said.</param>
public sealed record FeedProvenance(
    string Commit, DateTimeOffset CommittedAt, string Branch, string? Origin = null)
{
    /// <summary>The form a person reads, in a sentence or a table cell.</summary>
    public string ShortCommit => Clipped.ShortCommit(Commit, 8);

    /// <summary>
    /// The commit this deployment held when the feeding machine asked git whether <see cref="Commit"/>
    /// descends from it (SYNC5a). Null where the machine could not order the two: nothing was held, the
    /// histories diverged, or the client predates the rule — and then commit time decides.
    /// </summary>
    public string? Base { get; init; }

    /// <summary>
    /// What the fed content says, hashed by the deployment (<see cref="FeedDigest"/>) — how the same
    /// commit fed twice is told from the same commit read two ways. Null on a row from before SYNC5a.
    /// </summary>
    public string? Digest { get; init; }
}
