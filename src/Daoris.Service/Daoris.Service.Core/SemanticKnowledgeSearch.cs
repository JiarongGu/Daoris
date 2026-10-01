using Lyntai.Inference;
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
/// The vector provider and the vector store are the cognition sibling's seams, consumed as a library
/// (D22). The provider is app-composed by that library's design, which is what keeps this optional:
/// with none configured the service is lexical-only, and local mode still works with nothing installed.
/// </remarks>
public sealed class SemanticKnowledgeSearch(
    IKnowledgeStore store, IVectorProvider embedder, IVectorStore vectors) : IKnowledgeSearch
{
    /// <summary>One collection: the corpus is a single searchable space, not one per repository.</summary>
    internal const string Collection = "daoris-knowledge";

    /// <summary>
    /// Embed every part of every entry and make the collection exactly that (SEM3, D123). Called during a
    /// refresh, with the whole corpus; batched because that is what real embedding endpoints reward, and
    /// because the library's primitive is a batch.
    /// </summary>
    /// <remarks>
    /// <para><b>An entry is its pieces</b> (<see cref="EntryPieces"/>), each its own vector, so a fact a
    /// long entry states near its end has a vector of its own instead of being cut.</para>
    ///
    /// <para><b>Embedded first, replaced whole after.</b> Every piece has its vector before the collection
    /// changes, so an embedder that fails part-way leaves the previous vectors as they were; then the
    /// collection is replaced rather than upserted into, because an entry is several ids now and an edited
    /// entry's old pieces would otherwise outlive it and be found for words it no longer has — the same
    /// reason the store replaces a repository wholesale rather than diffing it.</para>
    /// </remarks>
    /// <param name="entries">The whole corpus: what is not in it leaves the collection.</param>
    /// <param name="window">The most characters one embedded text carries, the title included.</param>
    public static async Task<EmbeddingReport> IndexAsync(
        IReadOnlyList<KnowledgeEntry> entries, IVectorProvider embedder, IVectorStore vectors,
        int window = EntryPieces.DefaultWindow, int batchSize = 32, CancellationToken ct = default)
    {
        var pieces = new List<(KnowledgeEntry Entry, EntryPiece Piece)>();
        var split = 0;
        foreach (var entry in entries)
        {
            var ofEntry = EntryPieces.Of(entry, window);
            if (ofEntry.Count > 1) split++;
            pieces.AddRange(ofEntry.Select(piece => (entry, piece)));
        }

        var embedded = new List<float[]>(pieces.Count);
        for (var offset = 0; offset < pieces.Count; offset += batchSize)
        {
            ct.ThrowIfCancellationRequested();
            var texts = pieces.Skip(offset).Take(batchSize).Select(p => p.Piece.Text).ToList();
            embedded.AddRange(await Embedding.EmbedAsync(embedder, texts, EmbeddingRole.Document, ct)
                .ConfigureAwait(false));
        }

        await vectors.RemoveCollectionAsync(Collection, ct).ConfigureAwait(false);
        for (var i = 0; i < pieces.Count; i++)
        {
            var (entry, piece) = pieces[i];
            // The payload is the entry's id: its text lives in the store, and duplicating it here would
            // give two copies that can disagree about what an entry says. The vector's own id says which
            // piece it was, so a hit can show that passage.
            await vectors.UpsertAsync(Collection, EntryPieces.VectorId(entry.Id, piece.Start), embedded[i], entry.Id, ct)
                .ConfigureAwait(false);
        }

        return new EmbeddingReport(entries.Count, pieces.Count, split, window);
    }

    public async Task<IReadOnlyList<KnowledgeHit>> SearchAsync(KnowledgeQuery query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query.Text)) return [];

        // The QUERY role, now that the seam can say it: asymmetric embedding models are trained with
        // a distinct instruction per side, and a symmetric one ignores the role entirely.
        var vector = (await Embedding.EmbedAsync(embedder, [query.Text], EmbeddingRole.Query, ct)
            .ConfigureAwait(false))[0];

        // Over-fetch, because filtering happens after the search: asking for exactly `Limit` and then
        // discarding the ones that fail a filter silently returns fewer results than requested. And read
        // further while that still leaves too few (SEM3): an entry is several vectors and is named once,
        // so a long entry's pieces can take many of the places one read returns.
        var hits = new List<KnowledgeHit>();
        var named = new HashSet<string>(StringComparer.Ordinal);
        for (var k = Math.Max(query.Limit * 4, 40); ; k *= 4)
        {
            var matches = await vectors.SearchAsync(Collection, vector, k, ct).ConfigureAwait(false);
            hits.Clear();
            named.Clear();
            foreach (var match in matches)
            {
                // Ranked best first, so an entry's first piece here is its best: it speaks for the entry,
                // and the entry's other pieces add nothing a reader could use.
                if (!named.Add(match.Payload)) continue;

                var entry = await store.FindAsync(match.Payload, ct).ConfigureAwait(false);
                // A vector whose entry is gone is a stale index, not a result. Skipping is right: the
                // next refresh removes it, and returning an id that resolves to nothing reads as a bug.
                if (entry is null || !query.Admits(entry)) continue;

                hits.Add(new KnowledgeHit(entry, match.Score, Excerpt(entry, EntryPieces.StartOf(match.Id))));
                if (hits.Count == query.Limit) return hits;
            }

            if (matches.Count < k) return hits;
        }
    }

    /// <summary>
    /// The passage of the piece that matched: a hit found by a long entry's later piece shows that
    /// piece's opening, not the entry's, or the reader cannot see why it matched.
    /// </summary>
    private static string Excerpt(KnowledgeEntry entry, int start) =>
        start > 0 && start < entry.Body.Length
            ? "…" + Text.Excerpt(entry.Body[start..])
            : Text.Excerpt(entry.Body);
}
