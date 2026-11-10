using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>A repository's drive, hold and own-tree switches as their presses' cards show them: refused with no card where it is not registered, and the command that sets it.</summary>
public sealed class PlaceSwitchViewTests
{
    private static StubGrounds Grounds()
    {
        var grounds = new StubGrounds();
        grounds.Placed["engine"] = "work";
        grounds.Placed["my engine"] = "work";
        return grounds;
    }

    private static Task<PressViewed> View(PlaceSwitchView view, object payload) =>
        view.ViewAsync(Grounds(), JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    [Theory]
    [InlineData(true, "place-drive", "daoris place drive engine")]
    [InlineData(false, "place-undrive", "daoris place undrive engine")]
    public async Task Driving_is_shown_with_its_command(bool drivable, string kind, string command)
    {
        var viewed = await View(PlaceSwitchView.Drivable(), new { repository = "engine", drivable });

        Assert.Equal((kind, command), (viewed.View!.Kind, viewed.View.Command));
        Assert.Equal("engine", viewed.View.Facts["repository"]);
    }

    [Theory]
    [InlineData(true, "place-hold", "daoris place hold engine")]
    [InlineData(false, "place-resume", "daoris place resume engine")]
    public async Task A_hold_and_its_release_are_shown_with_their_commands(bool held, string kind, string command)
    {
        var viewed = await View(PlaceSwitchView.Hold(), new { repository = "engine", held });

        Assert.Equal((kind, command), (viewed.View!.Kind, viewed.View.Command));
    }

    [Fact]
    public async Task A_repository_is_spelled_for_any_shell()
    {
        var viewed = await View(PlaceSwitchView.Hold(), new { repository = "my engine", held = true });

        Assert.Equal("daoris place hold \"my engine\"", viewed.View!.Command);
    }

    [Fact]
    public async Task A_repository_not_registered_here_is_refused_at_the_ask_with_no_card()
    {
        var viewed = await View(PlaceSwitchView.Drivable(), new { repository = "ghost", drivable = true });

        Assert.Null(viewed.View);
        Assert.Contains("`ghost` is not registered on this machine", viewed.Refusal);
        Assert.Contains("names the repository", (await View(PlaceSwitchView.Hold(), new { held = true })).Refusal);
    }
}
