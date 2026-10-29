namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// PAGELOOP1 (c, D217): the tray's reload of every open window's page. What is here is the judgement the window forms (WinForms, no
/// unit tests) cannot hold: which windows a reload reaches, that one still on its splash or trouble label is skipped, that each reload
/// is said in the machine log by the window's name, and the menu item's words in both languages.
/// </summary>
public sealed class PageReloadsTests
{
    private sealed class Window(string name, bool showingPage) : IReloadableWindow
    {
        public int Reloaded;

        public string Name => name;

        public bool ReloadPage()
        {
            if (!showingPage) return false;
            Reloaded++;
            return true;
        }
    }

    [Fact]
    public void Every_open_window_showing_its_page_is_reloaded_and_each_is_logged_by_name()
    {
        var main = new Window("main:1", true);
        var other = new Window("main:2", true);
        var monitor = new Window("monitor", true);
        var lines = new List<string>();

        var reloaded = new PageReloads(() => [main, other, monitor], lines.Add).Reload();

        Assert.Equal(["main:1", "main:2", "monitor"], reloaded);
        Assert.Equal([1, 1, 1], new[] { main.Reloaded, other.Reloaded, monitor.Reloaded });
        Assert.Equal(["main:1", "main:2", "monitor"], lines);
    }

    [Fact]
    public void A_window_still_on_its_splash_or_its_trouble_label_is_skipped_and_not_logged()
    {
        var splash = new Window("main:1", false);
        var shown = new Window("main:2", true);
        var lines = new List<string>();

        var reloaded = new PageReloads(() => [splash, shown], lines.Add).Reload();

        Assert.Equal(["main:2"], reloaded);
        Assert.Equal(0, splash.Reloaded);
        Assert.Equal(["main:2"], lines);
    }

    [Fact]
    public void One_window_that_throws_does_not_stop_the_others()
    {
        var shown = new Window("main:2", true);
        var broken = new Throwing();

        var reloaded = new PageReloads(() => [broken, shown], _ => { }).Reload();

        Assert.Equal(["main:2"], reloaded);
    }

    private sealed class Throwing : IReloadableWindow
    {
        public string Name => "main:1";

        public bool ReloadPage() => throw new InvalidOperationException("the browser went");
    }

    [Theory]
    [InlineData("en", "Reload the page of every window")]
    [InlineData("zh", "重新加载所有窗口的页面")]
    public void The_menu_item_is_said_in_the_words_the_page_handed_in_either_language(string language, string said)
    {
        Assert.Equal(said, SessionBalloon.ReloadPageWord(SessionBalloonTests.Handed(language)));
    }

    [Fact]
    public void With_no_words_handed_the_menu_item_is_said_in_english()
    {
        Assert.Equal("Reload the page of every window", SessionBalloon.ReloadPageWord(null));
    }
}
