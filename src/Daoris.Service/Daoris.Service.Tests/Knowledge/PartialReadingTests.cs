using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>A reading of some roots alone replaces each repository it read, one that now yields nothing included.</summary>
public sealed class PartialReadingTests
{
    private sealed class RootSource : IRootReadingSource
    {
        public string Name => "test";
        public Dictionary<string, List<KnowledgeEntry>> ByRoot { get; } = [];
        public Task<IReadOnlyList<KnowledgeEntry>> ReadAsync(CancellationToken ct = default) =>
            ReadRootsAsync([.. ByRoot.Keys], ct);
        public Task<IReadOnlyList<KnowledgeEntry>> ReadRootsAsync(IReadOnlyList<string> roots, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeEntry>>([.. roots.SelectMany(root => ByRoot.GetValueOrDefault(root) ?? [])]);
    }

    private static KnowledgeEntry Entry(string repository, string title) => new(
        repository, EntryKind.Rule, Provenance.Local, title, "body", $".claude/rules/{title}.md", null);

    [Fact]
    public async Task A_moved_root_that_now_yields_nothing_is_replaced_with_nothing_and_the_other_stands()
    {
        var store = new InMemoryKnowledgeStore();
        var index = new KnowledgeIndex(store);
        var source = new RootSource();
        source.ByRoot["a"] = [Entry("Alpha", "one")];
        source.ByRoot["b"] = [Entry("Beta", "two")];
        await index.RefreshAsync(source);

        source.ByRoot["a"] = []; // its docs were removed
        await index.RefreshReadingAsync(
            source, null, null, ["a"], generations: null, readOf: _ => (null, null), readRepositories: ["Alpha"], CancellationToken.None);

        Assert.Equal(["Beta"], (await store.AllAsync()).Select(e => e.Repository).Distinct().ToList());
    }
}
