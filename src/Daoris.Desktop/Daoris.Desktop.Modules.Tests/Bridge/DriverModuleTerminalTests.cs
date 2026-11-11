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

    [Fact]
    public async Task Each_change_is_recorded_as_the_window_s_newest_first_and_answered_with_the_state_and_a_change_to_the_same_tier_is_none()
    {
        await AnswerAsync(Module(), "SET_TERMINAL", new { tier = "act" });
        await AnswerAsync(Module(), "SET_TERMINAL", new { tier = "act" });
        var state = await AnswerAsync(Module(), "SET_TERMINAL", new { tier = "ask" });

        var changes = state.GetProperty("terminalActs").GetProperty("changes");
        Assert.Equal(
            [(TerminalTiers.Ask, TerminalTierBy.Window), (TerminalTiers.Act, TerminalTierBy.Window)],
            changes.EnumerateArray().Select(each => (each.GetProperty("tier").GetString(), each.GetProperty("by").GetString())).ToArray());
        Assert.Equal(2, new TerminalActs(DaorisHome.Entry(Home, HomeEntry.TerminalActs)).Now().Changes.Count);
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
