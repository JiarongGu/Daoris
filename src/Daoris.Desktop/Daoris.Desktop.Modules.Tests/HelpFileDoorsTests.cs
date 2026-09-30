using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Ask Daoris's doors that start nothing (HELP10): each is the code the screen's own route runs, tested here without a
/// process, so a worktree's fast half holds them. <c>HelpDoorsTests</c> holds the doors that start one, or ask the
/// roster again.
/// </summary>
public sealed class HelpFileDoorsTests : Bridge
{
    private DriverModule Module() => new(Bus, new DriverLoop(Bus, new HostSupervisor("http://localhost:0"), "http://localhost:0"));

    /// <summary>
    /// A default on an agent Daoris manages no accounts for is refused in <c>HARNESS_ACTION</c>'s own words, before
    /// anything is written.
    /// </summary>
    [Fact]
    public async Task A_default_on_an_agent_with_no_toolchain_is_refused_as_the_agents_screens_route_refuses_it()
    {
        var refused = await Assert.ThrowsAsync<DriverException>(() =>
            Module().HelpDoors(null).SetDefaultAccountAsync("acp-stub", "work", null, CancellationToken.None));

        Assert.Contains("Daoris manages no toolchain for `acp-stub`", refused.Message);
        Assert.False(File.Exists(HarnessSettingsPath));
    }

    /// <summary>
    /// HELP10: the browser's settings are <c>BrowserModule</c>'s own edits, written to the files the Browser screen and
    /// <c>daoris browser</c> write, and read by <c>daoris-browser</c> at its next start.
    /// </summary>
    [Fact]
    public void The_browsers_settings_are_the_browser_screens_own_edits()
    {
        var doors = Module().HelpDoors(null);

        doors.ChangeBrowser("use", "edge", null, null);
        doors.ChangeBrowser("links", "daoris", null, null);
        doors.ChangeBrowser("extensions", "refuse", null, null);
        doors.ChangeBrowser("favorite", "add", "site.example/board", "Board");

        var settings = BrowserSettings.Read(Home);
        Assert.Equal((BrowserChoice.Edge, LinksSetting.Daoris, ExtensionsSetting.Refuse), (settings.Browser, settings.Links, settings.Extensions));
        Assert.Equal([new Favorite("https://site.example/board", "Board")], BrowserFavorites.Read(Home).Favorites);

        doors.ChangeBrowser("favorite", "remove", "https://site.example/board", null);
        Assert.Empty(BrowserFavorites.Read(Home).Favorites);
    }

    /// <summary>What the Browser screen's route refuses is refused here in its words, as the driver's refusal, so the card settles refused.</summary>
    [Fact]
    public void A_browser_change_the_screens_route_refuses_is_refused_in_its_words()
    {
        var doors = Module().HelpDoors(null);
        Directory.CreateDirectory(Path.GetDirectoryName(BrowserSettings.FilePath(Home))!);
        File.WriteAllText(BrowserSettings.FilePath(Home), "{ not json");

        Assert.Contains("Daoris will not write over a file it could not read",
            Assert.Throws<DriverException>(() => doors.ChangeBrowser("links", "daoris", null, null)).Message);
        Assert.Contains("is not a web page, so it cannot be a favorite",
            Assert.Throws<DriverException>(() => doors.ChangeBrowser("favorite", "add", "javascript:alert(1)", null)).Message);
        Assert.Equal("{ not json", File.ReadAllText(BrowserSettings.FilePath(Home)));
    }

    /// <summary>HELP10: what a browser proposal is judged against is the files as the Browser screen reads them, and the route's reader of an address.</summary>
    [Fact]
    public void The_browsers_facts_are_its_files_as_the_screen_reads_them()
    {
        BrowserSettings.SetLinks(Home, LinksSetting.Daoris);
        BrowserFavorites.Add(Home, "site.example/board", null);

        var facts = BrowserModule.HelpFacts(Home);

        Assert.Equal((BrowserChoice.Daoris, LinksSetting.Daoris, ExtensionsSetting.Offer), (facts.Browser, facts.Links, facts.Extensions));
        Assert.Equal(["https://site.example/board"], facts.Favorites);
        Assert.Equal(BrowserFavorites.Address("docs.example/guide"), facts.Page("docs.example/guide"));
        Assert.Null(facts.Page("javascript:alert(1)"));
        Assert.Equal((null, null), (facts.Problem, facts.FavoritesProblem));
    }
}
