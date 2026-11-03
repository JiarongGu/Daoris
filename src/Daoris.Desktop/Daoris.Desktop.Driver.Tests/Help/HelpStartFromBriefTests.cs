using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// D233 choice 2: an Ask Daoris conversation started from an earlier one is handed Daoris's brief of that one's line with the person's
/// first message, never its transcript. The brief is the prompt's first part, and the record keeps it as Daoris's first prompt, with its
/// account and the session it continues, ahead of the person's words.
/// </summary>
public sealed class HelpStartFromBriefTests
{
    private static readonly ComposedBrief Brief =
        new([new BriefPiece("opening", "You continue session `h1`, which ran on claude; it moved because the person asked for it.")], [])
        {
            Continues = "h1",
        };

    [Fact]
    public void The_first_message_carries_the_brief_first_and_the_record_keeps_it_as_daoriss_prompt()
    {
        var message = new ChatMessage("and a remote?", []) { Brief = Brief, Preface = "Sessions" };

        Assert.StartsWith(Brief.Text, message.Prompt, StringComparison.Ordinal);
        var recorded = ChatRunner.Recorded(message).ToList();

        var brief = recorded[0];
        Assert.Equal((SessionEventKind.User, "target", Brief.Text, "h1"), (brief.Kind, brief.Origin, brief.Text, brief.Continues));
        Assert.Equal(Brief.Text.Length, brief.Account!.Chars);
        var asked = Assert.Single(recorded, each => each.Origin == "person");
        Assert.Equal("and a remote?", asked.Text);
        Assert.True(recorded.IndexOf(brief) < recorded.IndexOf(asked));
    }

    [Fact]
    public void A_first_message_with_no_brief_records_none()
    {
        var recorded = ChatRunner.Recorded(new ChatMessage("hello", [])).ToList();

        Assert.DoesNotContain(recorded, each => each.Origin == "target");
    }
}
