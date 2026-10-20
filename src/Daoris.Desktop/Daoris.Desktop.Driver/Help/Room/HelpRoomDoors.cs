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
    /// <summary>A repository's Setup (UX6f, D150 §4.2), where its own values are set, before the section that holds one.</summary>
    private const string Setup = "Repositories → the repository's page → Setup → ";

    /// <summary>A workspace's page (UX6g, D150 §3.1), where its own values, branches and rules are, before its tab.</summary>
    private const string Ws = "Repositories → the workspace's page → ";

    /// <summary>An agent's page in the Agents place (UX6e, D150 §5.2), before the section or press that holds a change.</summary>
    private const string Agent = "Agents → the agent's page → ";

    /// <summary>What each change is, where it is on the screen, and the command that does the same (D50).</summary>
    internal static readonly (string To, string Screen, string Terminal)[] Doors =
    [
        // SETUP1b: the page hands the helper a first message asking to be walked through it, naming the
        // steps by these titles, so it is told the guide exists and what each step is done on. UX6i2a: named Get started
        // since UX6j (D150 §2), in Settings and on Help's row.
        ("walk through setting this machine up: an agent, Ask Daoris's agent, a workspace and its repositories, "
            + "what is driven, how work lands, what agents may do", "Settings → Get started (Help → *Get started*)",
            "(each step shows its own command there)"),
        // HELPSETUP1: a repository's own values are on its Setup since UX6f (D150 §4.2); a workspace's are on its page
        // since UX6g (D150 §3.1).
        ("drive a repository, or stop", Setup + "Driving", "`daoris driver drive|undrive <repository>`"),
        // PAUSE1b (D132 §13): a repository's hold, never a pause; a pause is an ask's or a quest's, and stops what runs.
        ("hold one, so nothing new starts there, or resume it", Setup + "Driving", "`daoris driver hold|resume <repository>`"),
        ("give its sessions their own tree", Setup + "Driving", "`daoris driver trees <repository> on|off`"),
        // LAYOUT7: the set-up press's terminal door; LAYOUT8 (D170 point 1): its screen, the page's press with its plan first,
        // and Ask Daoris's `setup` kind, which proposes it.
        ("set a repository up for every agent: one quest to its own session, which takes up the doctrine, writes down what "
            + "the repository owns and its brief on its own branch (`--plan` shows what was read and the quest, and asks nothing; "
            + "Ask Daoris proposes it)",
            "Repositories → the repository's page → Details → Set up for agents (one not adopted, with a checkout here)",
            "`daoris-driver setup <repository> [--plan]`"),
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
        ("set the line its work grows from and lands on", Setup + "Line and landing; a workspace's, " + Ws + "Setup → Defaults → Line",
            "`daoris driver line <repository> <branch>|--clear` (`--workspace <name>` for a whole workspace)"),
        // DOORS1a (D164's amendment point 2): the ticket's key and the squash (PROCESS1b-i, -ii), which Ask Daoris's `landing`
        // door takes too, keeping the ones set there that a card does not name.
        ("set how accepted work lands", Setup + "Line and landing; a workspace's, " + Ws + "Setup → Defaults → How work lands",
            "`daoris driver landing <repository> merge|branch <pattern>|--clear` (`--workspace <name>`, `--tidy`, "
            + "and on a branch `--plugin <id>`: an installed plugin that pushes it and opens the pull request, "
            + "`--auto-accept`: a quest's done lands it with no press, `--key <shape>|--no-key`: the shape a `{ticket}` in the "
            + "pattern is found by in the title or the request, the session's id where none is, and `--squash|--no-squash`: "
            + "Accept makes the pull request's branch one commit of the session's work)"),
        // DOORS1a (D164's amendment points 1, 3 and 5): how work is committed; Ask Daoris proposes it as the `setting` kind's
        // `commits` door, its convention seeded from the repository's own instructions where the person asks. Setup's row since DOORS1b.
        ("set how a repository's work is committed, or a workspace's: as it goes (each piece that passes its checks; the "
            + "default), each commit waiting for your yes, or one commit when the work is done, and your words on how a commit "
            + "message is written (Ask Daoris proposes it, and may quote the convention from the repository's own instructions "
            + "for you to apply)",
            Setup + "Line and landing → Commits; a workspace's, " + Ws + "Setup → Defaults → Commits",
            "`daoris driver commits <repository> --mode as-you-go|ask|at-end [--convention \"…\"|--no-convention]` "
            + "(`--workspace <name>` for a whole workspace), `daoris driver commits <repository> --clear`"),
        // DOORS1a (D163 point 3): what a helper an agent asks for does; Ask Daoris proposes it as the `setting` kind's `helpers`
        // door. Setup's row since DOORS1b. Before the review and the opinion, whose rows the workflow's follows.
        ("set what a helper a repository's agent asks for does, or a workspace's: wait for your yes (the default), start at once "
            + "within the limits, or not be offered, and the other agents it may ask, in the order they are tried (Ask Daoris "
            + "proposes it)",
            Setup + "Sessions → Helpers; a workspace's, " + Ws + "Setup → Defaults → Helpers",
            "`daoris driver helpers <repository> --mode ask|on|off [--agents <adapter,adapter>]` (`--workspace <name>` for a "
            + "whole workspace), `daoris driver helpers <repository> --clear`"),
        // REVIEWENV1a (D154 point 2): where work is shown to the person before it is offered to land; Ask Daoris proposes it
        // as the `setting` kind's `review` door. The landing's gate reads it since REVIEWENV1c, and the room says what lets work
        // that waits go, and that the intake composes no set-up step yet (REVIEWENV1c2, REVIEWENV1f), as every door does.
        ("declare where a repository's work is shown to you before it is offered to land, or a workspace's: a review "
            + "environment, local (shown in Daoris's browser at the address where the app runs) or deployed (by the "
            + "repository's documented procedure), and whether work waits for your look (work that waits lands only once you "
            + "say it is reviewed, `daoris-driver quest review <quest> reviewed`, or skip the review; no set-up step is composed "
            + "for you yet; production is never one)",
            Setup + "Line and landing → Review before landing; a workspace's, Repositories → the workspace's page → Setup → Defaults",
            "`daoris driver review <repository> <environment> --kind local|deployed --procedure <path> [--address <url>] "
            + "[--run \"<command>\"] [--required|--not-required]` (`--workspace <name>` for a whole workspace), "
            + "`daoris driver review <repository> none|--drop <environment>|--clear`"),
        // XAGENT1a (D155 point 3): which other agent reads work before it lands; Ask Daoris proposes it as the `setting` kind's
        // `opinion` door. The gate reads it since XAGENT1f, and the room says what it does in the sentences every door says after
        // a rule's own, read from the twin (XAGENT1f4), as the review's row says its gate's.
        ("declare which other agent reads a repository's work before it lands, or a workspace's: reviewers named in the order "
            + "they are tried, another maker's agent by your naming, each reading in a copy of its own that nothing is taken back "
            + "from, at landing or each step, whether work waits for you when none can, whether it may run what is declared safe, "
            + "and how long a pass may take (never on unless you name a reviewer). Each door then says what the gate does: "
            + $"{OpinionRules.Waiting} {OpinionRules.StepWaiting} {OpinionRules.SafeNotHanded}",
            Setup + "Line and landing → Second opinion before landing; a workspace's, Repositories → the workspace's page → Setup → Defaults",
            "`daoris driver opinion <repository> --reviewers <adapter,adapter> [--on landing,steps] [--required|--not-required] "
            + "[--verify|--no-verify] [--minutes <n>] [--recheck|--no-recheck]` (`--workspace <name>` for a whole workspace), "
            + "`daoris driver opinion <repository> none|--clear`"),
        // WORKFLOW1c4: Current drawn from the rules above (WORKFLOW1a's verb, WORKFLOW1b's tab, D157 point 7). Exempt from Ask
        // Daoris, since it reads (HelpCoverageTests' `workflow show`); a change is a rule's, on the Setup row a step opens.
        ("see how a repository's work moves, or a workspace's, read from its rules as they stand at each gate: each step they "
            + "make (the work, a second opinion, your look, the landing, a pull request a plugin opens after it) and the plugins "
            + "that may hold a start, who takes part (agent alone, agent + you, you, automatic) and who acts, where each was set, "
            + "and what Daoris cannot do of it yet; a step opens the Setup row that sets it, where a change is made (Ask Daoris "
            + "never proposes it: it reads and changes nothing)",
            "Repositories → the repository's page → Workflow; a workspace's, Repositories → the workspace's page → Workflow",
            "`daoris driver workflow show --repository <name>|--workspace <name>`"),
        // WORKFLOW1d (D157 points 5 and 6): a named workflow, its versions never edited; its screen is WORKFLOW1g's. Ask Daoris's
        // `workflow` kind takes `new` and `edit` since WORKFLOW1h-b, a whole version; `apply` and `import` are WORKFLOW1h2's
        // (HelpCoverageTests). WORKFLOW1e chooses one, and every gate reads the version a run bound (WORKFLOW1f).
        ("name a workflow: its steps from a preset, a repository's Current or another workflow, each change saved as its next "
            + "version, never edited, said step by step with your part first (Ask Daoris proposes a whole version, new or after "
            + "the newest, which you apply; not an import yet)",
            "(no screen yet)",
            "`daoris driver workflow new <id> --from <preset>|current:<repository>|<id>[@<version>]`, `daoris driver workflow "
            + "edit|apply|export|import …`, `daoris driver workflow list`, `daoris driver workflow show <id>[@<version>]`"),
        // WORKFLOW1e (D157 point 10): which workflow work follows, by repository, workspace and kind of task, the first level
        // that names one deciding; a run binds it at its first start, and every gate reads that version (WORKFLOW1f). Ask Daoris's
        // `workflow` kind takes `use` since WORKFLOW1h-b, with the version it proposes; `kind` is WORKFLOW1h2's.
        ("choose which workflow a repository's or a workspace's work follows, or a kind of task's, and declare a workspace's kinds "
            + "with the paths their work keeps to (a repository's choice replaces its workspace's whole; each run binds the "
            + "newest version at its first start, and how it lands, your look and its second opinion follow that version) "
            + "(Ask Daoris proposes the choice with a version it proposes, as a repository's or a workspace's default; not a kind yet)",
            "(no screen yet)",
            "`daoris driver workflow use <id>|current|--clear --repository <name>|--workspace <name> [--kind <kind>]`, "
            + "`daoris driver workflow kind <workspace> <kind> --label \"…\" [--paths <path,…>]|--drop`, "
            + "`daoris driver workflow show --repository <name>|--workspace <name> [--kind <kind>]`"),
        // WSR6: after a pull request merges — the line pulled, what merged deleted, what still works replayed onto it. It
        // takes the repositories holding Daoris's branches, and the others where included (WSR7, D112); one workspace's
        // alone with `--workspace`, as its Branches tab does (BRSCOPE1a).
        ("bring a repository up to date after its pull request merged: fetch and fast-forward the line, delete the branches "
            + "whose work reached it, replay the branches still at work onto it (Daoris fetches, never pushes; it takes the "
            + "repositories holding Daoris's branches, and another where named or included)",
            Ws + "Branches → Updates",
            "`daoris-driver trees sync [--repository <name>] [--workspace <name>] [--all] [--yes]`"),
        ("clean up session branches whose work landed, and branches a landing made whose work reached the line",
            Ws + "Branches → Session branches", "`daoris-driver trees clean [--workspace <name>]`"),
        // LAND3: a failed or superseded attempt's branch holds commits on no branch of the person's, so only they discard it.
        ("discard a failed or superseded session's branch, with its tree where it is still here",
            "Sessions → the session's review → Discard, while its tree is here",
            "`daoris-driver trees remove <session|branch> [--repository <name>] --force`"),
        // WSR5b: a landed branch handed to a landing plugin after its landing.
        ("hand a branch a landing made to a landing plugin, to push it and open the pull request",
            "Sessions → the session's review → Hand to <plugin>", "`daoris-driver trees hand <session|branch> [--plugin <id>]`"),
        // PLUGHOOK1c (D148 point 2): Ask again from a terminal, exempt from Ask Daoris as a read; the review's press is PLUGHOOK1d's.
        ("ask a landed branch's plugin again whether its pull request completed, and see what that answer proves on this machine "
            + "(Ask Daoris never proposes it: it changes nothing of yours or on the platform, and the room already says what was "
            + "last answered)",
            "(no screen yet)", "`daoris-driver trees state <session|branch> [--repository <name>]`"),
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
        // TOOL4e: the terminal's door onto the default cool-off; TOOL4g gave it Settings → Driver's control, and TOOL4l-b
        // Ask Daoris's `setting` kind's `cooloff` (HelpCoverageTests).
        ("set how long an account cools when its agent hits a limit and names no time", "Settings → Driver → Cool-off when no time is named",
            "`daoris driver cooloff <minutes>`"),
        ("bound how many sessions run at once", "Settings → Driver → Sessions at once on this machine", "`daoris driver cap <n>`"),
        ("say so when a session parks", "Settings → Driver", "`daoris driver notify on|off`"),
        // UX6e2: the rules for every session and Daoris's defaults are the agent's page's (UX6e); a repository's are on its
        // Setup (UX6f); a workspace's are on its page (UX6g2a).
        ("allow, ask or deny what an agent may do",
            Agent + "What it may do; a repository's, " + Setup + "Reach; a workspace's, " + Ws + "Setup → Remote and reach", "`daoris agent rules …`"),
        // D107: reading other checkouts is on unless switched off; writing into one is a declared relationship.
        ("let agents read a repository's checkout, or not; let one repository's sessions write into another",
            Setup + "Reach; a workspace's reading, " + Ws + "Setup → Defaults → Read by agents outside",
            "`daoris driver across <repository> read on|off|--clear` (`--workspace <name>` for a whole workspace), "
            + "`daoris driver across <repository> write-to <other> [--clear]`"),
        // KNOWUSE1b: what the person says holds for every session in one repository, kept on this machine (D135 §3).
        ("keep a standing answer for a repository, handed to every session there: which writes are allowed, where to test",
            Setup + "Sessions", "`daoris driver standing <repository> \"…\"|--clear`"),
        // LANG1c (D142 points 7–8): the work's language, apart from the window's; Ask Daoris proposes it as the `setting`
        // kind's `language` door since LANG1c2.
        ("set the language a repository's sessions write to you in, or a workspace's: their questions, closing notes, decline "
            + "reasons and last words (the window's own language is Settings → Appearance, and neither sets the other)",
            Setup + "Sessions; a workspace's, " + Ws + "Setup → Defaults → Session language",
            "`daoris driver language <repository> en|zh|--clear` (`--workspace <name>` for a whole workspace)"),
        // UX6e2: an agent's accounts, ways in and model are on its page in the Agents place since UX6e (D150 §5.2). ACCT1: a
        // sign-in reaches the account named, never a new folder; a new account joins the lists named at its end. CODEXLOGIN1:
        // Codex's device code, in place of its browser, by `--device-code`.
        ("sign an agent in, or add an account", Agent + "Accounts",
            "`daoris agent login <agent> --profile <account>` (an account that is here, never a new one), "
            + "`daoris agent login <agent> --new [--name <name>] [--join <workspace>] [--join-machine]`, "
            + "either with `--device-code` for Codex's device code in place of its browser"),
        // ACCT2: an account's name is the person's, its id the folder's; ACCT1: an account in no list is put in one. UX7b
        // builds the screen's halves.
        ("name an account: its id stays, so every list, default and record keeps it", "(no screen yet)",
            "`daoris agent profile rename <agent> <account> <name>`"),
        ("put an account in a workspace's list, or this machine's, so starts run on it", "(no screen yet)",
            "`daoris agent profile join <agent> <account> <workspace>…|--machine`"),
        // HELP9 named it while Ask Daoris owed it (D110); HELP10 proposes it as an agent's `default`. LEFT3 gave the
        // screen's clear its terminal door, which Ask Daoris still owes (HelpCoverageTests' Forms).
        ("choose which account an agent's sessions use, for the machine or a workspace",
            Agent + "an account's ⋯ → Use by default, Use by default in a workspace",
            "`daoris agent profile default <agent> <profile>|--clear [--workspace <name>]` (`--clear` names none again: "
            + "Daoris's accounts serve again, or for a workspace the machine's default)"),
        // TOOL4e (D125 §6): the order and *Try now*, the terminal's doors; TOOL4g gave them the screen, and TOOL4l-b Ask Daoris's
        // `order` and `ready` (HelpCoverageTests).
        ("choose the accounts rotation may use, in order, for the machine or a workspace (an account not listed never "
            + "rotates)", Agent + "How accounts are used → Use, up and down; and Workspaces → Its own accounts",
            "`daoris agent profile order <agent> <profile>…|--clear [--workspace <name>]`"),
        // TOOL6a (D130 §16.6): how a list is used, the terminal's door; TOOL4g gave it the screen's controls, and judges Ask
        // Daoris's `use` door (`use`, `keep`, since SWITCH1d `switch` and `curve`), listed since TOOL4l-b.
        ("choose how a list is used, for the machine or a workspace: make the most of its accounts (`goal`) or one by one "
            + "in order (`order`), one kept for conversations, and when to switch accounts, by Daoris's usage curve, your own, "
            + "or not while a session runs",
            Agent + "How accounts are used → Use accounts, Keep for conversations, Switch accounts",
            "`daoris agent profile use <agent> [goal|order] [--keep <account>|--no-keep] [--switch smart|curve|off] "
            + "[--curve <window>=<elapsed>:<used>,…] [--no-curve] [--workspace <name>]` (no flag prints them; `--clear` returns "
            + "to today's defaults)"),
        ("end an account's cool-off now, after its agent hit a limit", Agent + "the account's Try now",
            "`daoris agent profile ready <agent> <profile>|--own` (`--own` only ends an old cool-off of the tool's own sign-in, which Daoris no longer starts sessions in)"),
        // HELP6: the doors built since, which the helper now proposes too.
        ("update an agent, or pin it to one version", Agent + "Ways in → Update, Pin a version",
            "`daoris agent update <agent>`, `daoris agent pin <agent> <version>`"),
        ("set an account's own model and effort", Agent + "Model and effort",
            "`daoris agent settings <agent> --account <name> model <model> effort <effort>`"),
        // PAUSE1b (D132 §7.2): the terminal's doors; PAUSE1e's pages' and a session's acts (§7.1), and Ask Daoris's `pause` kind,
        // its `pause` and `resume` doors, since PAUSE1f-b (HelpCoverageTests).
        ("pause an ask's work, or one quest's, on this machine: what of it runs is stopped as your stop, and nothing of it "
            + "starts until you resume it, which carries it on where it stood",
            "Quests → the ask's page or the quest's page → Pause…, Resume; Sessions → the session's header or its row's ⋯ → "
            + "Pause quest…, Pause ask…, Resume quest, Resume ask",
            "`daoris-driver ask --pause|--resume <id>`, `daoris-driver quest pause|resume <id>`"),
        // PAUSE1d (D132 §7.4): exempt from Ask Daoris, since its reason is the person's answer (D37), as Decline… is; the pages'
        // acts are PAUSE1e's, on the ask's and the quest's pages alone (§7.1).
        ("abandon an ask's work, or one quest's, on this machine: listed first, then on your reason each quest is declined, what "
            + "only Daoris holds of it is discarded and its sessions archived (Ask Daoris never proposes it: the reason is your answer)",
            "Quests → the ask's page or the quest's page → Abandon…",
            "`daoris-driver ask --abandon <id> [--reason \"…\" --yes]`, `daoris-driver quest abandon <id> [--reason \"…\" --yes]`"),
        ("delete a quest or an ask made by mistake", "Quests → the quest's drawer, or the ask's record → Delete…",
            "`daoris-driver quest delete <id>`, `daoris-driver ask --delete <id>`"),
        // SESSUX1h2b (D126 §5.2, §5.3, §7.3): Sessions' archive, archive what ended and unarchive, the `session` kind's doors; a
        // session's stop is its own conversation (SESSUX1h1), named on its page.
        ("archive sessions on this machine, so they leave the list with their records whole, or every one that ended, listed "
            + "first; or unarchive one (a session running, waiting on you or to review is never archived)",
            "Sessions → a session's ⋯ → Archive or Unarchive; the list's ⋯ → Archive what ended…, and Show archived",
            "`daoris-driver sessions archive <id>… | --ended [--yes]`, `daoris-driver sessions unarchive <id>…`"),
        // SESSUX1h2b (D126 §5.4): the `delete` kind's `session` door.
        ("delete a conversation that served no quest, with what this machine kept of it: its words, its transcript and its "
            + "files (one that worked on a quest is archived instead)",
            "Sessions → a session's ⋯ → Delete…", "`daoris-driver sessions delete <id>`"),
        // HIST1f-b (D153, design §6.1, §6.2, §6.4): the pages' clears, a workspace's reading first, and the terminal's history
        // verbs; the `clear` kind's `quest`, `ask`, `failed` (HIST1f-d) and `workspace` doors.
        ("clear finished history from this machine: a closed quest's work, an ask's with every quest it became, a closed quest's "
            + "failed sessions, or a workspace's, listed first, then pressed; what is open, running or waiting on you is kept, and "
            + "nothing brings it back",
            "Quests → the quest's page or the ask's page → its header's ⋯ → Clear from this machine…, and the quest's Clear failed "
            + "sessions…; " + Ws + "Details → Kept on this machine → Clear history…",
            "`daoris-driver history [--workspace <name>]`, `daoris-driver history clear --workspace <name> [--yes]`, "
            + "`daoris-driver quest clear <id> [--failed] [--yes]`, `daoris-driver ask --clear <id> [--yes]`"),
        // DRIFT1d: the person's yes to a done's departure, the terminal's door; DRIFT1d2 gave it the quest page's yes and
        // Ask Daoris's `accept` kind.
        ("accept a quest's departure from what you required, so what it held goes on: the chain's next step, a quest "
            + "waiting on it", "Quests → the quest's page → Accept the departure", "`daoris-driver quest accept <id>`"),
        // QUESTCLOSE1b3: the person's own done, with the note they give (D126's QUESTCLOSE1): the page's press is its own
        // conversation on Quests, which Ask Daoris takes the person to and never writes (D161's ENTRY1 note).
        ("mark a quest done as yours, with a note if you give one: what a finish at a checkpoint says, said on the quest itself "
            + "(Ask Daoris takes you to the quest's page and never writes the done)",
            "Quests → the quest's page → Mark done…", "`daoris-driver quest done <id> [--note \"…\"]`"),
        // EVID1b (D144 §5): reading a done's evidence again, the terminal's door; the quest page's *Check again* and Ask Daoris's
        // check card are EVID1c's, owed meanwhile (HelpCoverageTests).
        ("read a done's evidence again, so a commit that holds it now lifts its hold: at the commit a session's end here read, "
            + "or one named after it on the same history (a done no session here ended needs the commit named)",
            "(no screen yet)", "`daoris-driver quest check <id> [--commit <sha>]`"),
        // PLUG9: the card installs one that landed; the screen switches one installed, installs one of
        // Daoris's own (d) and updates one from where it came from (c). UX6i2a: the Plugins place's since UX6j (D150 §2),
        // by its list's group and its page's presses.
        ("add a plugin that has landed, or switch one on or off", "Plugins → the plugin's page → Turn on or Turn off",
            "`daoris plugin add <folder>`, `daoris plugin enable|disable <id>`"),
        ("install one of Daoris's own plugins, or update one from where it came from",
            "Plugins → Daoris's own plugins → Install; Plugins → the plugin's page → Update…",
            "`daoris plugin add --offer <id>`, `daoris plugin update <id>`"),
        // HELPPLUG1a's tightening (D170's note): what a plugin's server offers Ask Daoris reaches it only as the person approved
        // it, exact. HELPPLUG1c: the plugin kind's `approve` and `withdraw`, a card naming every tool, the person's press.
        ("let Ask Daoris call the exact tools a plugin's server offers it, all its reads and each write outside Daoris you "
            + "accept, or take that back; an update that adds or widens a tool asks again",
            "Plugins → the plugin's page → Ask Daoris → Approve, Withdraw",
            "`daoris-driver plugins approve <id> <server> [--reads-only | --writes <tool,…>]`, `daoris-driver plugins withdraw <id> <server>`"),
        ("choose Daoris's browser, where the page's links open, whether extensions are offered, and its favorites",
            "Settings → Browser",
            "`daoris browser use daoris|edge`, `daoris browser links system|daoris`, `daoris browser extensions offer|refuse`, "
            + "`daoris browser favorite add|remove <address>`"),
        // UPDATE1f (D139): exempt from Ask Daoris, since an update is the person's act on the application; the room names both
        // doors, as it does for `sessions say`.
        ("install the build staged beside this install: when its work allows (nothing new starts, and once no driven session "
            + "runs Daoris closes and starts again on it), now (Daoris closes at once, ending what runs as a close does), or not "
            + "now (it stays staged, for that build only); plain `update` says what is staged and how the last update ended "
            + "(Ask Daoris never proposes it: an update is your act on the application)",
            "the update banner, and Settings → Driver → Update: *Update when idle*, *Update now*, *Not now*",
            "`daoris-driver update --when-idle|--now|--cancel`, `daoris-driver update`"),
        ("start a task", "Quests → Ask", "`daoris-driver ask --workspace <name> \"…\"`"),
        // REVIEWENV1j (D154 point 3, D50): the person's review choice, the composer's and the ask page's (REVIEWENV1g), and at a
        // done quest's gate, where `off` is the skip and an environment is *Set it up* (design §3.6).
        ("choose whether an ask's work is reviewed before it lands, and where: as each repository's rule says, in the default "
            + "environment, in one its workspace declares, or not at all; at a done quest's gate, skip the review or set it up",
            "Quests → Ask → Review before it lands, and the ask's page; the gate's *Skip the review for this work…* and "
            + "*Set it up in `<environment>`*",
            "`daoris-driver ask … --short \"…\"`, `daoris-driver ask … --review rule|on|<environment>|off`, `daoris-driver ask --set-review <id> on|<environment>|off "
            + "[\"…\"]`, `daoris-driver quest review <quest> off [\"…\"]`, `daoris-driver quest review <quest> on|<environment>`"),
        // WORKFLOW1c4: the run (WORKFLOW1c's view, line and door; WORKFLOW1c2's verb; the workflow design §7). Exempt from Ask
        // Daoris, since it reads (D157, D110 §4); each press a waiting step needs is a row of its own owner's.
        ("see where a piece of work stands in its workflow: each step's state (waiting on you, waiting on an agent, working, "
            + "done, …) and what it says, the step the run stands at, and a door to where each step's record is; a session's run "
            + "is its quest's, an ask's one per repository its work reaches, and a chat has none (Ask Daoris never proposes it: "
            + "it reads and changes nothing, and each press a waiting step needs is its owner's: the step's door opens it, or the "
            + "view names its command)",
            "Sessions → the session's *Workflow* view, in the right side bar beside Timeline and Review (View → Workflow); "
            + "*Workflow:* in a quest's page's head and an ask's, and *Workflow* on a row of What needs you, each attending its "
            + "session in Sessions with the view open",
            "`daoris-driver workflow run --session|--quest|--ask <id>`"),
        // WORKFLOW1e (D157 point 10, D156): an ask's kind and workflow, the person's door; the composer's and the ask page's are
        // WORKFLOW1g's, and the intake's proposal of a kind WORKFLOW1i's.
        ("say what kind of task an ask is and which workflow its work follows (yours alone: an agent proposes a kind and never sets "
            + "one; each run binds what is chosen at its first start)",
            "(no screen yet)",
            "`daoris-driver ask … --kind <kind> [--workflow <id>|current]`, `daoris-driver ask --set-workflow <id> [--kind <kind>] "
            + "[--workflow <id>|current]|--clear [\"…\"]`"),
        // WORKFLOW1f (D157 point 10, the workflow design §4.4): the person's Keep where a kind's paths hold its work. Exempt from Ask
        // Daoris (HelpCoverageTests): keeping a workflow that asks less of the person is their say-so, as going on without a second
        // opinion is. The screen's press, and §4.4's other two, are the editor's row (WORKFLOW1g).
        ("keep the workflow a kind of task chose for a piece of work that changed paths outside the kind's, where that workflow "
            + "asks less of you than the one it would follow without the kind (yours alone, with your words: Ask Daoris never "
            + "proposes it, and nothing switches by itself)",
            "(no screen yet)",
            "`daoris-driver workflow keep <session> [\"…\"]`"),
        // XAGENT1f4 (D155 point 10, design §8.5, §9): the person's presses at the second opinion's gate, the terminal's
        // `daoris-driver opinion` and the screen's (XAGENT1g). Each is exempt from Ask Daoris (HelpCoverageTests): a pass spends an
        // account at the person's choice and a stop is theirs, and going on, or reading it themselves, is their judgement (D110).
        ("ask another agent to read a session's work now, again, or after it could not, or the same agent in a fresh "
            + "conversation, which is never an independent reading; see where its second opinion stands; or stop a reading, which "
            + "then gives no opinion (Ask Daoris never proposes these: a pass spends an account at your choice, and a stop is yours)",
            "Sessions → the session's page → Second opinion: *Ask now*, *Ask again*, *Try again*, *Ask the same agent, fresh*, *Stop…*",
            "`daoris-driver opinion ask <session> [--reviewer <adapter>] [--same-agent] [\"…\"]`, "
            + "`daoris-driver opinion show <session|opinion>`, `daoris-driver opinion stop <opinion>`"),
        ("answer a second opinion's gate yourself: go on without a settled one, what is unsettled staying so, or say you looked "
            + "at the work yourself in place of another agent's reading; each is kept with your words at the commit it was given "
            + "at, and nothing lands by it (Ask Daoris never proposes these: each is your judgement)",
            "Sessions → the session's page → Second opinion, and What needs you: *Go on anyway…*, *I looked myself…*",
            "`daoris-driver opinion anyway <session> [\"…\"]`, `daoris-driver opinion myself <session> [\"…\"]`"),
        ("answer what waits on the person", "Sessions, and what needs you", "`daoris-driver answer`"),
        // MSG1e (D137 §5.4): exempt from Ask Daoris, since the words are the person's (D133 §1); the room names both doors.
        ("say something to a session of this machine's, running, parked or ended: it reads your words at its next step or when "
            + "its turn ends, or the same session goes on with them (Ask Daoris never proposes it: the words are yours)",
            "Sessions → the session's page → its box", "`daoris-driver sessions say <id> \"…\" [--file <path>]…`"),
        // ASKHIST1: exempt from Ask Daoris, since its conversations and what the person keeps of them are the person's own; the
        // room names both doors, so the helper points there when asked about an earlier conversation. ASKSCOPE1e: the list's
        // workspace flags, as its usage spells them (D158's ASKSCOPE1d note point 1).
        ("find an earlier conversation with Ask Daoris and go on in it, start a new one from its words, or name, pin or delete "
            + "one; they are kept on this machine only (Ask Daoris never proposes it: the conversations are yours)",
            "Ask Daoris → History",
            "`daoris-driver help list [--workspace <name> | --all] [--search \"…\"]`, `daoris-driver help resume <id> \"…\"`, "
            + "`daoris-driver help rename|pin|unpin|delete <id>`"),
        // KNOWUSE1a: a go-ahead is the person's yes or no to an act outside a repository, asked once on the ask. Ask Daoris
        // never answers one: the production acts stay the person's (D135 §2).
        ("answer a go-ahead a session asked on an ask, for an act outside its repository: yes or no, which every session on "
            + "the ask is handed", "Quests → the ask's page → Go-aheads", "`daoris-driver ask --go-ahead <id> <n> approve|refuse [\"…\"]`"),
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
