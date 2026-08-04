using Daoris.Knowledge;
using Lyntai.Memory;

namespace Daoris.Service.Tests;

public class ConvergenceTests
{
    private static KnowledgeEntry Entry(
        string repository, string title, string body, Provenance provenance = Provenance.Local) =>
        new(repository, EntryKind.Knowledge, provenance, title, body, $".claude/knowledge/{title}.md");

    private static async Task<(ConvergenceDetector Detector, InMemoryKnowledgeStore Store)> BuildAsync(
        DimensionEmbedder embedder, params KnowledgeEntry[] entries)
    {
        var store = new InMemoryKnowledgeStore();
        foreach (var group in entries.GroupBy(e => e.Repository))
        {
            await store.ReplaceRepositoryAsync(group.Key, group.ToList());
        }

        var vectors = new InMemoryVectorStore();
        await SemanticKnowledgeSearch.IndexAsync(await store.AllAsync(), embedder, vectors);
        return (new ConvergenceDetector(store, embedder, vectors), store);
    }

    /// <summary>
    /// The survey this automates. Canonizing this project's own doctrine meant reading twelve
    /// repositories by hand to notice which documents said the same thing in different words.
    /// </summary>
    [Fact]
    public async Task Finds_the_same_lesson_stated_differently_in_two_repositories()
    {
        var embedder = new DimensionEmbedder(["capture", "screenshot", "image", "size", "large"]);
        var (detector, _) = await BuildAsync(embedder,
            Entry("alpha", "screenshot-hygiene", "Keep captures small: a large image is rejected."),
            Entry("beta", "capture-limits", "Screenshot capture must stay under the size limit."),
            Entry("gamma", "unrelated", "Queue ordering and crossfade windows."));

        var candidates = await detector.FindAsync(new ConvergenceOptions(MinimumSimilarity: 0.5));

        var found = Assert.Single(candidates);
        Assert.Equal(["alpha", "beta"], found.Repositories);
        Assert.DoesNotContain(found.Entries, e => e.Repository == "gamma");
    }

    /// <summary>
    /// Two related documents inside one repository are a repository being coherent, which is not
    /// news — and would drown the results that are.
    /// </summary>
    [Fact]
    public async Task Two_similar_documents_in_ONE_repository_are_not_convergence()
    {
        var embedder = new DimensionEmbedder(["capture", "screenshot", "size"]);
        var (detector, _) = await BuildAsync(embedder,
            Entry("alpha", "screenshot-hygiene", "Keep screenshot capture size small."),
            Entry("alpha", "capture-notes", "Screenshot capture size matters."));

        var candidates = await detector.FindAsync(new ConvergenceOptions(MinimumSimilarity: 0.5));

        Assert.Empty(candidates);
    }

    /// <summary>
    /// Canonical entries are byte-identical in every adopter, so they would match themselves across
    /// the family, dominate every result, and mean nothing. What is worth finding is two repositories
    /// arriving somewhere independently.
    /// </summary>
    [Fact]
    public async Task Canonical_entries_are_excluded_because_they_match_by_construction()
    {
        var embedder = new DimensionEmbedder(["sensitive", "paths", "tracked"]);
        var (detector, _) = await BuildAsync(embedder,
            Entry("alpha", "sensitive-info", "No machine paths in tracked files.", Provenance.Canonical),
            Entry("beta", "sensitive-info", "No machine paths in tracked files.", Provenance.Canonical));

        var candidates = await detector.FindAsync(new ConvergenceOptions(MinimumSimilarity: 0.5));

        Assert.Empty(candidates);
    }

    [Fact]
    public async Task A_pair_below_the_threshold_is_not_reported()
    {
        var embedder = new DimensionEmbedder(["capture"], ["queue"]);
        var (detector, _) = await BuildAsync(embedder,
            Entry("alpha", "captures", "capture"),
            Entry("beta", "queues", "queue"));

        Assert.Empty(await detector.FindAsync(new ConvergenceOptions(MinimumSimilarity: 0.9)));
    }

    [Fact]
    public async Task An_entry_appears_in_at_most_one_candidate()
    {
        var embedder = new DimensionEmbedder(["shared"]);
        var (detector, _) = await BuildAsync(embedder,
            Entry("alpha", "one", "shared"),
            Entry("beta", "two", "shared"),
            Entry("gamma", "three", "shared"));

        var candidates = await detector.FindAsync(new ConvergenceOptions(MinimumSimilarity: 0.5));

        var ids = candidates.SelectMany(c => c.Entries).Select(e => e.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task Nothing_to_compare_yields_nothing_rather_than_throwing()
    {
        var embedder = new DimensionEmbedder(["anything"]);
        var (detector, _) = await BuildAsync(embedder, Entry("alpha", "alone", "anything"));

        Assert.Empty(await detector.FindAsync());
    }
}
