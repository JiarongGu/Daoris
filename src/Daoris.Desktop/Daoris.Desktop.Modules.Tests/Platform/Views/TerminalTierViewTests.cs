using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>The terminal's tier as its press's card shows it: <c>act</c> and <c>ask</c> each with its command, any other tier refused at the ask in the driver's words.</summary>
public sealed class TerminalTierViewTests
{
    private static Task<PressViewed> View(object payload) =>
        MachineSettingView.Terminal().ViewAsync(new StubGrounds(), JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    [Theory]
    [InlineData(TerminalTiers.Act, "terminal-act", "daoris settings terminal act")]
    [InlineData(TerminalTiers.Ask, "terminal-ask", "daoris settings terminal ask")]
    public async Task Each_tier_is_shown_with_its_command(string tier, string kind, string command)
    {
        var viewed = await View(new { tier });

        Assert.Equal(("SET_TERMINAL", kind, command), (MachineSettingView.Terminal().Route, viewed.View!.Kind, viewed.View.Command));
    }

    [Fact]
    public async Task Another_tier_is_refused_with_no_card()
    {
        var viewed = await View(new { tier = "always" });

        Assert.Null(viewed.View);
        Assert.Equal(TerminalTiers.Refusal, viewed.Refusal);
    }
}
