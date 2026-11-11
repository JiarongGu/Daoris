namespace Daoris.Driver;

/// <summary>
/// A landing supervisor's decision on a review, bound to the review record it read: kept naming the record's revision, and released only
/// while the attempt's record still holds it. A step that keeps no record, an approval, is held to its attempt alone. A decision naming no
/// revision stands on a branch's review, whose record follows its tip, and on no other kind: it read nothing the review reads now.
/// </summary>
public static class SupervisorReviewBinding
{
    /// <summary>The record the decision names is not the one its attempt reads, or it names none on work that is no branch.</summary>
    public const string Moved = "moved";

    /// <summary>The revision of the record <paramref name="attempt"/> of <paramref name="node"/> reads; null where it keeps none.</summary>
    public static string? RevisionOf(string home, string run, string node, int attempt) => ReviewRecords.Read(home, run, node, attempt)?.Revision;

    /// <summary>Why a decision naming <paramref name="revision"/> no longer decides the attempt's review (<see cref="Moved"/>); null where it may go on.</summary>
    public static string? Refused(string home, string run, string node, int attempt, string? revision) =>
        ReviewRecords.Read(home, run, node, attempt) is { } record ? Refused(record, revision) : null;

    /// <summary>Why a decision naming <paramref name="revision"/> does not decide <paramref name="record"/> (<see cref="Moved"/>); null where it does.</summary>
    public static string? Refused(ReviewRecord record, string? revision)
    {
        if (revision is null) return record.Kind == ReviewKinds.Branch ? null : Moved;
        return ReviewAccepts.Names(revision, record.Revision) ? null : Moved;
    }
}
