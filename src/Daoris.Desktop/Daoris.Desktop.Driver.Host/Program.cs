using System.Text;
using Daoris.Driver;

// The headless door to the driver (D46 §7): what the family rehearsal drives, and what the desktop
// shell embeds. It watches a service, starts sessions where the person opted in, and prints every
// verdict — a quest that is sitting must always say why.
//
//   DAORIS_HOME            where every machine-local file lives (D63) — required unless each file's
//                          own variable names it; the installed desktop sets it for the account
//   DAORIS_SERVICE_URL     where the service is                (required — the driver is its client)
//   DAORIS_SERVICE_KEY     sent as a bearer token when set     (absent: local trust, D21)
//   DAORIS_DRIVER_CONFIG   the person's standing choices       (default: $DAORIS_HOME/driver.json)
//   DAORIS_REMOTE_URL      one workspace's remote, with its key (or $DAORIS_HOME/remotes.json — D48 §5;
//   DAORIS_REMOTE_KEY        either env var present means the environment is the answer, whole,
//   DAORIS_REMOTE_WORKSPACE  for the workspace named here — absent: `default`)
//   DAORIS_REMOTE_CONFIG   where the map is                    (default: $DAORIS_HOME/remotes.json)
//
//   drive [--once | --until-idle] [--share]
//                 the loop, asked for by name (DRV8a, D104): watch, ticking pollSeconds apart until
//                 Ctrl+C; --once ticks once; --until-idle ticks until nothing starts, the deterministic
//                 mode a gate drives. A bare --once or --until-idle is the same ask. It takes the home's
//                 driver lock first, and a home another live driver holds is refused, naming it (exit 1)
//                 — unless --share, which runs beside it on purpose.
//   (no verb)     the usage, exit 2 — never a loop: run to read its usage, a bare invocation used to
//                 start one beside the desktop's.
//
//   chat --repository <name> [--adapter <name>] [--own-tree]
//                 hold a conversation in a repository (D49 §3): stdin is the person, stdout is the
//                 session, end of input ends it. One session per working tree (D51) — and --own-tree
//                 opens the conversation in a worktree of its own, beside your work in the checkout.
//
//   ask [--workspace <name>] [--to <repo>] [--file <path>]… [--url <address>]… "…"
//                 ask at a WORKSPACE (D65 §1a): the service answers with the tier that answered —
//                 by declarations only (proposes, publishes nothing), or the receiver --to names.
//   ask --publish <id> --to <repo> · ask --close <id> --reason "…" · ask --delete <id>
//                 turn an ask into a quest, close it with the reason, or delete one made by mistake with
//                 every quest asked by it (D95) — refused whole if any of them must stay. Where driver.json names an
//                 `intakeAdapter` (`daoris driver intake <adapter>`), the loop answers an ask the
//                 declarations left open with an INTAKE session (D65 §1b) in <home>/intake/<workspace>/:
//                 it publishes onto the ask, or parks asking you — and ends when you answer the ask.
//   ask --pause <id> · ask --resume <id> · quest pause <id> · quest resume <id>
//                 pause an ask's work, or one quest's, on this machine (PAUSE1b, D132 §2): the pause is written to
//                 driver.json first, then every live session of it this machine runs is stopped as your stop (one another
//                 Daoris process runs through the request in <home>/sessions/requests/ its loop takes, `by: pause`), and
//                 nothing of it starts until you resume it, which releases each stop the pause made and names what still
//                 holds a quest. A session waiting on you stays parked, a running intake goes on, and a teammate's is named.
//   ask --abandon <id> [--reason "…" --yes] · quest abandon <id> [--reason "…" --yes]
//                 abandon an ask's work, or one quest's, on this machine (PAUSE1d, D132 §3): without --yes, the list of
//                 what it would decline, discard and archive, and what it keeps and why, changing nothing. With --reason
//                 and --yes: the scope paused, its sessions stopped, each quest declined with your reason (an open one
//                 only while open), the ask closed with it, one sync pass, each tree and branch discarded only where no
//                 commit of it is on any ref but this machine's local daoris/* branches, the sessions archived, and
//                 <home>/abandoned.json written with each discarded branch's tip. A step that fails leaves it paused.
//
//   setup <repository> [--plan]
//                 ask a repository's own session to set it up for every agent (LAYOUT7; D117 §6, D124 §2): take up
//                 the doctrine on the agents layout, initialise its knowledge, write its brief and declare its
//                 documents and safe work, on its own branch. Read from its LINE as git objects, every refusal said
//                 with its door. A press publishes one ask to it, as yours, and adds to its rules the doctrine tool's
//                 exact verbs; --plan prints what was read, the rule, the landing, the agent and the quest's text,
//                 and publishes nothing.
//
//   setup --workspace <name> [--plan] [--at-once <n>] [--pilot <n>] [--first <repo>…] [--skip <repo>…]
//   setup --workspace <name> --pause | --resume | --stop
//                 set a whole workspace up (WSSETUP6, D124 §4): a press writes <home>/setup/<workspace>.json, a plan of
//                 single set-ups in the order other work touches them, and adds the doctrine tool's verbs to the
//                 workspace's rules once; the loop then publishes each in turn, one at a time by default and never
//                 the cap's last slot, skipping a repository the press refuses and saying why, and pausing once a
//                 pilot of two has closed. --plan prints the list and each refusal (or, with a plan working, where it
//                 stands); --pause, --resume (which judges the skipped again) and --stop steer it.
//
//   register [--repository <name>]
//                 register each repository with a checkout here, or the one named, from what its LINE declares
//                 (WSSETUP5, D124 §3): daoris.json and daoris.lanes.json read as git objects, sent as `connect` would
//                 send them, and only where the row holds something else. Each says what it came to. A start, and a
//                 line Daoris moves (a merge landing, `trees sync`'s fast-forward), follow on their own.
//
//   quest delete <id>
//                 delete a quest made by mistake (D95): only one nobody has started on — open, with no
//                 session record naming it — goes, and the service's refusal says what to do instead.
//                 A shared quest's delete travels to its remote as an operation. The drawer's Delete is
//                 the other door.
//   quest accept <id>
//                 accept a done's departure from what you required (DRIFT1d, D133 §4): what the departure
//                 held — the chain's next step, a quest waiting on it — goes on, and the yes travels like any
//                 verb. A quest is otherwise answered by the session that takes it, bar the person's done below.
//   quest done <id> [--note "…"]
//                 mark an open or taken quest done as yours (QUESTCLOSE1, D126's note): what a finish at a checkpoint
//                 that left its quest taken names. Its record says you marked it done, with your words, and it answers
//                 none of its requirements one by one. The quest page's Mark done… is the other door.
//   quest check <id> [--commit <sha>]
//                 read a done's evidence again (EVID1b, D144 §3, §5): each path a met requirement names, at the commit
//                 a driven end here read, or the one named, which must be that commit or come after it on the same
//                 history; with no driven end on record, the commit must be named. Posts what it read to the evidence
//                 door as the terminal's, and prints it: 0 when all is found, 1 when anything is missing or unread, 2
//                 when a store did not answer. The quest page's Check again is EVID1c's door to the same read.
//   quest review <id> reviewed|not-yet|skip ["…"]
//                 the person's verdict on a review (REVIEWENV1c, D154 point 8): prints what the set-up step showed and how to
//                 show it again, sends the verdict with that set-up, and says what the gate now says. not-yet needs your words,
//                 which go to the step's session as its next turn; skip lets the work land without a review.
//
//   history [--workspace <name>] [--json]
//   history clear --workspace <name> [--yes]
//   quest clear <id> [--failed] [--yes] · ask --clear <id> [--yes]
//                 finished history on this machine (HIST1d, D153, the history-clearing design §6.2): what the home keeps
//                 of each workspace's finished work, or the one named, what a clear would take and what it keeps and why,
//                 and --json HISTORY_PLAN's answer field for field; then a clear of a workspace's, one closed quest's work,
//                 its failed sessions alone, or one ask's work. Without --yes a clear lists and changes nothing; with it,
//                 each unit is judged again and what may go goes, records first and then what the home kept of them, and
//                 the machine log says the terminal's door. Never a tree, a branch, open work, or the team's copy on a
//                 remote. A quest's or an ask's page and the workspace's *Kept on this machine* are the other doors.
//
//   answer <session> ["…"]
//                 answer a driven session that parked to ask you (STANDDOWN2): its record stays parked with
//                 your words, and at the next tick the same session goes on with them (D131), its own
//                 conversation resumed; where it cannot be, a new session carries the quest on in the same
//                 tree, handed them, and says why. Nothing after the id is "carry on". The page's box on the
//                 parked session is the other door.
//
//   sessions [--group you|review|working|later|ended|archived] [--repository <name>] [--json]
//   sessions stop <id> · finish <id> [--note "…"] · decline <id> --reason "…"
//   sessions archive <id>… | --ended [--yes] · unarchive <id>… · delete <id>
//                 this machine's sessions by what they need (SESSUX1g, D126 §7.1), from the reader Sessions' list reads;
//                 --json prints its answer. stop, finish and decline reach a session another process runs through a
//                 request in <home>/sessions/requests/ that its loop takes, waiting ten seconds for the record to move;
//                 one nothing here runs is moved by the ledger, as the screen's stop moves an orphan. archive, unarchive
//                 and delete are the screen's owners, and their log lines say this door. Sessions' rows are the other door.
//   sessions say <id> "…" [--file <path>]…
//                 say something to a session of this machine's (MSG1e, D137 §5.2): a say request in the same folder,
//                 which the loop that runs it holds at its door, and any loop keeps on the record of one nothing here
//                 runs, answering beside it. What never goes on is refused first, exit 1. Where no loop drives the
//                 home, a driven session's words are kept on its record for the next, and a conversation's refused.
//                 It waits up to ten seconds and prints where the words stand. The session's box is the other door.
//   sessions go-on-new <id>
//                 words a resume holds while the account its session ran on cools (MSG1g, D137 §2.2): the person's choice
//                 to go on now in a new session, kept under the home for the driver's next look, which carries them on
//                 handed them, without the session's conversation. A conversation, a closed quest's session and an
//                 account that is not cooling are refused, exit 1. The screen's *Go on in a new session* is the other door.
//   sessions start-from <id>
//                 words a session cannot go on with, which nothing carries on by itself (MSG1f3, D137 §2.2): a conversation
//                 in this terminal, as `chat` opens one, its first message the words, which then leave that session naming
//                 it, through the chat runner's act the screen's route calls. What never goes on, a session still running, no
//                 words and a quest the driver carries on are refused, exit 1. *Start a conversation with these words* is the
//                 other door.
//
//   trees [list | remove <path|session|branch> [--repository <name>] [--force] | clean [--workspace <name>] [--yes]
//         | land <session> [--plan] | hand <session|branch> [...]
//         | sync [--repository <name>] [--workspace <name>] [--all] [--yes]]
//                 the session worktrees this machine has grown (D51): list them, or remove one, or a session's
//                 branch by the session or the branch (LAND3) —
//                 refusing while it holds uncommitted changes or unmerged commits, unless forced —
//                 or list every session branch and, with --yes, remove the empty and landed (D88),
//                 or accept a session's work as the review's Accept does, by the workspace's rule (D87),
//                 or bring each repository up to date after its pull request merged (WSR6, D109). The clean-up
//                 and bringing up to date take one workspace's checkouts with --workspace (BRSCOPE1a).
//
//   sync [status | dismiss <quest>] [--workspace <name>]
//                 one pass now, the tick's own, for every circle with a remote or the one named; with
//                 `status`, where each circle stands — ahead, behind, in conflict, last synced (SYNC6a);
//                 with `dismiss`, the conflicts a quest carries go, here and at the next pass (SYNC6c).
//
//   logs [--since <30m|2h|3d>] [--source <name>] [--event <name>] [--level <warn|error>] [--json]
//                 the machine log (LOG1c, D94): every source's lines under the home, merged by time,
//                 one readable line each, or as written with --json. A line that cannot be read is
//                 skipped and counted. Settings → Logs is the screen's door to the same reading.
//
//   trace <commit|session|quest> · trace commit|session|quest <id>
//                 one read back to the ask (TRACE1, D143): from a commit (a landing's tip, or in a session's evidence), a
//                 session or a quest, the ask and the person's words and go-aheads, the quest's requirements and answers,
//                 each session's agent, account, harness, tree and base commit, its instruction by event and size, its rules,
//                 its landing, and what stood when it started, each from the store that keeps it, and a link nothing keeps
//                 said missing. It writes nothing; the screen's door to the same read is a row of its own.
//
//   git branches [--repository <name>] [--all] [--json]
//                 each repository's line and branches by kind (GIT1c, D147 §2.2, §3.3): the line with how it stands to
//                 origin's copy and when the checkout last fetched, the sessions' branches named by their sessions, the
//                 landed ones with whether they are on origin and their pull request, yours, and how many only origin
//                 holds, each with its distance from the line, from one `git for-each-ref` per repository through the git
//                 Tools resolves; and the git each read ran, ready to copy. The repositories holding Daoris's branches, the
//                 rest named (D112); --all every one. It writes nothing.
//
//   update [--install <folder>] · update --when-idle | --now | --cancel [--install <folder>]
//                 an install's update (UPDATE1, D139): what `publish:desktop --stage` put beside the install, what holds
//                 for it and how the last swap ended; or the word on it, written to <home>/update.json as the window's
//                 banner writes it. When idle (the default for a staged build): the desktop starts nothing new, lets what
//                 runs end or park, closes, and its launcher swaps app/ and starts again. Now: the close ends what runs as
//                 any close does, each driven session carried on at the next start. Cancel: not now, for that build.
//
//   plugins install <file.nupkg>
//                 a plugin package (PLUGDIST1a, D120): read before anything is extracted (the type
//                 `DaorisPlugin` at a plugin API this build speaks, no dependencies, `plugin/plugin.json`),
//                 its `plugin/` folder alone extracted and read as the catalogue reads one, and added under
//                 its id, never over an installed one, with the package recorded. Nothing reaches a network.
//
//   plugins new <id> --point <point>… [--in <folder>]
//   plugins try <folder|id> [--point <point>] [--frame <file.json>]
//                 the plugin kit (PLUG8, D101): `new` writes a plugin's folder — a manifest, a wire script
//                 answering each point, its self-contained wire test (`node --test`) and a README — into a
//                 new or empty folder, and installs nothing. `try` starts a plugin as the driver would,
//                 speaks the handshake, one frame at each point and the shutdown, and checks every answer
//                 by the driver's own reader: 0 when all are ones it reads, 1 when the plugin failed a check.
//                 Settings → Plugins is the other door.
//   plugins show <id> [--json]
//   plugins activity <id> [--since <30m|2h|3d>] [--json]
//                 a plugin's page and what it did (PLUGUI1d, D119 §4.4): what it declares as written, a
//                 server's environment by name only, its health from the machine log's last word, said with
//                 when; and its activity from the machine log over the last 7 days, or the span --since names.
//
// While watching, a line marked `!` is what would have been a toast on a machine with a screen
// (SURF5b): a session parked, or one ended without the person asking. `daoris driver notify off`
// turns it off here exactly as the desktop's checkbox does — one file, two doors (D50).
//
// Exit codes keep the family contract: 0 clean · 2 tool error.
if (OperatingSystem.IsWindows())
{
    Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
}

// One read from a commit, a session or a quest back to its ask (TRACE1, D143, D50). Routed before the machine log opens,
// since that open prunes old files and the trace writes nothing anywhere; it keeps its own catch for the same reason.
if (args is ["trace", .. var traceArgs])
{
    return await Daoris.Driver.Host.TraceConsole.RunAsync(traceArgs);
}

// The branch list (GIT1c, D147 §3.3, D50): a read, routed before the machine log opens for the trace's reason, with its own catch.
if (args is ["git", .. var gitArgs])
{
    return await Daoris.Driver.Host.GitConsole.RunAsync(gitArgs);
}

// The machine log (LOG1, D94): this host's watch and every exception nothing caught, in a file of its
// own beside the desktop's. With no home it writes nothing, and the sentence below still says why.
using var log = MachineLog.Open("driver");
log.WatchUnhandled();

// HOSTSTART2: the exception that ends this entry point is written as `error`. The entry point is async, so the `using` above
// has closed `log` by the time the runtime raises that exception as unhandled, and a driver that died at start left nothing
// of why. `start` is the loop's start, until its first look; a verb runs once with no start of its own to tell apart, so
// what ends one is `unhandled` from the moment it is chosen.
var entryPoint = log.WatchEntryPoint();
if (DriverCommand.Read(args, out _) is null) entryPoint.Running();

// The loop's close (REV3), cancelled only by the person's Ctrl+C, which is registered below once a loop is asked for. Made
// here, outside the one catch, so that catch can tell that close from any other cancellation (DEV3b).
using var closing = new CancellationTokenSource();

// Every door inside the one catch, so a missing home or service is exit 2 and a sentence, never a
// stack trace (REV3: `trees` with no home was an unhandled exception).
try
{
    // A conversation from a terminal (D49 §3, D50): the same ledger, the same lock, the same record —
    // the desktop is where a person usually chats, and a machine with no screen is still a machine.
    if (args is ["chat", .. var chatArgs])
    {
        return await Daoris.Driver.Host.ChatConsole.RunAsync(chatArgs);
    }

    // Pausing, resuming and abandoning an ask's work from a terminal (PAUSE1b, PAUSE1d, D132 §7.2, D50): the ask's page is the
    // other door. Asked for before the ask's other words, which would take `--pause` as the words of a new ask.
    if (args is ["ask", .. var workArgs] && WorkCommand.Asks(WorkScope.Ask, workArgs))
    {
        return await Daoris.Driver.Host.WorkConsole.RunAsync(WorkScope.Ask, workArgs, log);
    }

    // Clearing an ask's work from this machine (HIST1d, D153, the history-clearing design §6.2, D50): the ask's page is the
    // other door. Asked for before the ask's other words, which would take `--clear` as the words of a new ask.
    if (args is ["ask", .. var clearArgs] && HistoryCommand.Asks(WorkScope.Ask, clearArgs))
    {
        return await Daoris.Driver.Host.HistoryConsole.RunAsync(WorkScope.Ask, clearArgs, log);
    }

    // An ask from a terminal (D65 §1a, D50): the page's composer at workspace scope is the other door.
    if (args is ["ask", .. var askArgs])
    {
        return await Daoris.Driver.Host.AskConsole.RunAsync(askArgs);
    }

    // A set-up asked of a repository from a terminal (LAYOUT7, D117 §6.1, D50): one ask to one repository, as the
    // person's, with the rule its session needs. Repositories' *Set up for agents* and Ask Daoris's `setup` kind are
    // LAYOUT8's doors to the same press. With `--workspace`, a whole workspace's plan (WSSETUP6, D124 §4.5), whose lines
    // go to this host's log; its screen and its Ask Daoris doors are WSSETUP7's.
    if (args is ["setup", .. var setupArgs])
    {
        return await Daoris.Driver.Host.SetupConsole.RunAsync(setupArgs, log);
    }

    // Registration followed from each line when the person asks (WSSETUP5, D124 §3.1, D50): the repository row's
    // *Refresh* is the other door. A start and a line Daoris moves follow on their own.
    if (args is ["register", .. var registerArgs])
    {
        return await Daoris.Driver.Host.RegisterConsole.RunAsync(registerArgs, log);
    }

    // Deleting a quest made by mistake (D95, D50): the quest drawer's Delete is the other door. Accepting a done's
    // departure from what the person required (DRIFT1d, D133 §4): the quest page's yes and Ask Daoris's are owed. Pausing,
    // resuming and abandoning one quest's work (PAUSE1b, PAUSE1d, D132 §7.2): the quest's page is the other door. Clearing a
    // closed quest's work, or its failed sessions, from this machine (HIST1d, D153, the history-clearing design §6.2): the quest's
    // page is the other door. Reading a done's evidence again (EVID1b, D144 §5): the quest page's Check again is EVID1c's.
    if (args is ["quest", .. var questArgs])
    {
        if (WorkCommand.Asks(WorkScope.Quest, questArgs))
        {
            return await Daoris.Driver.Host.WorkConsole.RunAsync(WorkScope.Quest, questArgs, log);
        }

        if (HistoryCommand.Asks(WorkScope.Quest, questArgs))
        {
            return await Daoris.Driver.Host.HistoryConsole.RunAsync(WorkScope.Quest, questArgs, log);
        }

        // A done's evidence read again (EVID1b, D144 §5): the quest page's Check again is EVID1c's door to the same read.
        if (QuestCheckCommand.Asks(questArgs))
        {
            return await Daoris.Driver.Host.QuestCheckConsole.RunAsync(questArgs, log);
        }

        // The person's verdict on a review (REVIEWENV1c, D154 point 8, D50): the quest's page and the strip's chip are
        // REVIEWENV1g's doors to the same verdict. Ask Daoris is exempt: the verdict is a look only the person took (D110).
        if (QuestReviewCommand.Asks(questArgs))
        {
            return await Daoris.Driver.Host.QuestReviewConsole.RunAsync(questArgs, log);
        }

        // The person marks a quest done (QUESTCLOSE1, D126's note, D50): the quest page's *Mark done…* is the other door, and a
        // finish at a checkpoint that left its quest taken names this one.
        if (QuestDoneCommand.Asks(questArgs))
        {
            if (QuestDoneCommand.Read(questArgs, out var doneProblem) is not { } doneAsk)
            {
                Console.Error.WriteLine($"daoris-driver: {doneProblem}");
                Console.Error.WriteLine(QuestDoneCommand.Usage);
                return 2;
            }

            using var doneClient = ServiceClient.FromEnvironment();
            return await QuestDoneCommand.RunAsync(doneAsk, doneClient, Console.Out);
        }

        if (questArgs is not [("delete" or "accept") and var verb, var questId])
        {
            Console.Error.WriteLine(
                "usage: daoris-driver quest delete <id>  ·  quest accept <id>  ·  quest done <id> [--note \"…\"]  ·  "
                + "quest pause <id>  ·  quest resume <id>  ·  "
                + "quest abandon <id> [--reason \"…\" --yes]  ·  quest clear <id> [--failed] [--yes]  ·  "
                + "quest check <id> [--commit <sha>]  ·  quest review <id> reviewed|not-yet|skip [\"…\"]");
            return 2;
        }

        using var client = ServiceClient.FromEnvironment();
        var (ok, message) = verb == "delete"
            ? await client.DeleteQuestAsync(questId)
            : await client.AcceptDepartureAsync(questId);
        Console.WriteLine($"daoris-driver: {message}");
        // A refusal — something stands on the quest, or nothing waits for a yes — is an answer, not a tool error.
        return ok ? 0 : 1;
    }

    // Answering a session that parked to ask the person (STANDDOWN2, D50): the page's box is the other door.
    if (args is ["answer", var answered, .. var words])
    {
        using var client = ServiceClient.FromEnvironment();
        var (ok, message) = await client.AnswerSessionAsync(answered, words.Length == 0 ? null : string.Join(" ", words));
        Console.WriteLine($"daoris-driver: {message}");
        // A refusal — nothing parked by that id — is an answer, not a tool error.
        return ok ? 0 : 1;
    }

    // Sessions from a terminal (SESSUX1g, D126 §7.1, D50): Sessions' list and its acts are the other door to the same
    // reader and the same owners; a stop of a session another process runs reaches it through the request its loop takes.
    if (args is ["sessions", .. var sessionsArgs])
    {
        return await Daoris.Driver.Host.SessionsConsole.RunAsync(sessionsArgs, log);
    }

    // Ask Daoris's history from a terminal (ASKHIST1, D50): the panel's list and search, words to one that go on in it, its name,
    // its pin and its delete. `help` alone, or a word it does not take, is still the usage below.
    if (args is ["help", var helpVerb, ..] && HelpCommand.Verbs.Contains(helpVerb))
    {
        return await Daoris.Driver.Host.HelpConsole.RunAsync(args[1..], log);
    }

    // Finished history from a terminal (HIST1d, D153, the history-clearing design §6.2, D50): what this machine keeps of it, per
    // workspace, and a workspace's clear, listed first and pressed with --yes. The workspace page's *Kept on this machine* is the
    // other door; a clear's log line says this one.
    if (args is ["history", .. var historyArgs])
    {
        return await Daoris.Driver.Host.HistoryConsole.RunAsync(historyArgs, log);
    }

    // The tree lifecycle from a terminal (D51, D50): the verbs live on the binary that already owns git —
    // the CLI's `daoris driver trees <repo> on|off` is the standing opt-in, a file edit; these are disk.
    if (args is ["trees", .. var treesArgs])
    {
        return await Daoris.Driver.Host.TreesConsole.RunAsync(treesArgs, log);
    }

    // The sync on demand (SYNC6a, D50): the screen's *Sync now* is the other door to the same pass.
    if (args is ["sync", .. var syncArgs])
    {
        return await Daoris.Driver.Host.SyncConsole.RunAsync(syncArgs);
    }

    // A plugin package from a file (PLUGDIST1a, D120 §5.7): read, checked and installed with no network. Asked
    // for before the kit's verbs below, which answer every other `plugins` word.
    if (args is ["plugins", "install", .. var installArgs])
    {
        return PluginPackageCommand.Install(installArgs, Console.Out, DaorisHome.Resolve());
    }

    // An install's update from a terminal (UPDATE1, D139 §3, D50): the window's banner is the other door to the same request.
    // Needs no service: the request is a file under the home, and the staged build a folder beside its install.
    if (args is ["update", .. var updateArgs])
    {
        var exit = UpdateCommand.Run(
            updateArgs, DaorisHome.Resolve() ?? throw new DriverException(DaorisHome.Sentence), () => DateTimeOffset.UtcNow, log,
            out var updateSaid);
        (exit == 2 ? Console.Error : Console.Out).Write(updateSaid);
        return exit;
    }

    // The machine log from a terminal (LOG1c, D50): Settings → Logs is the other door to the same reading.
    if (args is ["logs", .. var logsArgs])
    {
        return Daoris.Driver.Host.LogsConsole.Run(logsArgs);
    }

    // Plugins from a terminal (PLUG8, PLUGUI1d, D50): the kit's new and try, and a plugin's page and activity,
    // the Plugins view's twins. None needs a service; try a home only to try an installed plugin by its id.
    if (args is ["plugins", .. var pluginsArgs])
    {
        return await PluginsCommand.RunAsync(pluginsArgs, Console.Out, DaorisHome.Resolve(), log);
    }

    // 🔴 The loop only by its verb (DRV8a, D104): a bare invocation, or a word nobody answers, is the
    // usage and starts nothing — it used to fall through to the watch loop.
    if (DriverCommand.Read(args, out var problem) is not { } loop)
    {
        var help = DriverCommand.AskedForHelp(args);
        if (problem is not null) Console.Error.WriteLine($"daoris-driver: {problem}");
        (help ? Console.Out : Console.Error).WriteLine(DriverCommand.Usage);
        return help ? 0 : 2;
    }

    var once = loop.Mode == LoopMode.Once;
    var untilIdle = loop.Mode == LoopMode.UntilIdle;

    // 🔴 Ctrl+C ends the loop, not the process (REV3): killed outright, this host left every session
    // it ran working in its record and its agent running on. Cancelled, each session is ended and says
    // so, as the desktop's close does. Registered here, after the subcommands: `chat` has its own.
    Console.CancelKeyPress += (_, press) =>
    {
        press.Cancel = true;
        closing.Cancel();
    };

    var configPath = DriverConfig.ResolvePath();
    var config = DriverConfig.Load(configPath);
    var home = DriverConfig.HomeOf(configPath);

    // One live driver per home (DRV8a, D104), taken before anything reaches the service: a second loop
    // races the first for the same quests, so it is refused naming the first — a refusal, exit 1, not a
    // tool error — unless it was asked to share.
    DriverHolder? holder = null;
    using var held = loop.Share
        ? DriverLock.Share(home, DriverKind.Headless)
        : DriverLock.TryAcquire(home, DriverKind.Headless, out holder);
    if (held is null)
    {
        Console.Error.WriteLine($"driver: {DriverLock.Refusal(home, holder!)}");
        return 1;
    }

    if (held.Beside is { } beside)
    {
        Console.WriteLine(
            $"driver: running beside {beside.Named}, as --share asked — the two loops race for the same quests, and the take decides.");
    }

    var started = DateTimeOffset.UtcNow;
    log.Info("app.started", ("mode", once ? "once" : untilIdle ? "until-idle" : "watch"));
    // Marked under the home, so the desktop sharing it can tell this host's sessions from orphans.
    var processes = new SessionProcesses(Path.Combine(home, "sessions"));

    using var service = ServiceClient.FromEnvironment();

    // What this host runs and how long each part takes, into its log (LOG1b): the watcher hears the
    // client's opens and moves and the record's events, so the record is handed to every driver below.
    var events = new SessionEvents(Path.Combine(home, "sessions"));
    using var sessionLog = new SessionLog(log, service, events);

    // The watch, once it runs, so a terminal's words a loop keeps are taken up at once rather than at its next look (MSG1e).
    DriverWatch? watching = null;

    // Every loop on the home watches the requests a terminal's `sessions stop|finish|decline` writes (SESSUX1g, D126 §7.1),
    // and acts on one for a session this host runs as the screen's route would. A terminal's `sessions say` (MSG1e) is held
    // at the door of a session this host runs, or kept on the record of one nothing here runs, shown in this host's own
    // record of it, and the look nudged.
    await using var requests = new SessionRequestWatch(home, processes, () => service)
    {
        Say = new LoopWords(processes, () => service) { Events = events, Nudge = () => watching?.Nudge() }.HoldAsync,
    };

    // The machine's remotes, one per workspace that has one ($DAORIS_HOME/remotes.json, environment
    // overriding — D47 §9, D48 §5): the syncs ride the tick, so a headless driver on a server machine
    // feeds and mirrors exactly as the desktop does. Absence is silent and local, and the map is
    // re-read every pass, so a remote wired later syncs without a restart (SYNC0d).
    using var sync = RemoteSyncSet.FromEnvironment(
        service.BaseUrl, Environment.GetEnvironmentVariable(ServiceClient.KeyVariable));
    if (sync.Workspaces.Count > 0)
    {
        Console.WriteLine(
            $"driver: syncing each tick with the remotes for {string.Join(", ", sync.Workspaces)}");
    }

    if (config.Drivable.Count == 0)
    {
        Console.WriteLine($"driver: nothing is opted in — name repositories under \"drivable\" in {configPath}");
    }

    // The plugins that speak (D64): their processes live as long as this host does, and are stopped
    // with it. Their diagnostics have no console buffer here, so they go to stderr under their name.
    // What the set does with each, without their words, goes to this host's machine log (PLUGUI1d).
    await using var hooks = new HookSet(home, say: (id, line) => Console.Error.WriteLine($"plugin:{id}  {line}"), log: log);

    // The loop's start is over: its first look begins, and what ends the driver from here is `unhandled` (HOSTSTART2).
    entryPoint.Running();

    if (once)
    {
        // One look, then what it started to its end (DEV3): a look no longer waits for its sessions, and a
        // single run that let go of them would leave them working with nothing watching. Each part is printed
        // as it is said (DEV3a), each pass beside the sessions as it ends (DEV3b), so a stop or an ending already
        // made is never lost with the process.
        await new Driver(service, config, AdapterSet.Built(), home, processes, sync, hooks: hooks, events: events) { Log = log }
            .RunOnceAsync(closing.Token, said: report => Print(report));
    }
    else if (untilIdle)
    {
        // Each look printed as it ends, and what ended before a failure or a close lets go (DEV3a), where every
        // look used to be printed only once the whole run returned; a look that failed, what it had said (DEV3c).
        await new Driver(service, config, AdapterSet.Built(), home, processes, sync, hooks: hooks, events: events) { Log = log }
            .RunUntilIdleAsync(closing.Token, said: report => Print(report));
    }
    else
    {
        Console.WriteLine($"driver: watching {service.BaseUrl} every {config.PollSeconds}s — Ctrl+C stops it");

        // What is worth interrupting a person for (SURF5b). A machine with no screen cannot toast
        // and still has to answer the question (D50), so the judgement is the library's and this
        // door's delivery is a line — the same shape as everything else this host prints.
        var attention = new AttentionWatch();
        // A quest's park is said with its last session's facts (SESSUX1i), read from the records when the parks change.
        var parks = new QuestParkReader();
        var key = Environment.GetEnvironmentVariable(ServiceClient.KeyVariable);

        // The loop itself — re-read the config, tick, wait — is the library's (DriverWatch); this host
        // keeps only its reporting half. A null onError lets a failed tick propagate to the catch
        // below, which is this door's exit-2 contract; what that look had said before it failed, a
        // stop among it, is printed first (DEV3c).
        // The machine log rides each look, where a run's failure nothing else awaits is written with its place (ANSWER2).
        watching = new DriverWatch(service, configPath, home, processes, sync, hooks: hooks, events: events) { Log = log };
        await watching.RunAsync(
            async (report, ticked) =>
            {
                Print(report, quietWhenIdle: true);
                // Observed either way, so turning notifications back on does not then announce
                // everything that happened while they were off — the switch is about being TOLD.
                var read = await parks.LookAsync(
                    report, ticked.ForgivenAt, token => SessionRecords.ReadAsync(service.BaseUrl, key, ct: token), closing.Token);
                var events = attention.Observe(report, read);
                if (ticked.Notify) foreach (var item in events) Console.WriteLine($"  !  {item.Line}");
            },
            onError: null,
            closing.Token,
            said: report => Print(report));
        Console.WriteLine("driver: stopped — every session it ran was ended and recorded.");
    }

    log.Info("app.stopped", ("uptimeSeconds", (long)(DateTimeOffset.UtcNow - started).TotalSeconds));
    return 0;
}
catch (OperationCanceledException cancelled)
{
    // Ctrl+C during --once or --until-idle is the close: the tick ended its sessions before it let go. Any other cancellation,
    // a look's own request meeting the client's timeout among them, is a failure that says so, exit 2 (DEV3b): it used to
    // print the close's line and exit 0.
    var ending = DriverCommand.Cancelled(cancelled, closing.IsCancellationRequested);
    (ending.Failed ? Console.Error : Console.Out).WriteLine(ending.Said);
    if (ending.Failed) log.Failed("the headless driver, waiting on the service", cancelled);
    else log.Info("app.stopped");
    return ending.Exit;
}
catch (DriverException error)
{
    Console.Error.WriteLine($"driver: {error.Message}");
    log.Failed("the headless driver", error);
    return 2;
}
catch (HttpRequestException error)
{
    Console.Error.WriteLine($"driver: could not reach the service — {error.Message}");
    log.Failed("the headless driver, reaching the service", error);
    return 2;
}

static void Print(TickReport report, bool quietWhenIdle = false)
{
    if (quietWhenIdle && !report.PlannedAnything && report.Events.Count == 0) return;

    foreach (var consideration in report.Considerations)
    {
        var verdict = consideration.Verdict == StartVerdict.Start ? "start" : "sitting";
        Console.WriteLine($"  {verdict,-8} #{consideration.Quest.Id} → {consideration.Quest.Address} — {consideration.Reason}");
    }

    foreach (var line in report.Events)
    {
        Console.WriteLine($"  {line}");
    }
}
