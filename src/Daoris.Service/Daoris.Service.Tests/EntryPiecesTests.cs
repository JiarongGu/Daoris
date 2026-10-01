using Daoris.Knowledge;

namespace Daoris.Service.Tests;

/// <summary>
/// SEM3 (D123): how an entry becomes the texts its vectors are made from — every part of its body, in
/// pieces no longer than the deployment's window, each led by its title.
/// </summary>
public class EntryPiecesTests
{
    private static KnowledgeEntry Entry(string body, string title = "Release handbook") =>
        new("alpha", EntryKind.Knowledge, Provenance.Local, title, body, ".claude/knowledge/handbook.md");

    private static string Paragraphs(int count) => string.Join("\n\n", Enumerable.Range(1, count)
        .Select(i => $"Paragraph {i} says one thing. It says it in two sentences, and a third for length."));

    [Fact]
    public void An_entry_within_the_window_is_one_piece_the_same_text_as_before()
    {
        var entry = Entry("A short body.");

        var piece = Assert.Single(EntryPieces.Of(entry, EntryPieces.DefaultWindow));

        Assert.Equal(0, piece.Start);
        Assert.Equal("Release handbook\nRelease handbook\nA short body.", piece.Text);
    }

    [Theory]
    [InlineData(200)]
    [InlineData(700)]
    [InlineData(2000)]
    public void No_piece_is_longer_than_the_window_title_included(int window)
    {
        var pieces = EntryPieces.Of(Entry(Paragraphs(120)), window);

        Assert.True(pieces.Count > 1);
        Assert.All(pieces, piece => Assert.True(piece.Text.Length <= window, $"{piece.Text.Length} > {window}"));
    }

    /// <summary>The point of the whole change: nothing of the body is left out of every vector.</summary>
    [Theory]
    [InlineData(200)]
    [InlineData(700)]
    [InlineData(2000)]
    public void Every_character_of_the_body_is_in_some_piece(int window)
    {
        var entry = Entry(Paragraphs(120) + "\n\nThe last word is here.");
        var lead = EntryPieces.Lead(entry.Title, window);

        var covered = new bool[entry.Body.Length];
        foreach (var piece in EntryPieces.Of(entry, window))
        {
            var text = piece.Text[lead.Length..];
            Assert.Equal(entry.Body.Substring(piece.Start, text.Length), text);
            for (var i = piece.Start; i < piece.Start + text.Length; i++) covered[i] = true;
        }

        var missed = Enumerable.Range(0, covered.Length).Where(i => !covered[i] && !char.IsWhiteSpace(entry.Body[i])).ToList();
        Assert.Empty(missed);
    }

    [Fact]
    public void A_cut_falls_at_a_paragraph_and_the_next_piece_reaches_back_into_the_one_before()
    {
        var entry = Entry(Paragraphs(40));
        var lead = EntryPieces.Lead(entry.Title, 700);

        var pieces = EntryPieces.Of(entry, 700);

        for (var i = 0; i < pieces.Count - 1; i++)
        {
            var end = pieces[i].Start + pieces[i].Text.Length - lead.Length;
            // Cut at a paragraph's end, so no sentence is broken by the cut itself.
            Assert.EndsWith("for length.", pieces[i].Text);
            // And the next one starts at a sentence or a line inside the last of it.
            Assert.True(pieces[i + 1].Start < end, $"piece {i + 1} starts at {pieces[i + 1].Start}, after {end}");
            Assert.True(pieces[i + 1].Start > pieces[i].Start);
        }
    }

    [Fact]
    public void A_body_with_no_boundary_is_cut_at_the_window_and_never_inside_a_surrogate_pair()
    {
        var body = string.Concat(Enumerable.Repeat("a😀", 400));
        var window = 301;

        var pieces = EntryPieces.Of(Entry(body, "T"), window);

        Assert.True(pieces.Count > 1);
        Assert.All(pieces, piece =>
        {
            Assert.True(piece.Text.Length <= window);
            Assert.False(char.IsHighSurrogate(piece.Text[^1]), "a piece ends inside a surrogate pair");
            Assert.False(char.IsLowSurrogate(body[piece.Start]), "a piece starts inside a surrogate pair");
        });
    }

    [Fact]
    public void A_title_that_would_take_half_the_window_twice_leads_once()
    {
        var title = new string('t', 70);

        Assert.Equal($"{title}\n{title}\n", EntryPieces.Lead(title, 2000));
        Assert.Equal($"{title}\n", EntryPieces.Lead(title, 200));
        Assert.Equal(99, EntryPieces.Lead(new string('t', 500), 200).Length - 1);
    }

    [Fact]
    public void The_same_entry_and_window_give_the_same_pieces()
    {
        var entry = Entry(Paragraphs(60));

        Assert.Equal(EntryPieces.Of(entry, 500), EntryPieces.Of(entry, 500));
    }

    [Fact]
    public void A_window_below_the_floor_is_refused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EntryPieces.Of(Entry("x"), EntryPieces.MinimumWindow - 1));
    }

    [Fact]
    public void A_vector_id_carries_where_its_piece_starts()
    {
        var id = EntryPieces.VectorId("alpha:docs/DECISIONS.md#D7 — sign @ release", 1234);

        Assert.Equal(1234, EntryPieces.StartOf(id));
        Assert.Equal(0, EntryPieces.StartOf(EntryPieces.VectorId("alpha:docs/a.md#x@9", 0)));
    }

    // ——— The window is the deployment's (D24): read once, refused when it cannot mean what it says.

    [Theory]
    [InlineData(null, EntryPieces.DefaultWindow)]
    [InlineData("", EntryPieces.DefaultWindow)]
    [InlineData("1500", 1500)]
    [InlineData(" 8000 ", 8000)]
    [InlineData("200", 200)]
    public void An_absent_or_whole_window_is_read(string? value, int expected)
    {
        var (window, error) = ServiceOptions.ParseEmbedWindow(value);

        Assert.Null(error);
        Assert.Equal(expected, window);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("199")]
    [InlineData("-5")]
    [InlineData("1,500")]
    [InlineData("1500.5")]
    public void A_window_that_is_not_one_is_refused_naming_the_variable_and_the_floor(string value)
    {
        var (_, error) = ServiceOptions.ParseEmbedWindow(value);

        Assert.NotNull(error);
        Assert.Contains(ServiceOptions.WindowVariable, error);
        Assert.Contains($"at least {EntryPieces.MinimumWindow}", error);
    }
}
