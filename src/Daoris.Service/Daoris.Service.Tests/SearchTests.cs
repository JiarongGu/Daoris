using Daoris.Knowledge;

namespace Daoris.Service.Tests;

public class SearchTests : IAsyncLifetime
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

    // The SQLite search over a file of its own, for the cases that hold both searches: the FTS row is fed
    // by `Text.Segment` and asked by `Text.QueryTerms`, where the lexical search reads the same two.
    private readonly List<(SqliteKnowledgeStore Store, string File)> _sqlite = [];

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        foreach (var (store, _) in _sqlite) await store.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var (_, file) in _sqlite) File.Delete(file);
    }

    private async Task<IKnowledgeSearch> Over(bool sqlite, params KnowledgeEntry[] entries)
    {
        if (!sqlite) return await SearchOver(entries);

        var file = Path.Combine(Path.GetTempPath(), $"daoris-search-{Guid.NewGuid():N}.db");
        var store = await SqliteKnowledgeStore.OpenAsync(file);
        _sqlite.Add((store, file));
        foreach (var group in entries.GroupBy(e => e.Repository))
        {
            await store.ReplaceRepositoryAsync(group.Key, group.ToList());
        }

        return new SqliteKnowledgeSearch(store);
    }

    // ── identifiers (ORIENT1f) ───────────────────────────────────────────────────────────────────
    //
    // *What decided the probe lock* never reached the decision that named `ProbeLock` (D24's ORIENT1c
    // note): the code's word was one token, and no word of the question was it.

    public static TheoryData<string, string, bool> Spellings()
    {
        var data = new TheoryData<string, string, bool>();
        foreach (var sqlite in new[] { false, true })
        {
            foreach (var named in new[] { "ProbeLock", "probeLock", "probe_lock" })
            {
                foreach (var asked in new[] { "probe lock", "ProbeLock", "probeLock", "probe_lock" })
                {
                    data.Add(named, asked, sqlite);
                }
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Spellings))]
    public async Task An_identifier_is_found_by_its_words_and_by_each_spelling_of_it(string named, string asked, bool sqlite)
    {
        var search = await Over(sqlite,
            Entry("alpha", "The status question", $"One question per account at a time, held by `{named}`."),
            Entry("beta", "Unrelated", "A note about the merge order of branches."));

        var hits = await search.SearchAsync(new KnowledgeQuery(asked));

        Assert.Equal("The status question", Assert.Single(hits).Entry.Title);
    }

    /// <summary>
    /// An identifier still matches itself whole, and best: the entry that names it outranks one that only
    /// says its words, asked by the identifier or by its words.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task The_entry_naming_an_identifier_outranks_one_that_only_says_its_words(bool sqlite)
    {
        var search = await Over(sqlite,
            Entry("alpha", "Prose", "A probe can hold a lock while it asks the agent."),
            Entry("beta", "Named", "The status question is asked under `ProbeLock` alone."),
            Entry("gamma", "Unrelated", "Nothing of the kind is said here at all."));

        Assert.Equal("Named", (await search.SearchAsync(new KnowledgeQuery("ProbeLock")))[0].Entry.Title);
        Assert.Equal(
            ["Named", "Prose"],
            (await search.SearchAsync(new KnowledgeQuery("probe lock"))).Select(hit => hit.Entry.Title));
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

    /// <summary>
    /// 🔴 Seen on the window (POLISH4): every canon-shaped entry opens with its frontmatter, so its
    /// excerpt read "--- name: world-streaming applies_when: …" — the file's machinery, flattened onto
    /// one line, where the reader wanted its prose. The frontmatter still matches; it is not shown.
    /// </summary>
    [Fact]
    public async Task An_excerpt_is_taken_from_the_prose_and_never_from_the_frontmatter()
    {
        var search = await SearchOver(Entry(
            "game", "world-streaming",
            "---\nname: world-streaming\napplies_when: loading or unloading world chunks\nenforces: hydrate first\n---\n\n"
            + "# Streaming the world\n\nHydrate a chunk before its neighbours are visible."));

        var byProse = await search.SearchAsync(new KnowledgeQuery("streaming"));
        Assert.StartsWith("Streaming the world", byProse[0].Excerpt);

        // Matched only in the frontmatter: still found, and the excerpt is the prose's opening.
        var byField = await search.SearchAsync(new KnowledgeQuery("unloading"));
        Assert.Single(byField);
        Assert.DoesNotContain("applies_when", byField[0].Excerpt);
        Assert.DoesNotContain("---", byField[0].Excerpt);
        Assert.StartsWith("Streaming the world", byField[0].Excerpt);
    }

    /// <summary>
    /// 🔴 UX5 U39: the frontmatter went (POLISH4) and the Markdown's own markers stayed, so an excerpt
    /// read "# World streaming — chunk hydration order … **chunk hydration** runs neighbours-first".
    /// The page shows an excerpt as plain text, so a heading's hashes, emphasis's asterisks and code's
    /// backticks are machinery too. The words stay; an identifier's underscore stays.
    /// </summary>
    [Fact]
    public async Task An_excerpt_shows_the_words_and_not_the_markdown_that_marks_them()
    {
        var search = await SearchOver(Entry(
            "game", "world-streaming",
            "# World streaming — chunk hydration order\n\n"
            + "The example game's own knowledge: **chunk hydration** runs neighbours-first, set by `hydrate_first` "
            + "and __never__ skipped.\n\n## Why\n\nA seam is visible."));

        var excerpt = (await search.SearchAsync(new KnowledgeQuery("hydration")))[0].Excerpt!;

        Assert.StartsWith("World streaming — chunk hydration order", excerpt);
        Assert.Contains("chunk hydration runs neighbours-first, set by hydrate_first and never skipped.", excerpt);
        // The second heading's hashes too, wherever the window reaches.
        Assert.DoesNotContain("#", Text.Excerpt("## Why\n\nA seam is visible.", ["seam"]));
        Assert.DoesNotContain("#", excerpt);
        Assert.DoesNotContain("**", excerpt);
        Assert.DoesNotContain("`", excerpt);
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
