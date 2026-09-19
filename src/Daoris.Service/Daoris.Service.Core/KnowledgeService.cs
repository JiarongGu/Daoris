namespace Daoris.Knowledge;

/// <summary>How much one repository contributes to the index.</summary>
public sealed record RepositorySummary(string Repository, int Total, int Local, int Canonical);

/// <param name="Accepted">Whether the feed was taken. A refusal names the missing declaration.</param>
/// <param name="Message">The full answer, phrased once here so no two doors can drift on it.</param>
/// <param name="Entries">How many entries now stand for that repository, when accepted.</param>
public sealed record FeedOutcome(bool Accepted, string Message, int Entries);

/// <summary>
/// The service, as a client sees it: search, read, list, refresh.
/// </summary>
/// <remarks>
/// Every surface — MCP today, a web API and a desktop shell later — talks to this rather than to the
/// store and the search directly. One place to add caching, tracing or a permission check, instead of
/// three places that have to agree.
///
/// It refreshes on first use if the index is empty, because an empty index that requires a separate
/// setup call is a first run that looks broken.
/// </remarks>
public sealed class KnowledgeService(
    IKnowledgeStore store,
    IKnowledgeSearch search,
    IKnowledgeSource source,
    IDisclosurePolicy? disclosure = null,
    Lyntai.Inference.IVectorProvider? embedder = null,
    Lyntai.Memory.IVectorStore? vectors = null,
    // Last and optional: a service composed without one still searches and still finds convergence —
    // it simply cannot say who is out there, and reports an empty family rather than refusing to start.
    Registry? registry = null,
    // Where pushed registrations persist. Optional for the same reason as the registry; without it a
    // registration lives only as long as the process, which is fine for a test and wrong for a service.
    RegistrationStore? registrations = null)
{
    private readonly KnowledgeIndex _index = new(store, disclosure);

    /// <summary>
    /// ONE detector, not one per call.
    /// </summary>
    /// <remarks>
    /// It was constructed per request, which threw away the vectors it had just computed — so every
    /// look cost a full re-embed of the corpus, measured at 31 seconds over 449 entries. The interesting
    /// use is a person moving a threshold and looking again, which made that the common path rather than
    /// the rare one.
    /// </remarks>
    private readonly ConvergenceDetector _convergence = new(store, embedder, vectors);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private bool _everRefreshed;

    /// <summary>Whether semantic recall is available, which depends on an embedder being configured.</summary>
    public bool SemanticEnabled => embedder is not null && vectors is not null;

    public async Task<IReadOnlyList<KnowledgeHit>> SearchAsync(KnowledgeQuery query, CancellationToken ct = default)
    {
        await EnsureIndexedAsync(ct).ConfigureAwait(false);
        return await search.SearchAsync(query, ct).ConfigureAwait(false);
    }

    public async Task<KnowledgeEntry?> FindAsync(string id, CancellationToken ct = default)
    {
        await EnsureIndexedAsync(ct).ConfigureAwait(false);
        return await store.FindAsync(id, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RepositorySummary>> SummarizeAsync(CancellationToken ct = default)
    {
        await EnsureIndexedAsync(ct).ConfigureAwait(false);
        var all = await store.AllAsync(ct).ConfigureAwait(false);
        return all
            .GroupBy(e => e.Repository, StringComparer.Ordinal)
            .Select(g => new RepositorySummary(
                g.Key,
                g.Count(),
                g.Count(e => e.Provenance == Provenance.Local),
                g.Count(e => e.Provenance == Provenance.Canonical)))
            .OrderByDescending(r => r.Total)
            .ToList();
    }

    /// <summary>
    /// Where different repositories learned the same lesson independently.
    /// </summary>
    /// <remarks>
    /// Works with or without an embedder. Without one it still finds identical copies and
    /// restatements; with one it also finds convergence. A feature that returned nothing without an
    /// optional dependency would have made that dependency mandatory in all but name.
    /// </remarks>
    public async Task<IReadOnlyList<ConvergenceCandidate>> FindConvergenceAsync(
        ConvergenceOptions? options = null, CancellationToken ct = default)
    {
        await EnsureIndexedAsync(ct).ConfigureAwait(false);
        return await _convergence.FindAsync(options, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Who is out there, what each owns, and what is worth asking of them.
    /// </summary>
    /// <remarks>
    /// Read from each repository's own manifest rather than configured here, so the declaration sits
    /// next to the thing it describes and is reviewed by the people it describes. It is also the gate
    /// on addressing a quest: a repository with no manifest has no client to see one, and a quest
    /// nobody can read looks exactly like a quest that was read and ignored.
    /// </remarks>
    public async Task<IReadOnlyList<Registration>> RegistryAsync(CancellationToken ct = default)
    {
        await EnsureIndexedAsync(ct).ConfigureAwait(false);
        var counts = (await store.AllAsync(ct).ConfigureAwait(false))
            .GroupBy(entry => entry.Repository, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        return registry?.Read(counts) ?? [];
    }

    /// <summary>
    /// Record what a repository said about itself, when it told us rather than we found it.
    /// </summary>
    /// <remarks>
    /// Persisted before it is served: a pushed registration is the only registration a remote service
    /// has, and one that evaporates on restart looks exactly like a repository that never connected.
    /// </remarks>
    public async Task RegisterAsync(Registration registration, DateTimeOffset now, CancellationToken ct = default)
    {
        if (registrations is not null)
        {
            await registrations.UpsertAsync(registration, now, ct).ConfigureAwait(false);
        }

        registry?.Register(registration);
    }

    /// <summary>
    /// One repository's own knowledge, whole — what a sync loop feeds from (D47 §4). Local provenance
    /// only: canonical content is identical everywhere by construction and never feeds.
    /// </summary>
    public async Task<IReadOnlyList<KnowledgeEntry>> LocalEntriesAsync(
        string repository, CancellationToken ct = default)
    {
        await EnsureIndexedAsync(ct).ConfigureAwait(false);
        return (await store.AllAsync(ct).ConfigureAwait(false))
            .Where(entry => string.Equals(entry.Repository, repository, StringComparison.OrdinalIgnoreCase)
                && entry.Provenance == Provenance.Local)
            .ToList();
    }

    /// <summary>
    /// Accept one repository's knowledge content from a feed — the only ingest a remote deployment has,
    /// since it never scans a filesystem (D47 §4). The disclosure judgement runs HERE, at the door,
    /// over what the repository's own reviewed manifest declared: join admits records, knowledge is a
    /// second declaration, and silence refused both. Accepted entries are normalized — the repository
    /// name from the registration, provenance forced Local, because canonical doctrine is distributed
    /// by `sync`, never by the feed — and replace that repository's entries wholesale, so the feed is
    /// idempotent.
    /// </summary>
    public async Task<FeedOutcome> FeedAsync(
        string repository, IReadOnlyList<KnowledgeEntry> entries, CancellationToken ct = default)
    {
        var registration = (await RegistryAsync(ct).ConfigureAwait(false))
            .FirstOrDefault(r => string.Equals(r.Repository, repository, StringComparison.OrdinalIgnoreCase));

        if (registration is null || !registration.Joined)
        {
            return new(
                Accepted: false,
                $"`{repository}` has not joined this deployment — the manifest's `remote.join` is the "
                + "declaration that admits it, and silence means local.",
                Entries: 0);
        }

        if (!registration.SharesKnowledge)
        {
            return new(
                Accepted: false,
                $"`{registration.Repository}` joined without sharing knowledge — its records travel, its "
                + "knowledge stays home. `remote.knowledge` is the declaration that changes that.",
                Entries: 0);
        }

        var normalized = entries
            .Select(entry => entry with { Repository = registration.Repository, Provenance = Provenance.Local })
            .ToList();
        await store.ReplaceRepositoryAsync(registration.Repository, normalized, ct).ConfigureAwait(false);

        return new(Accepted: true, $"Indexed {normalized.Count} entries from `{registration.Repository}`.", normalized.Count);
    }

    /// <summary>Re-read every repository and rebuild the index.</summary>
    public async Task<IndexReport> RefreshAsync(CancellationToken ct = default)
    {
        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var report = await _index.RefreshAsync(source, ct).ConfigureAwait(false);

            // Embedding happens here rather than inside the index, because it is the expensive,
            // optional half: the store is usable the moment the refresh returns, and semantic recall
            // arrives when it arrives.
            string? semanticError = null;
            if (embedder is not null && vectors is not null)
            {
                try
                {
                    var entries = await store.AllAsync(ct).ConfigureAwait(false);
                    await SemanticKnowledgeSearch.IndexAsync(entries, embedder, vectors, ct: ct)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw; // the caller's, not a failure to absorb
                }
                catch (Exception error)
                {
                    // The lexical index is complete and usable. Failing the whole refresh because an
                    // embedding endpoint is unreachable, misconfigured or slow would trade the half
                    // that works for the half that does not — and it did, on the first real run,
                    // against a local server started without embeddings enabled.
                    semanticError = error.Message;
                }
            }

            _everRefreshed = true;
            return report with { SemanticError = semanticError };
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    /// <summary>
    /// Index on first use when there is nothing to search. A persisted index survives restarts, so
    /// this normally costs one cheap count and does nothing.
    /// </summary>
    private async Task EnsureIndexedAsync(CancellationToken ct)
    {
        if (_everRefreshed) return;

        var existing = await store.AllAsync(ct).ConfigureAwait(false);
        if (existing.Count > 0)
        {
            _everRefreshed = true;
            return;
        }

        await RefreshAsync(ct).ConfigureAwait(false);
    }
}
