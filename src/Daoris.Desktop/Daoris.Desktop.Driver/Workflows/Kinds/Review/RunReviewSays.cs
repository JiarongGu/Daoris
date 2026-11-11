namespace Daoris.Driver;

/// <summary>
/// The values the review's one sentence of its kind is worded from: what the work is, how much of it there is, and what of it is left unread
/// or unchecked. The words are the page's, in the reader's language.
/// </summary>
/// <param name="Kind">One of <see cref="ReviewKinds"/>; null before the review keeps a record.</param>
public sealed record RunReviewSays(string? Kind)
{
    /// <summary>The files a folder's or a copy's work changed.</summary>
    public int Files { get; init; }

    /// <summary>The parts of the chain handed back, and how many of them nothing checked.</summary>
    public int Parts { get; init; }

    public int Unchecked { get; init; }

    /// <summary>A checkout's turns.</summary>
    public int Turns { get; init; }

    public string? Branch { get; init; }

    public string? Copy { get; init; }

    /// <summary>A system's plan by its number, and the system it is for.</summary>
    public int? Plan { get; init; }

    public string? System { get; init; }

    /// <summary>What could not be read or shown, counted, and how far back the rows reach (<see cref="ReviewCoverage"/>).</summary>
    public int Incomplete { get; init; }

    public string? Coverage { get; init; }

    public static RunReviewSays Of(ReviewRecord? record) => record is null ? new((string?)null) : new(record.Kind)
    {
        Files = record.Rows.Count, Parts = record.Parts.Count, Unchecked = record.Parts.Count(part => part.Unchecked),
        Turns = record.Reference.Turns.Count, Branch = record.Reference.Branch, Copy = record.Reference.Copy,
        Plan = record.Reference.Plan, System = record.Reference.System, Incomplete = record.Incomplete.Count, Coverage = record.Coverage,
    };
}
