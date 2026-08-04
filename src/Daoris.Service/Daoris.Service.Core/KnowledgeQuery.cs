namespace Daoris.Knowledge;

/// <summary>
/// What to look for, and what to look in.
/// </summary>
/// <remarks>
/// The filters are all optional and all narrowing. Absent means "no restriction" rather than
/// "exclude everything", because a query object whose defaults return nothing is a trap every caller
/// falls into once.
/// </remarks>
/// <param name="Text">The terms to match. Empty matches everything, which is how a caller browses.</param>
public sealed record KnowledgeQuery(string Text = "")
{
    /// <summary>Restrict to these kinds. Null means every kind.</summary>
    public IReadOnlySet<EntryKind>? Kinds { get; init; }

    /// <summary>Restrict to these repositories, by directory name. Null means every repository.</summary>
    public IReadOnlySet<string>? Repositories { get; init; }

    /// <summary>
    /// Restrict to canonical or local. Null means both — but <see cref="Provenance.Local"/> is the
    /// interesting one across repositories, since canonical content is identical wherever it is
    /// installed.
    /// </summary>
    public Provenance? Provenance { get; init; }

    /// <summary>How many hits to return.</summary>
    public int Limit { get; init; } = 20;

    /// <summary>Whether this entry passes the query's filters, ignoring its text.</summary>
    public bool Admits(KnowledgeEntry entry) =>
        (Kinds is null || Kinds.Contains(entry.Kind))
        && (Repositories is null || Repositories.Contains(entry.Repository))
        && (Provenance is null || Provenance == entry.Provenance);
}

/// <summary>One result: the entry, how well it matched, and where it matched.</summary>
/// <param name="Entry">The matched entry.</param>
/// <param name="Score">Relative score within one result set. Not comparable across searches.</param>
/// <param name="Excerpt">
/// The passage that matched, so a caller can show why without loading the whole entry. A result list
/// that cannot show its reasoning gets treated as an oracle, which is exactly what it is not.
/// </param>
public sealed record KnowledgeHit(KnowledgeEntry Entry, double Score, string? Excerpt = null);
