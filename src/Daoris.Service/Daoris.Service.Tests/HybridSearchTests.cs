using Daoris.Knowledge;
using Lyntai.Embeddings;
using Lyntai.Memory;

namespace Daoris.Service.Tests;

/// <summary>
/// A deterministic stand-in for a real embedding model: each configured word owns one dimension, so
/// two texts are "similar" exactly when they share vocabulary from a chosen list.
/// </summary>
/// <remarks>
/// The point is not to imitate a model. It is to make the <em>plumbing</em> testable without a network
/// call or an API key — the mapping is chosen per test, so a test can place two entries close together
/// that share no words at all, which is precisely the convergence case lexical search cannot see.
/// </remarks>
internal sealed class DimensionEmbedder(params string[][] synonymGroups) : IEmbedder
{
    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var vectors = texts.Select(text =>
        {
            var vector = new float[synonymGroups.Length];
            for (var i = 0; i < synonymGroups.Length; i++)
            {
                foreach (var word in synonymGroups[i])
                {
                    if (text.Contains(word, StringComparison.OrdinalIgnoreCase)) vector[i] += 1f;
                }
            }

            return vector;
        }).ToList();

        return Task.FromResult<IReadOnlyList<float[]>>(vectors);
    }
}

public class HybridSearchTests
{
    private static KnowledgeEntry Entry(string repository, string title, string body) =>
        new(repository, EntryKind.Decision, Provenance.Local, title, body, $"docs/{title}.md", title);

    private static async Task<InMemoryKnowledgeStore> StoreOf(params KnowledgeEntry[] entries)
    {
        var store = new InMemoryKnowledgeStore();
        foreach (var group in entries.GroupBy(e => e.Repository))
        {
            await store.ReplaceRepositoryAsync(group.Key, group.ToList());
        }

        return store;
    }

    /// <summary>
    /// The gap semantic search exists to close: two repositories reaching the same conclusion in
    /// entirely different vocabulary. Lexical search scores that like an unrelated document — the
    /// same measured limit the drift detector has — so the fused result must surface it.
    /// </summary>
    [Fact]
    public async Task Finds_convergence_that_shares_no_vocabulary()
    {
        var shellFlavoured = Entry("alpha", "Avoid shelling out for reads",
            "Reading files through the terminal prompts every time and slows the loop.");
        var toolFlavoured = Entry("beta", "Purpose-built inspection",
            "Dedicated readers integrate with approvals, so a harmless lookup never interrupts.");

        var store = await StoreOf(shellFlavoured, toolFlavoured);
        var embedder = new DimensionEmbedder(
            ["shell", "terminal", "dedicated", "readers", "inspection", "prompts", "approvals"]);
        var vectors = new InMemoryVectorStore();
        await SemanticKnowledgeSearch.IndexAsync(await store.AllAsync(), embedder, vectors);

        var semantic = new SemanticKnowledgeSearch(store, embedder, vectors);
        var lexical = new LexicalKnowledgeSearch(store);

        // Lexically, this query cannot reach the second entry: no shared words.
        var lexicalHits = await lexical.SearchAsync(new KnowledgeQuery("terminal prompts"));
        Assert.DoesNotContain(lexicalHits, h => h.Entry.Repository == "beta");

        var fused = await new HybridKnowledgeSearch(lexical, semantic)
            .SearchAsync(new KnowledgeQuery("terminal prompts"));

        Assert.Contains(fused, h => h.Entry.Repository == "beta");
    }

    [Fact]
    public async Task Agreement_between_both_searches_outranks_confidence_in_one()
    {
        var agreed = Entry("alpha", "Cache invalidation", "The manifest version drives the cache key.");
        var lexicalOnly = Entry("beta", "Cache cache cache", "cache cache cache cache cache");

        var store = await StoreOf(agreed, lexicalOnly);
        var embedder = new DimensionEmbedder(["manifest", "version", "invalidation"]);
        var vectors = new InMemoryVectorStore();
        await SemanticKnowledgeSearch.IndexAsync(await store.AllAsync(), embedder, vectors);

        var fused = await new HybridKnowledgeSearch(
                new LexicalKnowledgeSearch(store),
                new SemanticKnowledgeSearch(store, embedder, vectors))
            .SearchAsync(new KnowledgeQuery("cache manifest version"));

        Assert.Equal("Cache invalidation", fused[0].Entry.Title);
    }

    [Fact]
    public async Task Without_a_semantic_search_it_is_simply_the_lexical_one()
    {
        var store = await StoreOf(Entry("alpha", "Only", "the lexical result"));

        var fused = await new HybridKnowledgeSearch(new LexicalKnowledgeSearch(store))
            .SearchAsync(new KnowledgeQuery("lexical"));

        Assert.Single(fused);
    }

    /// <summary>
    /// An index that answers nothing because an embedding endpoint is down is worse than one that
    /// answers with half of what it knows.
    /// </summary>
    [Fact]
    public async Task A_failing_half_degrades_the_answer_rather_than_removing_it()
    {
        var store = await StoreOf(Entry("alpha", "Survivor", "still findable by words"));

        var fused = await new HybridKnowledgeSearch(new LexicalKnowledgeSearch(store), new ThrowingSearch())
            .SearchAsync(new KnowledgeQuery("findable"));

        Assert.Single(fused);
        Assert.Equal("Survivor", fused[0].Entry.Title);
    }

    [Fact]
    public async Task Cancellation_is_not_swallowed_as_a_failed_search()
    {
        var store = await StoreOf(Entry("alpha", "Any", "body"));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new HybridKnowledgeSearch(new CancelObservingSearch()).SearchAsync(new KnowledgeQuery("x"), cts.Token));
    }

    [Fact]
    public async Task Semantic_results_respect_the_query_filters()
    {
        var store = await StoreOf(
            Entry("alpha", "Kept", "manifest version"),
            Entry("beta", "Excluded", "manifest version"));
        var embedder = new DimensionEmbedder(["manifest", "version"]);
        var vectors = new InMemoryVectorStore();
        await SemanticKnowledgeSearch.IndexAsync(await store.AllAsync(), embedder, vectors);

        var hits = await new SemanticKnowledgeSearch(store, embedder, vectors).SearchAsync(
            new KnowledgeQuery("manifest") { Repositories = new HashSet<string> { "alpha" } });

        Assert.All(hits, h => Assert.Equal("alpha", h.Entry.Repository));
    }

    private sealed class ThrowingSearch : IKnowledgeSearch
    {
        public Task<IReadOnlyList<KnowledgeHit>> SearchAsync(KnowledgeQuery query, CancellationToken ct = default) =>
            throw new HttpRequestException("the embedding endpoint is unreachable");
    }

    private sealed class CancelObservingSearch : IKnowledgeSearch
    {
        public Task<IReadOnlyList<KnowledgeHit>> SearchAsync(KnowledgeQuery query, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IReadOnlyList<KnowledgeHit>>([]);
        }
    }
}
