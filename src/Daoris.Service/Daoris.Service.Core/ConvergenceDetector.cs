using Lyntai.Embeddings;
using Lyntai.Memory;

namespace Daoris.Knowledge;

/// <summary>A set of entries from different repositories that appear to state the same lesson.</summary>
/// <param name="Entries">The entries, the strongest-connected first.</param>
/// <param name="Similarity">The best pairwise similarity in the group, in [-1, 1].</param>
public sealed record ConvergenceCandidate(IReadOnlyList<KnowledgeEntry> Entries, double Similarity)
{
    /// <summary>The repositories involved, which is what makes this worth a person's attention.</summary>
    public IReadOnlyList<string> Repositories =>
        Entries.Select(e => e.Repository).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();

    /// <summary>
    /// A copy rather than a convergence — the same document, pasted.
    /// </summary>
    /// <remarks>
    /// Worth separating, because the two are different findings with different work attached. A copy
    /// is easy to spot by name and easy to canonize; a convergence is two people reaching the same
    /// conclusion in different words, which nobody can find by looking. Run against a real family the
    /// copies dominate at any useful threshold and crowd the convergences out of the top of the list —
    /// so the caller is told which is which rather than left to infer it from a score.
    /// </remarks>
    public bool IsIdenticalCopy => Similarity >= 0.999;
}

/// <summary>How hard to look.</summary>
/// <param name="MinimumSimilarity">
/// Cosine similarity a pair must reach. Deliberately a parameter with no clever default: the right
/// value depends on the embedding model, and a threshold copied from a different model is a guess
/// wearing a number.
/// </param>
/// <param name="Kinds">Restrict to these kinds. Null means every kind.</param>
/// <param name="MaxCandidates">How many groups to return.</param>
public sealed record ConvergenceOptions(
    double MinimumSimilarity = 0.82,
    IReadOnlySet<EntryKind>? Kinds = null,
    int MaxCandidates = 25);

/// <summary>
/// Finds the same lesson learned twice in different repositories.
/// </summary>
/// <remarks>
/// This automates the survey that produced this project's own canon. Canonizing five skills and
/// seven rules meant reading twelve repositories by hand and noticing which documents were saying the
/// same thing — work that took a day and is exactly what a nearest-neighbour search over embeddings
/// does in a second.
///
/// <para>It proposes; a person disposes. A candidate is a prompt to look, not a merge — doctrine that
/// appeared without anyone choosing it is the failure this whole project exists to prevent
/// (<c>docs/DECISIONS.md</c> D21).</para>
///
/// <para>Two exclusions do most of the work:</para>
/// <list type="bullet">
///   <item><b>Same-repository pairs are not convergence.</b> Two related documents in one repository
///   are a repository being coherent, which is not news.</item>
///   <item><b>Canonical entries are excluded.</b> They are identical everywhere by construction, so
///   they would match themselves across every adopter, dominate every result, and mean nothing. What
///   is worth finding is two repositories arriving somewhere independently.</item>
/// </list>
/// </remarks>
public sealed class ConvergenceDetector(IKnowledgeStore store, IEmbedder embedder, IVectorStore vectors)
{
    public async Task<IReadOnlyList<ConvergenceCandidate>> FindAsync(
        ConvergenceOptions? options = null, CancellationToken ct = default)
    {
        options ??= new ConvergenceOptions();

        var all = await store.AllAsync(ct).ConfigureAwait(false);
        var considered = all
            .Where(e => e.Provenance == Provenance.Local)
            .Where(e => options.Kinds is null || options.Kinds.Contains(e.Kind))
            .ToList();

        if (considered.Count < 2) return [];

        var byId = considered.ToDictionary(e => e.Id, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var candidates = new List<ConvergenceCandidate>();

        // Embed every seed FIRST, in batches. One call per entry is what the naive version did, and
        // on a real corpus it was four hundred sequential HTTP round trips — slow enough to time out
        // rather than merely be inefficient. The library's primitive is a batch precisely because
        // that is what real endpoints reward.
        var vectorsBySeed = await EmbedAllAsync(considered, ct).ConfigureAwait(false);

        foreach (var seed in considered)
        {
            ct.ThrowIfCancellationRequested();
            if (seen.Contains(seed.Id)) continue;

            var matches = await vectors
                .SearchAsync(SemanticKnowledgeSearch.Collection, vectorsBySeed[seed.Id], 12, ct)
                .ConfigureAwait(false);

            var group = new List<KnowledgeEntry> { seed };
            var best = 0.0;

            foreach (var match in matches)
            {
                if (match.Score < options.MinimumSimilarity) continue;
                if (!byId.TryGetValue(match.Payload, out var other)) continue;
                if (other.Id == seed.Id) continue;
                // The whole point: a different repository reaching the same place.
                if (string.Equals(other.Repository, seed.Repository, StringComparison.Ordinal)) continue;
                if (seen.Contains(other.Id)) continue;

                group.Add(other);
                best = Math.Max(best, match.Score);
            }

            if (group.Count < 2) continue;

            foreach (var entry in group) seen.Add(entry.Id);
            candidates.Add(new ConvergenceCandidate(group, best));
        }

        return candidates
            .OrderByDescending(c => c.Similarity)
            .ThenByDescending(c => c.Entries.Count)
            .Take(options.MaxCandidates)
            .ToList();
    }

    /// <summary>
    /// Every entry's vector, batched.
    /// </summary>
    /// <remarks>
    /// These vectors already exist — the refresh put them in the store — but the vector-store seam
    /// offers upsert, search and delete, not fetch-by-id, so they are recomputed. Batching makes that
    /// cheap enough not to matter; adding a fetch to someone else's interface to save it would not be.
    /// </remarks>
    private async Task<Dictionary<string, float[]>> EmbedAllAsync(
        IReadOnlyList<KnowledgeEntry> entries, CancellationToken ct, int batchSize = 32)
    {
        var byId = new Dictionary<string, float[]>(StringComparer.Ordinal);
        for (var offset = 0; offset < entries.Count; offset += batchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = entries.Skip(offset).Take(batchSize).ToList();
            var embedded = await embedder
                .EmbedAsync(batch.Select(SemanticKnowledgeSearch.Embeddable).ToList(), ct)
                .ConfigureAwait(false);

            for (var i = 0; i < batch.Count; i++) byId[batch[i].Id] = embedded[i];
        }

        return byId;
    }
}
