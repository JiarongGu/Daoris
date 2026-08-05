namespace Daoris.Knowledge;

/// <summary>How much one repository contributes to the index.</summary>
public sealed record RepositorySummary(string Repository, int Total, int Local, int Canonical);

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
    Lyntai.Embeddings.IEmbedder? embedder = null,
    Lyntai.Memory.IVectorStore? vectors = null)
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
