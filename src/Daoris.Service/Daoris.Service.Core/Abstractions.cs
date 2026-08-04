namespace Daoris.Knowledge;

/// <summary>
/// Where knowledge comes from.
/// </summary>
/// <remarks>
/// The local filesystem is one source. A git remote, or an endpoint a devkit gate pushes to, are
/// others — and which of those is right is still open, so the shape that survives all three is a
/// source that yields entries and knows what to call itself.
/// </remarks>
public interface IKnowledgeSource
{
    /// <summary>Identifies this source in an index report — a path, a remote, a hostname.</summary>
    string Name { get; }

    /// <summary>Read everything this source currently holds.</summary>
    Task<IReadOnlyList<KnowledgeEntry>> ReadAsync(CancellationToken ct = default);
}

/// <summary>
/// Where entries are kept.
/// </summary>
/// <remarks>
/// In-memory first, embedded single-file next, and a hosted store only if query volume ever demands
/// one. The batch shape is the primitive because that is what every real store rewards, and because
/// a per-entry API invites a caller to write the loop that a store should own.
///
/// Replacing a repository wholesale rather than diffing is deliberate: re-reading is cheap, an entry
/// that was deleted upstream must not survive in the index, and a diff is a second thing that can be
/// wrong about what changed.
/// </remarks>
public interface IKnowledgeStore
{
    /// <summary>Replace everything held for a repository with exactly these entries.</summary>
    Task ReplaceRepositoryAsync(string repository, IReadOnlyList<KnowledgeEntry> entries, CancellationToken ct = default);

    /// <summary>Every entry currently held, in no guaranteed order.</summary>
    Task<IReadOnlyList<KnowledgeEntry>> AllAsync(CancellationToken ct = default);

    /// <summary>One entry by its <see cref="KnowledgeEntry.Id"/>, or null.</summary>
    Task<KnowledgeEntry?> FindAsync(string id, CancellationToken ct = default);
}

/// <summary>
/// How entries are found.
/// </summary>
/// <remarks>
/// Lexical first, because it needs no model and answers most of what a person actually asks.
/// Semantic arrives with the cognition sibling, and hybrid is then a composition of the two rather
/// than a third implementation — which is the reason this returns scored hits rather than a bare
/// list: scores are what let two searches be merged.
/// </remarks>
public interface IKnowledgeSearch
{
    /// <summary>Find entries matching a query, best first.</summary>
    Task<IReadOnlyList<KnowledgeHit>> SearchAsync(KnowledgeQuery query, CancellationToken ct = default);
}

/// <summary>
/// Whether an entry may leave this machine.
/// </summary>
/// <remarks>
/// The disclosure boundary as a type rather than a paragraph in a design document. Ordinary
/// applications ask who may read something; this one must first ask what may travel at all, because
/// several repositories in this family are private and their doctrine names things that are
/// deliberately kept out of tracked files.
///
/// Making it a seam means shared mode cannot be built without answering it, and local mode answers
/// it trivially — <see cref="DisclosurePolicy.LocalOnly"/> permits everything, because nothing is
/// leaving. A rule that lives only in prose is one that gets remembered until it doesn't.
/// </remarks>
public interface IDisclosurePolicy
{
    /// <summary>Whether this entry may be shared beyond the machine that indexed it.</summary>
    bool MayLeaveMachine(KnowledgeEntry entry);
}

/// <summary>What one refresh of the index did.</summary>
/// <param name="Source">The source that was read.</param>
/// <param name="Repositories">How many repositories it yielded.</param>
/// <param name="Entries">How many entries were stored.</param>
/// <param name="Withheld">How many the disclosure policy excluded.</param>
/// <param name="SemanticError">
/// Why semantic indexing did not happen, or null if it did (or was never configured). Carried rather
/// than thrown: the lexical index is complete and usable either way, and taking a whole refresh down
/// for the optional half would trade the feature that works for the one that does not.
/// </param>
public sealed record IndexReport(
    string Source, int Repositories, int Entries, int Withheld, string? SemanticError = null);
