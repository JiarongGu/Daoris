using Daoris.Knowledge;

namespace Daoris.Service.Tests;

public class MarkdownSectionsTests
{
    [Fact]
    public void Splits_at_the_requested_level_and_drops_the_preamble()
    {
        const string doc = """
            # Decisions

            Numbered, dated, with the reasoning.

            ## D1 — first

            Body of one.

            ## D2 — second

            Body of two.
            """;

        var sections = MarkdownSections.Split(doc);

        Assert.Equal(2, sections.Count);
        Assert.Equal("D1 — first", sections[0].Heading);
        Assert.Equal("Body of one.", sections[0].Body);
        Assert.Equal("D2 — second", sections[1].Heading);
        // The text before the first heading describes the file, not any entry in it.
        Assert.DoesNotContain(sections, s => s.Body.Contains("Numbered, dated"));
    }

    /// <summary>
    /// A document that quotes markdown splits itself apart at headings that were only ever examples.
    /// This repository's own decision log contains such a fence, so the naive version fails on the
    /// first real input rather than on some hypothetical one.
    /// </summary>
    [Fact]
    public void Ignores_headings_inside_fenced_code()
    {
        const string doc = """
            ## Real heading

            Here is the shape an entry takes:

            ```
            ## Not a heading
            - **Symptom:** what was observed
            ```

            Still the same entry.
            """;

        var sections = MarkdownSections.Split(doc);

        Assert.Single(sections);
        Assert.Equal("Real heading", sections[0].Heading);
        Assert.Contains("Not a heading", sections[0].Body);
        Assert.Contains("Still the same entry.", sections[0].Body);
    }

    [Fact]
    public void A_document_with_no_headings_yields_nothing()
    {
        Assert.Empty(MarkdownSections.Split("Just prose, no headings at all."));
        Assert.Empty(MarkdownSections.Split(string.Empty));
    }

    [Fact]
    public void Deeper_headings_stay_inside_their_section()
    {
        const string doc = """
            ## Outer

            Intro.

            ### Inner

            Detail.

            ## Next

            Other.
            """;

        var sections = MarkdownSections.Split(doc);

        Assert.Equal(2, sections.Count);
        Assert.Contains("### Inner", sections[0].Body);
        Assert.Contains("Detail.", sections[0].Body);
    }

    /// <summary>
    /// The text a log drops is a README's most useful part: what the repository is (WSSETUP8; D124 §5).
    /// Everything before the first heading at the level, its own title line included, trimmed.
    /// </summary>
    [Fact]
    public void The_preamble_is_the_text_before_the_first_heading()
    {
        const string doc = """
            # Report engine

            Computes the monthly figures.

            ## Figures

            How each is computed.
            """;

        Assert.Equal("# Report engine\n\nComputes the monthly figures.", MarkdownSections.Preamble(doc));
    }

    [Theory]
    [InlineData("a document that opens with a heading", "## First\n\nBody.\n", "")]
    [InlineData("a document with no heading at the level", "# Title\n\nAll of it.\n\n### Deeper\n\nStill.", "# Title\n\nAll of it.\n\n### Deeper\n\nStill.")]
    [InlineData("a heading inside a fence does not end it", "Intro.\n```\n## Example\n```\nMore.\n\n## Real\n\nBody.", "Intro.\n```\n## Example\n```\nMore.")]
    [InlineData("CRLF line endings", "Intro.\r\n\r\n## Real\r\n\r\nBody.", "Intro.")]
    [InlineData("nothing at all", "", "")]
    public void What_the_preamble_is(string name, string doc, string expected)
    {
        Assert.NotEmpty(name);
        Assert.Equal(expected, MarkdownSections.Preamble(doc));
    }
}
