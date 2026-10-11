using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// BRWHINT1b (D78): a quest's session and an intake are told the browser they were handed, in BRWHINT1a's one line, as a block
/// of their own, and say nothing where none was handed.
/// </summary>
public sealed class DrivenBrowserToldTests
{
    private static readonly string Line = InAppBrowserServers.Told(["browser"])!;

    [Theory]
    [InlineData("bare")]
    [InlineData("full")]
    [InlineData("carry-on")]
    [InlineData("set-up")]
    public void A_quest_handed_a_browser_is_told_it_as_a_block_of_its_own(string which)
    {
        var told = TargetPrompt.Compose(TargetPromptGoldenTests.Of(which) with { Browser = Line });
        Assert.Contains($"\n{Line}\n", told);
        Assert.Equal(1, told.Split(Line).Length - 1);

        var composed = TargetPrompt.Composed(TargetPromptGoldenTests.Of(which) with { Browser = Line });
        Assert.Equal(told, composed.Text);
        Assert.Equal(told.Length, composed.Account.Chars);
    }

    [Theory]
    [InlineData("bare")]
    [InlineData("set-up")]
    public void A_quest_handed_none_says_nothing_of_one(string which)
    {
        Assert.DoesNotContain("handed you its own browser", TargetPrompt.Compose(TargetPromptGoldenTests.Of(which)));
    }

    [Fact]
    public void An_intake_handed_a_browser_is_told_it_and_one_handed_none_is_not()
    {
        var ask = IntakePromptGoldenTests.Bare;
        var told = IntakePrompt.Compose(ask, browser: Line);
        Assert.Contains($"\n{Line}\n", told);
        Assert.DoesNotContain("handed you its own browser", IntakePrompt.Compose(ask));
        Assert.Equal(IntakePrompt.Compose(ask), IntakePrompt.Compose(ask, browser: null));
    }
}
