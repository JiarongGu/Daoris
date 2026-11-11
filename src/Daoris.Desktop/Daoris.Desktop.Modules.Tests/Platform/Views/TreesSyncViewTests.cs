using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Bringing repositories up to date as its press's card shows it: the rows its list moves, read from the list the press rests on,
/// refused at the ask where the scope names nothing here, and the command the press is.
/// </summary>
public sealed class TreesSyncViewTests
{
    private static StubGrounds Listing(bool moves = true)
    {
        var grounds = new StubGrounds();
        grounds.Placed["engine"] = "work";
        grounds.Placed["game"] = "my team";
        grounds.Registered.Add("engine");
        grounds.Reads["TREES_SYNC_PLAN"] = _ => new
        {
            lines = new[] { new { repository = "engine", line = "main", kind = "behind", commits = 3, moves } },
            rebases = new[]
            {
                new { repository = "engine", branch = "daoris/q1", kind = "behind", commits = 2, replays = moves },
                new { repository = "engine", branch = "daoris/q2", kind = "diverged", commits = 1, replays = false },
            },
            deletes = new[] { new { repository = "engine", branch = "daoris/q0", kind = "landed", commits = 0, removable = moves } },
            looked = Array.Empty<object>(), apart = Array.Empty<object>(),
        };
        return grounds;
    }

    private static Task<PressViewed> View(StubGrounds grounds, object payload) =>
        new TreesSyncView().ViewAsync(grounds, JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    [Fact]
    public async Task A_repositorys_sync_says_each_row_that_moves_and_the_command_it_is()
    {
        var viewed = await View(Listing(), new { repository = "engine", fetch = true, follow = true });

        Assert.Equal("sync-repository", viewed.View!.Kind);
        Assert.Equal("engine", viewed.View.Facts["repository"]);
        Assert.Equal("daoris tree sync --repository engine --yes", viewed.View.Command);
        Assert.Equal(["sync-line", "sync-replay", "sync-delete"], viewed.View.Also!.Select(said => said.Kind));
        Assert.Equal("3", viewed.View.Also![0].Facts["commits"]);
    }

    [Fact]
    public async Task A_workspaces_and_every_repositorys_are_their_own_sentences_and_a_list_with_nothing_moving_says_so()
    {
        var workspace = await View(Listing(), new { workspace = "my team" });
        // Without --all the sync takes the repositories holding a branch of Daoris's, as the route does; the headline says so.
        Assert.Equal("sync-workspace-held", workspace.View!.Kind);
        Assert.Equal("sync-workspace", (await View(Listing(), new { workspace = "my team", all = true })).View!.Kind);
        Assert.Equal("sync-held", (await View(Listing(), new { })).View!.Kind);
        Assert.Equal("daoris tree sync --workspace \"my team\" --yes", workspace.View.Command);

        var every = await View(Listing(moves: false), new { all = true });
        Assert.Equal("sync-all", every.View!.Kind);
        Assert.Equal("daoris tree sync --all --yes", every.View.Command);
        Assert.Equal(["sync-nothing"], every.View.Also!.Select(said => said.Kind));
    }

    [Fact]
    public async Task A_repository_not_registered_here_is_refused_at_the_ask_with_no_card()
    {
        Assert.Contains("`nope` is not registered on this machine", (await View(Listing(), new { repository = "nope" })).Refusal);
        Assert.Contains("there is no workspace `elsewhere` on this machine", (await View(Listing(), new { workspace = "elsewhere" })).Refusal);
    }

    [Fact]
    public void It_is_the_view_of_bringing_up_to_date()
    {
        Assert.IsType<TreesSyncView>(PressViews.Find(DriverModule.Name, "TREES_SYNC"));
    }
}
