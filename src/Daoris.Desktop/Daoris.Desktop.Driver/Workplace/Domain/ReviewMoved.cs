namespace Daoris.Driver;

/// <summary>
/// The word an Accept is refused or dropped in when the review moved from the record it named, and a revision as that word names it: the
/// review's pass, an entry's Accept across its runs and an entry part's outcome say both alike.
/// </summary>
public static class ReviewMoved
{
    /// <summary>The record the word names is not the one the review stands on, or the work moved from it before the pass applied it.</summary>
    public const string Word = "moved";

    /// <summary>The first twelve characters of <paramref name="revision"/>, as the words name a record.</summary>
    public static string Short(string? revision) => revision is null ? "none" : revision.Length > 12 ? revision[..12] : revision;
}
