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

    /// <summary>Query and index are cut the same way, so they meet — the whole point of bigrams.</summary>
    [Fact]
    public void A_query_bigram_is_among_the_index_bigrams_of_a_body_that_contains_the_word()
    {
        var body = Text.Tokenize("每个会话都留下一份记录，写在磁盘上。");
        foreach (var term in Text.Tokenize("记录")) Assert.Contains(term, body);
        foreach (var term in Text.Tokenize("会话")) Assert.Contains(term, body);
    }
}
