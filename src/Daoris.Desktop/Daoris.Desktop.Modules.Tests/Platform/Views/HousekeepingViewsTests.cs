using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Housekeeping's presses as their cards show them: the clean-up's rows its list clears to go and the command it is, a workspace this
/// machine lacks refused at the ask; a session branch's discard by its name, its repository and whether it is forced.
/// </summary>
public sealed class HousekeepingViewsTests
{
    private static StubGrounds Listing(bool removable = true)
    {
        var grounds = new StubGrounds();
        grounds.Placed["engine"] = "my team";
        grounds.Reads["SWEEP_PLAN"] = _ => new
        {
            branches = new[]
            {
                new { repository = "engine", branch = "daoris/s-1", removable },
                new { repository = "engine", branch = "daoris/s-2", removable = false },
            },
            landed = new[] { new { repository = "engine", branch = "quest/q1", removable } },
        };
        return grounds;
    }

    private static Task<PressViewed> View(IPressView view, StubGrounds grounds, object payload) =>
        view.ViewAsync(grounds, JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    [Fact]
    public async Task A_clean_up_says_each_branch_cleared_to_go_and_the_command_it_is()
    {
        var every = await View(new SweepView(), Listing(), new { });
        Assert.Equal("clean-up", every.View!.Kind);
        Assert.Equal("daoris tree clean --yes", every.View.Command);
        Assert.Equal(["daoris/s-1", "quest/q1"], every.View.Also!.Select(said => said.Facts["branch"]));

        var workspace = await View(new SweepView(), Listing(), new { workspace = "my team", only = new[] { "engine:quest/q1" } });
        Assert.Equal("clean-up-workspace", workspace.View!.Kind);
        Assert.Equal("daoris tree clean --workspace \"my team\" --yes", workspace.View.Command);
        Assert.Equal(["quest/q1"], workspace.View.Also!.Select(said => said.Facts["branch"]));

        Assert.Equal(["clean-up-nothing"], (await View(new SweepView(), Listing(removable: false), new { })).View!.Also!.Select(said => said.Kind));
    }

    [Fact]
    public async Task A_workspace_this_machine_lacks_is_refused_at_the_ask_with_no_card()
    {
        Assert.Contains("there is no workspace `elsewhere` on this machine", (await View(new SweepView(), Listing(), new { workspace = "elsewhere" })).Refusal);
    }

    [Fact]
    public async Task A_discard_names_its_branch_its_repository_and_whether_it_is_forced()
    {
        var verb = await View(new SessionBranchDiscardView(), Listing(), new { named = "s-1", repository = "engine", force = true });
        Assert.Equal("discard-branch-in", verb.View!.Kind);
        Assert.Equal("daoris tree remove s-1 --repository engine --force", verb.View.Command);
        Assert.Equal(["discard-forced"], verb.View.Also!.Select(said => said.Kind));

        var page = await View(new SessionBranchDiscardView(), Listing(), new { branch = "daoris/s-2" });
        Assert.Equal("discard-branch", page.View!.Kind);
        Assert.Equal("daoris/s-2", page.View.Facts["branch"]);
        Assert.Null(page.View.Also);

        Assert.NotNull((await View(new SessionBranchDiscardView(), Listing(), new { repository = "engine" })).Refusal);
    }

    [Fact]
    public void They_are_the_views_of_the_clean_up_and_the_discard()
    {
        Assert.IsType<SweepView>(PressViews.Find(DriverModule.Name, "SWEEP"));
        Assert.IsType<SessionBranchDiscardView>(PressViews.Find(DriverModule.Name, "DISCARD_SESSION_BRANCH"));
    }
}
