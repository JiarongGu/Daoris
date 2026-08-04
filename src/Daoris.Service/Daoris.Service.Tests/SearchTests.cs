using Daoris.Knowledge;

namespace Daoris.Service.Tests;

public class SearchTests
{
    private static KnowledgeEntry Entry(
        string repository, string title, string body,
        EntryKind kind = EntryKind.Decision, Provenance provenance = Provenance.Local) =>
        new(repository, kind, provenance, title, body, $"docs/{title}.md", title);

    private static async Task<IKnowledgeSearch> SearchOver(params KnowledgeEntry[] entries)
    {
        var store = new InMemoryKnowledgeStore();
        foreach (var group in entries.GroupBy(e => e.Repository))
        {
            await store.ReplaceRepositoryAsync(group.Key, group.ToList());
        }

        return new LexicalKnowledgeSearch(store);
    }

    [Fact]
    public async Task A_title_match_outranks_a_body_mention()
    {
        var search = await SearchOver(
            Entry("alpha", "Passing mention", "We considered the migration numbering only briefly."),
            Entry("beta", "Migration numbering", "Numbers are assigned at merge, not at authoring."));

        var hits = await search.SearchAsync(new KnowledgeQuery("migration numbering"));

        Assert.Equal("Migration numbering", hits[0].Entry.Title);
    }

    [Fact]
    public async Task Covering_more_of_the_query_beats_repeating_one_term()
    {
        var search = await SearchOver(
            Entry("alpha", "Repetition", "cache cache cache cache cache cache cache"),
            Entry("beta", "Coverage", "The cache is invalidated when the manifest version changes."));

        var hits = await search.SearchAsync(new KnowledgeQuery("cache manifest version"));

        Assert.Equal("Coverage", hits[0].Entry.Title);
    }

    [Fact]
    public async Task Filters_narrow_and_absent_filters_do_not()
    {
        var search = await SearchOver(
            Entry("alpha", "Storage", "sqlite affinity", EntryKind.Decision),
            Entry("beta", "Storage", "sqlite affinity", EntryKind.Fix, Provenance.Canonical));

        Assert.Equal(2, (await search.SearchAsync(new KnowledgeQuery("sqlite"))).Count);

        var decisionsOnly = await search.SearchAsync(new KnowledgeQuery("sqlite")
        {
            Kinds = new HashSet<EntryKind> { EntryKind.Decision },
        });
        Assert.Single(decisionsOnly);
        Assert.Equal("alpha", decisionsOnly[0].Entry.Repository);

        var localOnly = await search.SearchAsync(new KnowledgeQuery("sqlite") { Provenance = Provenance.Local });
        Assert.Single(localOnly);
    }

    [Fact]
    public async Task An_empty_query_browses_rather_than_returning_nothing()
    {
        var search = await SearchOver(
            Entry("alpha", "One", "body"),
            Entry("beta", "Two", "body"));

        var hits = await search.SearchAsync(new KnowledgeQuery());

        Assert.Equal(2, hits.Count);
    }

    [Fact]
    public async Task A_hit_carries_an_excerpt_showing_why_it_matched()
    {
        var search = await SearchOver(Entry(
            "alpha", "Long entry",
            new string('x', 400) + " the provenance header must sit under the frontmatter " + new string('y', 400)));

        var hits = await search.SearchAsync(new KnowledgeQuery("frontmatter"));

        Assert.NotNull(hits[0].Excerpt);
        Assert.Contains("frontmatter", hits[0].Excerpt!);
        Assert.True(hits[0].Excerpt!.Length < 250, "an excerpt is a window, not the whole entry");
    }

    [Fact]
    public async Task Results_are_stable_for_equal_scores()
    {
        var search = await SearchOver(
            Entry("beta", "Same", "identical body text"),
            Entry("alpha", "Same", "identical body text"));

        var first = await search.SearchAsync(new KnowledgeQuery("identical"));
        var second = await search.SearchAsync(new KnowledgeQuery("identical"));

        Assert.Equal(first.Select(h => h.Entry.Id), second.Select(h => h.Entry.Id));
    }
}
