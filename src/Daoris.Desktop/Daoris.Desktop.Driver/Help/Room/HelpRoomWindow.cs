using System.Text;

namespace Daoris.Driver;

/// <summary>
/// How the window is laid out (HELP2): asked what the panel held, the helper guessed at a menu that does not
/// exist. Said by the names the window's own labels use (DOCK1b), keys as its menus show them, and that the
/// helper cannot see the window and asks rather than guesses.
/// </summary>
internal sealed class HelpRoomWindow : IHelpRoomSection
{
    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        // How the window is laid out (HELP2): asked what the panel held, the helper guessed at a menu that
        // does not exist. Said by the names the window's own labels use (DOCK1b), keys as its menus show them.
        text.Append("## The window\n\n");
        text.Append("The desktop is laid out as VS Code is. The activity bar at the left holds the views: Overview,\n");
        text.Append("Sessions, Quests, Projects, Map, Convergence and Search, with Settings at its foot; `Ctrl+K` opens\n");
        text.Append("the command palette. Every view sits in one frame: the view in the centre, the panel beneath it,\n");
        text.Append("and the right side bar beside it; on Sessions the session list is at the left and the centre is the\n");
        text.Append("attended session. Five views stand in the two regions and move between them:\n");
        text.Append("the timeline, the review and the console of the session attended on Sessions, Ask Daoris, and the\n");
        text.Append("terminal: the person's own shell (PowerShell unless they choose another), in the panel beside the\n");
        text.Append("console, which starts where the attended session works. A session's console takes no typing; the\n");
        text.Append("terminal is the person's, never a session's.\n");
        text.Append("A view moves from its region's tab list (the button at the end of the tab row), by a right-click on\n");
        text.Append("its tab, or by dragging its tab to the other region; View → Reset view locations puts every view\n");
        text.Append("back. The toggles beside the window controls, and the View menu, show or hide the panel (`Ctrl+J`)\n");
        text.Append("and the right side bar (`Ctrl+Alt+B`) on every view, and the session list (`Ctrl+B`) on Sessions.\n");
        text.Append("You open on `F1` or `Ctrl+Alt+I`, and Quick Ask, a box where the palette opens, on\n");
        text.Append("`Ctrl+Shift+Alt+L`.\n\n");
        text.Append("You cannot see the window. Where the person is, and where the views stand, comes with their\n");
        text.Append("message when it changed; for anything else on the screen, ask them rather than guess.\n\n");
        return text.ToString();
    }
}
