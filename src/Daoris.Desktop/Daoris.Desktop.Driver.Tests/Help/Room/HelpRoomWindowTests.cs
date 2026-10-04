using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The room's <see cref="HelpRoomWindow"/>: how the window is laid out, and that the helper cannot see it.</summary>
public sealed class HelpRoomWindowTests
{
    /// <summary>
    /// HELP2: asked what the panel held, the helper guessed that the View menu names each view's region,
    /// which it does not — its room said nothing of the window. The room says how the window is laid out
    /// and how a view moves, by the names the window uses, and that where the views stand now arrives with
    /// the person's message.
    /// </summary>
    [Fact]
    public void The_room_says_how_the_window_is_laid_out_and_how_a_view_moves()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.Contains("## The window", agents);
        foreach (var said in new[]
        {
            "the right side bar", "the panel", "the timeline, the review and the console", "Every view sits in one frame",
            "tab list", "right-click", "drag", "Reset view locations",
            "`Ctrl+B`", "`Ctrl+J`", "`Ctrl+Alt+B`", "`F1`", "`Ctrl+Alt+I`", "`Ctrl+Shift+Alt+L`", "`Ctrl+K`",
            // The person's own terminal (CONSOLE4b), and that it is theirs rather than a session's.
            "terminal: the person's own shell", "never a session's",
        })
        {
            Assert.Contains(said, agents);
        }

        // It cannot see the window: what it is told of it comes with the message, and otherwise it asks.
        Assert.Contains("You cannot see the window", agents);
        // UX6e2: the activity bar holds Agents since UX6e (D150 §2.1).
        Assert.Contains("Sessions, Quests, Repositories, Map, Convergence, Search and Agents, with Settings at its foot", agents);
    }

    /// <summary>
    /// UX7a (D152 §1): the menu bar holds the window's verbs and places, as VS Code's does, and lists every key, so asked
    /// where a verb is the helper names a menu the window has rather than guessing one, as HELP2's guessed a region.
    /// </summary>
    [Fact]
    public void The_room_names_the_menu_bar_by_its_menus_and_where_every_key_is_listed()
    {
        var agents = HelpRoom.Render(HelpRoomFixture.Machine);

        Assert.Contains("The menu bar across the top holds Workspace, Edit, View, Go, Run, Terminal and Help", agents);
        Assert.Contains("(`Ctrl+1` to `Ctrl+8`)", agents);
        Assert.Contains("Help → Keyboard shortcuts lists every key", agents);
    }

    /// <summary>
    /// HELP10: PREVIEW1's file preview (D111) — its two doors, where it opens, that it reads and never writes — so the
    /// helper asked how to read a file an agent touched points at it rather than at an editor there is none of.
    /// </summary>
    [Fact]
    public void The_room_says_a_file_opens_in_a_preview_in_the_right_side_bar()
    {
        var window = new HelpRoomWindow().Render(HelpRoomFixture.Machine);

        Assert.Contains("A file path in a session's tool card, or a file's button in the review's list, opens a preview", window);
        Assert.Contains("a tab of the right side bar", window);
        Assert.Contains("as it is on disk now, read-only", window);
        Assert.Contains("In this conversation a path is plain text.", window);
    }
}
