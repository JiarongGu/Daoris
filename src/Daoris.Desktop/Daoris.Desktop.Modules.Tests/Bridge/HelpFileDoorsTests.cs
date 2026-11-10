using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>Ask Daoris's doors that start nothing, tested without a process.</summary>
public sealed class HelpFileDoorsTests : Bridge
{
    private DriverModule Module() => new(Bus, new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0"));

    /// <summary>
    /// Bringing up to date is <c>TREES_SYNC_PLAN</c>'s list and <c>TREES_SYNC</c>'s press, which read the
    /// registry and the running sessions, so before the driver is up both are the cold-start sentence and nothing is fetched.
    /// </summary>
    [Fact]
    public async Task Bringing_up_to_date_before_the_driver_is_up_is_the_cold_start_sentence()
    {
        var doors = Module().HelpDoors(null);

        Assert.Contains("still coming up", (await Assert.ThrowsAnyAsync<Exception>(() => doors.SyncPlanAsync(null, null, CancellationToken.None))).Message);
        Assert.Contains("still coming up",
            (await Assert.ThrowsAnyAsync<Exception>(() => doors.SyncAsync("engine", null, new HashSet<string> { "engine:main" }, CancellationToken.None))).Message);
    }

    /// <summary>What the room says of the browser is the files as the Browser screen reads them.</summary>
    [Fact]
    public void The_browsers_facts_are_its_files_as_the_screen_reads_them()
    {
        BrowserSettings.SetLinks(Home, LinksSetting.Daoris);
        BrowserFavorites.Add(Home, "site.example/board", null);

        var facts = BrowserModule.HelpFacts(Home);

        Assert.Equal((BrowserChoice.Daoris, LinksSetting.Daoris, ExtensionsSetting.Offer), (facts.Browser, facts.Links, facts.Extensions));
        Assert.Equal(["https://site.example/board"], facts.Favorites);
        Assert.Equal((null, null), (facts.Problem, facts.FavoritesProblem));
    }
}
