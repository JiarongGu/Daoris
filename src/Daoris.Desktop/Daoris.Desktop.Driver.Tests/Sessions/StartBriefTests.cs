using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>The brief a start is given is what the page's *Started with* reads: kept whole to a bound, its cut said.</summary>
public sealed class StartBriefTests
{
    private static SessionEvent User(string text, string? continues) =>
        new() { Kind = SessionEventKind.User, Origin = "target", Text = text, Continues = continues };

    [Fact]
    public void A_part_that_continues_another_keeps_its_first_message_as_the_brief()
    {
        Assert.Equal("Go on with the hydrator fix", StartBrief.Of(User("Go on with the hydrator fix", "a1b2c3d4")));
    }

    [Fact]
    public void A_brief_past_its_bound_is_cut_and_says_how_long_it_was()
    {
        var said = StartBrief.Of(User(new string('x', StartBrief.Limit + 50), "a1b2c3d4"));

        Assert.NotNull(said);
        Assert.EndsWith($"({StartBrief.Limit + 50} chars)", said);
        Assert.StartsWith(new string('x', StartBrief.Limit), said);
    }

    [Fact]
    public void Words_that_continue_nothing_and_events_that_are_not_user_words_are_no_brief()
    {
        Assert.Null(StartBrief.Of(User("The quest's words", null)));
        Assert.Null(StartBrief.Of(new SessionEvent { Kind = SessionEventKind.Message, Text = "Done.", Continues = "a1b2c3d4" }));
    }
}
