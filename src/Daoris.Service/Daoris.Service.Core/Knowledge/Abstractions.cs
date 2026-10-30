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
    /// <summary>
    /// Replace everything held for a repository with exactly these entries, and its reading with <paramref name="reading"/>, in
    /// one write (KNOWCENTRAL2a); <paramref name="alongside"/> runs inside it, so what it writes lands or fails with them.
    /// </summary>
    /// <returns>False, with nothing written, where the reading expected a generation the store no longer holds.</returns>
    Task<bool> ReplaceRepositoryAsync(
        string repository, IReadOnlyList<KnowledgeEntry> entries, ReadingWrite reading,
        Func<CancellationToken, Task>? alongside = null, CancellationToken ct = default);

    /// <summary>
    /// Stamp a repository's entries (its name in any case) with another workspace in one write, their bodies untouched, moving
    /// each one's generation on.
    /// </summary>
    Task RestampAsync(string repository, string workspace, CancellationToken ct = default);

    /// <summary>The generation each repository's reading stands at; a repository absent here stands at 0.</summary>
    Task<IReadOnlyDictionary<string, long>> GenerationsAsync(CancellationToken ct = default);

    /// <summary>One repository's reading as stored, or null where none is.</summary>
    Task<IndexReading?> ReadingAsync(string repository, CancellationToken ct = default);

    /// <summary>
    /// The entries by these ids, each with its file's entries where <paramref name="withFile"/> says so, and each repository's
    /// reading, read together in one read: no write lands between the three.
    /// </summary>
    Task<HeldTogether> HeldAsync(IReadOnlyCollection<string> ids, Func<KnowledgeEntry, bool> withFile, CancellationToken ct = default);

    /// <summary>Every entry currently held, in no guaranteed order.</summary>
    Task<IReadOnlyList<KnowledgeEntry>> AllAsync(CancellationToken ct = default);

    /// <summary>One entry by its <see cref="KnowledgeEntry.Id"/>, or null.</summary>
    Task<KnowledgeEntry?> FindAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// The entries of one file of one repository, so a hit's standing reads the notes of its own decision and no other
    /// (KNOWFRESH1a-ii). A store that answers it without reading every entry overrides this.
    /// </summary>
    async Task<IReadOnlyList<KnowledgeEntry>> OfFileAsync(string repository, string relativePath, CancellationToken ct = default) =>
        (await AllAsync(ct).ConfigureAwait(false))
            .Where(entry => entry.Repository == repository && entry.RelativePath == relativePath)
            .ToList();

    /// <summary>How many entries each repository holds — a count, never a read of every body (REV3 C1).</summary>
    Task<IReadOnlyDictionary<string, int>> CountByRepositoryAsync(CancellationToken ct = default);

    /// <summary>
    /// Why this store's index answers nothing, or null when it answers (KSCHEMA1): a newer Daoris wrote it, so each
    /// method above throws this refusal and the rest of the store works.
    /// </summary>
    NewerIndexException? Refusal => null;
}

/// <summary>
/// A repository's reading as the index keeps it beside its entries (KNOWCENTRAL2a; the memory coordinator design's §5): the
/// generation every write of the repository moves on, the commit and root signature it was read at, and whether a feed gave
/// it. Every process over the store reads this one.
/// </summary>
/// <param name="Commit">The commit read or fed; null where none was named (a retirement, a folder that is no checkout).</param>
/// <param name="Signature">The root's signature taken before it was read; null where none was taken.</param>
public sealed record IndexReading(
    string Repository, long Generation, string? Commit, RootSignature? Signature, bool Fed, DateTimeOffset At);

/// <summary>What a write of a repository's entries says of the reading it stores.</summary>
/// <param name="Expected">
/// The generation its writer read before reading the source: the write lands only while the store still holds it (none is 0).
/// Null for a write that reads no source, which takes the generation inside its own write and always lands.
/// </param>
public sealed record ReadingWrite(long? Expected, string? Commit, RootSignature? Signature, bool Fed, DateTimeOffset At)
{
    /// <summary>A write that reads no source and names no commit: a retirement, a withdrawal, a seeded store.</summary>
    public static ReadingWrite Taken(DateTimeOffset at) => new(null, null, null, false, at);
}

/// <summary>What <see cref="IKnowledgeStore.HeldAsync"/> read together.</summary>
/// <param name="Entries">Each asked id that is held, by id.</param>
/// <param name="Files">Each such entry's file's entries, by repository and path, where they were asked for.</param>
/// <param name="Readings">Each such entry's repository's reading, where it has one.</param>
public sealed record HeldTogether(
    IReadOnlyDictionary<string, KnowledgeEntry> Entries,
    IReadOnlyDictionary<(string Repository, string Path), IReadOnlyList<KnowledgeEntry>> Files,
    IReadOnlyDictionary<string, IndexReading> Readings);

/// <summary>The finished trees' records, searched and read (KEEPSIZE1c); machine-local, so only a local host is given one.</summary>
public interface IWorkRecordSearch
{
    /// <summary>Records matching a query, best first; none where the query's kinds, provenance or workspace leave them out.</summary>
    Task<IReadOnlyList<KnowledgeHit>> SearchRecordsAsync(KnowledgeQuery query, CancellationToken ct = default);

    /// <summary>One record read whole, by its id (<c>record:&lt;workspace&gt;/&lt;repository&gt;/&lt;folder&gt;</c>); null where none is kept.</summary>
    Task<KnowledgeEntry?> FindRecordAsync(string id, CancellationToken ct = default);
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
/// A search that can say which of its halves answered (TIER1). The hybrid is the one composition with
/// two halves, and so the one that can lose one without the caller knowing.
/// </summary>
public interface IAnsweringSearch : IKnowledgeSearch
{
    Task<SearchAnswer> AnswerAsync(KnowledgeQuery query, CancellationToken ct = default);
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
/// <b>As built, no host passes one</b>: every index runs <see cref="DisclosurePolicy.LocalOnly"/>,
/// which permits everything, so a refresh reports nothing withheld. Shared mode draws the boundary at
/// the feed instead — a deployment takes a repository's knowledge only when its manifest joins and
/// shares (D47 §4), refuses the rest at the door, and drops what a repository fed once it stops
/// sharing. This seam is where a policy over the index itself would go (REV3 CLEAN1 corrected the
/// claim that shared mode could not be built without it).
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
/// <param name="Absent">
/// Registered repositories whose checkout is no longer where the registry says it is (D48 §3). Named,
/// never silently skipped: a repository that quietly stops contributing looks exactly like one with
/// nothing to say, and the refresh would otherwise report a healthy count over a moved checkout — the
/// ghost failure from the other direction. A registration that never named a path is not an absence:
/// a teammate's mirrored row has no checkout here by construction.
/// </param>
/// <param name="Embedded">
/// What the semantic half made of the entries (SEM3, D123), or null when it did not run: none is
/// configured, or it failed and <paramref name="SemanticError"/> says why.
/// </param>
/// <param name="Read">
/// When and from which commit each registered repository was read (KNOWCURRENT1), so a reader can tell how current the
/// index is. Empty where the source reads no registered checkout (a fed host).
/// </param>
public sealed record IndexReport(
    string Source, int Repositories, int Entries, int Withheld, string? SemanticError = null,
    IReadOnlyList<string>? Absent = null, EmbeddingReport? Embedded = null,
    IReadOnlyList<RepositoryReading>? Read = null)
{
    /// <summary>The absences, never null — a caller reporting "none" should not have to branch.</summary>
    public IReadOnlyList<string> Absent { get; init; } = Absent ?? [];

    /// <summary>The readings, never null.</summary>
    public IReadOnlyList<RepositoryReading> Read { get; init; } = Read ?? [];
}

/// <summary>
/// When the index read a repository and the commit its checkout was at (KNOWCURRENT1). The commit is null for a
/// checkout that is no git repository or has no commit yet.
/// </summary>
public sealed record RepositoryReading(string Repository, DateTimeOffset ReadAt, string? Commit)
{
    /// <summary>The form a person reads.</summary>
    public string? ShortCommit => Commit is null ? null : Clipped.ShortCommit(Commit, 8);
}

/// <summary>A source that can read some of its roots alone, so a root that moved is read again and the others are not (KNOWCURRENT1).</summary>
public interface IRootReadingSource : IKnowledgeSource
{
    /// <summary>Read only these checkouts.</summary>
    Task<IReadOnlyList<KnowledgeEntry>> ReadRootsAsync(IReadOnlyList<string> roots, CancellationToken ct = default);
}

/// <summary>
/// What one pass of the semantic half embedded (SEM3, D123): said, because a long entry split into
/// several vectors is something a reader of the index should be able to see rather than assume.
/// </summary>
/// <param name="Entries">How many entries were embedded.</param>
/// <param name="Pieces">How many vectors they became.</param>
/// <param name="Split">How many entries were longer than the window, and became more than one.</param>
/// <param name="Window">The most characters one embedded text carried, the title included.</param>
public sealed record EmbeddingReport(int Entries, int Pieces, int Split, int Window);
