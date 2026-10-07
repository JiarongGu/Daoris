using Daoris.Knowledge;
using Lyntai.Memory;

namespace Daoris.Service.Tests;

/// <summary>
/// A hit names its lines (ORIENT2e; the orientation design §3.2): the entry's <c>path:first-last</c> and the line
/// its excerpt starts on, by every search and through the store, so the read that follows is a range.
/// </summary>
public sealed class HitLinesTests
{
    private static readonly string Filler = new('a', 120);

    /// <summary>Lines 10 to 12 of its file: an opening, a long line, then the line that matches.</summary>
    private static KnowledgeEntry Renewal(string repository = "alpha", EntryKind kind = EntryKind.Decision) =>
        new(repository, kind, Provenance.Local, "Renewal", $"Opening.\n{Filler}\nThen the certificate is renewed by hand.",
            "docs/DECISIONS.md", "Renewal", Lines: new LineSpan(10, 12));

    [Fact]
    public void An_excerpt_says_the_line_of_the_body_it_starts_on()
    {
        var body = $"Opening.\n{Filler}\nThen the certificate is renewed by hand.";

        var (text, line) = Text.ExcerptAt(body, ["certificate"]);

        Assert.Contains("certificate", text);
        Assert.Equal(1, line);   // sixty characters before the match is inside the long line
        Assert.Equal(0, Text.ExcerptAt(body, ["opening"]).Line);
        Assert.Equal(0, Text.ExcerptAt(body, ["nowhere"]).Line);
        Assert.Equal(Text.Excerpt(body, ["certificate"]), text);
    }

    /// <summary>The frontmatter an excerpt skips is still counted, so the line is the body's own.</summary>
    [Fact]
    public void An_excerpt_after_frontmatter_counts_the_lines_it_skipped()
    {
        const string note = "---\nname: note\n---\n\n# Note\n\n";

        // The prose's opening, after the four lines the excerpt skipped.
        Assert.Equal(4, Text.ExcerptAt(note + "Body.", ["nowhere"]).Line);
        Assert.Equal(6, Text.ExcerptAt(note + Filler + "\nThe certificate.", ["certificate"]).Line);
    }

    [Fact]
    public void A_range_is_read_as_a_hit_names_it()
    {
        Assert.Equal(new LineSpan(12, 30), LineSpan.Parse("12-30"));
        Assert.Equal(new LineSpan(7, 7), LineSpan.Parse(" 7 "));
        Assert.Equal("12-30", new LineSpan(12, 30).ToString());
        Assert.Equal("7", new LineSpan(7, 7).ToString());
        foreach (var refused in new[] { "", "0", "0-3", "9-3", "a-b", "3-", "-3", "1-2-3", "1.5" })
        {
            Assert.Null(LineSpan.Parse(refused));
        }
    }

    /// <summary>
    /// Lines a feed claims are taken only where they can be its body's, and a section that moved is new content
    /// to a feed's digest; an entry naming none hashes as it did before lines were kept.
    /// </summary>
    [Fact]
    public void A_feed_s_lines_are_taken_only_where_they_can_be_its_body_s()
    {
        Assert.Equal(new LineSpan(4, 5), LineSpan.Of(4, 5, "one\ntwo"));
        Assert.Null(LineSpan.Of(4, 9, "one\ntwo"));
        Assert.Null(LineSpan.Of(null, 5, "one"));
        Assert.Null(LineSpan.Of(0, 0, "one"));
        Assert.Null(LineSpan.Of(5, 4, "one"));

        var entry = Renewal();
        Assert.NotEqual(FeedDigest.Of([entry]), FeedDigest.Of([entry with { Lines = new LineSpan(20, 22) }]));
        Assert.NotEqual(FeedDigest.Of([entry]), FeedDigest.Of([entry with { Lines = null }]));
    }

    /// <summary>A range reads the lines of the entry it covers, never past the entry's own.</summary>
    [Fact]
    public void An_entry_is_cut_to_the_lines_a_range_names()
    {
        var entry = Renewal();

        Assert.Equal((new LineSpan(11, 12), $"{Filler}\nThen the certificate is renewed by hand."), entry.Cut(new LineSpan(11, 40)));
        Assert.Equal((new LineSpan(10, 10), "Opening."), entry.Cut(new LineSpan(1, 10)));
        Assert.Null(entry.Cut(new LineSpan(13, 20)));
        Assert.Null((entry with { Lines = null }).Cut(new LineSpan(10, 12)));
        // A body that is not as many lines as it claims is not cut: its lines would name the wrong text.
        Assert.Null((entry with { Lines = new LineSpan(10, 30) }).Cut(new LineSpan(10, 12)));
    }

    [Fact]
    public async Task A_search_by_words_names_the_line_its_excerpt_starts_on()
    {
        var store = new InMemoryKnowledgeStore();
        await store.ReplaceRepositoryAsync("alpha", [Renewal()]);

        var hit = Assert.Single(await new LexicalKnowledgeSearch(store).SearchAsync(new KnowledgeQuery("certificate")));

        Assert.Equal(11, hit.ExcerptLine);
        Assert.Equal(new LineSpan(10, 12), hit.Entry.Lines);
    }

    [Fact]
    public async Task The_sqlite_store_keeps_an_entry_s_lines_and_its_search_names_them()
    {
        var file = Path.Combine(Path.GetTempPath(), $"daoris-lines-{Guid.NewGuid():N}.db");
        try
        {
            await using (var store = await SqliteKnowledgeStore.OpenAsync(file))
            {
                var unlined = Renewal("beta") with { Lines = null };
                await store.ReplaceRepositoryAsync("alpha", [Renewal()]);
                await store.ReplaceRepositoryAsync("beta", [unlined]);

                Assert.Equal(Renewal(), await store.FindAsync(Renewal().Id));
                Assert.Equal(unlined, await store.FindAsync(unlined.Id));

                var hits = await new SqliteKnowledgeSearch(store).SearchAsync(new KnowledgeQuery("certificate"));
                Assert.Equal(11, Assert.Single(hits, h => h.Entry.Repository == "alpha").ExcerptLine);
                Assert.Null(Assert.Single(hits, h => h.Entry.Repository == "beta").ExcerptLine);
            }
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(file)) File.Delete(file);
        }
    }

    /// <summary>An index written before entries kept their lines is rebuilt when it opens, as every schema bump is.</summary>
    [Fact]
    public async Task A_store_written_before_lines_is_rebuilt()
    {
        var file = Path.Combine(Path.GetTempPath(), $"daoris-lines-{Guid.NewGuid():N}.db");
        try
        {
            await using (var store = await SqliteKnowledgeStore.OpenAsync(file))
            {
                await store.ReplaceRepositoryAsync("alpha", [Renewal()]);
            }
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            await using (var older = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={file}"))
            {
                await older.OpenAsync();
                await using var command = older.CreateCommand();
                command.CommandText = "PRAGMA user_version = 4;";
                await command.ExecuteNonQueryAsync();
            }
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            await using var reopened = await SqliteKnowledgeStore.OpenAsync(file);

            Assert.True(reopened.Rebuilt);
            Assert.Empty(await reopened.AllAsync());
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(file)) File.Delete(file);
        }
    }

    /// <summary>
    /// A hit found by meaning alone shows the opening of the piece that matched (SEM3), and names the line that
    /// piece starts on; fused with the words, the excerpt and its line stay together.
    /// </summary>
    [Fact]
    public async Task A_hit_by_meaning_names_the_line_its_piece_starts_on()
    {
        var store = new InMemoryKnowledgeStore();
        var embedder = new DimensionEmbedder(["certificate", "credential"]);
        var vectors = new InMemoryVectorStore();
        var entry = Renewal();
        var service = new KnowledgeService(
            store,
            new HybridKnowledgeSearch(new LexicalKnowledgeSearch(store), new SemanticKnowledgeSearch(store, embedder, vectors)),
            new Fixed(entry), DisclosurePolicy.LocalOnly, embedder, vectors);
        await service.RefreshAsync();

        var hit = Assert.Single((await service.AnswerAsync(new KnowledgeQuery("credential") { Limit = 1 })).Hits);

        Assert.Equal(entry.Id, hit.Entry.Id);
        Assert.Equal(10, hit.ExcerptLine);
    }

    // ── a search that names no kinds ─────────────────────────────────────────────────────────────

    private static KnowledgeEntry Row(int n) => new(
        "alpha", EntryKind.Index, Provenance.Local, $"Routes › certificate {n}", $"| `certificate{n}` | `Renewal.cs:{n}` |",
        "docs/index/routes.md", $"Routes › certificate {n}", Lines: new LineSpan(n, n));

    private static KnowledgeEntry Decision(int n) => new(
        "alpha", EntryKind.Decision, Provenance.Local, $"D{n}", $"Why the certificate is renewed, the {n}th time.",
        "docs/DECISIONS.md", $"D{n}");

    private static async Task<KnowledgeService> ServiceAsync(params KnowledgeEntry[] entries)
    {
        var store = new InMemoryKnowledgeStore();
        var service = new KnowledgeService(store, new LexicalKnowledgeSearch(store), new Fixed(entries), DisclosurePolicy.LocalOnly);
        await service.RefreshAsync();
        return service;
    }

    /// <summary>
    /// A search that names no kinds answers with at most two index entries (§3.1), so a question about why
    /// something was decided is not crowded out by rows of identifiers, and it says more matched.
    /// </summary>
    [Fact]
    public async Task A_search_naming_no_kinds_answers_with_at_most_two_index_entries_and_says_more_matched()
    {
        var service = await ServiceAsync([.. Enumerable.Range(1, 6).Select(Row), .. Enumerable.Range(1, 3).Select(Decision)]);

        var answer = await service.AnswerAsync(new KnowledgeQuery("certificate") { Limit = 5 });

        Assert.Equal(5, answer.Hits.Count);
        Assert.Equal(2, answer.Hits.Count(h => h.Entry.Kind == EntryKind.Index));
        Assert.Equal(3, answer.Hits.Count(h => h.Entry.Kind == EntryKind.Decision));
        Assert.True(answer.MoreIndex);
        // Each kept index entry keeps the rank it had: the best two, at their places.
        var ranked = (await service.AnswerAsync(new KnowledgeQuery("certificate") { Kinds = new HashSet<EntryKind> { EntryKind.Index }, Limit = 2 })).Hits;
        Assert.Equal(ranked.Select(h => h.Entry.Id), answer.Hits.Where(h => h.Entry.Kind == EntryKind.Index).Select(h => h.Entry.Id));
    }

    [Fact]
    public async Task A_search_for_the_index_by_kind_answers_with_every_index_entry()
    {
        var service = await ServiceAsync([.. Enumerable.Range(1, 6).Select(Row), Decision(1)]);

        var answer = await service.AnswerAsync(new KnowledgeQuery("certificate") { Kinds = new HashSet<EntryKind> { EntryKind.Index } });

        Assert.Equal(6, answer.Hits.Count);
        Assert.False(answer.MoreIndex);
    }

    [Fact]
    public async Task Two_index_entries_or_fewer_are_answered_as_they_rank()
    {
        var service = await ServiceAsync(Row(1), Row(2), Decision(1));

        var answer = await service.AnswerAsync(new KnowledgeQuery("certificate"));

        Assert.Equal(3, answer.Hits.Count);
        Assert.False(answer.MoreIndex);
    }

    /// <summary>
    /// Convergence is two repositories learning one lesson; two generated indexes from one template are not
    /// that, so the index is left out unless asked for by kind.
    /// </summary>
    [Fact]
    public async Task Convergence_leaves_the_index_out_unless_it_is_asked_for()
    {
        var alpha = Row(1);
        var beta = Row(1) with { Repository = "beta" };
        var service = await ServiceAsync(alpha, beta);

        var found = await service.FindConvergenceAsync(new ConvergenceOptions(0.5));
        var asked = await service.FindConvergenceAsync(new ConvergenceOptions(0.5, new HashSet<EntryKind> { EntryKind.Index }));

        Assert.Empty(found);
        Assert.NotEmpty(asked);
    }

    private sealed class Fixed(params KnowledgeEntry[] entries) : IKnowledgeSource
    {
        public string Name => "fixture";

        public Task<IReadOnlyList<KnowledgeEntry>> ReadAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<KnowledgeEntry>>(entries);
    }
}
