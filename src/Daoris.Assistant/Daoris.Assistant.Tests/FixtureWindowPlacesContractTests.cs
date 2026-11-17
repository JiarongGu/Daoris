namespace Daoris.Assistant.Tests;

/// <summary>The window places' contract on a fixture's text, and the fixture's refusal of a kind it does not know.</summary>
public sealed class FixtureWindowPlacesContractTests : WindowPlacesContract
{
    private const string Fixture = """
        {
          "places": [
            { "kind": "view", "name": "work" },
            { "kind": "view", "name": "settings" },
            { "kind": "domain", "name": "work" },
            { "kind": "part", "name": "quests" }
          ],
          "doors": [
            { "view": "work", "sentence": "Send a quest to a repository." },
            { "view": "settings", "sentence": "Change who staffs a role." }
          ]
        }
        """;

    protected override IWindowPlaces Backend { get; } = FixtureWindowPlaces.Parse(Fixture);

    [Fact]
    public void The_fixture_reads_each_place_and_door_in_order()
    {
        Assert.Equal(
            [new(WindowPlaceKind.View, "work"), new(WindowPlaceKind.View, "settings"), new(WindowPlaceKind.Domain, "work"), new WindowPlace(WindowPlaceKind.Part, "quests")],
            Backend.Places);
        Assert.Equal(["work", "settings"], Backend.Doors.Select(door => door.View));
    }

    [Fact]
    public void A_kind_the_window_has_not_is_refused()
    {
        Assert.Throws<ArgumentException>(() => FixtureWindowPlaces.Parse("""{ "places": [{ "kind": "shelf", "name": "x" }] }"""));
    }
}
