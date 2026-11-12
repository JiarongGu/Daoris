using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// The growth switch on the two views that open a place: a department's and a room's card read the plan the route's own code made,
/// whose <c>growth</c> says the switch and whose refusals carry the route's words where it is off. <c>off</c> is refused at the ask in those words with
/// no card; <c>asked</c> gives the same card restricted mode shows with one more line saying the switch asks for it here; <c>mode</c> adds nothing.
/// </summary>
public sealed class GrowthViewTests
{
    private const string OffOpening = "Opening repositories in `aurora` is off (the Settings of the workspace `aurora`, row New repositories and rooms).";
    private const string OffRoom = "Opening rooms in `engine` is off (the Settings of `engine`, row New repositories and rooms).";

    private static object[] Refusals(string? words) => words is null ? [] : [new { code = "growth-off", sentence = words }];

    private static StubGrounds CreatePlan(string growth, string? refusal)
    {
        var grounds = new StubGrounds();
        grounds.Reads["REGISTRY_CREATE_PLAN"] = _ => new
        {
            workspace = "aurora",
            folder = "/work/aurora/ledger",
            line = "main",
            author = new { name = "Fixture", email = "fixture@example.test" },
            steps = new object[] { new { id = "folder", said = "make the folder /work/aurora/ledger" } },
            refusals = Refusals(refusal),
            applies = refusal is null,
            digest = new string('a', 64),
            growth,
            quests = Array.Empty<object>(),
        };
        return grounds;
    }

    private static StubGrounds RoomPlan(string growth, string? refusal)
    {
        var grounds = new StubGrounds();
        grounds.Reads["PLACE_PLAN"] = _ => new
        {
            repositories = new[] { new { repository = "engine", workspace = "aurora" } },
            changes = new object[] { new { change = new { key = "ROOM_PRESS", part = "room", said = "a room at `tools/web` asked of its repository's own session", before = "no room", after = "asked" }, repositories = new[] { "engine" } } },
            refusals = refusal is null ? Array.Empty<object>() : [new { repository = "engine", part = "room", code = "growth-off", sentence = refusal }],
            digest = new string('b', 64),
            growth,
        };
        return grounds;
    }

    private static Task<PressViewed> Created(StubGrounds grounds) =>
        new RegistryCreateView().ViewAsync(grounds, JsonSerializer.SerializeToElement(new { name = "ledger", purpose = "keeps the books", workspace = "aurora" }), CancellationToken.None);

    private static Task<PressViewed> Roomed(StubGrounds grounds) =>
        new PlaceApplyView().ViewAsync(grounds, JsonSerializer.SerializeToElement(new { repositories = new[] { "engine" }, room = "tools/web", purpose = "the web app needs its own conventions" }), CancellationToken.None);

    [Fact]
    public async Task A_department_at_mode_shows_the_card_it_showed_with_no_line_for_the_switch()
    {
        var viewed = await Created(CreatePlan("mode", null));

        Assert.Equal("repository-create", viewed.View!.Kind);
        Assert.DoesNotContain(viewed.View.Also!, said => said.Kind == "growth-asked");
    }

    [Fact]
    public async Task A_department_at_asked_shows_the_same_card_with_one_more_line_naming_the_workspace_the_switch_asks_for()
    {
        var viewed = await Created(CreatePlan("asked", null));

        var line = Assert.Single(viewed.View!.Also!, said => said.Kind == "growth-asked");
        Assert.Equal("aurora", line.Facts["place"]);
        Assert.Equal("repository-create", viewed.View.Kind);
        Assert.Contains(viewed.View.Also!, said => said.Kind == "repository-create-step-folder");
    }

    [Fact]
    public async Task A_department_at_off_is_refused_at_the_ask_in_the_routes_words_with_no_card()
    {
        var viewed = await Created(CreatePlan("off", OffOpening));

        Assert.Null(viewed.View);
        Assert.Equal(OffOpening, viewed.Refusal);
    }

    [Fact]
    public async Task A_room_at_mode_shows_the_card_it_showed_with_no_line_for_the_switch()
    {
        var viewed = await Roomed(RoomPlan("mode", null));

        Assert.Equal("place-room", viewed.View!.Kind);
        Assert.DoesNotContain(viewed.View.Also!, said => said.Kind == "growth-asked");
    }

    [Fact]
    public async Task A_room_at_asked_shows_the_same_card_with_one_more_line_naming_the_repository_the_switch_asks_for()
    {
        var viewed = await Roomed(RoomPlan("asked", null));

        var line = Assert.Single(viewed.View!.Also!, said => said.Kind == "growth-asked");
        Assert.Equal("engine", line.Facts["place"]);
        Assert.Contains(viewed.View.Also!, said => said.Kind == "place-room-quest");
    }

    [Fact]
    public async Task A_room_at_off_is_refused_at_the_ask_in_the_routes_words_with_no_card()
    {
        var viewed = await Roomed(RoomPlan("off", OffRoom));

        Assert.Null(viewed.View);
        Assert.Equal(OffRoom, viewed.Refusal);
    }
}
