namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// NOTIFY2c (D196 point 6, the notification design §2.4): each main window wears its own workspace's count on its own taskbar button
/// and in its title, as its own page told it. Until then the first main window's page alone told one count, which every window wore
/// whichever workspace it held. What is here is what each window wears, held apart from the windows, which draw it.
/// </summary>
public sealed class WaitingBadgeTests
{
    [Fact]
    public void Each_main_window_wears_the_count_its_own_page_told()
    {
        var count = new WaitingCount();
        count.Told(MainWindows.First, 3, "3 waiting on you");
        count.Told("main:2", 1, "1 waiting on you");

        Assert.Equal((3, "3 waiting on you"), count.For(MainWindows.First));
        Assert.Equal((1, "1 waiting on you"), count.For("main:2"));
        // A window whose page has told nothing yet wears nothing, and a utility window never does.
        Assert.Equal((0, null), count.For("main:3"));
        count.Told(SecondaryWindow.Monitor, 5, "5 waiting on you");
        Assert.Equal((0, null), count.For(SecondaryWindow.Monitor));
    }

    /// <summary>MAINWINDOWS2: the first put away takes its button with it, and every other main window still wears its own.</summary>
    [Fact]
    public void Another_main_window_wears_its_own_count_whether_the_first_is_put_away_or_not()
    {
        var mains = new MainWindows();
        mains.Opened("main:2");
        var count = new WaitingCount();
        count.Told(MainWindows.First, 3, "3 waiting on you");
        count.Told("main:2", 1, "1 waiting on you");

        Assert.True(mains.FirstClosing(byPerson: true));
        Assert.Equal((1, "1 waiting on you"), count.For("main:2"));
    }

    [Fact]
    public void A_count_below_none_is_none_and_words_with_none_are_none()
    {
        var count = new WaitingCount();
        count.Told("main:2", -2, "stale words");
        Assert.Equal((0, null), count.For("main:2"));

        count.Told("main:2", 0, "0 waiting on you");
        Assert.Equal((0, null), count.For("main:2"));
    }

    /// <summary>A window closed forgets its count, so one opened again under its name wears nothing until its own page tells it.</summary>
    [Fact]
    public void A_window_closed_forgets_its_count()
    {
        var count = new WaitingCount();
        count.Told("main:2", 4, "4 waiting on you");
        count.Closed("main:2");

        Assert.Equal((0, null), count.For("main:2"));
    }
}
