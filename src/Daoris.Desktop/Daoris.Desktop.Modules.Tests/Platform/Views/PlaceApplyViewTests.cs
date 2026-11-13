using System.Text.Json;
using Daoris.Driver;

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
        repositories = new object[]
        {
            new { repository = "engine", workspace = "aurora", declaration = new { acceptance = new { standing = "unaccepted" } } },
            new { repository = "site", workspace = "aurora" },
        },
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
        Assert.Equal("daoris place engine site --preset merge-after-accept --adoption --apply --accept engine", viewed.View.Command);
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

    [Theory]
    [InlineData("site")]
    [InlineData("Site")]
    [InlineData("ghost")]
    public async Task An_acceptance_the_plan_does_not_offer_is_refused_at_the_ask_as_the_apply_refuses_it(string repository)
    {
        var viewed = await View(Planned(Plan([])), new { repositories = new[] { "engine", "site" }, preset = "keep", accept = new[] { new { repository } } });

        Assert.Null(viewed.View);
        Assert.Equal($"the plan offers no acceptance for `{repository}`'s declaration, so nothing was written.", viewed.Refusal);
    }

    /// <summary>A room is the same apply, judged by the same plan; its card is its own sentence, naming the room and the reason and the first lines of the quest it asks.</summary>
    [Fact]
    public async Task A_rooms_apply_asks_the_plan_for_the_room_and_has_a_card_of_its_own_with_the_quest_it_asks_and_the_command_it_is()
    {
        var asked = new List<JsonElement>();
        var grounds = Planned(Plan([new { change = new { key = "ROOM_PRESS", part = "room", said = "a room at `tools/web` asked of its repository's own session", before = "no room", after = "asked" }, repositories = new[] { "engine" } }]));
        var plan = grounds.Reads["PLACE_PLAN"];
        grounds.Reads["PLACE_PLAN"] = payload =>
        {
            asked.Add(payload!.Value);
            return plan(payload);
        };

        var viewed = await View(grounds, new { repositories = new[] { "engine" }, room = "tools/web", purpose = "the web app needs its own conventions", said = true });

        var sent = Assert.Single(asked);
        Assert.Equal(("tools/web", "the web app needs its own conventions"), (sent.GetProperty("room").GetString(), sent.GetProperty("purpose").GetString()));
        Assert.Equal("place-room", viewed.View!.Kind);
        Assert.Equal(
            ("engine", "aurora", "tools/web", "the web app needs its own conventions"),
            (viewed.View.Facts["repositories"], viewed.View.Facts["workspace"], viewed.View.Facts["room"], viewed.View.Facts["purpose"]));
        Assert.False(viewed.View.Facts.ContainsKey("preset"));
        var quest = Assert.Single(viewed.View.Also!);
        Assert.Equal("place-room-quest", quest.Kind);
        Assert.Equal((RoomBrief.TitleOf("tools/web"), QuestHeads.Plain(RoomBrief.Opening("tools/web", "the web app needs its own conventions"))), (quest.Facts["title"], quest.Facts["head"]));
        Assert.Equal("daoris place engine --room tools/web --purpose \"the web app needs its own conventions\" --apply", viewed.View.Command);
    }

    /// <summary>The quest is the one the plan would ask, so the path and the reason are the plan's: as the repository keeps them.</summary>
    [Fact]
    public async Task A_rooms_quest_is_headed_by_the_path_and_reason_as_the_plan_keeps_them_and_a_repository_not_driven_says_it_starts_alone()
    {
        var grounds = Planned(Plan(
        [
            new { change = new { key = "ROOM_PRESS", part = "room", said = "a room at `tools/web` asked of its repository's own session", before = "no room", after = "asked" }, repositories = new[] { "engine" } },
            new { change = new { key = "ROOM_PRESS", part = "room", said = "what starts here while general driving stays off", before = "nothing", after = "its room quest alone" }, repositories = new[] { "engine" } },
        ]));

        var viewed = await View(grounds, new { repositories = new[] { "engine" }, room = "tools\\web\\", purpose = "the web app\n needs its own conventions", said = true });

        Assert.Equal(["place-room-alone", "place-room-quest"], viewed.View!.Also!.Select(said => said.Kind));
        Assert.Equal(QuestHeads.Plain(RoomBrief.Opening("tools/web", "the web app needs its own conventions")), viewed.View.Also![1].Facts["head"]);
    }

    /// <summary>The route's own refusals reach the person at the ask in the route's words, with no card: the view adds none of its own.</summary>
    [Fact]
    public async Task A_rooms_apply_the_plan_refuses_is_refused_at_the_ask_in_the_plans_words()
    {
        var plan = Plan([], [new { repository = "engine", part = "room", code = "room-exists", sentence = "`engine` already has a room at `tools/web` (`daoris.json`'s `rooms`)." }]);

        var viewed = await View(Planned(plan), new { repositories = new[] { "engine" }, room = "tools/web", purpose = "why" });

        Assert.Null(viewed.View);
        Assert.Equal("`engine` already has a room at `tools/web` (`daoris.json`'s `rooms`).", viewed.Refusal);
    }

    [Fact]
    public void It_is_the_view_of_the_apply()
    {
        Assert.IsType<PlaceApplyView>(PressViews.Find(DriverModule.Name, "PLACE_APPLY"));
    }
}
