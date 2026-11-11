namespace Daoris.Driver;

/// <summary>
/// A word an older build kept on a branchless review before a review had a record to name: an Accept pressed with no commit, a supervisor's
/// decision released on no revision. Read only by the <c>review-records</c> step (<see cref="ReviewRecordsStep"/>), which ends with this
/// release's migration.
/// </summary>
public static class ReviewLegacy
{
    /// <summary>A person's legacy Accept on a run whose graph goes on past its review: it never allowed what follows, so it is not applied.</summary>
    public const string Dropped = "legacy-accept-dropped";

    /// <summary>A supervisor's legacy decision: it read no record, so it decides nothing now.</summary>
    public const string Stale = "legacy-decision-stale";

    /// <summary>What the review's Accept says where a legacy Accept ends it: the moment it came, and that no revision stood then.</summary>
    public static string Receipt(DateTimeOffset at) => $"Accepted as it stood at {Moments.Write(at)}, before Daoris drew a revision.";

    /// <summary>
    /// Whether <paramref name="word"/> on a review standing at <paramref name="status"/> names nothing the review reads now: an Accept with no
    /// revision and no commit, on a review not bound to a commit, and not already kept as legacy.
    /// </summary>
    public static bool Unscoped(NodeWord word, NodeStatus status) =>
        word is { Act: NodeWordActs.Accept, Revision: null, Tip: null, Review: null }
        && !IsCommit(status.Binding?.SubjectRevision)
        && !(word.By is null && word.Words == Receipt(word.At));

    private static bool IsCommit(string? revision) => revision is { Length: 40 or 64 } && revision.All(Uri.IsHexDigit);
}
