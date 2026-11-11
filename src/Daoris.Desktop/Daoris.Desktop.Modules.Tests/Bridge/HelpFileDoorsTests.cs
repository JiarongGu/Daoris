using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>Ask Daoris's doors that start nothing, tested without a process.</summary>
public sealed class HelpFileDoorsTests : Bridge
{
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
