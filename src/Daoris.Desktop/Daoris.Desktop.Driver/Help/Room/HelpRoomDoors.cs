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
        ("walk through setting this machine up: an agent, Ask Daoris's agent, a workspace and its repositories, "
            + "what is driven, how work lands, what agents may do", "Settings → Setup (the Daoris menu's *Setup*)",
            "(each step shows its own command there)"),
        ("drive a repository, or stop", "Repositories", "`daoris driver drive|undrive <repository>`"),
        ("pause one, or release it", "Repositories", "`daoris driver hold|resume <repository>`"),
        ("give its sessions their own tree", "Repositories", "`daoris driver trees <repository> on|off`"),
        // LAYOUT7: the set-up press's terminal door; its screen and Ask Daoris's `setup` kind are LAYOUT8's, owed meanwhile.
        ("set a repository up for every agent: one quest to its own session, which takes up the doctrine, writes down what "
            + "the repository owns and its brief on its own branch (`--plan` shows what was read and the quest, and asks nothing)",
            "(no screen yet)", "`daoris-driver setup <repository> [--plan]`"),
        // WSSETUP6: the workspace plan's terminal door; its screen and Ask Daoris's doors are WSSETUP7's, owed meanwhile.
        ("set every repository of a workspace up, one at a time, the ones other work touches first: a plan the loop works, "
            + "pausing after a pilot of two (`--plan` shows the list and each refusal, and writes nothing); pause, resume or stop it",
            "(no screen yet)",
            "`daoris-driver setup --workspace <name> [--plan] [--at-once <n>] [--pilot <n>] [--first <repo>…] [--skip <repo>…]`, "
            + "`daoris-driver setup --workspace <name> --pause|--resume|--stop`"),
        // WSSETUP5: registration follows the line on its own (a start, a line Daoris moves); this is the person's ask. The
        // row's *Refresh* and Ask Daoris's `register` door are WSSETUP7's, owed meanwhile.
        ("register a repository from what its line declares, as `connect` would, without running it: after a set-up's "
            + "branch is merged and brought up to date, or a declaration changed outside Daoris",
            "(no screen yet)", "`daoris-driver register [--repository <name>]`"),
        ("set the line its work grows from and lands on", "Settings → Workspace → Lines",
            "`daoris driver line <repository> <branch>|--clear` (`--workspace <name>` for a whole workspace)"),
        ("set how accepted work lands", "Settings → Workspace → How work lands",
            "`daoris driver landing <repository> merge|branch <pattern>|--clear` (`--workspace <name>`, `--tidy`, "
            + "and on a branch `--plugin <id>`: an installed plugin that pushes it and opens the pull request)"),
        // WSR6: after a pull request merges — the line pulled, what merged deleted, what still works replayed onto it. It
        // takes the repositories holding Daoris's branches, and the others where included (WSR7, D112).
        ("bring a repository up to date after its pull request merged: fetch and fast-forward the line, delete the branches "
            + "whose work reached it, replay the branches still at work onto it (Daoris fetches, never pushes; it takes the "
            + "repositories holding Daoris's branches, and another where named or included)",
            "Settings → Workspace → Session branches → Updates", "`daoris-driver trees sync [--repository <name>] [--all] [--yes]`"),
        ("clean up session branches whose work landed, and branches a landing made whose work reached the line",
            "Settings → Workspace → Session branches", "`daoris-driver trees clean`"),
        // WSR5b: a landed branch handed to a landing plugin after its landing.
        ("hand a branch a landing made to a landing plugin, to push it and open the pull request",
            "Sessions → the session's review → Hand to <plugin>", "`daoris-driver trees hand <session|branch> [--plugin <id>]`"),
        ("choose the agent that answers asks", "Settings → AI features", "`daoris driver intake <agent>|off`"),
        ("choose the agent Ask Daoris runs on", "Settings → AI features", "`daoris driver helper <agent>|off`"),
        // HELP9: every `daoris driver` verb that changes something is a row, the two a terminal alone set among them.
        ("choose the agent driven sessions start on", "(no screen yet)", "`daoris driver adapter <agent>`"),
        ("park a quest after failed sessions", "Settings → Driver", "`daoris driver strikes <n>`"),
        // SESSUX1b: FRAME1d made the quest's drawer a page. A stop's release names its session, since the terminal cannot
        // see which hold a quest is under (D50); the page's Try again on a stopped quest is the web shell's to add.
        ("start a quest its failed sessions parked again", "Quests → the quest's page → Try again", "`daoris driver retry <quest>`"),
        ("start a quest your stop holds again: carry a taken one on in its tree, or plan an open one", "Quests → the quest's page, "
            + "whose *Sitting* names the session", "`daoris driver retry <quest> --session <id>`"),
        ("bound how long one session runs", "(no screen yet)", "`daoris driver timeout <minutes>`"),
        // TOOL4e: the terminal's door onto the default cool-off; Settings → Driver's control and Ask Daoris's are TOOL4g's.
        ("set how long an account cools when its agent hits a limit and names no time", "(no screen yet)",
            "`daoris driver cooloff <minutes>`"),
        ("bound how many sessions run at once", "(no screen yet)", "`daoris driver cap <n>`"),
        ("say so when a session parks", "Settings → Driver", "`daoris driver notify on|off`"),
        ("allow, ask or deny what an agent may do", "Settings → Permissions", "`daoris agent rules …`"),
        // D107: reading other checkouts is on unless switched off; writing into one is a declared relationship.
        ("let agents read a repository's checkout, or not; let one repository's sessions write into another",
            "Settings → Permissions → Across repositories",
            "`daoris driver across <repository> read on|off|--clear` (`--workspace <name>` for a whole workspace), "
            + "`daoris driver across <repository> write-to <other> [--clear]`"),
        ("sign an agent in, or add an account", "Settings → Agents", "`daoris agent login <agent>`"),
        // HELP9 named it while Ask Daoris owed it (D110); HELP10 proposes it as an agent's `default`. LEFT3 gave the
        // screen's clear its terminal door, which Ask Daoris still owes (HelpCoverageTests' Forms).
        ("choose which account an agent's sessions use, for the machine or a workspace",
            "Settings → Agents → Make default, use for a workspace",
            "`daoris agent profile default <agent> <profile>|--clear [--workspace <name>]` (`--clear` names none again: "
            + "the tool's own home, or for a workspace the machine's default)"),
        // TOOL4e (D125 §6): the order and *Try now*, the terminal's doors; the screen's and Ask Daoris's are TOOL4g's.
        ("choose the accounts rotation may use, in order, for the machine or a workspace (an account not listed never "
            + "rotates)", "(no screen yet)",
            "`daoris agent profile order <agent> <profile>…|--clear [--workspace <name>]`"),
        // TOOL6a (D130 §16.6): how a list is used, the terminal's door; the screen's controls and Ask Daoris's `use` door
        // (`use`, `keep`, `early`, `near`) are TOOL4g's.
        ("choose how a list is used, for the machine or a workspace: make the most of its accounts (`goal`) or one by one "
            + "in order (`order`), one kept for conversations, and switching before a limit", "(no screen yet)",
            "`daoris agent profile use <agent> [goal|order] [--keep <account>|--no-keep] [--early on|off] [--near <percent>] "
            + "[--workspace <name>]` (no flag prints them; `--clear` returns to today's defaults)"),
        ("end an account's cool-off now, after its agent hit a limit", "(no screen yet)",
            "`daoris agent profile ready <agent> <profile>|--own` (`--own` is the tool's own sign-in)"),
        // HELP6: the doors built since, which the helper now proposes too.
        ("update an agent, or pin it to one version", "Settings → Agents → Update, Pin a version",
            "`daoris agent update <agent>`, `daoris agent pin <agent> <version>`"),
        ("set an account's own model and effort", "Settings → Agents → Model & effort",
            "`daoris agent settings <agent> --account <name> model <model> effort <effort>`"),
        ("delete a quest or an ask made by mistake", "Quests → the quest's drawer, or the ask's record → Delete…",
            "`daoris-driver quest delete <id>`, `daoris-driver ask --delete <id>`"),
        // PLUG9: the card installs one that landed; the screen switches one installed, installs one of
        // Daoris's own (d) and updates one from where it came from (c).
        ("add a plugin that has landed, or switch one on or off", "Settings → Plugins (its switch)",
            "`daoris plugin add <folder>`, `daoris plugin enable|disable <id>`"),
        ("install one of Daoris's own plugins, or update one from where it came from",
            "Settings → Plugins (Install beside Daoris's own; Update on an installed one's row)",
            "`daoris plugin add --offer <id>`, `daoris plugin update <id>`"),
        ("choose Daoris's browser, where the page's links open, whether extensions are offered, and its favorites",
            "Settings → Browser",
            "`daoris browser use daoris|edge`, `daoris browser links system|daoris`, `daoris browser extensions offer|refuse`, "
            + "`daoris browser favorite add|remove <address>`"),
        ("start a task", "Quests → Ask", "`daoris-driver ask --workspace <name> \"…\"`"),
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
