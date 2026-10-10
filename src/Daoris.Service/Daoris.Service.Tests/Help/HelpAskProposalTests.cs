using System.Text.Json;

namespace Daoris.Service.Tests;

/// <summary>The <c>ask</c> kind's writer (HELP1c): something to start, with its words and its workspace.</summary>
public sealed class HelpAskProposalTests : HelpProposalBoxFixture
{
    [Fact]
    public void An_ask_is_written_with_its_words_and_its_workspace()
    {
        var (id, _) = Box().ProposeAsk("fix the chunk streamer's cold-cache stall", "work", "the person wants it started", "h1", Now);

        var file = Written(id!);
        Assert.Equal("ask", file.GetProperty("kind").GetString());
        Assert.Equal("fix the chunk streamer's cold-cache stall", file.GetProperty("sentence").GetString());
        Assert.Equal("work", file.GetProperty("workspace").GetString());
        // ENTRY1c: nothing said is the composer's default, each repository's rule, so no choice is written.
        Assert.Equal(JsonValueKind.Null, file.GetProperty("review").ValueKind);
        Assert.Equal(JsonValueKind.Null, file.GetProperty("reviewWords").ValueKind);
    }

    /// <summary>
    /// ENTRY1c: the composer's choices, as the box writes them. Each row but the last is one the driver's
    /// <c>AskReviewCommand.Compose</c> takes (its <c>Composed</c> table, and <c>HelpAskProposalsTests</c>), so the box never refuses
    /// what the driver would send; the last is a tool's empty argument, which says nothing, so none is written.
    /// </summary>
    [Theory]
    [InlineData(null, null, null, null)]
    [InlineData("rule", null, null, null)]
    [InlineData("on", null, "on", null)]
    [InlineData("off", "  a readme change ", "off", "a readme change")]
    [InlineData("dev", null, "dev", null)]
    [InlineData(" dev ", "it touches the page", "dev", "it touches the page")]
    [InlineData("", " ", null, null)]
    public void An_ask_carries_the_persons_review_choice_and_its_words(string? review, string? words, string? choice, string? kept)
    {
        var (id, message) = Box().ProposeAsk("add the compare setting", "work", "the person wants it reviewed", "h1", Now, review, words);

        Assert.NotNull(id);
        var file = Written(id!);
        Assert.Equal(choice, file.GetProperty("review").GetString());
        Assert.Equal(kept, file.GetProperty("reviewWords").GetString());
        Assert.DoesNotContain("Nothing was proposed", message);
    }

    /// <summary>
    /// ENTRY1c: the box judges the shape only, as the setting kind's <c>review</c> door does: words go with a choice, and a choice
    /// is <c>on</c>, <c>off</c> or one name. Whether a name is one the ask's workspace declares is the driver's, at Apply.
    /// </summary>
    [Theory]
    [InlineData(null, "a readme change", "words go with a review choice")]
    [InlineData("rule", "a readme change", "words go with a review choice")]
    [InlineData("show it to me", null, "one word")]
    public void A_review_that_is_not_a_choices_shape_is_refused(string? review, string? words, string named)
    {
        var (id, message) = Box().ProposeAsk("add the compare setting", "work", "the person wants it reviewed", "h1", Now, review, words);

        Assert.Null(id);
        Assert.Contains(named, message);
        Assert.EndsWith("Nothing was proposed.", message);
    }
}
