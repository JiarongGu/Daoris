namespace Daoris.Driver;

/// <summary>The doors for keeping a machine's branches, roles, settings and agents' reach: what a person sets once and leaves.</summary>
internal static class HelpDoorRowsMachine
{
    private const string Setup = HelpDoorPlaces.Setup;
    private const string Ws = HelpDoorPlaces.Ws;
    private const string Agent = HelpDoorPlaces.Agent;

    internal static readonly HelpDoor[] Rows =
    [
        // After a pull request merges: the line pulled, what merged deleted, what still works replayed onto it. It takes the
        // repositories holding Daoris's branches, and the others where included; one workspace's alone with `--workspace`.
        ("bring a repository up to date after its pull request merged: fetch and fast-forward the line, delete the branches "
            + "whose work reached it, replay the branches still at work onto it (Daoris fetches, never pushes; it takes the "
            + "repositories holding Daoris's branches, and another where named or included)",
            Ws + "Branches → Updates",
            "`daoris tree sync [--repository <name>] [--workspace <name>] [--all] [--yes]`"),
        ("clean up session branches whose work landed, and branches a landing made whose work reached the line",
            Ws + "Branches → Session branches", "`daoris tree clean [--workspace <name>]`"),
        // A failed or superseded attempt's branch holds commits on no branch of the person's, so only they discard it.
        ("discard a failed or superseded session's branch, with its tree where it is still here",
            "Sessions → the session's review → Discard, while its tree is here",
            "`daoris tree remove <session|branch> [--repository <name>] --force`"),
        new("ask a landed branch's plugin again whether its pull request completed, and see what that answer proves on this machine",
            "(no screen yet)", "`daoris tree state <session|branch> [--repository <name>]`",
            new HelpDoorAsk.Never("it changes nothing of yours or on the platform, and the room already says what was last answered")),
        // A role's agents through the role door alone, Build, Intake and Ask Daoris among them.
        ("choose who does each kind of work, with which agent and model: driven sessions (Build), asks (Intake), Ask Daoris and every "
            + "other role", "Agents → Roles → Working roles", "`daoris role set <role> \"<agent> [<model>|-] [<effort>|-]\" …|off`"),
        // The role catalogue, a role added, changed, removed or given back its seed, or every seeded role at once with added roles kept.
        ("add, change or remove a role that takes a kind of work, give a seeded one back its default, or every seeded one at once, "
            + "added roles kept", "Agents → Roles → Working roles, and its *Restore all role defaults*",
            "`daoris role set|remove|default <id> …`, `daoris role default --all`"),
        // How a role spends its choices; the terminal names a choice by its number.
        ("choose how a role spends its choices: first that can run, or spread by weight, with a share, a daily budget and the work a choice takes",
            "Agents → Roles → Working roles, a role's Edit",
            "`daoris role policy <id> [--preset first|even|weighted] [--rule order|weight] [--weight <n>=<0-10>] [--clear]`"),
        // The quest's drawer is a page; the press judges from the planner's verdict whether to mark the quest forgiven or release the stop.
        ("start a quest again that its failed sessions parked or your stop holds", "Quests → the quest's page → Try again", "`daoris quest retry <quest>`"),
        // The terminal's door onto the default cool-off.
        ("set how long an account cools when its agent hits a limit and names no time", "Settings → Driver → Cool-off when no time is named",
            "`daoris settings cooloff <minutes>`"),
        // The one quiet setting: a session runs as long as it works, and this is how long it may say nothing before Daoris looks in.
        ("set how long a session may say nothing and run nothing before Daoris looks in on it", "Settings → Driver → Look in on a quiet session after",
            "`daoris settings quiet <minutes>`"),
        // A repository's remote or a workspace's host pointed at a git identity that exists, pressed; setting one up, testing or
        // removing it is the person's own act.
        ("point a repository's remote, or a workspace's host, at a git identity already set up",
            "Repository → Setup → Git remote → Change…, and Workspace → Setup → Git",
            "`daoris git use <repository> [--remote <name>] <identity>|own|--clear`, `daoris git use --workspace <workspace> --host <host> <identity>|own|--clear`"),
        // How long each kind Daoris keeps is kept.
        ("set how long Daoris keeps set-aside trees, unused downloads, the machine log or finished work's records", "Settings → Disk space → a kind's Change…",
            "`daoris settings keep <kind> <rule>`"),
        ("bound how many sessions run at once", "Settings → Driver → Sessions at once on this machine", "`daoris settings cap <n>`"),
        ("say so when a session parks", "Settings → Driver", "`daoris settings notify on|off`"),
        // The rules for every session and Daoris's defaults are the agent's page's; a repository's are on its Setup; a workspace's
        // are on its page.
        ("allow, ask or deny what an agent may do",
            Agent + "What it may do; a repository's, " + Setup + "Reach; a workspace's, " + Ws + "Setup → Remote and reach", "`daoris agent rules …`"),
        // Reading other checkouts is on unless switched off; writing into one is a declared relationship.
        ("let agents read a repository's checkout, or not; let one repository's sessions write into another",
            Setup + "Reach; a workspace's reading, " + Ws + "Setup → Defaults → Read by agents outside",
            "`daoris place across <repository> read on|off|--clear` (`--workspace <name>` for a whole workspace), "
            + "`daoris place across <repository> write-to <other> [--clear]`"),
        // What the person says holds for every session in one repository, kept on this machine.
        ("keep a standing answer for a repository, handed to every session there: which writes are allowed, where to test",
            Setup + "Sessions", "`daoris place standing <repository> \"…\"|--clear`"),
        // The work's language, apart from the window's.
        ("set the language a repository's sessions write to you in, or a workspace's: their questions, closing notes, decline "
            + "reasons and last words (the window's own language is Settings → Appearance, and neither sets the other)",
            Setup + "Sessions; a workspace's, " + Ws + "Setup → Defaults → Session language",
            "`daoris place language <repository> en|zh|--clear` (`--workspace <name>` for a whole workspace)"),
    ];
}
