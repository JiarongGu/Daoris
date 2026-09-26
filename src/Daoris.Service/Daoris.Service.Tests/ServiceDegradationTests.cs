using Daoris.Knowledge;
using Lyntai.Inference;
using Lyntai.Memory;

namespace Daoris.Service.Tests;

/// <summary>
/// Semantic recall is the optional half. When it is unavailable the lexical index must still be
/// complete and usable, and the reason must reach the caller — found on the first real run, against a
/// local embedding server that had been started without embeddings enabled.
/// </summary>
public class ServiceDegradationTests
{
    private sealed class BrokenEmbedder : IVectorProvider
    {
        public string Id => "test-broken";

        public ProviderCapabilities Capabilities { get; } = new()
        {
            Accepts = [ProviderKinds.Text],
            Produces = [ProviderKinds.Vector],
            Operations = [ProviderOperation.Complete],
        };

        // The sibling's 3.x contract: failure is a VERDICT beside empty vectors, never a throw —
        // and this test holds that the service still degrades to lexical on exactly that shape.
        public Task<VectorResponse> CallAsync(VectorRequest request, CancellationToken ct = default) =>
            Task.FromResult(VectorResponse.Failure(
                Lyntai.Inference.ProviderVerdict.Failed, "This server does not support embeddings."));
    }

    private sealed class OneEntrySource : IKnowledgeSource
    {
        public string Name => "test";
        public Task<IReadOnlyList<KnowledgeEntry>> ReadAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeEntry>>([
                new("alpha", EntryKind.Decision, Provenance.Local, "D1", "drift is measured against the lock",
                    "docs/DECISIONS.md", "D1"),
            ]);
    }

    private static KnowledgeService Build(IVectorProvider? embedder, out IKnowledgeStore store)
    {
        var memory = new InMemoryKnowledgeStore();
        store = memory;
        return new KnowledgeService(
            memory,
            new LexicalKnowledgeSearch(memory),
            new OneEntrySource(),
            DisclosurePolicy.LocalOnly,
            embedder,
            embedder is null ? null : new InMemoryVectorStore());
    }

    /// <summary>
    /// SEM1: a restart over an index already on disk. The rows persist and the vectors do not, and the
    /// start skipped the refresh because the rows were there — so the semantic half found nothing
    /// until somebody pressed *rebuild*, while every door still said the tier was semantic.
    /// </summary>
    [Fact]
    public async Task After_a_restart_over_an_index_on_disk_the_semantic_half_still_answers()
    {
        var entry = new KnowledgeEntry(
            "alpha", EntryKind.Decision, Provenance.Local, "D1", "drift is measured against the lock",
            "docs/DECISIONS.md", "D1");
        var store = new InMemoryKnowledgeStore();
        await store.ReplaceRepositoryAsync("alpha", [entry]); // what the last run left on disk
        var embedder = new DimensionEmbedder(["drift", "divergence"]);
        var vectors = new InMemoryVectorStore(); // what a new process starts with: nothing
        var service = new KnowledgeService(
            store,
            new HybridKnowledgeSearch(new LexicalKnowledgeSearch(store), new SemanticKnowledgeSearch(store, embedder, vectors)),
            new EmptyKnowledgeSource(),
            DisclosurePolicy.LocalOnly,
            embedder,
            vectors);

        // "divergence" shares no word with the entry; only its vector can find it.
        var found = await service.SearchAsync(new KnowledgeQuery("divergence"));

        Assert.Contains(found, hit => hit.Entry.Id == entry.Id);
    }

    [Fact]
    public async Task A_failing_embedder_does_not_fail_the_refresh()
    {
        var service = Build(new BrokenEmbedder(), out var store);

        var report = await service.RefreshAsync();

        Assert.Equal(1, report.Entries);
        Assert.Single(await store.AllAsync());
    }

    /// <summary>
    /// The reason has to reach the caller. On the first real run the tool reported only "an error
    /// occurred", for a problem whose fix was a single server flag.
    /// </summary>
    [Fact]
    public async Task The_reason_reaches_the_caller()
    {
        var service = Build(new BrokenEmbedder(), out _);

        var report = await service.RefreshAsync();

        Assert.NotNull(report.SemanticError);
        Assert.Contains("does not support embeddings", report.SemanticError);
    }

    [Fact]
    public async Task Lexical_search_still_answers_when_semantic_is_broken()
    {
        var service = Build(new BrokenEmbedder(), out _);
        await service.RefreshAsync();

        var hits = await service.SearchAsync(new KnowledgeQuery("drift lock"));

        Assert.NotEmpty(hits);
    }

    [Fact]
    public async Task With_no_embedder_configured_there_is_nothing_to_report()
    {
        var service = Build(null, out _);

        var report = await service.RefreshAsync();

        Assert.Null(report.SemanticError);
        Assert.False(service.SemanticEnabled);
    }
}
