namespace Daoris.Driver;

/// <summary>
/// Where a session tree's review is measured from, and how its HEAD stands against its repository's line (REVIEWBASE1, D164's
/// note): <paramref name="From"/> is the commit the review names as its base; <paramref name="Behind"/> is null where the line could
/// not be read; <paramref name="Against"/> is the tree the diff reads from where it is not <paramref name="From"/>'s own: a merge of
/// the line's base and the record's where neither reaches the other (REVIEWBASE3), so a chain's step that merged the line shows
/// neither the line's changes nor its parent's as its own.
/// </summary>
public sealed record ReviewRange(string From, LineBehind? Behind, string? Against = null)
{
    /// <summary>What the diff reads from: the merged tree where there is one, else the base.</summary>
    public string Reads => Against ?? From;
}

/// <summary>
/// How a tree's HEAD stands against the latest its repository's line is known here, the line and its copy on <c>origin</c> taken
/// together (REVIEWBASE1): <paramref name="Commits"/> the line has that HEAD does not, <paramref name="Ahead"/> HEAD has that the line
/// does not. Read from the refs as the last fetch left them: the review fetches nothing.
/// </summary>
public sealed record LineBehind(string Line, int Commits, int Ahead);

/// <summary>
/// The choice of a review's base among the commits a tree's history holds (REVIEWBASE1): the newest commit the work shares with
/// the line, here or on origin, and the commit its record says it began at. Pure; <c>SessionTrees.ReviewRangeAsync</c> asks git,
/// and says why the record's base alone was not enough.
/// </summary>
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

    /// <summary>
    /// The line's base and the record's, where git names both among those no other reaches (REVIEWBASE3): a chain's step grew from
    /// its parent's tip and merged the line since, so the line's base, which <see cref="Newest"/> takes, leaves the parent's work in
    /// the review, and the record's leaves the line's. The review reads from a merge of the two. Null where one reaches the other,
    /// there is no record's, or git named none: the base alone stands, and no further git start is asked for.
    /// </summary>
    public static (string Line, string Record)? Incomparable(IReadOnlyList<string> candidates, IReadOnlyCollection<string> maximal, string? record)
    {
        if (string.IsNullOrWhiteSpace(record) || Newest(candidates, maximal) is not { } line) return null;
        if (string.Equals(line.Trim(), record.Trim(), StringComparison.OrdinalIgnoreCase)) return null;
        return maximal.Any(named => Names(named, record)) ? (line, record) : null;
    }

    /// <summary>Whether git's full id <paramref name="named"/> is <paramref name="candidate"/>, which a record may hold shorter.</summary>
    private static bool Names(string named, string candidate) =>
        candidate.Length > 0 && named.Trim().StartsWith(candidate.Trim(), StringComparison.OrdinalIgnoreCase);
}
