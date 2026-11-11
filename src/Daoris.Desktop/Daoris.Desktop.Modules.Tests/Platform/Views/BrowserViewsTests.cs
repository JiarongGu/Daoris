using System.Text.Json;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>Daoris's browser's favorites and settings as their presses' cards show them: judged by the Browser screen's rules, refused with no card.</summary>
public sealed class BrowserViewsTests
{
    private static StubGrounds Files(string? favoritesProblem = null, string? settingsProblem = null, bool edge = true)
    {
        var grounds = new StubGrounds();
        grounds.Reads["STATE"] = _ => new
        {
            favorites = new[] { new { url = "https://kept.test/", title = "Kept" } },
            favoritesProblem,
            settingsProblem,
            edgeFound = edge,
        };
        return grounds;
    }

    private static Task<PressViewed> Favorite(object payload, StubGrounds? grounds = null) =>
        new BrowserFavoriteView().ViewAsync(grounds ?? Files(), JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    private static Task<PressViewed> Setting(object payload, StubGrounds? grounds = null) =>
        new BrowserSettingView().ViewAsync(grounds ?? Files(), JsonSerializer.SerializeToElement(payload), CancellationToken.None);

    [Fact]
    public async Task A_favorite_kept_is_shown_with_its_page_and_its_title_when_it_has_one()
    {
        var plain = await Favorite(new { action = "add", address = "https://example.test/a" });
        var titled = await Favorite(new { action = "add", address = "https://example.test/a", title = "Docs" });

        Assert.Equal("browser-favorite-add", plain.View!.Kind);
        Assert.Equal("browser-favorite-add-titled", titled.View!.Kind);
        Assert.Equal("Docs", titled.View.Facts["title"]);
        Assert.StartsWith("daoris browser favorite add ", titled.View.Command);
        Assert.Contains("--title", titled.View.Command);
    }

    [Fact]
    public async Task A_favorite_dropped_must_be_one_kept()
    {
        var kept = await Favorite(new { action = "remove", address = "https://kept.test/" });
        var other = await Favorite(new { action = "remove", address = "https://other.test/" });

        Assert.Equal("browser-favorite-remove", kept.View!.Kind);
        Assert.Null(other.View);
        Assert.Contains("is not a favorite", other.Refusal);
        Assert.Contains("https://kept.test/", other.Refusal);
    }

    [Theory]
    [InlineData("add", "not a page", "is not a web page")]
    [InlineData("add", "", "names its address")]
    [InlineData("star", "https://example.test/", "`add` or `remove`")]
    public async Task A_favorite_the_route_would_refuse_is_refused_with_no_card(string action, string address, string says)
    {
        var viewed = await Favorite(new { action, address });

        Assert.Null(viewed.View);
        Assert.Contains(says, viewed.Refusal);
    }

    [Fact]
    public async Task A_favorites_file_that_could_not_be_read_is_never_written_over()
    {
        var viewed = await Favorite(new { action = "add", address = "https://example.test/" }, Files(favoritesProblem: "favorites.json is not valid JSON"));

        Assert.Null(viewed.View);
        Assert.Contains("will not write over a file it could not read", viewed.Refusal);
    }

    [Theory]
    [InlineData("use", "daoris", "browser-use-daoris")]
    [InlineData("use", "edge", "browser-use-edge")]
    [InlineData("links", "daoris", "browser-links-daoris")]
    [InlineData("links", "system", "browser-links-system")]
    [InlineData("extensions", "offer", "browser-extensions-offer")]
    [InlineData("extensions", "refuse", "browser-extensions-refuse")]
    public async Task A_setting_is_shown_by_what_it_becomes(string setting, string value, string kind)
    {
        var viewed = await Setting(new { setting, value });

        Assert.Equal(kind, viewed.View!.Kind);
        Assert.Equal($"daoris browser {setting} {value}", viewed.View.Command);
    }

    [Fact]
    public async Task Choosing_an_Edge_none_is_installed_for_says_so_beside_the_sentence()
    {
        var missing = await Setting(new { setting = "use", value = "edge" }, Files(edge: false));
        var found = await Setting(new { setting = "use", value = "edge" });

        Assert.Equal("browser-edge-missing", Assert.Single(missing.View!.Also!).Kind);
        Assert.Null(found.View!.Also);
    }

    [Theory]
    [InlineData("use", "firefox", "`daoris` or `edge`")]
    [InlineData("links", "browser", "`system` or `daoris`")]
    [InlineData("extensions", "maybe", "`offer` or `refuse`")]
    [InlineData("history", "on", "not a setting")]
    public async Task A_value_the_route_would_refuse_is_refused_with_no_card(string setting, string value, string says)
    {
        var viewed = await Setting(new { setting, value });

        Assert.Null(viewed.View);
        Assert.Contains(says, viewed.Refusal);
    }

    [Fact]
    public async Task A_settings_file_that_could_not_be_read_is_never_written_over()
    {
        var viewed = await Setting(new { setting = "use", value = "edge" }, Files(settingsProblem: "settings.json is not valid JSON"));

        Assert.Null(viewed.View);
        Assert.Contains("will not write over", viewed.Refusal);
    }
}
