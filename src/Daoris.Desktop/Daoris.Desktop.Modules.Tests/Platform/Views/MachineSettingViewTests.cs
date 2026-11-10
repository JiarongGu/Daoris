using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>The machine's four dials as their presses' cards show them: a value the route would refuse is refused at the ask in the driver's words, one it takes is shown with its command.</summary>
public sealed class MachineSettingViewTests
{
    private static Task<PressViewed> View(MachineSettingView view, object payload) =>
        view.ViewAsync(new StubGrounds(), JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    [Theory]
    [InlineData(true, "notify-on", "daoris settings notify on")]
    [InlineData(false, "notify-off", "daoris settings notify off")]
    public async Task Notifying_is_shown_with_its_command(bool notify, string kind, string command)
    {
        var viewed = await View(MachineSettingView.Notify(), new { notify });

        Assert.Equal((kind, command), (viewed.View!.Kind, viewed.View.Command));
    }

    [Fact]
    public async Task The_session_cap_is_shown_with_its_count()
    {
        var viewed = await View(MachineSettingView.Cap(), new { cap = 3 });

        Assert.Equal(("cap", "daoris settings cap 3"), (viewed.View!.Kind, viewed.View.Command));
        Assert.Equal("3", viewed.View.Facts["sessions"]);
        Assert.Equal("cap-one", (await View(MachineSettingView.Cap(), new { cap = 1 })).View!.Kind);
    }

    [Theory]
    [InlineData("""{"cap":0}""")]
    [InlineData("""{}""")]
    public async Task A_cap_below_one_or_none_is_refused_with_no_card(string json)
    {
        var viewed = await MachineSettingView.Cap().ViewAsync(new StubGrounds(), JsonDocument.Parse(json).RootElement, CancellationToken.None);

        Assert.Null(viewed.View);
        Assert.Contains("session", viewed.Refusal);
    }

    [Fact]
    public async Task The_cool_off_is_shown_in_minutes_and_a_zero_is_refused()
    {
        var shown = await View(MachineSettingView.CoolOff(), new { minutes = 45 });

        Assert.Equal(("cooloff", "daoris settings cooloff 45"), (shown.View!.Kind, shown.View.Command));
        Assert.Null((await View(MachineSettingView.CoolOff(), new { minutes = 0 })).View);
    }

    [Fact]
    public async Task The_quiet_wait_is_judged_by_its_own_bounds()
    {
        var shown = await View(MachineSettingView.Quiet(), new { minutes = 20 });

        Assert.Equal(("quiet", "daoris settings quiet 20"), (shown.View!.Kind, shown.View.Command));
        var refused = await View(MachineSettingView.Quiet(), new { minutes = 1 });
        Assert.Null(refused.View);
        Assert.Contains("minutes", refused.Refusal);
    }
}
