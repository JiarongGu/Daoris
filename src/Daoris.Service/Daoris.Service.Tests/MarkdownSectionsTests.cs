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

    /// <summary>
    /// ORIENT2h3: a fence opens on three or more backticks or tildes and closes only on a run of the same character at
    /// least as long with nothing after it, as CommonMark reads one. Toggled on any line opening with three, a fence
    /// quoting a shorter one closed on the quote, and the example's headings split the document.
    /// </summary>
    [Theory]
    [InlineData("a longer fence holds a shorter one", "## Real\n\n````\n```\n## Example\n```\n````\n\nAfter.", "Real")]
    [InlineData("a tilde fence is not closed by backticks", "## Real\n\n~~~\n```\n## Example\n~~~\n\nAfter.", "Real")]
    [InlineData("a fence with words after it closes nothing", "## Real\n\n```\n```js\n## Example\n```\n\nAfter.", "Real")]
    [InlineData("a closing fence may be longer", "## Real\n\n```\n## Example\n`````\n\n## Next\n\nBody.", "Real|Next")]
    [InlineData("a fence left open runs to the end", "## Real\n\n````\n## Example\n```\n", "Real")]
    [InlineData("backticks closed on their own line are code, not a fence", "## Real\n\n```a``` is code.\n\n## Next\n\nBody.", "Real|Next")]
    [InlineData("two backticks are no fence", "## Real\n\n``\n\n## Next\n\nBody.", "Real|Next")]
    [InlineData("a fence indented in a list item is a fence", "## Real\n\n- An item:\n\n    ```\n## Example\n    ```\n\n## Next\n\nBody.", "Real|Next")]
    public void A_fence_closes_as_commonmark_closes_it(string name, string doc, string headings)
    {
        Assert.NotEmpty(name);
        Assert.Equal(headings, string.Join('|', MarkdownSections.Split(doc).Select(s => s.Heading)));
    }

    /// <summary>ORIENT2h3: a record's title is read past a longer fence's quoted example, as a section is.</summary>
    [Fact]
    public void The_first_heading_is_read_past_a_longer_fence_s_quoted_example()
    {
        Assert.Equal("Real", MarkdownSections.FirstHeading("````\n```\n# Example\n```\n````\n\n# Real\n"));
    }
}
