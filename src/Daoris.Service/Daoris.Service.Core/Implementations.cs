using System.Collections.Concurrent;

namespace Daoris.Knowledge;

/// <summary>Reads repositories from a directory on this machine — the local-first source.</summary>
public sealed class FileSystemKnowledgeSource(IReadOnlyList<string> repositoryRoots) : IKnowledgeSource
{
    private readonly RepositoryScanner _scanner = new();

    /// <summary>Every immediate subdirectory of a folder — the usual "all my repositories" case.</summary>
    public static FileSystemKnowledgeSource UnderFolder(string folder) =>
        new(Directory.Exists(folder)
            ? Directory.EnumerateDirectories(folder).Order(StringComparer.Ordinal).ToList()
            : []);

    public string Name { get; init; } = "filesystem";

    public Task<IReadOnlyList<KnowledgeEntry>> ReadAsync(CancellationToken ct = default)
    {
        var entries = new List<KnowledgeEntry>();
        foreach (var root in repositoryRoots)
        {
            ct.ThrowIfCancellationRequested();
            entries.AddRange(_scanner.Scan(root));
        }

        return Task.FromResult<IReadOnlyList<KnowledgeEntry>>(entries);
    }
}

/// <summary>
/// Keeps entries in memory. The store the tests use, and enough to run locally before an embedded
/// store exists — a knowledge index that must be persisted before it can be tried is one that gets
/// tried late.
/// </summary>
public sealed class InMemoryKnowledgeStore : IKnowledgeStore
{
    private readonly ConcurrentDictionary<string, List<KnowledgeEntry>> _byRepository = new(StringComparer.Ordinal);

    public Task ReplaceRepositoryAsync(string repository, IReadOnlyList<KnowledgeEntry> entries, CancellationToken ct = default)
    {
        _byRepository[repository] = [.. entries];
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<KnowledgeEntry>> AllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<KnowledgeEntry>>(_byRepository.Values.SelectMany(e => e).ToList());

    public Task<KnowledgeEntry?> FindAsync(string id, CancellationToken ct = default) =>
        Task.FromResult(_byRepository.Values.SelectMany(e => e)
            .FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.Ordinal)));
}

/// <summary>Permits or withholds entries by their provenance and origin.</summary>
public sealed class DisclosurePolicy(bool sharing, IReadOnlySet<string>? shareableRepositories = null) : IDisclosurePolicy
{
    /// <summary>Nothing is leaving, so nothing is withheld. The default, and the local mode.</summary>
    public static IDisclosurePolicy LocalOnly { get; } = new DisclosurePolicy(sharing: false);

    /// <summary>
    /// Shared mode. A repository shares only if it is named — <b>silence means keep it local</b>,
    /// because the cost is asymmetric: over-sharing is a disclosure and under-sharing is an
    /// inconvenience.
    /// </summary>
    public static IDisclosurePolicy Sharing(IReadOnlySet<string> repositories) =>
        new DisclosurePolicy(sharing: true, repositories);

    public bool MayLeaveMachine(KnowledgeEntry entry)
    {
        if (!sharing) return true;

        // Canonical content is public by construction — it is the canon, installed identically
        // everywhere — so it travels regardless of which repository it was read from.
        if (entry.Provenance == Provenance.Canonical) return true;

        return shareableRepositories?.Contains(entry.Repository) == true;
    }
}

/// <summary>
/// Scores entries by term overlap.
/// </summary>
/// <remarks>
/// Deliberately simple, and deliberately first: it needs no model, runs offline, and answers most of
/// what is actually asked. Its weakness is known and measured elsewhere in this project — word
/// overlap finds <em>restatement</em> and cannot find <em>convergence</em>, where two repositories
/// reach the same conclusion in different vocabulary. Closing that gap is what semantic search is
/// for, and why <see cref="IKnowledgeSearch"/> returns scores that two searches can be merged on.
/// </remarks>
public sealed class LexicalKnowledgeSearch(IKnowledgeStore store) : IKnowledgeSearch
{
    private static readonly char[] Separators =
        " \t\r\n.,;:!?()[]{}<>\"'`|/\\*_#=+~".ToCharArray();

    public async Task<IReadOnlyList<KnowledgeHit>> SearchAsync(KnowledgeQuery query, CancellationToken ct = default)
    {
        var entries = await store.AllAsync(ct).ConfigureAwait(false);
        var terms = Tokenize(query.Text);

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
            var score = Score(entry, terms, out var excerpt);
            if (score > 0) hits.Add(new KnowledgeHit(entry, score, excerpt));
        }

        return hits
            .OrderByDescending(h => h.Score)
            .ThenBy(h => h.Entry.Id, StringComparer.Ordinal)   // stable for equal scores
            .Take(query.Limit)
            .ToList();
    }

    private static double Score(KnowledgeEntry entry, IReadOnlyCollection<string> terms, out string? excerpt)
    {
        excerpt = null;
        var title = Tokenize(entry.Title);
        var body = Tokenize(entry.Body);

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
        score *= 1.0 + (double)matched / terms.Count;

        excerpt = Excerpt(entry.Body, terms);
        return score;
    }

    private static string? Excerpt(string body, IReadOnlyCollection<string> terms, int window = 180)
    {
        var index = -1;
        foreach (var term in terms)
        {
            index = body.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (index >= 0) break;
        }

        if (index < 0) return body.Length <= window ? body : body[..window] + "…";

        var start = Math.Max(0, index - window / 3);
        var length = Math.Min(window, body.Length - start);
        var text = body.Substring(start, length).Replace('\n', ' ').Trim();
        return (start > 0 ? "…" : "") + text + (start + length < body.Length ? "…" : "");
    }

    private static List<string> Tokenize(string text) =>
        text.ToLowerInvariant()
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length > 2)
            .ToList();
}

/// <summary>
/// Reads a source into a store, applying the disclosure policy on the way in.
/// </summary>
/// <remarks>
/// The policy is applied at <b>ingest</b>, not at query time. Withholding at query time means the
/// material is already in the store and one forgotten filter discloses it; withholding at ingest
/// means it was never there to leak.
/// </remarks>
public sealed class KnowledgeIndex(IKnowledgeStore store, IDisclosurePolicy? disclosure = null)
{
    private readonly IDisclosurePolicy _disclosure = disclosure ?? DisclosurePolicy.LocalOnly;

    public async Task<IndexReport> RefreshAsync(IKnowledgeSource source, CancellationToken ct = default)
    {
        var read = await source.ReadAsync(ct).ConfigureAwait(false);
        var permitted = read.Where(_disclosure.MayLeaveMachine).ToList();

        var byRepository = permitted.GroupBy(e => e.Repository, StringComparer.Ordinal).ToList();
        foreach (var group in byRepository)
        {
            await store.ReplaceRepositoryAsync(group.Key, group.ToList(), ct).ConfigureAwait(false);
        }

        return new IndexReport(source.Name, byRepository.Count, permitted.Count, read.Count - permitted.Count);
    }
}
