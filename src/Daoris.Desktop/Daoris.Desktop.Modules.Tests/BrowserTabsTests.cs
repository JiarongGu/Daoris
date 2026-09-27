namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// BRW4: Daoris's own browser holds several pages, a tab each, with a regular browser's rules for
/// where a tab opens and which one is in front after one closes. The window is WinForms; the order
/// is decided here, where it is tested.
/// </summary>
public sealed class BrowserTabsTests
{
    [Fact]
    public void A_new_tab_the_person_asks_for_goes_last_and_comes_to_the_front()
    {
        var tabs = new BrowserTabs();
        var first = tabs.Open();
        var second = tabs.Open();

        Assert.Equal([first, second], tabs.Order);
        Assert.Equal(second, tabs.Front);
    }

    /// <summary>A page that opens a window opens it beside itself, as a browser puts a link's tab.</summary>
    [Fact]
    public void A_tab_a_page_opens_sits_beside_that_page_and_comes_to_the_front()
    {
        var tabs = new BrowserTabs();
        var first = tabs.Open();
        var second = tabs.Open();

        var opened = tabs.Open(opener: first);

        Assert.Equal([first, opened, second], tabs.Order);
        Assert.Equal(opened, tabs.Front);
    }

    /// <summary>Closing the one in front brings its right neighbour forward, or its left at the end.</summary>
    [Fact]
    public void Closing_the_front_tab_brings_its_neighbour_forward()
    {
        var tabs = new BrowserTabs();
        var a = tabs.Open();
        var b = tabs.Open();
        var c = tabs.Open();
        tabs.Bring(b);

        Assert.True(tabs.Close(b));
        Assert.Equal(c, tabs.Front);
        Assert.True(tabs.Close(c));
        Assert.Equal(a, tabs.Front);
    }

    [Fact]
    public void Closing_a_tab_behind_leaves_the_front_one_in_front()
    {
        var tabs = new BrowserTabs();
        var a = tabs.Open();
        var b = tabs.Open();

        tabs.Close(a);

        Assert.Equal([b], tabs.Order);
        Assert.Equal(b, tabs.Front);
    }

    /// <summary>The last tab closing is the window closing, as in any browser: it says so, never leaves none in front.</summary>
    [Fact]
    public void Closing_the_last_tab_says_the_window_goes_with_it()
    {
        var tabs = new BrowserTabs();
        var only = tabs.Open();

        Assert.False(tabs.Close(only));
        Assert.Empty(tabs.Order);
        Assert.Null(tabs.Front);
    }

    /// <summary>A tab nobody holds, or one already closed, changes nothing: a page's close and the person's can race.</summary>
    [Fact]
    public void Closing_or_bringing_a_tab_that_is_not_there_changes_nothing()
    {
        var tabs = new BrowserTabs();
        var a = tabs.Open();

        Assert.True(tabs.Close(999));
        tabs.Bring(999);

        Assert.Equal([a], tabs.Order);
        Assert.Equal(a, tabs.Front);
    }

    /// <summary>Next and previous go round, as Ctrl+Tab does.</summary>
    [Fact]
    public void Stepping_through_the_tabs_goes_round()
    {
        var tabs = new BrowserTabs();
        var a = tabs.Open();
        var b = tabs.Open();

        Assert.Equal(a, tabs.Step(+1));
        Assert.Equal(b, tabs.Step(-1));
        Assert.Equal(a, tabs.Step(-1));
    }

    /// <summary>A tab is named by its page's title, else its address's host, else as new.</summary>
    [Fact]
    public void A_tab_is_named_by_its_title_then_its_host()
    {
        Assert.Equal("Board — Jira", BrowserTabs.Name("Board — Jira", "https://site.example/board"));
        Assert.Equal("site.example", BrowserTabs.Name("  ", "https://site.example/board"));
        Assert.Equal("New tab", BrowserTabs.Name(null, "about:blank"));
        Assert.Equal("New tab", BrowserTabs.Name(null, null));
    }
}
