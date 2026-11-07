using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// <c>SET_QUIET</c>, the window's door onto <c>quietMinutes</c>, the one quiet setting: answered with the state, set to whole minutes in
/// its bounds, and refused out of them with the file unchanged.
/// </summary>
public sealed class DriverModuleQuietTests : DriverModuleBridge
{
    [Fact]
    public async Task The_default_is_answered_with_the_state_and_set_to_whole_minutes_in_its_bounds()
    {
        Assert.Equal(QuietSetting.Default, (await AnswerAsync(Module(), "STATE")).GetProperty("quiet").GetInt32());

        var state = await AnswerAsync(Module(), "SET_QUIET", new { minutes = 25 });

        Assert.Equal(25, state.GetProperty("quiet").GetInt32());
        Assert.Equal(25, DriverConfig.Load(DriverConfigPath).QuietMinutes);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(121)]
    public async Task A_value_out_of_its_bounds_is_refused_and_nothing_is_written(int minutes)
    {
        await AnswerAsync(Module(), "SET_QUIET", new { minutes = 30 });

        Assert.StartsWith(Refusals.DriverRefused, await RefusalAsync(Module(), "SET_QUIET", new { minutes }));
        Assert.Equal(30, DriverConfig.Load(DriverConfigPath).QuietMinutes);
    }

    [Fact]
    public async Task No_minutes_is_refused()
    {
        Assert.StartsWith(Refusals.DriverRefused, await RefusalAsync(Module(), "SET_QUIET", new { }));
        Assert.Null(DriverConfig.Load(DriverConfigPath).QuietMinutes);
    }
}
