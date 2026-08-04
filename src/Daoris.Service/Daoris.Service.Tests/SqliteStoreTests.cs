using Daoris.Knowledge;

namespace Daoris.Service.Tests;

public sealed class SqliteStoreTests : IAsyncLifetime
{
    private readonly string _file = Path.Combine(
        Path.GetTempPath(), $"daoris-index-{Guid.NewGuid():N}.db");

    private SqliteKnowledgeStore _store = null!;

    public async Task InitializeAsync() => _store = await SqliteKnowledgeStore.OpenAsync(_file);

    public async Task DisposeAsync()
    {
        await _store.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_file)) File.Delete(_file);
    }

    private static KnowledgeEntry Entry(
        string repository, string title, string body,
        EntryKind kind = EntryKind.Decision, Provenance provenance = Provenance.Local) =>
        new(repository, kind, provenance, title, body, $"docs/{title}.md", title);

    [Fact]
    public async Task Round_trips_an_entry_with_every_field()
    {
        var entry = Entry("alpha", "D1 — a decision", "The body, with an em dash — and CJK 道衍.");
        await _store.ReplaceRepositoryAsync("alpha", [entry]);

        var found = await _store.FindAsync(entry.Id);

        Assert.NotNull(found);
        Assert.Equal(entry, found);
    }

    [Fact]
    public async Task Replacing_a_repository_drops_what_disappeared()
    {
        await _store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "One", "a"), Entry("alpha", "Two", "b")]);
        await _store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "One", "a")]);

        var all = await _store.AllAsync();

        Assert.Single(all);
        Assert.Equal("One", all[0].Title);
    }

    [Fact]
    public async Task Replacing_one_repository_leaves_the_others_alone()
    {
        await _store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "A", "a")]);
        await _store.ReplaceRepositoryAsync("beta", [Entry("beta", "B", "b")]);

        await _store.ReplaceRepositoryAsync("alpha", []);

        var all = await _store.AllAsync();
        Assert.Single(all);
        Assert.Equal("beta", all[0].Repository);
    }

    /// <summary>The index is derived, so it survives a restart — that is the whole point of a file.</summary>
    [Fact]
    public async Task Data_survives_reopening_the_file()
    {
        await _store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "Persisted", "still here")]);
        await _store.DisposeAsync();

        await using var reopened = await SqliteKnowledgeStore.OpenAsync(_file);
        var all = await reopened.AllAsync();

        Assert.Single(all);
        Assert.Equal("Persisted", all[0].Title);

        _store = await SqliteKnowledgeStore.OpenAsync(_file); // so DisposeAsync has something to close
    }

    [Fact]
    public async Task Search_ranks_a_title_match_above_a_body_mention()
    {
        await _store.ReplaceRepositoryAsync("alpha", [
            Entry("alpha", "Passing mention", "We considered migration numbering only briefly."),
            Entry("alpha", "Migration numbering", "Numbers are assigned at merge time."),
        ]);

        var hits = await new SqliteKnowledgeSearch(_store).SearchAsync(new KnowledgeQuery("migration numbering"));

        Assert.Equal("Migration numbering", hits[0].Entry.Title);
        Assert.True(hits[0].Score > 0, "scores are reported positive, best first");
    }

    [Fact]
    public async Task Search_applies_every_filter()
    {
        await _store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "Storage", "sqlite affinity")]);
        await _store.ReplaceRepositoryAsync("beta", [
            Entry("beta", "Storage", "sqlite affinity", EntryKind.Fix, Provenance.Canonical),
        ]);

        var search = new SqliteKnowledgeSearch(_store);

        Assert.Equal(2, (await search.SearchAsync(new KnowledgeQuery("sqlite"))).Count);
        Assert.Single(await search.SearchAsync(new KnowledgeQuery("sqlite") { Provenance = Provenance.Local }));
        Assert.Single(await search.SearchAsync(new KnowledgeQuery("sqlite")
        {
            Kinds = new HashSet<EntryKind> { EntryKind.Fix },
        }));
        Assert.Single(await search.SearchAsync(new KnowledgeQuery("sqlite")
        {
            Repositories = new HashSet<string> { "alpha" },
        }));
    }

    [Fact]
    public async Task An_empty_query_browses()
    {
        await _store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "One", "a"), Entry("alpha", "Two", "b")]);

        var hits = await new SqliteKnowledgeSearch(_store).SearchAsync(new KnowledgeQuery());

        Assert.Equal(2, hits.Count);
    }

    /// <summary>
    /// FTS5 treats quotes, colons, parentheses and bare OR/NOT as query syntax, so an ordinary
    /// sentence is a syntax error unless every term is quoted. A search that throws on a normal
    /// question is worse than one that finds nothing.
    /// </summary>
    [Theory]
    [InlineData("what about D13: drift?")]
    [InlineData("\"quoted\" (parens) AND OR NOT")]
    [InlineData("it's a nested-hyphen thing")]
    [InlineData("*")]
    [InlineData("道衍 propagation")]
    public async Task Ordinary_punctuation_does_not_break_the_query(string text)
    {
        await _store.ReplaceRepositoryAsync("alpha", [Entry("alpha", "Drift", "drift is measured against the lock")]);

        var hits = await new SqliteKnowledgeSearch(_store).SearchAsync(new KnowledgeQuery(text));

        Assert.NotNull(hits); // the assertion is that it returned at all
    }

    [Fact]
    public void A_query_of_only_short_words_has_no_match_expression()
    {
        Assert.Null(SqliteKnowledgeSearch.BuildMatchExpression("a of to"));
        Assert.Null(SqliteKnowledgeSearch.BuildMatchExpression(""));
        Assert.Equal("\"drift\" OR \"lock\"", SqliteKnowledgeSearch.BuildMatchExpression("drift lock"));
    }
}
