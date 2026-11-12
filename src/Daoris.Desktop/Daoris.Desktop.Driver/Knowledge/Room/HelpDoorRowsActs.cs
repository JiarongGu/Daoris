namespace Daoris.Driver;

/// <summary>The doors for the person's own acts: accounts, quests, sessions, plugins, the browser, updates and the workflow's presses.</summary>
internal static class HelpDoorRowsActs
{
    private const string Ws = HelpDoorPlaces.Ws;
    private const string Agent = HelpDoorPlaces.Agent;

    internal static readonly HelpDoor[] Rows =
    [
        // A sign-in reaches the account named, never a new folder; a new account joins the lists named at its end. Codex's device
        // code, in place of its browser, by `--device-code`.
        ("sign an agent in, or add an account", Agent + "Accounts",
            "`daoris agent login <agent> --account <account>` (an account that is here, never a new one), "
            + "`daoris agent login <agent> --new [--name <name>]` (it joins Daoris's accounts, which serve every workspace), "
            + "either with `--device-code` for Codex's device code in place of its browser"),
        // An account's name is the person's, its id the folder's. Daoris's accounts are one set, and its order says where starts begin.
        ("name an account: its id stays, so every list, default and record keeps it", "(no screen yet)",
            "`daoris agent account rename <agent> <account> <name>`"),
        // The order and *Try now*, the terminal's doors; one set's.
        ("choose the order of Daoris's accounts, one set for every workspace: where starts begin one by one, and the last tie "
            + "otherwise", Agent + "How accounts are used → Use, up and down",
            "`daoris agent account order <agent> <profile>…|--clear` (the accounts named first, the rest after; `--clear` "
            + "returns to the ids' order)"),
        // How the accounts are used, the terminal's door.
        ("choose how Daoris's accounts are used, for every workspace: make the most of them (`goal`) or one by one "
            + "in order (`order`), one kept for conversations, and when to switch accounts, by Daoris's usage curve, your own, "
            + "or not while a session runs",
            Agent + "How accounts are used → Use accounts, Keep for conversations, Switch accounts",
            "`daoris agent account use <agent> [goal|order] [--keep <account>|--no-keep] [--switch smart|curve|off] "
            + "[--curve <window>=<elapsed>:<used>,…] [--no-curve]` (no flag prints them; `--clear` returns "
            + "to today's defaults)"),
        ("end an account's cool-off now, after its agent hit a limit", Agent + "the account's Try now",
            "`daoris agent account ready <agent> <profile>|--own` (`--own` only ends an old cool-off of the tool's own sign-in, which Daoris no longer starts sessions in)"),
        // Further doors, which the helper proposes too.
        ("update an agent, or pin it to one version", Agent + "Ways in → Update, Pin a version",
            "`daoris agent update <agent>`, `daoris agent pin <agent> <version>`"),
        ("set an account's own model and effort", Agent + "Model and effort",
            "`daoris agent settings <agent> --account <name> model <model> effort <effort>`"),
        // A session's helper doors.
        ("ask another agent, read-only, one question about a session's work (or ask again after its answer, keeping the answer for "
            + "you), and pass an answer kept for you to the session that asked",
            "A session's page → Co-workers ⋯ → Bring in another agent…, a member's thread → Ask again… and Pass to the lead",
            "`daoris session helper <id> \"…\" [--agent <name>] [--after <ask>] [--to-me]`, `daoris session pass <ask>`"),
        // The terminal's doors, the pages' and a session's acts: the pause and the resume.
        ("pause an ask's work, or one quest's, on this machine: what of it runs is stopped as your stop, and nothing of it "
            + "starts until you resume it, which carries it on where it stood",
            "Quests → the ask's page or the quest's page → Pause…, Resume; Sessions → the session's header or its row's ⋯ → "
            + "Pause quest…, Pause ask…, Resume quest, Resume ask",
            "`daoris ask pause|--resume <id>`, `daoris quest pause|resume <id>`"),
        // Exempt from Ask Daoris, since its reason is the person's answer, as Decline… is; the pages' acts are on the ask's and the
        // quest's pages alone.
        new("abandon an ask's work, or one quest's, on this machine: listed first, then on your reason each quest is declined, what "
            + "only Daoris holds of it is discarded and its sessions archived",
            "Quests → the ask's page or the quest's page → Abandon…",
            "`daoris ask abandon <id> [--reason \"…\" --yes]`, `daoris quest abandon <id> [--reason \"…\" --yes]`",
            new HelpDoorAsk.Never("the reason is your answer")),
        ("delete a quest or an ask made by mistake", "Quests → the quest's drawer, or the ask's record → Delete…",
            "`daoris quest delete <id>`, `daoris ask --delete <id>`"),
        // Sessions' archive, archive what ended and unarchive; a session's stop is its own conversation, named on its page.
        ("archive sessions on this machine, so they leave the list with their records whole, or every one that ended, listed "
            + "first; or unarchive one (only a done or failed session is archived)",
            "Sessions → a session's ⋯ → Archive or Unarchive; the list's ⋯ → Archive what ended…, and Show archived",
            "`daoris session archive <id>… | --ended [--yes]`, `daoris session unarchive <id>…`"),
        // A session's delete.
        ("delete a conversation that served no quest, with what this machine kept of it: its words, its transcript and its "
            + "files (one that worked on a quest is archived instead)",
            "Sessions → a session's ⋯ → Delete…", "`daoris session delete <id>`"),
        // The pages' clears, a workspace's reading first, and the terminal's history verbs; the `clear` kind's `quest`, `ask`,
        // `failed` and `workspace` doors.
        ("clear finished history from this machine: a closed quest's work, an ask's with every quest it became, a closed quest's "
            + "failed sessions, or a workspace's, listed first, then pressed; what is open, running or waiting on you is kept, and "
            + "nothing brings it back",
            "Quests → the quest's page or the ask's page → its header's ⋯ → Clear from this machine…, and the quest's Clear failed "
            + "sessions…; " + Ws + "Details → Kept on this machine → Clear history…",
            "`daoris history [--workspace <name>]`, `daoris history clear --workspace <name> [--yes]`, "
            + "`daoris quest clear <id> [--failed] [--yes]`, `daoris ask clear <id> [--yes]`"),
        // The person's yes to a done's departure, on the quest's page and the terminal; never a proposal.
        new("accept a quest's departure from what you required, so what it held goes on: the chain's next step, a quest "
            + "waiting on it", "Quests → the quest's page → Accept the departure", "`daoris quest accept <id>`",
            new HelpDoorAsk.Never("the yes is yours")),
        // The person's own done, with the note they give: the page's press is its own conversation on Quests, which Ask Daoris
        // takes the person to and never writes.
        new("mark a quest done as yours, with a note if you give one: what a finish at a checkpoint says, said on the quest itself",
            "Quests → the quest's page → Mark done…", "`daoris quest done <id> [--note \"…\"]`",
            new HelpDoorAsk.Never("it takes you to the quest's page, and the done is yours to write")),
        // Reading a done's evidence again, the terminal's door beside the quest page's *Check again*.
        ("read a done's evidence again, so a commit that holds it now lifts its hold: at the commit a session's end here read, "
            + "or one named after it on the same history (a done no session here ended needs the commit named)",
            "(no screen yet)", "`daoris quest check <id> [--commit <sha>]`"),
        // The card installs one that landed; the screen switches one installed, installs one of Daoris's own and updates one from
        // where it came from, by the Plugins place's list and its page's presses.
        ("add a plugin that has landed, or switch one on or off", "Plugins → the plugin's page → Turn on or Turn off",
            "`daoris plugin add <folder>`, `daoris plugin enable|disable <id>`"),
        ("install one of Daoris's own plugins, or update one from where it came from",
            "Plugins → Daoris's own plugins → Install; Plugins → the plugin's page → Update…",
            "`daoris plugin add --offer <id>`, `daoris plugin update <id>`"),
        // What a plugin's server offers Ask Daoris reaches it only as the person approved it, exact: the plugin kind's `approve` and
        // `withdraw`, a card naming every tool, the person's press.
        ("let Ask Daoris call the exact tools a plugin's server offers it, all its reads and each write outside Daoris you "
            + "accept, or take that back; an update that adds or widens a tool asks again",
            "Plugins → the plugin's page → Ask Daoris → Approve, Withdraw",
            "`daoris plugin approve <id> <server> [--reads-only | --writes <tool,…>]`, `daoris plugin withdraw <id> <server>`"),
        ("choose Daoris's browser, where the page's links open, whether extensions are offered, and its favorites",
            "Settings → Browser",
            "`daoris browser use daoris|edge`, `daoris browser links system|daoris`, `daoris browser extensions offer|refuse`, "
            + "`daoris browser favorite add|remove <address>`"),
        // Exempt from Ask Daoris, since an update is the person's act on the application; the room names both doors, as it does
        // for `sessions say`.
        new("install the build staged beside this install: when its work allows (nothing new starts, and once no driven session "
            + "runs Daoris closes and starts again on it), now (Daoris closes at once, ending what runs as a close does), or not "
            + "now (it stays staged, for that build only); plain `update` says what is staged and how the last update ended. A build is "
            + "staged by the person alone, from their own terminal, and a stage drains the install; a session's terminal is refused both",
            "the update banner, and Settings → Updates: *Update when idle*, *Update now*, *Not now*",
            "`daoris update --when-idle|--now|--cancel`, `daoris update`",
            new HelpDoorAsk.Never("an update and a stage are your acts on the application, so it never stages a build, asks a session to, "
                + "or answers a session's question with one")),
        ("start a task", "Quests → Ask", "`daoris ask --workspace <name> \"…\"`"),
        // The person's review choice, the composer's and the ask page's, and at a done quest's gate, where `off` is the skip and an
        // environment is *Set it up*.
        ("choose whether an ask's work is reviewed before it lands, and where: as each repository's rule says, in the default "
            + "environment, in one its workspace declares, or not at all; at a done quest's gate, skip the review or set it up",
            "Quests → Ask → Review before it lands, and the ask's page; the gate's *Skip the review for this work…* and "
            + "*Set it up in `<environment>`*",
            "`daoris ask … --short \"…\"`, `daoris ask … --review rule|on|<environment>|off`, `daoris ask review <id> on|<environment>|off "
            + "[\"…\"]`, `daoris quest review <quest> off [\"…\"]`, `daoris quest review <quest> on|<environment>`"),
        // The run: its view, line and door. Exempt from Ask Daoris, since it reads; each press a waiting step needs is a row of its
        // own owner's.
        new("see where a piece of work stands in its workflow: each step's state (waiting on you, waiting on an agent, working, "
            + "done, …) and what it says, the step the run stands at, and a door to where each step's record is; a session's run "
            + "is its quest's, an ask's one per repository its work reaches, and a chat has none; a repository's line, its newest "
            + "verifications, each with the commit it judged, its verdict and its gates",
            "Sessions → the session's *Workflow* view, in the right side bar beside Timeline and Review (View → Workflow); "
            + "*Workflow:* in a quest's page's head and an ask's, and *Workflow* on a row of What needs you, each attending its "
            + "session in Sessions with the view open; a repository's *Verifications* on its Workflow tab",
            "`daoris workflow run --session|--quest|--ask <id>`, `daoris workflow run --line <repository>`",
            new HelpDoorAsk.Never("it reads and changes nothing, and each press a waiting step needs is its owner's: the step's door "
                + "opens it, or the view names its command")),
        // An ask's kind and workflow, the person's door; the composer's and the ask page's are owed, and the intake's proposal of a kind.
        ("say what kind of task an ask is and which workflow its work follows (yours alone: an agent proposes a kind and never sets "
            + "one; each run binds what is chosen at its first start)",
            "(no screen yet)",
            "`daoris ask … --kind <kind> [--workflow <id>|current]`, `daoris ask workflow <id> [--kind <kind>] "
            + "[--workflow <id>|current]|--clear [\"…\"]`, and for a quest no ask published, before it starts (WORKFLOW1e2), "
            + "`daoris quest workflow <id> [--kind <kind>] [--workflow <id>|current]|--clear [\"…\"]`"),
        // The person's Keep where a kind's paths hold its work. Never proposed: keeping a workflow that asks less of the person is
        // their say-so, as going on without a second opinion is.
        new("keep the workflow a kind of task chose for a piece of work that changed paths outside the kind's, where that workflow "
            + "asks less of you than the one it would follow without the kind, with your words; nothing switches by itself",
            "(no screen yet)",
            "`daoris workflow keep <session> [\"…\"]`",
            new HelpDoorAsk.Never("it is yours alone")),
        // The person's skip and stop of a node of a run bound to a graph version. Never proposed: passing a step over or stopping
        // it is the person's word on the work, as a held attempt's is.
        new("pass a step of a run that follows a saved graph over, with your words, such as a go-ahead nobody will raise, or stop it, which "
            + "ends the run once nothing else of it can move (kept on the run's record, and Daoris's next look applies it)",
            "Sessions → the session's *Workflow* view, *Pass over…* and *Stop…* under the step",
            "`daoris workflow skip <run> <node> \"…\"`, `daoris workflow stop <run> <node> [\"…\"]`",
            new HelpDoorAsk.Never("it is yours alone")),
        // The person's Accept of a run's review. Never proposed: the press is theirs.
        new("accept a run's review, with a note if you like: what it holds lands (kept on the run's record, and Daoris's next look applies it)",
            "Sessions → the session's *Workflow* view, *Accept* under the review",
            "`daoris workflow accept <run> <node> [\"…\"]`",
            new HelpDoorAsk.Never("it is yours alone")),
        // The person's changes on a run's review, its rejection with their words. Never proposed, as skip and stop never are.
        new("ask for changes on a run's review in place of your Accept, with your words: the work's session is told them as its next "
            + "turn, and what it commits is reviewed again before anything lands (kept on the run's record, and Daoris's next look "
            + "applies it)",
            "Sessions → the session's *Workflow* view, *Ask for changes…* under the review",
            "`daoris workflow changes <run> <node> \"…\"`",
            new HelpDoorAsk.Never("it is yours alone")),
        // The person's press of a task a run waits on, or their answer to its approval. Never proposed: the press is theirs.
        new("press your task a run waits on, or approve or reject it where it is an approval, with a note if you like: the run takes "
            + "the route for what you pressed (kept on the run's record, and Daoris's next look applies it)",
            "Sessions → the session's *Workflow* view, *Done…*, *Approve…* or *Reject…* under the step; or *What needs you*",
            "`daoris workflow done <run> <node> [\"…\"]`, `daoris workflow approve <run> <node> [\"…\"]`, `daoris workflow reject <run> <node> [\"…\"]`",
            new HelpDoorAsk.Never("it is yours alone")),
        // The person's ask for a fix of a failed verification. Never proposed: the ask is published in the person's name.
        new("ask for a fix of a failed verification's gate: one ask for the gate with its log, which the kind the check names routes into "
            + "the development pipeline; asking again finds the ask made",
            "What needs you → the failed verification, *Ask for a fix…*",
            "`daoris workflow fix <run> [<node>]`",
            new HelpDoorAsk.Never("it is yours alone")),
        // The person's presses at the second opinion's gate, the terminal's `daoris session opinion` and the screen's. None is
        // proposed: a pass spends an account at the person's choice and a stop is theirs, and going on, or reading it themselves,
        // is their judgement.
        new("ask another agent to read a session's work now, again, or after it could not, or the same agent in a fresh "
            + "conversation, which is never an independent reading; see where its second opinion stands; or stop a reading, which "
            + "then gives no opinion",
            "Sessions → the session's page → Second opinion: *Ask now*, *Ask again*, *Try again*, *Ask the same agent, fresh*, *Stop…*",
            "`daoris session opinion ask <session> [--agent <adapter>] [--same-agent] [\"…\"]`, "
            + "`daoris session opinion show <session|opinion>`, `daoris session opinion stop <opinion>`",
            new HelpDoorAsk.Never("a pass spends an account at your choice, and a stop is yours")),
        new("answer a second opinion's gate yourself: go on without a settled one, what is unsettled staying so, or say you looked "
            + "at the work yourself in place of another agent's reading; each is kept with your words at the commit it was given "
            + "at, and nothing lands by it",
            "Sessions → the session's page → Second opinion, and What needs you: *Go on anyway…*, *I looked myself…*",
            "`daoris session opinion anyway <session> [\"…\"]`, `daoris session opinion myself <session> [\"…\"]`",
            new HelpDoorAsk.Never("each is your judgement")),
        // Exempt from Ask Daoris, since the words are the person's; the room names both doors. A park's answer is said here, its
        // one door.
        new("say something to a session of this machine's, running, parked or ended, a park's answer among them: it reads your words "
            + "at its next step or when its turn ends, or the same session goes on with them",
            "Sessions → the session's page → its box", "`daoris session say <id> \"…\" [--file <path>]…`",
            new HelpDoorAsk.Never("the words are yours")),
        // Exempt from Ask Daoris, since its conversations and what the person keeps of them are the person's own; the room names
        // both doors, so the helper points there when asked about an earlier conversation.
        new("find an earlier conversation with Ask Daoris and go on in it, start a new one from its words, or name, pin or delete "
            + "one; they are kept on this machine only",
            "Ask Daoris → History",
            "`daoris ask-daoris list [--workspace <name> | --all] [--search \"…\"]`, `daoris session say <id> \"…\"`, "
            + "`daoris ask-daoris rename|pin|unpin|delete <id>`",
            new HelpDoorAsk.Never("the conversations are yours")),
        // A go-ahead is the person's yes or no to an act outside a repository, asked once on the ask. Ask Daoris never answers
        // one: the production acts stay the person's.
        ("answer a go-ahead a session asked on an ask, for an act outside its repository: yes or no, which every session on "
            + "the ask is handed", "Quests → the ask's page → Go-aheads", "`daoris ask go-ahead <id> <n> approve|refuse [\"…\"]`"),
    ];
}
