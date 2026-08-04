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
public sealed class KnowledgeService(IKnowledgeStore store, IKnowledgeSearch search, IKnowledgeSource source, IDisclosurePolicy? disclosure = null)
{
    private readonly KnowledgeIndex _index = new(store, disclosure);
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private bool _everRefreshed;

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

    /// <summary>Re-read every repository and rebuild the index.</summary>
    public async Task<IndexReport> RefreshAsync(CancellationToken ct = default)
    {
        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var report = await _index.RefreshAsync(source, ct).ConfigureAwait(false);
            _everRefreshed = true;
            return report;
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
