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

    /// <summary>
    /// UX6e2 and HELPSETUP1: a go names no repository and no agent, so the room says where each part lands — Repositories'
    /// Setup on the repository its list has chosen, an agent's part on the agent that has it — and the helper says which
    /// one the person means rather than promising it.
    /// </summary>
    [Fact]
    public void The_room_says_a_go_names_no_repository_and_no_agent()
    {
        var places = new HelpRoomPlaces().Render(HelpRoomFixture.Machine);

        Assert.Contains("- Views: `overview` (Overview)", places);
        Assert.Contains("`agents` (Agents), `plugins` (Plugins), `settings` (Settings).", places);
        Assert.DoesNotContain("`agents` (Agents), `permissions`", places);
        Assert.Contains("A go names no repository and no agent", places);
        Assert.Contains("`setup` opens the Setup of the repository Repositories has chosen", places);
        Assert.Contains("a part of `agents` opens the agent that has it", places);
    }

    /// <summary>
    /// UX6i2a (D150 §2): the room names the places as the window has them, Knowledge with its two modes as its parts and
    /// Plugins as a view, and lists no place that moved: a go still spelled so lands, but the helper is never offered it.
    /// </summary>
    [Fact]
    public void The_room_names_the_places_as_they_are_and_none_that_moved()
    {
        var places = new HelpRoomPlaces().Render(HelpRoomFixture.Machine);

        Assert.Contains("`map` (Map), `knowledge` (Knowledge), `agents` (Agents)", places);
        Assert.Contains("- Parts of `knowledge`: `search` (Search), `convergence` (Convergence).", places);
        Assert.Contains("`knowledge` alone opens Knowledge as the person left it", places);
        Assert.Contains("- Settings domains: `start` (Get started), ", places);
        Assert.DoesNotContain("`plugins` (Plugins), `browser`", places);
        Assert.DoesNotContain("`search` (Search), `agents`", places);
        foreach (var (was, _) in HelpPlaces.Kept)
        {
            Assert.DoesNotContain(HelpPlaces.Views, view => view.Id == was.View && was.Domain is null);
            Assert.DoesNotContain(HelpPlaces.Domains, domain => domain.Id == was.Domain);
        }
    }
}
