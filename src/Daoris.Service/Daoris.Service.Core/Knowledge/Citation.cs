namespace Daoris.Knowledge;

/// <summary>
/// A repository's record cited by its entry (KNOWCENTRAL2a; the memory coordinator design's §5): the session names the id and
/// the lines, and the index fills the rest from its stored reading. It names no machine path.
/// </summary>
/// <param name="Id">The entry's <see cref="KnowledgeEntry.Id"/>, which names its repository.</param>
/// <param name="Lines">The span of the entry a hit named, or null for the whole entry.</param>
/// <param name="Commit">The commit the stored reading was read or fed at.</param>
/// <param name="Fed">Whether a feed gave that reading.</param>
/// <param name="Digest">The entry's body, hashed (<see cref="FeedDigest.Of(string)"/>).</param>
public sealed record Citation(string Id, LineSpan? Lines, string Commit, bool Fed, string Digest)
{
    /// <summary>
    /// For another repository's memory entry cited (KNOWCENTRAL2g; the design §6 B), the revision it was read at, its <see cref="Id"/> the
    /// entry's reference; null for a record. Absent from a record's stored citation, which reads as it was.
    /// </summary>
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public long? Revision { get; init; }

    /// <summary>Another repository's entry, cited at the revision read: rechecked through its view, never copied.</summary>
    public static Citation OfEntry(MemoryReference entry, long revision) => new(entry.ToString(), null, "", false, "") { Revision = revision };
}

/// <summary>A citation made, or why none was.</summary>
/// <param name="Refused">Why nothing was cited, in a sentence; null when <paramref name="Cited"/> is.</param>
/// <param name="Successor">The record that already superseded the entry, as <c>D50</c> reads, where that is why.</param>
public sealed record CitationAnswer(Citation? Cited, string? Refused = null, string? Successor = null);

/// <summary>What a recheck found of a citation.</summary>
public enum CitationStanding
{
    /// <summary>The same body, no mark on it, its file as the reading's commit has it (or a fed reading).</summary>
    Holds,

    /// <summary>A later record superseded it, under the same conditions as <see cref="Holds"/>.</summary>
    Failed,

    /// <summary>A later record amended it and left it standing.</summary>
    Amended,

    /// <summary>Nothing could be told: the check is retried, and the citation's validity is untouched.</summary>
    Inconclusive,
}

/// <param name="Says">The finding in a line.</param>
/// <param name="By">The record that superseded or amended it, as <c>D50</c> reads; null for any other finding.</param>
/// <param name="Commit">The commit of the reading the check read; null where it read none.</param>
/// <param name="CiteAgain">Whether the owner's next session is asked to cite it again.</param>
public sealed record CitationCheck(
    CitationStanding Standing, string Says, string? By = null, string? Commit = null, bool Fed = false, bool CiteAgain = false);
