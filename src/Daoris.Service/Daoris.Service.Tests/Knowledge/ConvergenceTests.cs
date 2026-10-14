using Daoris.Knowledge;
using Lyntai.Inference;
using Lyntai.Memory;

namespace Daoris.Service.Tests;

public class ConvergenceTests
{
    /// <summary>
    /// The property that matters most: **no model required**. A feature that returned nothing without
    /// an optional dependency would have made that dependency mandatory in all but name, and thrown
    /// away the copies and restatements it could have found regardless.
    /// </summary>
    [Fact]
    public async Task Works_with_no_embedder_at_all()
    {
        var store = new InMemoryKnowledgeStore();
        await store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "shot", "Keep captures small.")]);
        await store.ReplaceRepositoryAsync("beta", [Entry("beta", "capture", "Keep captures small.")]);

        var detector = new ConvergenceDetector(store);   // no embedder, no vector store

        Assert.False(detector.SemanticAvailable);
        var found = Assert.Single(await detector.FindAsync());
        Assert.Equal(ConvergenceMethod.Identical, found.Method);
        Assert.Equal(["alpha", "beta"], found.Repositories);
    }

    [Fact]
    public async Task Identical_bodies_are_found_without_a_threshold_or_a_model()
    {
        var store = new InMemoryKnowledgeStore();
        // Re-wrapped, not retyped: whitespace must not hide a copy.
        await store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "a", "one two\nthree")]);
        await store.ReplaceRepositoryAsync("beta", [Entry("beta", "b", "one two three")]);

        var found = Assert.Single(await new ConvergenceDetector(store).FindAsync());

        Assert.Equal(ConvergenceMethod.Identical, found.Method);
        Assert.Equal(1.0, found.Similarity);
    }

    [Fact]
    public async Task A_drifted_copy_is_a_restatement_rather_than_missed()
    {
        var store = new InMemoryKnowledgeStore();
        await store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "hygiene",
            "Keep captures small because a large image is rejected by the reader.")]);
        await store.ReplaceRepositoryAsync("beta", [Entry("beta", "limits",
            "Keep captures small because a large image is rejected by the reader outright.")]);

        var found = Assert.Single(await new ConvergenceDetector(store).FindAsync(
            new ConvergenceOptions(MinimumSimilarity: 0.7)));

        Assert.Equal(ConvergenceMethod.Restatement, found.Method);
    }

    [Fact]
    public async Task An_embedder_adds_convergence_without_replacing_the_rest()
    {
        var store = new InMemoryKnowledgeStore();
        await store.ReplaceRepositoryAsync("alpha", [
            Entry("alpha", "copied", "identical text here"),
            Entry("alpha", "shell", "Reading files through the terminal prompts every time."),
        ]);
        await store.ReplaceRepositoryAsync("beta", [
            Entry("beta", "copied-too", "identical text here"),
            Entry("beta", "tools", "Dedicated readers integrate with approvals, so lookups never interrupt."),
        ]);

        var embedder = new DimensionEmbedder(["terminal", "prompts", "dedicated", "readers", "approvals"]);
        var vectors = new InMemoryVectorStore();
        await SemanticKnowledgeSearch.IndexAsync(await store.AllAsync(), embedder, vectors);

        var found = await new ConvergenceDetector(store, embedder, vectors)
            .FindAsync(new ConvergenceOptions(MinimumSimilarity: 0.5));

        Assert.Contains(found, c => c.Method == ConvergenceMethod.Identical);
        Assert.Contains(found, c => c.Method == ConvergenceMethod.Convergent);
    }

    /// <summary>
    /// 🔴 REV3: the semantic tier failing threw out of the whole call — the copies and restatements
    /// already found were thrown away with it, and the landing view answered 500. "An embedding
    /// exception means the semantic tier did not answer, and the lexical half carries on" (D24).
    /// </summary>
    [Fact]
    public async Task An_embedder_that_fails_costs_the_semantic_half_and_nothing_else()
    {
        // Two the text passes cannot pair, so the semantic pass has something to embed and is reached.
        var store = new InMemoryKnowledgeStore();
        await store.ReplaceRepositoryAsync("alpha", [
            Entry("alpha", "copied", "identical text here"),
            Entry("alpha", "shell", "Reading files through the terminal prompts every time."),
        ]);
        await store.ReplaceRepositoryAsync("beta", [
            Entry("beta", "copied-too", "identical text here"),
            Entry("beta", "tools", "Dedicated readers integrate with approvals, so lookups never interrupt."),
        ]);

        var found = await new ConvergenceDetector(store, new FailingEmbedder(), new InMemoryVectorStore())
            .FindAsync(new ConvergenceOptions(MinimumSimilarity: 0.5));

        Assert.Equal(ConvergenceMethod.Identical, Assert.Single(found).Method);
    }

    /// <summary>An endpoint started without embeddings: the verdict beside empty vectors, as the real one answers.</summary>
    private sealed class FailingEmbedder : IVectorProvider
    {
        public string Id => "test-failing";

        public ProviderCapabilities Capabilities { get; } = new()
        {
            Accepts = [ProviderKinds.Text],
            Produces = [ProviderKinds.Vector],
            Operations = [ProviderOperation.Complete],
        };

        public Task<VectorResponse> CallAsync(VectorRequest request, CancellationToken ct = default) =>
            Task.FromResult(VectorResponse.Failure(ProviderVerdict.Failed, "This server does not support embeddings."));
    }

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

    /// <summary>
    /// 🔴 <b>Found on the deployed application, where the index holds 965 entries.</b> The lead view's
    /// first group was a 990-character rules TEMPLATE, a dev-conventions rule and a 326 KB pitfalls
    /// document, at similarity 1.000 — because containment over token SETS, normalised by the smaller
    /// set, scores any short document of common words as fully contained in any long one. Measured
    /// across all 48 groups: 13 were this shape (vocabulary ratio ≤ 0.09, every one a tiny record
    /// against a tome), 35 were genuine (ratio ≥ 0.46, every one the same file in two repositories),
    /// and nothing sat between. A restatement is between documents of comparable size.
    /// </summary>
    [Fact]
    public async Task A_short_document_of_common_words_is_not_a_restatement_of_a_long_one()
    {
        var template = "Rule title imperative not historical. One sentence summary of what is "
            + "enforced. Why the reason this rule exists, a past incident, constraint or preference. "
            + "How to apply it, so future sessions judge edge cases instead of following blindly.";
        // Every word of the template, buried in a document ten times its vocabulary.
        var tome = template + " " + string.Join(' ', Enumerable.Range(0, 400).Select(i => $"distinct{i}"));

        var store = new InMemoryKnowledgeStore();
        await store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "TEMPLATE", template)]);
        await store.ReplaceRepositoryAsync("beta", [Entry("beta", "pitfalls", tome)]);

        Assert.Empty(await new ConvergenceDetector(store).FindAsync(
            new ConvergenceOptions(MinimumSimilarity: 0.75)));
    }

    /// <summary>The guard must not take the case it was written beside: a copy that GREW is still a copy.</summary>
    [Fact]
    public async Task A_copy_that_grew_by_a_paragraph_is_still_a_restatement()
    {
        var original = "Keep captures small because a large image is rejected by the reader.";
        var grown = original + " Measured on the second capture tool as well, where the limit is lower "
            + "and the rejection is silent rather than reported.";

        var store = new InMemoryKnowledgeStore();
        await store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "hygiene", original)]);
        await store.ReplaceRepositoryAsync("beta", [Entry("beta", "limits", grown)]);

        var found = Assert.Single(await new ConvergenceDetector(store).FindAsync(
            new ConvergenceOptions(MinimumSimilarity: 0.7)));
        Assert.Equal(ConvergenceMethod.Restatement, found.Method);
    }

    /// <summary>
    /// A chain: alpha is a restatement of beta and beta of gamma, but alpha is not one of gamma. The
    /// first seed claims its group and the chain's far end stays out. This pins the grouping the
    /// lexical pass makes, so comparing each pair once instead of twice (POLISH3) changes nothing.
    /// </summary>
    [Fact]
    public async Task A_chain_of_restatements_groups_from_the_first_seed_and_stops_there()
    {
        // Two-letter titles tokenise to nothing, so each vocabulary is exactly its body's ten words.
        // alpha·beta share 8 of 10, beta·gamma 8 of 10, alpha·gamma 6 of 10.
        var store = new InMemoryKnowledgeStore();
        await store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "aa",
            "amber basil cedar delta ember fjord grove heron iris juniper")]);
        await store.ReplaceRepositoryAsync("beta", [Entry("beta", "bb",
            "amber basil cedar delta ember fjord grove heron kelp lotus")]);
        await store.ReplaceRepositoryAsync("gamma", [Entry("gamma", "cc",
            "cedar delta ember fjord grove heron kelp lotus maple nectar")]);

        var found = Assert.Single(await new ConvergenceDetector(store).FindAsync(
            new ConvergenceOptions(MinimumSimilarity: 0.75)));
        Assert.Equal(["alpha", "beta"], found.Repositories);
        Assert.Equal(0.8, found.Similarity, 3);
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
