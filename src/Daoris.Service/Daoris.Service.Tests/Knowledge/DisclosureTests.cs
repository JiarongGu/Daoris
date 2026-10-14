using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The disclosure boundary, exercised as a type rather than trusted as a paragraph.
/// </summary>
public class DisclosureTests
{
    private static KnowledgeEntry Entry(string repository, Provenance provenance) =>
        new(repository, EntryKind.Decision, provenance, "D1", "body", "docs/DECISIONS.md", "D1");

    private sealed class FixedSource(string name, params KnowledgeEntry[] entries) : IKnowledgeSource
    {
        public string Name => name;
        public Task<IReadOnlyList<KnowledgeEntry>> ReadAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeEntry>>(entries);
    }

    [Fact]
    public void Local_mode_withholds_nothing_because_nothing_is_leaving()
    {
        var policy = DisclosurePolicy.LocalOnly;

        Assert.True(policy.MayLeaveMachine(Entry("private-sibling", Provenance.Local)));
        Assert.True(policy.MayLeaveMachine(Entry("public-sibling", Provenance.Canonical)));
    }

    /// <summary>
    /// Silence means keep it local. The cost is asymmetric: over-sharing is a disclosure and
    /// under-sharing is an inconvenience, so an unlisted repository must not travel.
    /// </summary>
    [Fact]
    public void Sharing_requires_a_repository_to_opt_in()
    {
        var policy = DisclosurePolicy.Sharing(new HashSet<string> { "opted-in" });

        Assert.True(policy.MayLeaveMachine(Entry("opted-in", Provenance.Local)));
        Assert.False(policy.MayLeaveMachine(Entry("never-mentioned", Provenance.Local)));
    }

    [Fact]
    public void Canonical_content_travels_regardless_because_it_is_already_public()
    {
        var policy = DisclosurePolicy.Sharing(new HashSet<string>());

        Assert.True(policy.MayLeaveMachine(Entry("any-repository", Provenance.Canonical)));
    }

    /// <summary>
    /// Applied at ingest, not at query. Withheld-at-query means the material is in the store and one
    /// forgotten filter discloses it; withheld-at-ingest means it was never there to leak.
    /// </summary>
    [Fact]
    public async Task Withheld_entries_never_enter_the_store()
    {
        var store = new InMemoryKnowledgeStore();
        var index = new KnowledgeIndex(store, DisclosurePolicy.Sharing(new HashSet<string> { "shared" }));
        var source = new FixedSource(
            "test",
            Entry("shared", Provenance.Local),
            Entry("secret", Provenance.Local));

        var report = await index.RefreshAsync(source);

        Assert.Equal(1, report.Entries);
        Assert.Equal(1, report.Withheld);

        var stored = await store.AllAsync();
        Assert.All(stored, e => Assert.Equal("shared", e.Repository));

        // And it cannot be reached by search either, because it is simply not there.
        var hits = await new LexicalKnowledgeSearch(store).SearchAsync(new KnowledgeQuery("body"));
        Assert.DoesNotContain(hits, h => h.Entry.Repository == "secret");
    }

    /// <summary>
    /// Re-reading replaces a repository wholesale, so an entry deleted upstream does not survive in
    /// the index — the failure mode a diff-based refresh has to be careful about and this avoids.
    /// </summary>
    [Fact]
    public async Task Refreshing_drops_entries_that_disappeared_upstream()
    {
        var store = new InMemoryKnowledgeStore();
        var index = new KnowledgeIndex(store);

        await index.RefreshAsync(new FixedSource("t",
            new KnowledgeEntry("r", EntryKind.Decision, Provenance.Local, "D1", "a", "docs/D.md", "D1"),
            new KnowledgeEntry("r", EntryKind.Decision, Provenance.Local, "D2", "b", "docs/D.md", "D2")));
        Assert.Equal(2, (await store.AllAsync()).Count);

        await index.RefreshAsync(new FixedSource("t",
            new KnowledgeEntry("r", EntryKind.Decision, Provenance.Local, "D1", "a", "docs/D.md", "D1")));

        var remaining = await store.AllAsync();
        Assert.Single(remaining);
        Assert.Equal("D1", remaining[0].Title);
    }
}
