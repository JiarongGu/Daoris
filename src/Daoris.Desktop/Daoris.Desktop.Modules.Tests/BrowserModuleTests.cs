namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// Daoris's browser as Settings reaches it (CHR5, CHR7, D50): the favorites and the extensions
/// setting, edited in the files <c>daoris browser</c> edits. The page's suite proves it renders what it
/// is given; this proves what the files hold afterwards, since the files are the contract.
/// </summary>
public sealed class BrowserModuleTests : Bridge
{
    private BrowserModule Module() => new(Bus);

    [Fact]
    public async Task A_machine_with_nothing_kept_answers_the_defaults_and_where_the_files_are()
    {
        var state = await AnswerAsync(Module(), "STATE");

        Assert.Empty(state.GetProperty("favorites").EnumerateArray());
        Assert.Equal(ExtensionsSetting.Offer, state.GetProperty("extensions").GetString());
        Assert.Equal(BrowserFavorites.FilePath(Home), state.GetProperty("favoritesPath").GetString());
        Assert.Equal(BrowserSettings.FilePath(Home), state.GetProperty("settingsPath").GetString());
    }

    [Fact]
    public async Task Keeping_a_page_writes_the_file_the_terminal_and_the_browser_read()
    {
        var state = await AnswerAsync(Module(), "ADD_FAVORITE", new { address = "site.example/board", title = "Board" });

        Assert.Equal([new Favorite("https://site.example/board", "Board")], BrowserFavorites.Read(Home).Favorites);
        Assert.Equal("Board", state.GetProperty("favorites")[0].GetProperty("title").GetString());

        await AnswerAsync(Module(), "REMOVE_FAVORITE", new { address = "https://site.example/board" });
        Assert.Empty(BrowserFavorites.Read(Home).Favorites);
    }

    [Fact]
    public async Task Setting_the_extensions_writes_the_settings_file()
    {
        await AnswerAsync(Module(), "SET_EXTENSIONS", new { extensions = "refuse" });

        Assert.Equal(ExtensionsSetting.Refuse, BrowserSettings.Read(Home).Extensions);
    }

    [Fact]
    public async Task What_is_no_web_page_is_refused_by_code_and_writes_nothing()
    {
        var refusal = await RefusalAsync(Module(), "ADD_FAVORITE", new { address = "javascript:alert(1)" });

        Assert.Contains(Refusals.BrowserNotAPage, refusal);
        Assert.False(File.Exists(BrowserFavorites.FilePath(Home)));
    }

    [Fact]
    public async Task A_setting_that_is_neither_is_refused_by_code()
    {
        var refusal = await RefusalAsync(Module(), "SET_EXTENSIONS", new { extensions = "block" });

        Assert.Contains(Refusals.BrowserSettingUnknown, refusal);
        Assert.False(File.Exists(BrowserSettings.FilePath(Home)));
    }

    /// <summary>A file that could not be read is never written over: refused by code, left as it was.</summary>
    [Fact]
    public async Task A_file_that_cannot_be_read_is_refused_and_left_as_it_was()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(BrowserFavorites.FilePath(Home))!);
        File.WriteAllText(BrowserFavorites.FilePath(Home), "not json");
        File.WriteAllText(BrowserSettings.FilePath(Home), "[1]");

        Assert.Contains(Refusals.BrowserFileUnreadable,
            await RefusalAsync(Module(), "ADD_FAVORITE", new { address = "site.example/board" }));
        Assert.Contains(Refusals.BrowserFileUnreadable,
            await RefusalAsync(Module(), "SET_EXTENSIONS", new { extensions = "refuse" }));

        Assert.Equal("not json", File.ReadAllText(BrowserFavorites.FilePath(Home)));
        Assert.Equal("[1]", File.ReadAllText(BrowserSettings.FilePath(Home)));
        var state = await AnswerAsync(Module(), "STATE");
        Assert.False(string.IsNullOrEmpty(state.GetProperty("favoritesProblem").GetString()));
    }
}
