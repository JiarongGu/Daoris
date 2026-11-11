using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// <c>SET_MANIFEST</c>, the window's door onto <c>manifest</c>, the bounds of a folder's manifest read: answered with the state, each
/// bound set alone in its bounds, and refused out of them or with neither or both named, the file unchanged.
/// </summary>
public sealed class DriverModuleManifestTests : DriverModuleBridge
{
    [Fact]
    public async Task The_defaults_are_answered_with_the_state_and_each_bound_is_set_alone()
    {
        var first = await AnswerAsync(Module(), "STATE");
        Assert.Equal((ManifestBounds.Default.Files, 5), (first.GetProperty("manifestFiles").GetInt32(), first.GetProperty("manifestMinutes").GetInt32()));

        await AnswerAsync(Module(), "SET_MANIFEST", new { files = 40000 });
        var state = await AnswerAsync(Module(), "SET_MANIFEST", new { minutes = 3 });

        Assert.Equal((40000, 3), (state.GetProperty("manifestFiles").GetInt32(), state.GetProperty("manifestMinutes").GetInt32()));
        Assert.Equal((40000, 3), (DriverConfig.Load(DriverConfigPath).Manifest.Files, DriverConfig.Load(DriverConfigPath).Manifest.Minutes));
    }

    [Theory]
    [InlineData(999, null)]
    [InlineData(null, 0)]
    [InlineData(null, 61)]
    [InlineData(5000, 3)]
    [InlineData(null, null)]
    public async Task A_value_out_of_its_bounds_or_both_or_neither_is_refused_and_nothing_is_written(int? files, int? minutes)
    {
        Assert.StartsWith(Refusals.DriverRefused, await RefusalAsync(Module(), "SET_MANIFEST", new { files, minutes }));
        Assert.Equal(ManifestSetting.Unset, DriverConfig.Load(DriverConfigPath).Manifest);
    }
}
