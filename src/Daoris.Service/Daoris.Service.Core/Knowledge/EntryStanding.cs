namespace Daoris.Knowledge;

/// <summary>
/// What a reader can tell of an entry's standing without opening its file (KNOWFRESH1a-ii; the knowledge-freshness design's
/// §3.1): the commit its reading was taken at, and for a decision the notes after it and the mark a later note of its own
/// record puts on it. No model judges which record is current: the mark is the record's own statement, and an entry that
/// carries none says <c>no mark</c>, never "current".
/// </summary>
/// <param name="Commit">The full commit the reading was taken at, or the one a feed named; null where there is none (unknown).</param>
/// <param name="Fed">Whether a feed gave the reading, and not a read of a checkout on this machine.</param>
/// <param name="Uncommitted">Whether the checkout's record folders differed from <paramref name="Commit"/> when it was read.</param>
/// <param name="Decision">What a decision's own record says of it; null for any other entry, or where its notes cannot be placed.</param>
/// <param name="UncommittedPending">Whether the checkout's uncommitted reading had not finished when this was asked, so <paramref name="Uncommitted"/> is no answer.</param>
/// <param name="UncommittedUnknown">Whether that reading was asked and git did not answer (KNOWCENTRAL2a), which is never clean.</param>
public sealed record EntryStanding(
    string? Commit, bool Fed, bool Uncommitted, DecisionStanding? Decision = null, bool UncommittedPending = false, bool UncommittedUnknown = false)
{
    /// <summary>The one line a hit and a read of the entry both carry, so the two doors cannot drift on it.</summary>
    public string Says
    {
        get
        {
            var uncommitted = UncommittedPending ? " + uncommitted not yet read"
                : UncommittedUnknown ? " + uncommitted unknown"
                : Uncommitted ? " + uncommitted" : "";
            var reading = Commit is null
                ? "unknown"
                : $"{(Fed ? "fed" : "read")} at {Clipped.ShortCommit(Commit, 8)}{uncommitted}";
            return Decision is null ? $"standing: {reading}" : $"standing: {reading} · {Decision.Says}";
        }
    }
}

/// <summary>A decision's standing in its own record.</summary>
/// <param name="Later">How many notes of its file follow it: all of them for the opening, those after it for a note.</param>
/// <param name="Marks">Each mark that targets it, as <c>superseded by D50</c> reads; empty is <c>no mark</c>.</param>
public sealed record DecisionStanding(int Later, IReadOnlyList<string> Marks)
{
    public string Says => $"later: {Later} {(Later == 1 ? "note" : "notes")} · {(Marks.Count == 0 ? "no mark" : string.Join(", ", Marks))}";
}
