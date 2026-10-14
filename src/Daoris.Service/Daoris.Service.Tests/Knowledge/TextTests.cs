using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// The tokeniser and the CJK segmenter, after the first real index showed every Chinese query
/// returning the whole corpus (2026-09-23). Two defects hid behind one symptom: the two-character
/// floor dropped the terms, and beneath it FTS5's <c>unicode61</c> keeps a run of ideographs as one
/// token, so even a kept term matched nothing. These hold each half on its own.
/// </summary>
public sealed class TextTests
{
    [Fact]
    public void Latin_text_is_returned_untouched_by_the_segmenter()
    {
        const string text = "Keep captures small — a large image is rejected.";
        Assert.Equal(text, Text.Segment(text));
    }

    [Fact]
    public void A_run_of_ideographs_becomes_its_overlapping_bigrams()
    {
        Assert.Equal("会话 话的 的记 记录", Text.Segment("会话的记录"));
    }

    /// <summary>A two-character word is one unit, which is what a two-character query is.</summary>
    [Fact]
    public void A_two_character_word_is_one_bigram()
    {
        Assert.Equal("记录", Text.Segment("记录"));
    }

    [Fact]
    public void A_lone_ideograph_stays_a_unigram()
    {
        Assert.Equal("书", Text.Segment("书"));
    }

    /// <summary>
    /// Mixed text keeps its Latin exactly and cuts only the ideographs — with a space at each edge of
    /// a run, so a bigram never glues itself to a Latin word on either side.
    /// </summary>
    [Fact]
    public void Mixed_text_cuts_only_the_ideographs()
    {
        Assert.Equal("the 会话 话的 的记 记录 on disk", Text.Segment("the会话的记录on disk"));
        // Punctuation is not an ideograph, so it stays glued to the Latin before it; the space is
        // owed only where a run of ideographs begins.
        Assert.Equal("D51： 会话 话记 记录", Text.Segment("D51：会话记录"));
    }

    [Fact]
    public void Punctuation_between_runs_starts_a_new_run()
    {
        // Two words separated by a comma are two runs, not one run with a comma-bigram inside it.
        Assert.Equal("会话 , 记录", Text.Segment("会话,记录"));
    }

    [Fact]
    public void The_empty_string_segments_to_itself()
    {
        Assert.Equal(string.Empty, Text.Segment(string.Empty));
    }

    /// <summary>The floor: "of" and "a" still go; a Chinese word of two characters stays.</summary>
    [Fact]
    public void Tokenize_keeps_a_short_ideograph_word_and_still_drops_short_Latin_fragments()
    {
        Assert.Equal(["记录"], Text.Tokenize("记录"));
        Assert.Equal(["会话", "话的", "的记", "记录"], Text.Tokenize("会话的记录"));
        Assert.Equal(["encoding", "trap"], Text.Tokenize("an encoding of a trap"));
    }

    /// <summary>
    /// A word character is a letter or a digit, in any script — the rule FTS5's <c>unicode61</c>
    /// applies to the same text — so fullwidth punctuation separates exactly as ASCII punctuation
    /// does. Before, the separators were a hand-listed ASCII string, and <c>D51：会话</c> tokenised to
    /// <c>d51：</c>, a term the index never held.
    /// </summary>
    [Fact]
    public void Tokenize_separates_on_fullwidth_punctuation_as_it_does_on_ascii()
    {
        Assert.Equal(["d51", "会话"], Text.Tokenize("D51：会话"));
        Assert.Equal(["encoding", "the", "trap"], Text.Tokenize("“encoding”—the trap"));
        Assert.Equal(["版本", "记录"], Text.Tokenize("版本2。记录"));
    }

    /// <summary>Query and index are cut the same way, so they meet — the whole point of bigrams.</summary>
    [Fact]
    public void A_query_bigram_is_among_the_index_bigrams_of_a_body_that_contains_the_word()
    {
        var body = Text.Tokenize("每个会话都留下一份记录，写在磁盘上。");
        foreach (var term in Text.Tokenize("记录")) Assert.Contains(term, body);
        foreach (var term in Text.Tokenize("会话")) Assert.Contains(term, body);
    }

    // ── identifiers (ORIENT1f) ───────────────────────────────────────────────────────────────────
    //
    // `unicode61` keeps `ProbeLock` as one token, as it keeps a run of ideographs: a question in plain
    // words ("probe lock") matched nothing inside it, and the decision that named the identifier was never
    // found by its words. An identifier is cut into its words beside itself, so it matches both ways.

    [Theory]
    [InlineData("ProbeLock", new[] { "probelock", "probe", "lock" })]
    [InlineData("probeLock", new[] { "probelock", "probe", "lock" })]
    [InlineData("probe_lock", new[] { "probe", "lock", "probelock" })]
    [InlineData("PROBE_LOCK", new[] { "probe", "lock", "probelock" })]
    [InlineData("HTTPServer", new[] { "httpserver", "http", "server" })]
    [InlineData("UTF8Encoding", new[] { "utf8encoding", "utf8", "encoding" })]
    [InlineData("URLsFor", new[] { "urlsfor", "urls", "for" })]
    [InlineData("probe_lockFile", new[] { "probe", "lockfile", "probelockfile", "lock", "file" })]
    public void An_identifier_is_tokenized_whole_and_as_its_words(string identifier, string[] expected)
    {
        Assert.Equal(expected, Text.Tokenize(identifier));
    }

    /// <summary>
    /// No inner boundary, no words: a task id's digit and letter, a plural acronym, a capitalised or
    /// lower-case word, and an underscore at an edge are not identifiers made of words.
    /// </summary>
    [Theory]
    [InlineData("TOOL6g", "tool6g")]
    [InlineData("ORIENT1f", "orient1f")]
    [InlineData("APIs", "apis")]
    [InlineData("PRs", "prs")]
    [InlineData("README", "readme")]
    [InlineData("Lock", "lock")]
    [InlineData("probelock", "probelock")]
    [InlineData("__init__", "init")]
    public void A_word_with_no_inner_boundary_stays_one_token(string word, string token)
    {
        Assert.Equal([token], Text.Tokenize(word));
    }

    /// <summary>
    /// The FTS row is fed what <see cref="Text.Segment"/> returns, so the words are spelled out beside the
    /// identifier, which stays as written; a part of two letters or fewer is not added, as the query floor
    /// would never ask for it.
    /// </summary>
    [Fact]
    public void The_segmenter_spells_an_identifier_s_words_beside_it()
    {
        Assert.Equal("held by `ProbeLock Probe Lock`.", Text.Segment("held by `ProbeLock`."));
        Assert.Equal("probe_lock probelock", Text.Segment("probe_lock"));
        Assert.Equal("macOS mac", Text.Segment("macOS"));
        // A run of ideographs is cut first, so an identifier glued to one is still found.
        Assert.Equal("记录 ProbeLock Probe Lock", Text.Segment("记录ProbeLock"));
    }

    /// <summary>
    /// A question's adjacent words are also asked joined, as an identifier spells them, so "probe lock"
    /// reaches the entry that names <c>ProbeLock</c> whole as well as by its words. A join counts for each
    /// word it joins. Never across an ideograph, whose bigrams joined are no word.
    /// </summary>
    [Fact]
    public void A_question_asks_its_adjacent_words_joined_as_well()
    {
        Assert.Equal(
            [new QueryTerm("probe", 1), new QueryTerm("lock", 1), new QueryTerm("probelock", 2)],
            Text.QueryTerms("probe lock"));
        Assert.Equal(
            ["account", "rotation", "tests", "accountrotation", "rotationtests", "accountrotationtests"],
            Text.QueryTerms("account rotation tests").Select(term => term.Term));
        Assert.Equal([1, 1, 1, 2, 2, 3], Text.QueryTerms("account rotation tests").Select(term => term.Words));
        Assert.Equal(["probelock", "probe", "lock"], Text.QueryTerms("ProbeLock").Select(term => term.Term));
        Assert.Equal(["记录", "会话"], Text.QueryTerms("记录 会话").Select(term => term.Term));
        Assert.Empty(Text.QueryTerms("a of to"));
    }
}
