using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A repository's move to a workspace, as its press's card shows it: judged against the registry as the route takes it, refused before
/// any card where the route would change nothing, and shown with the move, whether it starts the workspace, and the command it is.
/// </summary>
public sealed class RegistryRewireViewTests
{
    private static StubGrounds Registry()
    {
        var grounds = new StubGrounds();
        grounds.Placed["engine"] = "default";
        grounds.Placed["tools"] = "work";
        return grounds;
    }

    private static Task<PressViewed> View(StubGrounds grounds, object payload) =>
        new RegistryRewireView().ViewAsync(grounds, JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    [Fact]
    public async Task A_move_to_a_workspace_in_use_is_shown_with_both_workspaces_and_its_command()
    {
        var viewed = await View(Registry(), new { repository = "Engine", workspace = "work", said = true });

        Assert.Null(viewed.Refusal);
        Assert.Equal("repository-move", viewed.View!.Kind);
        Assert.Equal("engine", viewed.View.Facts["repository"]);
        Assert.Equal("default", viewed.View.Facts["from"]);
        Assert.Equal("work", viewed.View.Facts["to"]);
        Assert.Equal("daoris repository wire engine --workspace work", viewed.View.Command);
    }

    [Fact]
    public async Task A_move_to_a_workspace_no_repository_is_in_says_it_starts_it_and_quotes_a_spaced_name()
    {
        var viewed = await View(Registry(), new { repository = "engine", workspace = "my team" });

        Assert.Equal("repository-move-starts", viewed.View!.Kind);
        Assert.Equal("my team", viewed.View.Facts["to"]);
        Assert.Equal("daoris repository wire engine --workspace \"my team\"", viewed.View.Command);
    }

    [Theory]
    [InlineData("nowhere", "work", "`nowhere` is not registered on this machine — use a repository's name as Repositories lists it.")]
    [InlineData("tools", "Work", "`tools` is already in workspace `work`, so there is nothing to move.")]
    [InlineData("", "work", "a move names the repository, as Repositories lists it.")]
    [InlineData("engine", " ", "a move names the workspace to move `engine` to.")]
    public async Task A_move_the_route_would_change_nothing_by_is_refused_in_its_words(string repository, string workspace, string words)
    {
        var viewed = await View(Registry(), new { repository, workspace });

        Assert.Null(viewed.View);
        Assert.Equal(words, viewed.Refusal);
    }

    [Fact]
    public void The_route_s_view_is_found_by_its_module_and_route()
    {
        Assert.IsType<RegistryRewireView>(PressViews.Find("daoris.driver", "REGISTRY_REWIRE"));
        Assert.Null(PressViews.Find("DAORIS.DRIVER", "REGISTRY_RETIRE"));
    }
}
