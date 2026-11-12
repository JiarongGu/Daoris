using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// The growth switch on a room: the plan, which the apply, the terminal and the window's view all judge by, reads the
/// repository's own value, else its workspace's. <c>off</c> refuses in the route's words with every other refusal still said; <c>asked</c> and
/// <c>mode</c> ride on the plan for the card and the pipe to read. A repository's value overrides its workspace's either way.
/// </summary>
public sealed class PlaceRoomGrowthTests
{
    private static readonly SetupPresetsTests.Catalog Plugins = new(new SetupPresetsTests.Entry("example.github-pull-request", [HookPoints.Perform]));

    private static readonly DriverConfig Base = (DriverConfig.Empty with { Actors = StaffedRoles.On("claude-code-acp") }).WithDrivable("engine", true);

    private readonly string _home = Path.Combine(AppContext.BaseDirectory, "place-room-growth-" + Guid.NewGuid().ToString("N")[..8]);

    private Task<PlacePlanned> PlanAsync(string file, string repository = "engine", string path = "tools/web")
    {
        var config = DriverConfig.Parse(file) with { Actors = Base.Actors, Drivable = Base.Drivable };
        return PlacePlans.PlanAsync(new PlacePlanTests.World(), config, _home, Plugins, true, PlaceRoomTests.Room(repository, path));
    }

    private static string Sentences(PlacePlanned plan) => string.Join(" | ", plan.Refusals.Select(each => $"{each.Code}: {each.Sentence}"));

    [Theory]
    [InlineData("{}", "mode")]
    [InlineData("""{"workspaceGrowth":{"aurora":"asked"}}""", "asked")]
    [InlineData("""{"growth":{"engine":"asked"}}""", "asked")]
    [InlineData("""{"workspaceGrowth":{"aurora":"off"},"growth":{"engine":"asked"}}""", "asked")]
    [InlineData("""{"workspaceGrowth":{"aurora":"off"},"growth":{"engine":"mode"}}""", "mode")]
    [InlineData("""{"workspaceGrowth":{"aurora":"asked"},"growth":{"web":"off"}}""", "asked")]
    public async Task A_room_that_the_switch_allows_plans_as_it_did_and_carries_the_value_for_the_card(string file, string value)
    {
        var plan = await PlanAsync(file);

        Assert.True(plan.Applies, Sentences(plan));
        Assert.Equal(value, plan.Growth);
    }

    [Theory]
    [InlineData("""{"workspaceGrowth":{"aurora":"off"}}""")]
    [InlineData("""{"workspaceGrowth":{"Aurora":"asked"},"growth":{"ENGINE":"off"}}""")]
    [InlineData("""{"growth":{"engine":"off"}}""")]
    public async Task A_room_the_switch_turns_off_is_refused_in_the_routes_words_naming_the_repository(string file)
    {
        var plan = await PlanAsync(file);

        Assert.False(plan.Applies);
        var refusal = Assert.Single(plan.Refusals);
        Assert.Equal(("engine", "room", "growth-off", "Opening rooms in `engine` is off (the Settings of `engine`, row New repositories and rooms)."),
            (refusal.Repository, refusal.Part, refusal.Code, refusal.Sentence));
        Assert.Equal("off", plan.Growth);
        Assert.Empty(plan.Changes);
        Assert.Contains("Opening rooms in `engine` is off", string.Join('\n', PlaceRooms.Lines(plan, applying: false)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_off_switch_says_every_other_refusal_beside_it()
    {
        var plan = await PlanAsync("""{"workspaceGrowth":{"aurora":"off"}}""", path: "../elsewhere");

        Assert.Equal([PlaceRoomRefusals.Path, PlaceRoomRefusals.Growth], plan.Refusals.Select(each => each.Code));
    }

    [Fact]
    public async Task Another_repositorys_value_is_no_part_of_this_ones_room()
    {
        var plan = await PlanAsync("""{"growth":{"web":"off"},"workspaceGrowth":{"other":"off"}}""");

        Assert.True(plan.Applies, Sentences(plan));
        Assert.Equal("mode", plan.Growth);
    }
}
