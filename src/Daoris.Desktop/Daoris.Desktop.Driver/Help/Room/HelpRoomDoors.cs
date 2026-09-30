using System.Text;

namespace Daoris.Driver;

/// <summary>
/// The doors (D50): each change the room speaks of, where it is on the screen, and the terminal command that
/// does the same, spelled as the CLI's help spells it — a helper that invented a verb would send the person to
/// nothing.
/// </summary>
/// <remarks>
/// <see cref="Doors"/> is the one registry of rows, in the order a person meets the tasks, not grouped by the
/// feature that added each: a new door is a row where the person would look for it.
/// </remarks>
internal sealed class HelpRoomDoors : IHelpRoomSection
{
    /// <summary>What each change is, where it is on the screen, and the command that does the same (D50).</summary>
    internal static readonly (string To, string Screen, string Terminal)[] Doors =
    [
        // SETUP1b: the page hands the helper a first message asking to be walked through it, naming the
        // steps by these titles, so it is told the guide exists and what each step is done on.
        ("walk through setting this machine up: an agent, Daoris's own agent, a workspace and its repositories, "
            + "what is driven, how work lands, what agents may do", "Settings → Get started (the Daoris menu's *Set up Daoris*)",
            "(each step shows its own command there)"),
        ("drive a repository, or stop", "Projects", "`daoris driver drive|undrive <repository>`"),
        ("pause one, or release it", "Projects", "`daoris driver hold|resume <repository>`"),
        ("give its sessions their own tree", "Projects", "`daoris driver trees <repository> on|off`"),
        ("set the line its work grows from and lands on", "Settings → Workspace → Lines",
            "`daoris driver line <repository> <branch>|--clear` (`--workspace <name>` for a whole workspace)"),
        ("set how accepted work lands", "Settings → Workspace → How work lands",
            "`daoris driver landing <repository> merge|branch <pattern>|--clear` (`--workspace <name>`, `--tidy`, "
            + "and on a branch `--plugin <id>`: an installed plugin that pushes it and opens the pull request)"),
        // WSR6: after a pull request merges — the line pulled, what merged deleted, what still works replayed onto it.
        ("bring a repository up to date after its pull request merged: fetch and fast-forward the line, delete the branches "
            + "whose work reached it, replay the branches still at work onto it (Daoris fetches, never pushes)",
            "Settings → Workspace → Session branches → Bring up to date", "`daoris-driver trees sync [--repository <name>] [--yes]`"),
        ("clean up session branches whose work landed, and branches a landing made whose work reached the line",
            "Settings → Workspace → Session branches", "`daoris-driver trees clean`"),
        // WSR5b: a landed branch handed to a landing plugin after its landing.
        ("hand a branch a landing made to a landing plugin, to push it and open the pull request",
            "Sessions → the session's review → hand it to <plugin>", "`daoris-driver trees hand <session|branch> [--plugin <id>]`"),
        ("choose the agent that answers asks", "Settings → Daoris's own AI", "`daoris driver intake <agent>|off`"),
        ("choose the agent Ask Daoris runs on", "Settings → Daoris's own AI", "`daoris driver helper <agent>|off`"),
        ("park a quest after failed sessions", "Settings → Driver", "`daoris driver strikes <n>`"),
        ("bound how long one session runs", "(no screen yet)", "`daoris driver timeout <minutes>`"),
        ("say so when a session parks", "Settings → Driver", "`daoris driver notify on|off`"),
        ("allow, ask or deny what an agent may do", "Settings → Permissions", "`daoris agent rules …`"),
        // D107: reading other checkouts is on unless switched off; writing into one is a declared relationship.
        ("let agents read a repository's checkout, or not; let one repository's sessions write into another",
            "Settings → Permissions → Reading and writing across",
            "`daoris driver across <repository> read on|off|--clear` (`--workspace <name>` for a whole workspace), "
            + "`daoris driver across <repository> write-to <other> [--clear]`"),
        ("sign an agent in, or add an account", "Settings → Agents & accounts", "`daoris agent login <agent>`"),
        // HELP6: the doors built since, which the helper now proposes too.
        ("update an agent, or pin it to one version", "Settings → Agents & accounts → Update, Pin a version",
            "`daoris agent update <agent>`, `daoris agent pin <agent> <version>`"),
        ("set an account's own model and effort", "Settings → Agents & accounts → Model & effort",
            "`daoris agent settings <agent> --account <name> model <model> effort <effort>`"),
        ("delete a quest or an ask made by mistake", "Quests → the quest's drawer, or the ask's record → delete",
            "`daoris-driver quest delete <id>`, `daoris-driver ask --delete <id>`"),
        // PLUG9: the card installs one that landed; the screen switches one installed, installs one of
        // Daoris's own (d) and updates one from where it came from (c).
        ("add a plugin that has landed, or switch one on or off", "Settings → Plugins (its switch)",
            "`daoris plugin add <folder>`, `daoris plugin enable|disable <id>`"),
        ("install one of Daoris's own plugins, or update one from where it came from",
            "Settings → Plugins (Install beside Daoris's own; Update on an installed one's row)",
            "`daoris plugin add --offer <id>`, `daoris plugin update <id>`"),
        ("start a task", "Quests → ask for something", "`daoris-driver ask --workspace <name> \"…\"`"),
        ("answer what waits on the person", "Sessions, and what needs you", "`daoris-driver answer`"),
    ];

    public string Render(HelpMachine machine)
    {
        var text = new StringBuilder();
        text.Append("## The doors\n\n");
        text.Append("| To | On the screen | At a terminal |\n");
        text.Append("|---|---|---|\n");
        foreach (var (to, screen, terminal) in Doors) text.Append($"| {to} | {screen} | {terminal} |\n");
        text.Append('\n');
        return text.ToString();
    }
}
