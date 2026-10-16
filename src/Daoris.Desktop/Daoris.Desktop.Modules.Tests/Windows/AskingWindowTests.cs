namespace Daoris.Desktop.Modules.Tests;

/// <summary>
/// MAINWINDOWS2c (D171 point 2): a picker is owned by the window whose page asked, not by the first main window, which is
/// hidden while put away. What is held here is the choice of owner, apart from the windows, which are WinForms and live in
/// the application; the routes that open a picker (<c>RegistryModule</c>'s folder, <c>ToolsRoutes</c>' file) reach it through
/// the delegates the application hands them, so the owner is decided at each press, not when they are made.
/// </summary>
public sealed class AskingWindowTests
{
    private sealed record Window(string Name, bool Shown = true);

    private static readonly Window First = new("main");
    private static readonly Window Second = new("main:2");
    private static readonly Window Third = new("main:3");

    private static bool Usable(Window window) => window.Shown;

    [Fact]
    public void The_window_whose_page_asked_owns_the_picker()
    {
        Assert.Same(Second, AskingWindow.Owner(Second, First, [Second, Third], Usable));
        Assert.Same(First, AskingWindow.Owner(First, First, [Second], Usable));
    }

    [Fact]
    public void The_first_window_put_away_is_never_the_owner_of_a_picker_asked_from_another()
    {
        var away = First with { Shown = false };

        Assert.Same(Second, AskingWindow.Owner(Second, away, [Second], Usable));
    }

    [Fact]
    public void A_press_from_no_page_is_the_first_windows_or_the_first_open_one_while_it_is_away()
    {
        Assert.Same(First, AskingWindow.Owner(null, First, [Second], Usable));

        var away = First with { Shown = false };
        Assert.Same(Third, AskingWindow.Owner(null, away, [Second with { Shown = false }, Third], Usable));
    }

    [Fact]
    public void A_window_closed_since_it_asked_is_not_the_owner()
    {
        var closed = Second with { Shown = false };

        Assert.Same(First, AskingWindow.Owner(closed, First, [closed, Third], Usable));
    }

    [Fact]
    public void With_no_window_usable_the_first_is_the_owner_and_the_dialog_is_the_systems_to_place()
    {
        var away = First with { Shown = false };

        Assert.Same(away, AskingWindow.Owner(null, away, [], Usable));
    }
}
