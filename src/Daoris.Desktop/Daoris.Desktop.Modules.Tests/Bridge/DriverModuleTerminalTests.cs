using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// <c>SET_TERMINAL</c>, the window's door onto <c>driver.json</c>'s <c>terminal</c>, the tier of a terminal's presses: answered with the
/// state, set to <c>ask</c> or <c>act</c>, and any other tier refused with the file unchanged.
/// </summary>
public sealed class DriverModuleTerminalTests : DriverModuleBridge
{
    [Fact]
    public async Task The_default_ask_is_answered_with_the_state_and_act_is_set_and_written()
    {
        Assert.Equal(TerminalTiers.Ask, (await AnswerAsync(Module(), "STATE")).GetProperty("terminal").GetString());

        var state = await AnswerAsync(Module(), "SET_TERMINAL", new { tier = "act" });

        Assert.Equal(TerminalTiers.Act, state.GetProperty("terminal").GetString());
        Assert.Equal(TerminalTiers.Act, DriverConfig.Load(DriverConfigPath).Terminal);
        Assert.Equal(TerminalTiers.Ask, (await AnswerAsync(Module(), "SET_TERMINAL", new { tier = "ask" })).GetProperty("terminal").GetString());
    }

    [Theory]
    [InlineData("always")]
    [InlineData(null)]
    public async Task Any_other_tier_is_refused_and_nothing_is_written(string? tier)
    {
        await AnswerAsync(Module(), "SET_TERMINAL", new { tier = "act" });

        Assert.StartsWith(Refusals.DriverRefused, await RefusalAsync(Module(), "SET_TERMINAL", new { tier }));
        Assert.Equal(TerminalTiers.Act, DriverConfig.Load(DriverConfigPath).Terminal);
    }
}
