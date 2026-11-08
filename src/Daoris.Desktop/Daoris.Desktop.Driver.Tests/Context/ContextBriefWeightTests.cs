using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>
/// What a brief's weight counts: every character a token spends more than a Latin one on weighs as a Han character does, so a brief
/// of kana, Hangul, fullwidth forms or emoji is cut to the bound as a Han one is, never past what it leaves the next agent.
/// </summary>
public sealed class ContextBriefWeightTests
{
    [Theory]
    [InlineData("あ")]
    [InlineData("カ")]
    [InlineData("한")]
    [InlineData("Ａ")]
    [InlineData("！")]
    [InlineData("😀")]
    [InlineData("𠀀")]
    public void A_character_a_token_spends_more_on_weighs_as_a_Han_character_does(string text)
    {
        Assert.Equal(ContextBrief.Weight("暗"), ContextBrief.Weight(text));
    }

    [Fact]
    public void A_Latin_character_and_a_Han_one_weigh_as_they_did()
    {
        Assert.Equal(1, ContextBrief.Weight("a"));
        Assert.Equal(1, ContextBrief.Weight("é"));
        Assert.Equal(3, ContextBrief.Weight("暗"));
    }

    [Fact]
    public void A_brief_of_emoji_is_cut_within_the_bound_and_never_inside_a_pair()
    {
        var emoji = string.Concat(Enumerable.Repeat("😀", 2_000));
        List<SessionEvent> events = [new() { Kind = SessionEventKind.User, Origin = "person", Text = "go on" }];
        for (var n = 1; n <= 40; n++)
        {
            events.Add(new() { Kind = SessionEventKind.User, Origin = "person", Id = $"w{n}", Text = emoji });
            events.Add(new() { Kind = SessionEventKind.Message, Text = emoji });
            events.Add(new() { Kind = SessionEventKind.Turn, StopReason = "end_turn" });
        }

        var brief = ContextBrief.Compose(new BriefLine(HandOverWhy.Press, [new BriefPart("c0nv0001", "claude-code", events)]) { Conversation = true });

        Assert.True(ContextBrief.Weight(brief.Text) <= ContextBrief.Bound);
        Assert.DoesNotContain('�', brief.Text);
        Assert.All(brief.Text.Where(char.IsSurrogate).Chunk(2), pair => Assert.True(char.IsHighSurrogate(pair[0]) && char.IsLowSurrogate(pair[1])));
    }
}
