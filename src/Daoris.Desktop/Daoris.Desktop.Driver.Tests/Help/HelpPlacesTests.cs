using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The places a go link may name, the driver's twin of the page's <c>help/places.ts</c>, which opens the link.</summary>
public sealed class HelpPlacesTests
{
    /// <summary>
    /// The same views, Settings domains and parts as the page holds, the setup guide's steps among them, and how an ask's
    /// item and an offer's are told apart. The page's test holds the same table.
    /// </summary>
    [Fact]
    public void The_places_are_the_pages_twin()
    {
        Assert.Equal(["overview", "sessions", "quests", "projects", "map", "knowledge", "agents", "plugins", "settings"], HelpPlaces.Views.Select(view => view.Id));
        Assert.Equal(
            ["start", "appearance", "driver", "ai", "git", "browser", "space", "updates", "wiring", "logs"], HelpPlaces.Domains.Select(domain => domain.Id));
        Assert.Equal(
            [
                "sessions/needs-you", "sessions/failed", "sessions/new", "quests/asks", "quests/held",
                "projects/add", "projects/import", "projects/setup", "projects/branches", "projects/history",
                "projects/workspace-details", "projects/workspace-branches", "projects/workspace-workflow",
                "projects/workspace-defaults", "projects/workspace-remote",
                "knowledge/search", "knowledge/convergence",
                "start/agent", "start/helper", "start/repositories", "start/setUp", "start/rules",
                "agents/accounts", "agents/rules", "agents/usage", "agents/roles",
            ],
            HelpPlaces.Parts.Select(part => $"{part.Within}/{part.Id}"));
        Assert.Equal("ask:", HelpPlaces.AskItem);
        Assert.Equal("offer:", HelpPlaces.OfferItem);
    }
}
