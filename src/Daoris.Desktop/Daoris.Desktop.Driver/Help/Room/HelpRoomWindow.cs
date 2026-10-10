using System.Text;

namespace Daoris.Driver;

/// <summary>
/// How the window is laid out (HELP2): asked what the panel held, the helper guessed at a menu that does not
/// exist. Said by the names the window's own labels use (DOCK1b), keys as its menus show them, where a file's
/// preview opens (HELP10, D111), and that the helper cannot see the window and asks rather than guesses.
/// </summary>
internal sealed class HelpRoomWindow : IHelpRoomSection
{
    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        // How the window is laid out (HELP2): asked what the panel held, the helper guessed at a menu that
        // does not exist. Said by the names the window's own labels use (DOCK1b), keys as its menus show them.
        text.Append("## The window\n\n");
        // UX6i2a (D150 §2): the bar holds the eight places Go opens, Knowledge where Convergence and Search were (UX6i) and
        // Plugins after Agents, and Settings at its foot (UX6j).
        text.Append("The desktop is laid out as VS Code is. The activity bar at the left holds the places: Overview,\n");
        text.Append("Sessions, Quests, Repositories, Map, Knowledge, Agents and Plugins, with Settings at its foot;\n");
        text.Append("Knowledge's list switches between Search and Convergence. `Ctrl+K` opens\n");
        // UX7a (D152 §1): the menu bar holds the verbs and places, and Help lists every key.
        text.Append("the command palette. The menu bar across the top holds Workspace, Edit, View, Go, Run, Terminal and Help, as\n");
        text.Append("VS Code's does: Go opens the places (`Ctrl+1` to `Ctrl+8`), Run acts on the session or the quest in front, and\n");
        text.Append("Help → Keyboard shortcuts lists every key. Every view sits in one frame: the view in the centre, the panel beneath it,\n");
        text.Append("and the right side bar beside it; on Sessions the session list is at the left and the centre is the\n");
        // WORKFLOW1c4: the session's Workflow view (WORKFLOW1c, the workflow design §7) stands beside the timeline and the
        // review and moves as they do; the doors say what it draws.
        text.Append("attended session. Six views stand in the two regions and move between them:\n");
        text.Append("the timeline, the review, the workflow and the console of the session attended on Sessions, Ask Daoris, and the\n");
        text.Append("terminal: the person's own shell (PowerShell unless they choose another), in the panel beside the\n");
        text.Append("console, which starts where the attended session works. A session's console takes no typing; the\n");
        text.Append("terminal is the person's, never a session's.\n");
        text.Append("A view moves from its region's tab list (the button at the end of the tab row), by a right-click on\n");
        text.Append("its tab, or by dragging its tab to the other region; View → Reset view locations puts every view\n");
        text.Append("back. The toggles beside the window controls, and the View menu, show or hide the panel (`Ctrl+J`)\n");
        text.Append("and the right side bar (`Ctrl+Alt+B`) on every view, and the session list (`Ctrl+B`) on Sessions.\n");
        text.Append("You open on `F1` or `Ctrl+Alt+I`, and Quick Ask, a box where the palette opens, on\n");
        text.Append("`Ctrl+Shift+Alt+L`.\n\n");
        // HELP10: PREVIEW1's file preview (D111), so asked how to read a file an agent touched, the helper points
        // at it rather than at an editor there is none of (D55). Its doors are a session's; Ask Daoris has none.
        text.Append("A file path in a session's tool card, or a file's button in the review's list, opens a preview of that\n");
        text.Append("file as a tab of the right side bar: the file in the session's tree as it is on disk now, read-only,\n");
        text.Append("the lines a read named marked, and its changes one press away where the review holds them. One preview\n");
        text.Append("a session; opening another replaces it, and its own × goes back. In this conversation a path is plain text.\n\n");
        text.Append("You cannot see the window. Where the person is, and where the views stand, comes with their\n");
        text.Append("message when it changed; for anything else on the screen, ask them rather than guess.\n\n");
        return text.ToString();
    }
}
