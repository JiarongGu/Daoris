namespace Daoris.Knowledge;

/// <summary>
/// Runs two searches and fuses their rankings.
/// </summary>
/// <remarks>
/// <para><b>Fused on rank, not on score.</b> BM25 returns an unbounded relevance figure and cosine
/// similarity returns a number in [-1, 1]; adding or averaging them compares quantities that mean
/// different things, and whichever happens to have the larger range silently wins. Reciprocal rank
/// fusion uses only each result's <em>position</em> in its own list, which is the one thing the two
/// searches agree on the meaning of.</para>
///
/// <para>This is the composition the <see cref="IKnowledgeSearch"/> seam was shaped for — hybrid is
/// not a third implementation to keep in step with the other two.</para>
///
/// <para>If either search is unavailable or fails, the other still answers. A knowledge index that
/// returns nothing because an embedding endpoint is down is worse than one that returns the lexical
/// half. 🔴 But the caller must be able to tell (TIER1, D24): <see cref="AnswerAsync"/> says which
/// halves answered and why one did not, where swallowing the failure let every door report the
/// configured tier over an answer only one half had made.</para>
/// </remarks>
public sealed class HybridKnowledgeSearch(IKnowledgeSearch lexical, IKnowledgeSearch? semantic = null)
    : IAnsweringSearch
{
    /// <summary>
    /// The RRF damping constant. 60 is the value the original paper settled on, and it is what keeps
    /// one search's top hit from automatically taking the fused top spot: at k=60 the gap between
    /// rank 1 and rank 2 is small, so agreement between the two searches outweighs confidence within
    /// either one — which is the entire reason to fuse rather than pick.
    /// </summary>
    private const double K = 60;

    public async Task<IReadOnlyList<KnowledgeHit>> SearchAsync(KnowledgeQuery query, CancellationToken ct = default) =>
        (await AnswerAsync(query, ct).ConfigureAwait(false)).Hits;

    public async Task<SearchAnswer> AnswerAsync(KnowledgeQuery query, CancellationToken ct = default)
    {
        // Over-fetch from each side: a result ranked 15th lexically and 3rd semantically should be
        // able to surface, and it cannot if each list was truncated to the final limit first.
        var wide = query with { Limit = Math.Max(query.Limit * 3, 30) };

        var (lexicalHits, lexicalFailure) = await SafeAsync(lexical, wide, ct).ConfigureAwait(false);
        var (semanticHits, semanticFailure) = semantic is null
            ? ([], null)
            : await SafeAsync(semantic, wide, ct).ConfigureAwait(false);

        var failures = new List<string>();
        if (lexicalFailure is not null) failures.Add($"the search by words did not answer: {lexicalFailure}");
        if (semanticFailure is not null) failures.Add($"the search by meaning did not answer: {semanticFailure}");
        SearchAnswer Answered(IReadOnlyList<KnowledgeHit> hits) => new(
            hits, lexicalFailure is null, semantic is not null && semanticFailure is null,
            failures.Count == 0 ? null : string.Join("; ", failures));

        if (semanticHits.Count == 0) return Answered(Truncate(lexicalHits, query.Limit));
        if (lexicalHits.Count == 0) return Answered(Truncate(semanticHits, query.Limit));

        var fused = new Dictionary<string, (KnowledgeEntry Entry, double Score, KnowledgeHit Shown)>(StringComparer.Ordinal);
        Accumulate(fused, lexicalHits);
        Accumulate(fused, semanticHits);

        return Answered(fused.Values
            .OrderByDescending(v => v.Score)
            .ThenBy(v => v.Entry.Id, StringComparer.Ordinal)
            .Take(query.Limit)
            .Select(v => new KnowledgeHit(v.Entry, v.Score, v.Shown.Excerpt, v.Shown.ExcerptLine))
            .ToList());
    }

    /// <param name="fused">Each entry's fused score, and the hit whose excerpt it shows, with that excerpt's line.</param>
    private static void Accumulate(
        Dictionary<string, (KnowledgeEntry Entry, double Score, KnowledgeHit Shown)> fused,
        IReadOnlyList<KnowledgeHit> hits)
    {
        for (var rank = 0; rank < hits.Count; rank++)
        {
            var hit = hits[rank];
            var contribution = 1.0 / (K + rank + 1);
            if (fused.TryGetValue(hit.Entry.Id, out var existing))
            {
                // Keep the excerpt that already showed a matched passage: the lexical side knows
                // WHERE it matched and the semantic side does not. Its line goes with it (ORIENT2e).
                fused[hit.Entry.Id] = (existing.Entry, existing.Score + contribution,
                    existing.Shown.Excerpt is null ? hit : existing.Shown);
            }
            else
            {
                fused[hit.Entry.Id] = (hit.Entry, contribution, hit);
            }
        }
    }

    /// <returns>The half's hits, or none and why it did not answer.</returns>
    private static async Task<(IReadOnlyList<KnowledgeHit> Hits, string? Failure)> SafeAsync(
        IKnowledgeSearch search, KnowledgeQuery query, CancellationToken ct)
    {
        try
        {
            return (await search.SearchAsync(query, ct).ConfigureAwait(false), null);
        }
        catch (OperationCanceledException)
        {
            throw; // cancellation is the caller's, not a failure to absorb
        }
        catch (Exception error)
        {
            // One half being unavailable degrades the answer; it must not remove it — and it must
            // not go unsaid either (TIER1).
            return ([], error.Message);
        }
    }

    private static IReadOnlyList<KnowledgeHit> Truncate(IReadOnlyList<KnowledgeHit> hits, int limit) =>
        hits.Count <= limit ? hits : hits.Take(limit).ToList();
}
