using Lyntai.Embeddings;
using Lyntai.Memory;

namespace Daoris.Knowledge;

/// <summary>
/// Finds entries by meaning rather than by shared words.
/// </summary>
/// <remarks>
/// This exists to close a gap that is measured rather than assumed. Word overlap finds
/// <em>restatement</em> and cannot find <em>convergence</em>: two repositories that reached the same
/// conclusion in different vocabulary score like unrelated documents, and no threshold separates
/// them — the same limit the drift detector has, quantified in <c>docs/DECISIONS.md</c> D17. Since
/// the whole point of a cross-repository index is noticing that two repositories learned the same
/// thing, that gap is not a nicety.
///
/// The embedder and the vector store are the cognition sibling's seams, consumed as a library (D22).
/// The embedder is app-provided by that library's design, which is what keeps this optional: with
/// none configured the service is lexical-only, and local mode still works with nothing installed.
/// </remarks>
public sealed class SemanticKnowledgeSearch(
    IKnowledgeStore store, IEmbedder embedder, IVectorStore vectors) : IKnowledgeSearch
{
    /// <summary>One collection: the corpus is a single searchable space, not one per repository.</summary>
    internal const string Collection = "daoris-knowledge";

    /// <summary>
    /// Embed and store entries so they can be found. Called during a refresh; batched because that is
    /// what real embedding endpoints reward, and because the library's primitive is a batch.
    /// </summary>
    public static async Task IndexAsync(
        IReadOnlyList<KnowledgeEntry> entries, IEmbedder embedder, IVectorStore vectors,
        int batchSize = 32, CancellationToken ct = default)
    {
        for (var offset = 0; offset < entries.Count; offset += batchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = entries.Skip(offset).Take(batchSize).ToList();
            var texts = batch.Select(Embeddable).ToList();
            var embedded = await embedder.EmbedAsync(texts, ct).ConfigureAwait(false);

            for (var i = 0; i < batch.Count; i++)
            {
                // The payload is the id: the entry text lives in the store, and duplicating it here
                // would give two copies that can disagree about what an entry says.
                await vectors.UpsertAsync(Collection, batch[i].Id, embedded[i], batch[i].Id, ct)
                    .ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// What actually gets embedded. The title is repeated deliberately — it is the author's own
    /// summary of the entry, and one line of it carries more signal than several of body.
    /// </summary>
    internal static string Embeddable(KnowledgeEntry entry)
    {
        const int bodyLimit = 2000;
        var body = entry.Body.Length <= bodyLimit ? entry.Body : entry.Body[..bodyLimit];
        return $"{entry.Title}\n{entry.Title}\n{body}";
    }

    public async Task<IReadOnlyList<KnowledgeHit>> SearchAsync(KnowledgeQuery query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query.Text)) return [];

        var vector = await embedder.EmbedAsync(query.Text, ct).ConfigureAwait(false);

        // Over-fetch, because filtering happens after the search: asking for exactly `Limit` and then
        // discarding the ones that fail a filter silently returns fewer results than requested.
        var matches = await vectors
            .SearchAsync(Collection, vector, Math.Max(query.Limit * 4, 40), ct)
            .ConfigureAwait(false);

        var hits = new List<KnowledgeHit>();
        foreach (var match in matches)
        {
            var entry = await store.FindAsync(match.Payload, ct).ConfigureAwait(false);
            // A vector whose entry is gone is a stale index, not a result. Skipping is right: the
            // next refresh removes it, and returning an id that resolves to nothing reads as a bug.
            if (entry is null || !query.Admits(entry)) continue;

            hits.Add(new KnowledgeHit(entry, match.Score, Text.Excerpt(entry.Body)));
            if (hits.Count == query.Limit) break;
        }

        return hits;
    }
}
