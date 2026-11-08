using Daoris.Driver;

namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// What <c>daoris-browser</c> does to its profile before the engine starts, held apart from the executable: a file another
/// program holds is read as absent, said once in the log, and never written over, so a locked file never stops the browser starting.
/// </summary>
public sealed class EngineProfilePrepareTests : Bridge
{
    private static readonly DateTimeOffset Now = new(2026, 11, 9, 9, 0, 0, TimeSpan.Zero);

    private string Profile => EngineBrowser.ProfileFolder(Home);

    private static FileStream Hold(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.None);

    private List<string> Prepare(params string[] registered)
    {
        var unread = new List<string>();
        EngineProfilePrepare.Run(Profile, () => registered, Now, unread.Add);
        return unread;
    }

    private void Seed(string path, string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    [Fact]
    public void A_profile_nobody_holds_is_prepared_with_the_favorites_on_its_bar_and_nothing_said()
    {
        BrowserFavorites.Add(Home, "https://site.example/board", "Board");

        Assert.Empty(Prepare());

        Assert.Contains("Board", File.ReadAllText(EngineProfile.BookmarksPath(Profile)));
        Assert.True(File.Exists(EngineProfile.RecordPath(Profile)));
    }

    [Fact]
    public void A_locked_favorites_file_is_read_as_none_and_said_once_and_the_browser_still_prepares()
    {
        BrowserFavorites.Add(Home, "https://site.example/board", "Board");
        using var held = Hold(BrowserFavorites.FilePath(Home));

        var unread = Prepare();

        Assert.Equal([BrowserFavorites.FilePath(Home)], unread);
        Assert.True(File.Exists(EngineProfile.RecordPath(Profile)));
        Assert.False(File.Exists(EngineProfile.BookmarksPath(Profile)));
    }

    [Fact]
    public void A_locked_settings_file_is_read_as_the_defaults_and_said_once()
    {
        BrowserSettings.SetExtensions(Home, ExtensionsSetting.Refuse);
        using var held = Hold(BrowserSettings.FilePath(Home));

        var unread = Prepare();

        Assert.Equal([BrowserSettings.FilePath(Home)], unread);
        Assert.True(File.Exists(EngineProfile.RecordPath(Profile)));
    }

    [Fact]
    public void A_locked_bookmarks_file_is_said_once_and_the_persons_bookmarks_are_never_written_over()
    {
        BrowserFavorites.Add(Home, "https://site.example/board", "Board");
        var bookmarks = EngineProfile.BookmarksPath(Profile);
        Seed(bookmarks, """{ "roots": { "bookmark_bar": { "children": [], "id": "1", "name": "Bookmarks bar", "type": "folder" } }, "version": 1 }""");
        using var held = Hold(bookmarks);

        var unread = Prepare();

        Assert.Equal([bookmarks], unread);
        Assert.True(File.Exists(EngineProfile.RecordPath(Profile)));
        held.Dispose();
        Assert.DoesNotContain("Board", File.ReadAllText(bookmarks));
    }

    [Fact]
    public void A_locked_preferences_file_is_said_once_and_left_as_it_is()
    {
        var preferences = EngineProfile.PreferencesPath(Profile);
        Seed(preferences, """{ "extensions": { "settings": {} } }""");
        BrowserSettings.SetExtensions(Home, ExtensionsSetting.Refuse);
        using var held = Hold(preferences);

        var unread = Prepare("abcdefghijklmnopabcdefghijklmnop");

        Assert.Equal([preferences], unread);
        Assert.True(File.Exists(EngineProfile.RecordPath(Profile)));
    }

    [Fact]
    public void A_locked_record_is_said_once_and_the_browser_still_prepares()
    {
        Prepare();
        using var held = Hold(EngineProfile.RecordPath(Profile));

        var unread = Prepare();

        Assert.Equal([EngineProfile.RecordPath(Profile)], unread);
    }
}
