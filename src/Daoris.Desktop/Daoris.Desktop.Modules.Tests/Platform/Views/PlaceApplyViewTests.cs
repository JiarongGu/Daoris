using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// A place's set-up applied as its press's card shows it (<c>PLACE_APPLY</c>): the repositories and their workspace, each change the plan
/// makes (<c>PLACE_PLAN</c>, the press's own ground) said once with the repositories it is made at, what the ask carries, and the command
/// it is; a plan its own rules refuse is refused at the ask in their words, with no card.
/// </summary>
public sealed class PlaceApplyViewTests
{
    private static readonly object[] NoRefusals = [];

    private static StubGrounds Planned(object plan)
    {
        var grounds = new StubGrounds();
        grounds.Reads["PLACE_PLAN"] = _ => plan;
        return grounds;
    }

    private static Task<PressViewed> View(StubGrounds grounds, object payload) =>
        new PlaceApplyView().ViewAsync(grounds, JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    private static object Plan(object[] changes, object[]? refusals = null) => new
    {
        repositories = new[] { new { repository = "engine", workspace = "aurora" }, new { repository = "site", workspace = "aurora" } },
        changes, refusals = refusals ?? NoRefusals, digest = "abc",
    };

    [Fact]
    public async Task A_places_apply_says_each_change_its_plan_makes_and_the_command_it_is()
    {
        var plan = Plan([new { change = new { key = "drive", part = "drive", said = "Driving", before = "off", after = "on" }, repositories = new[] { "engine", "site" } }]);

        var viewed = await View(Planned(plan), new
        {
            repositories = new[] { "engine", "site" }, preset = "merge-after-accept", adoption = true, digest = "abc",
            accept = new[] { new { repository = "engine", purpose = "integration" } }, said = true,
        });

        Assert.Equal("place-apply", viewed.View!.Kind);
        Assert.Equal(("engine, site", "aurora", "merge-after-accept"),
            (viewed.View.Facts["repositories"], viewed.View.Facts["workspace"], viewed.View.Facts["preset"]));
        Assert.Equal(["place-change", "place-adoption", "place-accept"], viewed.View.Also!.Select(said => said.Kind));
        var change = viewed.View.Also![0].Facts;
        Assert.Equal(("Driving", "off", "on", "engine, site"), (change["said"], change["before"], change["after"], change["repositories"]));
        Assert.Equal("daoris place engine site --preset merge-after-accept --adoption --apply --accept engine:integration", viewed.View.Command);
    }

    [Fact]
    public async Task A_plan_that_changes_nothing_is_said_so()
    {
        var viewed = await View(Planned(Plan([])), new { repositories = new[] { "engine" }, preset = "keep", said = true });

        Assert.Equal(["place-nothing"], viewed.View!.Also!.Select(said => said.Kind));
        Assert.Equal("daoris place engine --preset keep --apply", viewed.View.Command);
    }

    [Fact]
    public async Task A_plan_its_rules_refuse_is_refused_at_the_ask_in_their_words()
    {
        var plan = Plan([], [new { repository = "site", part = "preset", code = "PLACE_UNKNOWN", sentence = "`site` is not registered here." }]);

        Assert.Equal("`site` is not registered here.", (await View(Planned(plan), new { repositories = new[] { "site" }, preset = "keep" })).Refusal);
        Assert.Contains("`repositories`", (await View(Planned(Plan([])), new { repository = "site", preset = "keep" })).Refusal);
    }

    [Fact]
    public void It_is_the_view_of_the_apply()
    {
        Assert.IsType<PlaceApplyView>(PressViews.Find(DriverModule.Name, "PLACE_APPLY"));
    }
}
