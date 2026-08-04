using Daoris.Service.Core;

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
}
