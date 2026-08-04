namespace Daoris.Knowledge;

/// <summary>
/// Scores entries by term overlap, over any <see cref="IKnowledgeStore"/>.
/// </summary>
/// <remarks>
/// Deliberately simple, and deliberately first: it needs no model, runs offline, and answers most of
/// what is actually asked. Its weakness is known and measured elsewhere in this project — word
/// overlap finds <em>restatement</em> and cannot find <em>convergence</em>, where two repositories
/// reach the same conclusion in different vocabulary. Closing that gap is what
/// <see cref="SemanticKnowledgeSearch"/> is for, and why this returns scores that two searches can be
/// merged on.
///
/// <see cref="SqliteKnowledgeSearch"/> is faster and ranks better where the store is SQLite. This one
/// exists because it works against ANY store, which the tests want and a store-agnostic default needs.
/// </remarks>
public sealed class LexicalKnowledgeSearch(IKnowledgeStore store) : IKnowledgeSearch
{
    public async Task<IReadOnlyList<KnowledgeHit>> SearchAsync(KnowledgeQuery query, CancellationToken ct = default)
    {
        var entries = await store.AllAsync(ct).ConfigureAwait(false);
        var terms = Text.Tokenize(query.Text);
        var admitted = entries.Where(query.Admits);

        if (terms.Count == 0)
        {
            // No terms is a browse, not a failed search. Order it so the result is stable.
            return admitted
                .OrderBy(e => e.Repository, StringComparer.Ordinal)
                .ThenBy(e => e.Title, StringComparer.Ordinal)
                .Take(query.Limit)
                .Select(e => new KnowledgeHit(e, 0))
                .ToList();
        }

        var hits = new List<KnowledgeHit>();
        foreach (var entry in admitted)
        {
            ct.ThrowIfCancellationRequested();
            var score = Score(entry, terms);
            if (score > 0) hits.Add(new KnowledgeHit(entry, score, Text.Excerpt(entry.Body, terms)));
        }

        return hits
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.Entry.Id, StringComparer.Ordinal)   // stable for equal scores
            .Take(query.Limit)
            .ToList();
    }

    private static double Score(KnowledgeEntry entry, IReadOnlyCollection<string> terms)
    {
        var title = Text.Tokenize(entry.Title);
        var body = Text.Tokenize(entry.Body);

        double score = 0;
        var matched = 0;
        foreach (var term in terms)
        {
            // A title match is worth far more than a body match: a title is what the author chose to
            // call the thing, and an entry titled for the query is almost always the one wanted.
            var inTitle = title.Contains(term);
            var bodyHits = body.Count(t => t == term);
            if (!inTitle && bodyHits == 0) continue;

            matched++;
            if (inTitle) score += 8;
            // Diminishing, so one long entry repeating a word cannot outrank a short exact one.
            score += Math.Log(1 + bodyHits) * 2;
        }

        if (matched == 0) return 0;

        // Reward covering more of the query. A hit on every term beats a hit on one, which raw term
        // frequency alone gets backwards.
        return score * (1.0 + (double)matched / terms.Count);
    }
}
