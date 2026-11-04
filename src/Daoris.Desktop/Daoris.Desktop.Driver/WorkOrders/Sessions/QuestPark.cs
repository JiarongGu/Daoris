namespace Daoris.Driver;

/// <summary>
/// A quest the planner parked on its failed sessions here (<see cref="StartVerdict.Exhausted"/>), with what its park is said by:
/// Overview's *What needs you* row and the park's notice.
/// </summary>
/// <param name="Quest">Its id.</param>
/// <param name="Repository">The repository it is addressed to.</param>
public sealed record QuestPark(string Quest, string Repository)
{
    /// <summary>Its last session here, which failed or was cut off; null where the records show no session of this machine's on it.</summary>
    public string? Session { get; init; }

    /// <summary>How many sessions failed since its last *Try again*, as the planner counted to park it; null where the records no longer say.</summary>
    public int? Strikes { get; init; }

    /// <summary>What its last session's record said about its end: the last failure's note. Null is a state, not a gap.</summary>
    public string? Note { get; init; }

    /// <summary>That note's lines by code, beside it; null where the note is, or the record is from before parts.</summary>
    public IReadOnlyList<NotePart>? NoteParts { get; init; }

    /// <summary>When its last session ended: what the park has waited since. Null where that record is not listed.</summary>
    public DateTimeOffset? Since { get; init; }
}
