using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The room's <see cref="HelpRoomPlaces"/>: every place a go may name, from the table a go is judged by.</summary>
public sealed class HelpRoomPlacesTests
{
    /// <summary>
    /// HELP6: the places `go_propose` may name are listed from the driver's own table, the one it judges a
    /// go by — so the room and the judge cannot disagree about which screens exist.
    /// </summary>
    [Fact]
    public void The_room_lists_every_place_a_go_may_name()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.Contains("## Where you may take the person", agents);
        foreach (var (id, _) in HelpPlaces.Views) Assert.Contains($"`{id}`", agents);
        foreach (var (id, name) in HelpPlaces.Domains) Assert.Contains($"`{id}` ({name})", agents);
        foreach (var (_, id, name) in HelpPlaces.Parts) Assert.Contains($"`{id}` ({name})", agents);
        Assert.Contains("changes nothing", agents);
    }
}
