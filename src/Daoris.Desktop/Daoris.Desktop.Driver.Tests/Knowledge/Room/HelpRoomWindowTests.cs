using Daoris.Driver;

namespace Daoris.Driver.Tests;

/// <summary>The room's <see cref="HelpRoomWindow"/>: how the window is laid out, and that the helper cannot see it.</summary>
public sealed class HelpRoomWindowTests
{
    /// <summary>
    /// Asked what the panel holds, the helper never guesses that the View menu names each view's region,
    /// which it does not. The room says how the window is laid out
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
            "the right side bar", "the panel", "Notifications, the timeline, Outputs, the workflow, At work and the console", "Every view sits in one frame",
            "tab list", "right-click", "drag", "Reset view locations",
            "`Ctrl+B`", "`Ctrl+J`", "`Ctrl+Alt+B`", "`F1`", "`Ctrl+Alt+I`", "`Ctrl+Shift+Alt+L`", "`Ctrl+K`",
            // The person's own terminal, and that it is theirs rather than a session's.
            "terminal: the person's own shell", "never a session's",
        })
        {
            Assert.Contains(said, agents);
        }

        // It cannot see the window: what it is told of it comes with the message, and otherwise it asks.
        Assert.Contains("You cannot see the window", agents);
        // The activity bar holds the eight places Go opens, Knowledge holding Convergence and Search
        // and Plugins after Agents, with Settings at its foot.
        Assert.Contains("Sessions, Quests, Repositories, Map, Knowledge, Agents and Plugins, with Settings at its foot", agents);
        Assert.Contains("Knowledge's list switches between Search and Convergence", agents);
        Assert.DoesNotContain("Convergence, Search", agents);
    }

    /// <summary>
    /// The menu bar holds the window's verbs and places, as VS Code's does, and lists every key, so asked
    /// where a verb is the helper names a menu the window has rather than guessing one.
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
    /// The session's *Workflow* view is among the views that stand in the
    /// two regions and move between them, beside the timeline and Outputs; asked where
    /// a piece of work stands, the helper names the view the window has, and the room's doors say what it draws.
    /// </summary>
    [Fact]
    public void The_room_names_the_sessions_workflow_view_among_the_views_that_move()
    {
        var window = new HelpRoomWindow().Render(HelpRoomFixture.Machine);

        Assert.Contains("Eight views stand in the two regions and move between them:", window);
        Assert.Contains("the timeline, Outputs, the workflow, At work and the console of the session attended on Sessions", window);
        Assert.DoesNotContain("Five views", window);
        Assert.DoesNotContain("Seven views", window);
    }

    /// <summary>
    /// *At work* is among the views, after the workflow; asked who works on a session, the
    /// helper names it, and that it reads Daoris's records, never the conversation's text.
    /// </summary>
    [Fact]
    public void The_room_names_at_work_among_the_views_and_what_it_reads()
    {
        var window = new HelpRoomWindow().Render(HelpRoomFixture.Machine);

        Assert.Contains("At work lists the session's parts and its subagents", window);
        Assert.Contains("never from the conversation's text", window);
    }

    /// <summary>
    /// *Notifications* is a view of the regions, on the right by default,
    /// which the title bar's bell shows; it holds the presses waiting for the person's confirmation and *What needs you*, and Overview
    /// keeps a summary of it. Asked where a terminal's press waits, the helper names the view the window has, and that only the
    /// person confirms it there.
    /// </summary>
    [Fact]
    public void The_room_names_notifications_among_the_views_and_the_bell_that_shows_it()
    {
        var window = new HelpRoomWindow().Render(HelpRoomFixture.Machine);

        Assert.Contains("Notifications, the timeline, Outputs, the workflow, At work and the console", window);
        Assert.Contains("The bell on the title bar shows it", window);
        Assert.Contains("What needs you", window);
        Assert.Contains("Overview leads with a summary", window);
        Assert.Contains("only the person confirms", window);
    }

    /// <summary>
    /// The preface names the item the view has open, by the id a go names, so "what is this?" is
    /// answered about it.
    /// </summary>
    [Fact]
    public void The_room_says_the_preface_names_the_item_the_view_has_open_by_the_id_a_go_names()
    {
        var window = new HelpRoomWindow().Render(HelpRoomFixture.Machine);

        Assert.Contains("It names the quest, ask, repository, workspace, agent or plugin the view has open, by the id a go names,", window);
        Assert.Contains("while the screen still shows it.", window);
    }

    /// <summary>
    /// Every view but Overview and Map has a list, and the list shows or hides by four doors, so the
    /// room does not tie the list and Ctrl+B to Sessions alone.
    /// </summary>
    [Fact]
    public void The_room_says_every_view_but_overview_and_map_has_a_list_and_its_four_doors()
    {
        var window = new HelpRoomWindow().Render(HelpRoomFixture.Machine);

        Assert.Contains("Every view but Overview and Map has a list beside its centre: Sessions, Quests, Repositories, Knowledge,", window);
        Assert.Contains("its strip, the View menu, `Ctrl+B`,\nand a press on the current place in the activity bar", window);
        Assert.DoesNotContain("and the session list (`Ctrl+B`) on Sessions", window);
    }

    /// <summary>
    /// The file preview — its two doors, where it opens, that it reads and never writes — so the
    /// helper asked how to read a file an agent touched points at it rather than at an editor there is none of.
    /// </summary>
    [Fact]
    public void The_room_says_a_file_opens_in_a_preview_in_the_right_side_bar()
    {
        var window = new HelpRoomWindow().Render(HelpRoomFixture.Machine);

        Assert.Contains("A file path in a session's tool card, or a file's button in Outputs' list, opens a preview", window);
        Assert.Contains("a tab of the right side bar", window);
        Assert.Contains("as it is on disk now, read-only", window);
        Assert.Contains("In this conversation a path is plain text.", window);
    }
}
