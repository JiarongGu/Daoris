using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A folder department's excludes change, as its press's card shows it: judged against the registry as the route takes it, refused before
/// any card where the name is no registered folder or nothing would change, and shown with what is added, taken back and left out then.
/// </summary>
public sealed class FolderExcludesViewTests
{
    private static StubGrounds Registry()
    {
        var grounds = new StubGrounds();
        grounds.Placed["engine"] = "default";
        grounds.Folders["scratch"] = ["build/**"];
        return grounds;
    }

    private static Task<PressViewed> View(object payload) =>
        new FolderExcludesView().ViewAsync(Registry(), JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    [Fact]
    public async Task An_edit_is_shown_with_what_it_adds_takes_back_and_leaves_out_and_the_command_it_is()
    {
        var viewed = await View(new { repository = "Scratch", add = new[] { "*.log" }, remove = new[] { "build/**" }, said = true });

        Assert.Null(viewed.Refusal);
        Assert.Equal("folder-excludes", viewed.View!.Kind);
        Assert.Equal(("scratch", "+ *.log, - build/**", "*.log"), (viewed.View.Facts["repository"], viewed.View.Facts["change"], viewed.View.Facts["now"]));
        Assert.Equal("daoris repository excludes scratch --add \"*.log\" --remove \"build/**\"", viewed.View.Command);
    }

    [Fact]
    public async Task An_edit_that_leaves_nothing_of_the_folders_own_is_shown_as_that_kind()
    {
        var viewed = await View(new { repository = "scratch", remove = new[] { "build/**" } });

        Assert.Equal("folder-excludes-none", viewed.View!.Kind);
        Assert.False(viewed.View.Facts.ContainsKey("now"));
        Assert.Equal("daoris repository excludes scratch --remove \"build/**\"", viewed.View.Command);
    }

    [Theory]
    [InlineData("nowhere", "*.log", "`nowhere` is not registered on this machine — use a folder's name as Repositories lists it.")]
    [InlineData("engine", "*.log", "`engine` is not a folder: excludes are a folder department's.")]
    [InlineData("scratch", "build/**", "`scratch` already leaves out exactly that, so there is nothing to change.")]
    [InlineData("", "*.log", "an excludes change names the folder, as Repositories lists it.")]
    [InlineData("scratch", " ", "an excludes change names a glob to add or to take back in `scratch`.")]
    public async Task An_edit_the_route_would_change_nothing_by_is_refused_in_its_words(string repository, string add, string words)
    {
        var viewed = await View(new { repository, add = new[] { add } });

        Assert.Null(viewed.View);
        Assert.Equal(words, viewed.Refusal);
    }

    [Fact]
    public void The_route_s_view_is_found_by_its_module_and_route()
    {
        Assert.IsType<FolderExcludesView>(PressViews.Find("daoris.driver", "REGISTRY_EXCLUDES"));
    }
}
