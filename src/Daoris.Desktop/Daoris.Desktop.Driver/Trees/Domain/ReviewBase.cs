namespace Daoris.Driver;

/// <summary>
/// Where a session tree's review is measured from, and how its HEAD stands against its repository's line (REVIEWBASE1, D164's
/// note): <paramref name="From"/> is the commit the diff reads from; <paramref name="Behind"/> is null where the line could not be read.
/// </summary>
public sealed record ReviewRange(string From, LineBehind? Behind);

/// <summary>
/// How a tree's HEAD stands against the latest its repository's line is known here, the line and its copy on <c>origin</c> taken
/// together (REVIEWBASE1): <paramref name="Commits"/> the line has that HEAD does not, <paramref name="Ahead"/> HEAD has that the line
/// does not. Read from the refs as the last fetch left them: the review fetches nothing.
/// </summary>
public sealed record LineBehind(string Line, int Commits, int Ahead);

/// <summary>
/// The choice of a review's base among the commits a tree's history holds (REVIEWBASE1): the newest commit the work shares with
/// the line, here or on origin, and the commit its record says it began at. Pure; <c>SessionTrees.ReviewRangeAsync</c> asks git.
/// </summary>
/// <remarks>
/// The record's base alone measured a tree that took in newer line commits since (merged them, rebased onto them, or checked the
/// line out) from its old grow point, so the line's own changes read as the session's work (seen 2026-10-13: 26 files, none its own).
/// </remarks>
public static class ReviewBase
{
    /// <summary>The candidates in the order a tie is broken by: the line's here, the line's on origin, the record's; each once, the unread left out.</summary>
    public static IReadOnlyList<string> Candidates(string? line, string? origin, string? record) =>
        new[] { line, origin, record }
            .Where(each => !string.IsNullOrWhiteSpace(each))
            .Select(each => each!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    /// <summary>
    /// The base: the first of <paramref name="candidates"/> that <paramref name="maximal"/> names, which is git's list of those no
    /// other candidate reaches (<c>merge-base --independent</c>). Where one descends from all the others it is the only one named;
    /// where none does, the line's comes before the record's, so the line's own changes are never read as the session's. Null where
    /// git named none of them.
    /// </summary>
    public static string? Newest(IReadOnlyList<string> candidates, IReadOnlyCollection<string> maximal) =>
        candidates.FirstOrDefault(candidate => maximal.Any(named => Names(named, candidate)));

    /// <summary>Whether git's full id <paramref name="named"/> is <paramref name="candidate"/>, which a record may hold shorter.</summary>
    private static bool Names(string named, string candidate) =>
        candidate.Length > 0 && named.Trim().StartsWith(candidate.Trim(), StringComparison.OrdinalIgnoreCase);
}
