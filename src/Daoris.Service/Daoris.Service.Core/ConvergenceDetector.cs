using Lyntai.Embeddings;
using Lyntai.Memory;

namespace Daoris.Knowledge;

/// <summary>How a candidate was found, which is also how much confidence it carries.</summary>
public enum ConvergenceMethod
{
    /// <summary>Byte-identical after normalisation. A copy, not a coincidence.</summary>
    Identical,

    /// <summary>Substantially the same words. A restatement — usually a copy that has since drifted.</summary>
    Restatement,

    /// <summary>The same meaning in different words. Only an embedding model finds these.</summary>
    Convergent,
}

/// <summary>A set of entries from different repositories that appear to state the same lesson.</summary>
/// <param name="Entries">The entries, the strongest-connected first.</param>
/// <param name="Similarity">The best pairwise similarity in the group, 0 to 1.</param>
/// <param name="Method">How it was found — see <see cref="ConvergenceMethod"/>.</param>
public sealed record ConvergenceCandidate(
    IReadOnlyList<KnowledgeEntry> Entries, double Similarity, ConvergenceMethod Method)
{
    /// <summary>The repositories involved, which is what makes this worth a person's attention.</summary>
    public IReadOnlyList<string> Repositories =>
        Entries.Select(e => e.Repository).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
}

/// <summary>How hard to look.</summary>
/// <param name="MinimumSimilarity">
/// How similar a pair must be, 0 to 1. Deliberately a parameter with no clever default: the right
/// value depends on the comparison in use, and a threshold copied from a different one is a guess
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
/// <para><b>It never requires a model.</b> Without one it compares text and finds identical copies and
/// restatements, which on a real corpus is most of what is there. With one it additionally finds
/// <em>convergence</em> — the same conclusion in different words — which text comparison provably
/// cannot see (<c>docs/DECISIONS.md</c> D17). The model raises the ceiling; it is not the floor.</para>
///
/// <para>That distinction is the point. A feature that returns nothing without an optional dependency
/// has made the dependency mandatory in everything but name, and the useful two-thirds it could have
/// delivered are lost to an all-or-nothing check.</para>
///
/// <para>It proposes; a person disposes. A candidate is a prompt to look, not a merge — doctrine that
/// appeared without anyone choosing it is the failure this project exists to prevent (D21).</para>
///
/// <para>Two exclusions do most of the work. <b>Same-repository pairs are not convergence</b> — two
/// related documents in one repository are a repository being coherent, which is not news. And
/// <b>canonical entries are excluded</b>: they are identical everywhere by construction, so they would
/// match themselves across every adopter and mean nothing.</para>
/// </remarks>
public sealed class ConvergenceDetector(
    IKnowledgeStore store, IEmbedder? embedder = null, IVectorStore? vectors = null)
{
    /// <summary>Whether the semantic pass is available. False still finds copies and restatements.</summary>
    public bool SemanticAvailable => embedder is not null && vectors is not null;

    public async Task<IReadOnlyList<ConvergenceCandidate>> FindAsync(
        ConvergenceOptions? options = null, CancellationToken ct = default)
    {
        options ??= new ConvergenceOptions();

        var considered = (await store.AllAsync(ct).ConfigureAwait(false))
            .Where(e => e.Provenance == Provenance.Local)
            .Where(e => options.Kinds is null || options.Kinds.Contains(e.Kind))
            .ToList();

        if (considered.Count < 2) return [];

        var claimed = new HashSet<string>(StringComparer.Ordinal);
        var candidates = new List<ConvergenceCandidate>();

        // Cheapest and most certain first, so a copy is never reported as a weaker finding — and so
        // the expensive pass has less left to do.
        candidates.AddRange(FindIdentical(considered, claimed));
        candidates.AddRange(FindRestatements(considered, claimed, options.MinimumSimilarity));

        if (SemanticAvailable)
        {
            candidates.AddRange(
                await FindConvergentAsync(considered, claimed, options, ct).ConfigureAwait(false));
        }

        return candidates
            .OrderByDescending(c => c.Method)      // Convergent first: the finding nobody could make by eye
            .ThenByDescending(c => c.Similarity)
            .Take(options.MaxCandidates)
            .ToList();
    }

    /// <summary>Byte-identical bodies. No model, no threshold, no doubt.</summary>
    private static IEnumerable<ConvergenceCandidate> FindIdentical(
        IReadOnlyList<KnowledgeEntry> entries, HashSet<string> claimed)
    {
        var groups = entries
            .GroupBy(e => Normalize(e.Body), StringComparer.Ordinal)
            .Where(g => g.Select(e => e.Repository).Distinct(StringComparer.Ordinal).Count() > 1);

        foreach (var group in groups)
        {
            var members = group.ToList();
            foreach (var entry in members) claimed.Add(entry.Id);
            yield return new ConvergenceCandidate(members, 1.0, ConvergenceMethod.Identical);
        }
    }

    /// <summary>
    /// Substantially the same words — a copy that has since drifted.
    /// </summary>
    /// <remarks>
    /// Containment over token sets rather than Jaccard, so a short document restating a long one still
    /// scores: the same choice the drift detector made, for the same reason.
    /// </remarks>
    private static IEnumerable<ConvergenceCandidate> FindRestatements(
        IReadOnlyList<KnowledgeEntry> entries, HashSet<string> claimed, double threshold)
    {
        var tokens = entries.ToDictionary(
            e => e.Id,
            e => new HashSet<string>(Text.Tokenize($"{e.Title} {e.Body}"), StringComparer.Ordinal),
            StringComparer.Ordinal);

        var found = new List<ConvergenceCandidate>();
        foreach (var seed in entries)
        {
            if (claimed.Contains(seed.Id)) continue;

            var group = new List<KnowledgeEntry> { seed };
            var best = 0.0;
            foreach (var other in entries)
            {
                if (other.Id == seed.Id || claimed.Contains(other.Id)) continue;
                if (string.Equals(other.Repository, seed.Repository, StringComparison.Ordinal)) continue;

                var score = Containment(tokens[seed.Id], tokens[other.Id]);
                if (score < threshold) continue;

                group.Add(other);
                best = Math.Max(best, score);
            }

            if (group.Count < 2) continue;
            foreach (var entry in group) claimed.Add(entry.Id);
            found.Add(new ConvergenceCandidate(group, best, ConvergenceMethod.Restatement));
        }

        return found;
    }

    /// <summary>The same meaning in different words — the pass only a model can make.</summary>
    private async Task<IReadOnlyList<ConvergenceCandidate>> FindConvergentAsync(
        IReadOnlyList<KnowledgeEntry> entries, HashSet<string> claimed,
        ConvergenceOptions options, CancellationToken ct)
    {
        var remaining = entries.Where(e => !claimed.Contains(e.Id)).ToList();
        if (remaining.Count < 2) return [];

        var byId = remaining.ToDictionary(e => e.Id, StringComparer.Ordinal);

        // Embedded in batches, not one call per entry. The naive version made four hundred sequential
        // round trips on a real corpus — slow enough to time out rather than merely be inefficient.
        var seedVectors = await EmbedAllAsync(remaining, ct).ConfigureAwait(false);

        var found = new List<ConvergenceCandidate>();
        foreach (var seed in remaining)
        {
            ct.ThrowIfCancellationRequested();
            if (claimed.Contains(seed.Id)) continue;

            var matches = await vectors!
                .SearchAsync(SemanticKnowledgeSearch.Collection, seedVectors[seed.Id], 12, ct)
                .ConfigureAwait(false);

            var group = new List<KnowledgeEntry> { seed };
            var best = 0.0;
            foreach (var match in matches)
            {
                if (match.Score < options.MinimumSimilarity) continue;
                if (!byId.TryGetValue(match.Payload, out var other)) continue;
                if (other.Id == seed.Id || claimed.Contains(other.Id)) continue;
                if (string.Equals(other.Repository, seed.Repository, StringComparison.Ordinal)) continue;

                group.Add(other);
                best = Math.Max(best, match.Score);
            }

            if (group.Count < 2) continue;
            foreach (var entry in group) claimed.Add(entry.Id);
            found.Add(new ConvergenceCandidate(group, best, ConvergenceMethod.Convergent));
        }

        return found;
    }

    /// <summary>
    /// Vectors already computed, keyed by what was embedded rather than by which entry it was.
    /// </summary>
    /// <remarks>
    /// The vector store can be searched but not read by key, so a seed vector cannot be fetched back
    /// out of it — hence a memo here. Keying on the embedded TEXT rather than the entry id is what
    /// makes it correct without an invalidation protocol: edit a document and the key changes, so the
    /// stale vector is simply never asked for. Keying on the id would have needed something to notice a
    /// refresh, and that something is what would eventually be wrong.
    ///
    /// It matters because the interesting use is a person moving a threshold and looking again. Without
    /// it every move re-embedded the whole corpus: measured at 31 seconds per call over 449 entries,
    /// which is long enough that the honest answer to "is it working?" is "probably".
    /// </remarks>
    private readonly Dictionary<string, float[]> _memo = new(StringComparer.Ordinal);

    private async Task<Dictionary<string, float[]>> EmbedAllAsync(
        IReadOnlyList<KnowledgeEntry> entries, CancellationToken ct, int batchSize = 32)
    {
        var byId = new Dictionary<string, float[]>(StringComparer.Ordinal);
        var pending = new List<(KnowledgeEntry Entry, string Text)>();

        foreach (var entry in entries)
        {
            var text = SemanticKnowledgeSearch.Embeddable(entry);
            if (_memo.TryGetValue(text, out var known)) byId[entry.Id] = known;
            else pending.Add((entry, text));
        }

        for (var offset = 0; offset < pending.Count; offset += batchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = pending.Skip(offset).Take(batchSize).ToList();
            var embedded = await embedder!
                .EmbedAsync(batch.Select(b => b.Text).ToList(), ct)
                .ConfigureAwait(false);

            for (var i = 0; i < batch.Count; i++)
            {
                byId[batch[i].Entry.Id] = embedded[i];
                _memo[batch[i].Text] = embedded[i];
            }
        }

        return byId;
    }

    /// <summary>Shared tokens over the smaller set, so a short document restating a long one scores.</summary>
    private static double Containment(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        return (double)a.Count(b.Contains) / Math.Min(a.Count, b.Count);
    }

    /// <summary>Whitespace-insensitive, so re-wrapping the same text is still the same text.</summary>
    private static string Normalize(string body) =>
        string.Join(' ', body.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
