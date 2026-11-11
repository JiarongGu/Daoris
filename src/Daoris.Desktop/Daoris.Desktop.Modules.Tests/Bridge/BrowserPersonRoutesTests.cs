using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// <c>daoris browser favorite|use|links|extensions</c> as the person's presses of the pipe: each runs the library's own edit of the home's
/// browser files in this Daoris and answers the verb's words, and a refusal is its words on the error with the verb's exit.
/// </summary>
public sealed class BrowserPersonRoutesTests : DriverModuleBridge
{
    private static string Said(System.Text.Json.JsonElement answer, string stream) =>
        string.Join('\n', answer.GetProperty("said").GetProperty(stream).EnumerateArray().Select(each => each.GetString()));

    [Fact]
    public async Task A_favorite_kept_is_written_to_the_home_and_said_as_the_verb_says_it()
    {
        var answer = await AnswerAsync(Module(), "BROWSER_FAVORITE", new { action = "add", address = "https://example.test/docs", title = "Docs", said = true });

        Assert.Equal(0, answer.GetProperty("said").GetProperty("code").GetInt32());
        Assert.Contains("is a favorite", Said(answer, "out"));
        Assert.Contains(BrowserFavorites.Read(Home).Favorites, favorite => favorite.Title == "Docs");

        var dropped = await AnswerAsync(Module(), "BROWSER_FAVORITE", new { action = "remove", address = "https://example.test/docs", said = true });

        Assert.Equal(0, dropped.GetProperty("said").GetProperty("code").GetInt32());
        Assert.Empty(BrowserFavorites.Read(Home).Favorites);
    }

    [Fact]
    public async Task An_address_that_is_no_web_page_is_refused_in_the_libraries_words_with_nothing_written()
    {
        var answer = await AnswerAsync(Module(), "BROWSER_FAVORITE", new { action = "add", address = "not a page", said = true });

        Assert.Equal(2, answer.GetProperty("said").GetProperty("code").GetInt32());
        Assert.Contains("is not a web page", Said(answer, "error"));
        Assert.Empty(BrowserFavorites.Read(Home).Favorites);
    }

    [Theory]
    [InlineData("use", "edge")]
    [InlineData("links", "daoris")]
    [InlineData("extensions", "refuse")]
    public async Task A_setting_is_written_to_the_home_and_a_value_the_screen_would_refuse_is_not(string setting, string value)
    {
        var done = await AnswerAsync(Module(), "BROWSER_SETTING", new { setting, value, said = true });

        Assert.Equal(0, done.GetProperty("said").GetProperty("code").GetInt32());
        var read = BrowserSettings.Read(Home);
        Assert.Equal(value, setting switch { "use" => read.Browser, "links" => read.Links, _ => read.Extensions });

        var refused = await AnswerAsync(Module(), "BROWSER_SETTING", new { setting, value = "nowhere", said = true });

        Assert.Equal(2, refused.GetProperty("said").GetProperty("code").GetInt32());
        Assert.Contains("not `nowhere`", Said(refused, "error"));
    }
}
