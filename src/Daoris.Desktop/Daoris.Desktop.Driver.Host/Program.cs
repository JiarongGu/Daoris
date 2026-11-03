using System.Text;
using Daoris.Driver;

// The desktop's program (D46 §7, D194; ONEWAY1ai-i): every group of `daoris …`, which the install's launcher in app/bin/ runs, what
// the family rehearsal drives, and what the desktop shell embeds. A person's words are routed once, at the entry, by the one command
// tree (`CommandTree`, held to the driver tests' `fixtures/command-tree.json`), into the words each command below reads: a doctrine
// command is handed to the CLI the install carries, and `DriverCommand.Usage` says each group. The loop watches a service, starts sessions where the person opted in, and prints every verdict — a quest that is sitting
// must always say why.
//
//   DAORIS_HOME            where every machine-local file lives (D63) — required unless each file's
//                          own variable names it; the installed desktop sets it for the account, and a
//                          host the install carries takes the install's `data/` when none is named (D171)
//   DAORIS_SERVICE_URL     where the service is                (the driver is its client: a host the install
//                          carries finds the running desktop's from its home when none is named, D171)
//   DAORIS_SERVICE_KEY     sent as a bearer token when set     (absent: local trust, D21)
//   DAORIS_REMOTE_URL      one workspace's remote, with its key (or $DAORIS_HOME/remotes.json — D48 §5;
//   DAORIS_REMOTE_KEY        either env var present means the environment is the answer, whole,
//   DAORIS_REMOTE_WORKSPACE  for the workspace named here — absent: `default`)
//   DAORIS_REMOTE_CONFIG   where the map is                    (default: $DAORIS_HOME/remotes.json)
//
// Every command that changes the home while acting alone holds the home's lease for its run (VerbLeases), and waits up to thirty
// seconds where another such act holds it, then is refused naming it (exit 1); beside a running Daoris it sends the person's press
// to it, confirmed in its window. With no words, the usage, exit 2 — never a loop: only `drive` starts one (DRV8a, D104).
//
// While watching, a line marked `!` is what would have been a toast on a machine with a screen
// (SURF5b): a session parked, or one ended without the person asking. `daoris settings notify off`
// turns it off here exactly as the desktop's checkbox does — one file, two doors (D50).
//
// Exit codes keep the family contract: 0 clean · 1 a refusal · 2 tool error.
// SUPERVISOR1a-ii (D208 §2.2): the holder key leaves this process first, so no start it builds can copy it. The verb is its one reader:
// `session supervise` reads what was taken, and nothing else does.
HolderKey.Take();

if (OperatingSystem.IsWindows())
{
    Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
}

// A word handed once is never handed back: who handed these words is read, and taken out of what this process passes on.
var handedBy = Environment.GetEnvironmentVariable(CommandWords.HandedVariable);
Environment.SetEnvironmentVariable(CommandWords.HandedVariable, null);

// CONFIRMWAIT1b (the confirm-wait design §4.1): how long a press waits on its Confirm, taken from the words before any verb, lease or
// family reads them, and set once on every world a verb is given (`InstanceHost.World`).
if (!InstanceDoor.TakeWait(args, out var verbArgs, out var pressWait, out var waitProblem))
{
    Console.Error.WriteLine($"daoris: {waitProblem}");
    return 2;
}

Daoris.Driver.Host.InstanceHost.Wait = pressWait;

// ONEWAY1ai-i: the one tree routes the words once, into the words the commands below read.
var typed = verbArgs;
var route = CommandTree.Route(typed, handedBy);
switch (route.Kind)
{
    case TreeRouteKind.Refused:
        Console.Error.WriteLine($"daoris: {route.Said}");
        return 2;
    case TreeRouteKind.Usage:
        if (route.Said is not null) Console.Error.WriteLine($"daoris: {route.Said}");
        (route.Asked ? Console.Out : Console.Error).WriteLine(
            route.Group is { } group ? DriverCommand.UsageOf(group) ?? DriverCommand.Usage : DriverCommand.Usage);
        return route.Asked ? 0 : 2;
}

args = [.. route.Words];

// STEWARDROLE1a-i (D209 §3.3): a role's terminal, by the entry's marker, runs its roles' verbs and the open reads its holder needs, and
// nothing else: no person's press and no verb acting alone. Those it runs refuse their own alone paths (`InstanceDoor`).
if (RoleTerminal.AtEntry(args, Console.Error) is { } roleRefused) return roleRefused;

// A doctrine command is the CLI's: handed to the one this install carries.
if (route.Kind == TreeRouteKind.Doctrine) return await Daoris.Driver.Host.DoctrineConsole.RunAsync(args);

// The holder's brief (STEWARDROLE1a-i, D209 §2.2): Daoris's own text, an open verb that reads nothing and names no key.
if (args is ["roles", "brief"])
{
    Console.Out.Write(RolesBrief.Text);
    return 0;
}

// A terminal finds the Daoris that runs (FINDDAORIS1, D171 point 1): the install's launcher sets no variable, so a host the
// install carries takes the install's own `data/` as its home where none is named, as the desktop does (D63), before any door
// reads one. The service is found where a door asks for it (`ServiceHttp.FromEnvironment`): the variable, else the address the
// running desktop left in this home. A workspace build is no install, and keeps needing both named.
DaorisHome.Adopt(AppContext.BaseDirectory);

// FOUNDATION1w-s (the foundation design §19.15): the home's driver.json reads its staffing from the store, wired before any door or the
// loop loads it.
RoleStaffing.WireHome(Environment.GetEnvironmentVariable);

// One writer per home (HOMELEASE1, the terminal RPC design §5): a verb acting alone holds the home's lease for its mutation,
// never for a read (`VerbLeases` says which). Beside a running instance it acts as it always did, holding nothing, until RPC1's
// pipe makes it that instance's client; where another verb, the first-start move or the launcher's swap holds the home, it
// waits up to thirty seconds and is then refused, exit 1, naming which. A home named nowhere takes none: the verb refuses as
// it always did. The lease is let go as the verb ends. It is taken in the home the loop drives and holds (LEASEHOME2), `DAORIS_HOME`.
HomeLease? verbLease = null;
if (VerbLeases.HoldsHere(args, Environment.GetEnvironmentVariable) && DriverConfig.FindHome() is { } leaseHome)
{
    try
    {
        verbLease = HomeLease.ForVerb(leaseHome, CommandWords.Named(typed), out var leaseRefused);
        if (verbLease is null)
        {
            Console.Error.WriteLine($"daoris: {leaseRefused}");
            return 1;
        }
    }
    catch (HomeLeaseException error)
    {
        Console.Error.WriteLine($"daoris: {error.Message}");
        return 2;
    }
}

using var verbHolds = verbLease;

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

// Where a piece of work stands in its workflow (WORKFLOW1c2, D157, D50): the session's Workflow view's twin, a read, routed before
// the machine log opens for the trace's reason, with its own catch; and the person's Keep (WORKFLOW1f), which writes only its run.
if (args is ["workflow", .. var workflowArgs])
{
    return await Daoris.Driver.Host.WorkflowConsole.RunAsync(workflowArgs);
}

// The home's one conversion door (ONEWAY1ae): through the running Daoris alone, routed before the machine log opens, with its own catch.
if (args is ["convert", .. var convertArgs])
{
    return await Daoris.Driver.Host.ConvertConsole.RunAsync(convertArgs);
}

// A checkout's heavy gates under its repository's one slot (HEAVYRUN1a, D180's HEAVYRUN1 note). Routed before the machine log opens,
// with its own catch: it runs for minutes beside everything else, holds no home lease (VerbLeases), and writes only its slot's files.
if (args is ["run-heavy", .. var heavyArgs])
{
    return await Daoris.Driver.Host.HeavyConsole.RunAsync(heavyArgs);
}

// A person's verification of a branch or a commit (FABRIC-PIPELINE1d): a run of its own bound under the home's lines, which the running
// driver verifies. Routed before the machine log opens, with its own catch: it writes only its run's binding, and holds no lease.
if (args is ["verify", .. var verifyArgs])
{
    return await Daoris.Driver.Host.VerifyConsole.RunAsync(verifyArgs);
}

// A repository's integration shown (INTEGRATE1h): read from the record, no lease; its acts are the owner's cards in the running window's, which it waits on.
if (args is ["integrate", .. var integrateArgs])
{
    return await Daoris.Driver.Host.IntegrateConsole.RunAsync(integrateArgs, CancellationToken.None);
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

    // An ask's channel from a terminal (SESSONE2c, D167, D50): read it, or post the person's words to it; the ask page's *Channel* is
    // the other door. Asked for before the ask's other words, which would take `channel` as the first word of a new ask.
    if (args is ["ask", .. var channelArgs] && ChannelCommand.Asks(channelArgs))
    {
        return await Daoris.Driver.Host.ChannelConsole.RunAsync(channelArgs[1..]);
    }

    // Who is at work on an ask (COWORKVIEW1d): the ask page's *At work* section, beside its channel and for the same reason before the
    // ask's other words.
    if (args is ["ask", .. var atWorkArgs] && AskAtWorkVerb.Asks(atWorkArgs))
    {
        return await Daoris.Driver.Host.AskAtWorkConsole.RunAsync(atWorkArgs[1..]);
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

    // A place's quick set-up from a terminal (AGENTSPLACE1a2): PLACE_PLAN's and PLACE_APPLY's twin, and the one door that adopts a repository
    // (WORKFLOWCLEAN1 1f). With `--workspace`, a whole workspace's paced adoption (WSSETUP6, D124 §4.5), whose lines go to this host's log.
    if (args is ["place", .. var placeArgs])
    {
        return await Daoris.Driver.Host.PlaceConsole.RunAsync(placeArgs, log);
    }

    // Registration followed from each line when the person asks (WSSETUP5, D124 §3.1, D50): the repository row's
    // *Refresh* is the other door. A start and a line Daoris moves follow on their own.
    if (args is ["register", .. var registerArgs])
    {
        return await Daoris.Driver.Host.RegisterConsole.RunAsync(registerArgs, log);
    }

    // The second opinion's gate (XAGENT1f, D155 point 10, D50): ask, show, stop, go on anyway, or look yourself; the review's and
    // What needs you's presses are XAGENT1g's doors to the same. Ask Daoris is exempt: each is the person's own (D110, design §9).
    if (args is ["opinion", .. var opinionArgs])
    {
        return await Daoris.Driver.Host.OpinionConsole.RunAsync(opinionArgs);
    }

    // A pass's room measured (BRAINSTORM1c, D176 point 2): without --run the account the walk would take, with it one probe pass on it,
    // a spend the person presses by running it. Before the brainstorm's own verb, which only writes files.
    if (args is ["brainstorm", "measure", .. var measureArgs])
    {
        return await Daoris.Driver.Host.BrainstormMeasureConsole.RunAsync(measureArgs);
    }

    // A brainstorm (BRAINSTORM1a, D176 point 1): the verb writes a request the driver's look takes, and show, send and clear are
    // the person's own. Files under this home only: it spawns nothing and asks no service.
    if (args is ["brainstorm", .. var brainstormArgs])
    {
        return BrainstormCommand.Run(brainstormArgs, DriverConfig.HomeOf(DriverConfig.ResolvePath()), Console.Out, DateTimeOffset.UtcNow);
    }

    // A meeting (AGENTROOM1a, D177 point 7): the verb writes a request the driver's look takes, and show, send, stop and clear are
    // the person's own. Files under this home only, as the brainstorm's: it spawns nothing and asks no service.
    if (args is ["meeting", .. var meetingArgs])
    {
        return MeetingCommand.Run(meetingArgs, DriverConfig.HomeOf(DriverConfig.ResolvePath()), Console.Out, DateTimeOffset.UtcNow);
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

        // A quest's own kind and workflow, where no ask published it (WORKFLOW1e2, D157, D156, D50): the person's door, as an ask's
        // `--set-workflow` is; the quest page's choice is WORKFLOW1g's.
        if (QuestWorkflowCommand.Asks(questArgs))
        {
            if (QuestWorkflowCommand.Read(questArgs, out var workflowProblem) is not { } workflowAsk)
            {
                Console.Error.WriteLine($"daoris: {workflowProblem}");
                Console.Error.WriteLine(QuestWorkflowCommand.Usage);
                return 2;
            }

            using var workflowClient = ServiceClient.FromEnvironment();
            var workflowConfig = DriverConfig.ResolvePath();
            return await QuestWorkflowCommand.RunAsync(
                workflowAsk, new AskWorkflowWorld(workflowClient, () => DriverConfig.Load(workflowConfig), DriverConfig.HomeOf(workflowConfig)),
                Console.Out);
        }

        // The person marks a quest done (QUESTCLOSE1, D126's note, D50): the quest page's *Mark done…* is the other door, and a
        // finish at a checkpoint that left its quest taken names this one.
        if (QuestDoneCommand.Asks(questArgs))
        {
            if (QuestDoneCommand.Read(questArgs, out var doneProblem) is not { } doneAsk)
            {
                Console.Error.WriteLine($"daoris: {doneProblem}");
                Console.Error.WriteLine(QuestDoneCommand.Usage);
                return 2;
            }

            using var doneClient = ServiceClient.FromEnvironment();
            return await QuestDoneCommand.RunAsync(doneAsk, doneClient, Console.Out);
        }

        // The person's reopen of a sealed submission and their go-on with the step it held (FABRIC-CLOSE1c3, D156, D50): the quest
        // page's *Reopen…* and *Go on with it* are the other doors; abandoning the step is `quest abandon`.
        if (QuestReopenCommand.Asks(questArgs))
        {
            if (QuestReopenCommand.Read(questArgs, out var reopenProblem) is not { } reopenAsk)
            {
                Console.Error.WriteLine($"daoris: {reopenProblem}");
                Console.Error.WriteLine(QuestReopenCommand.Usage);
                return 2;
            }

            using var reopenClient = ServiceClient.FromEnvironment();
            return await QuestReopenCommand.RunAsync(reopenAsk, reopenClient, Console.Out);
        }

        if (questArgs is not [("delete" or "accept") and var verb, var questId])
        {
            Console.Error.WriteLine(
                "usage: daoris quest delete <id>  ·  quest accept <id>  ·  quest done <id> [--note \"…\"]  ·  "
                + "quest reopen <id> [--note \"…\"]  ·  quest go-on <id>  ·  "
                + "quest pause <id>  ·  quest resume <id>  ·  "
                + "quest abandon <id> [--reason \"…\" --yes]  ·  quest clear <id> [--failed] [--yes]  ·  "
                + "quest check <id> [--commit <sha>]  ·  quest review <id> reviewed|not-yet|skip [\"…\"]  ·  "
                + "quest review <id> off [\"…\"]  ·  quest review <id> on|<environment>  ·  "
                + "quest workflow <id> [--kind <kind>] [--workflow <id>|current]|--clear [\"…\"]");
            return 2;
        }

        using var client = ServiceClient.FromEnvironment();
        var (ok, message) = verb == "delete"
            ? await client.DeleteQuestAsync(questId)
            : await client.AcceptDepartureAsync(questId);
        Console.WriteLine($"daoris: {message}");
        // A refusal — something stands on the quest, or nothing waits for a yes — is an answer, not a tool error.
        return ok ? 0 : 1;
    }

    // Sessions from a terminal (SESSUX1g, D126 §7.1, D50): Sessions' list and its acts are the other door to the same
    // reader and the same owners; a stop of a session another process runs reaches it through the request its loop takes.
    if (args is ["sessions", .. var sessionsArgs])
    {
        return await Daoris.Driver.Host.SessionsConsole.RunAsync(sessionsArgs, log);
    }

    // The steward's holder's verbs (STEWARDROLE1b, D209 §3.3): each act sent only to the running Daoris as ROLE_ACT, proved by the key the
    // entry took; no alone path.
    if (args is ["steward", .. var stewardArgs])
    {
        return await StewardCommand.RunAsync(
            stewardArgs, Daoris.Driver.Host.InstanceHost.World("steward"), () => HolderKey.Taken, Console.Out);
    }

    // What came to the person, from a terminal (NOTIFY2i, D196, D50): the running Daoris's NOTIFICATIONS where it answers, the notices'
    // record alone otherwise; it changes nothing. The window's Notifications is the other door.
    if (args is ["notices", .. var noticesArgs])
    {
        var noticesHome = DriverConfig.HomeOf(DriverConfig.ResolvePath());
        return await NoticesRead.RunAsync(noticesArgs, noticesHome, Daoris.Driver.Host.InstanceHost.World(noticesHome, "notices"), Console.Out, Console.Error);
    }

    // Ask Daoris's history from a terminal (ASKHIST1, D50): the panel's list and search, words to one that go on in it, its name,
    // its pin and its delete. `help` alone, or a word it does not take, is still the usage below.
    if (args is ["help", var helpVerb, ..] && HelpCommand.Verbs.Contains(helpVerb))
    {
        return await Daoris.Driver.Host.HelpConsole.RunAsync(args[1..], log);
    }

    // A repository's session access setup, its workspace's or this machine's (AUTONOMY1a, D184 point 1, D50): the repository's setup
    // page's `SET_ACCESS` is the other door, over the same file.
    if (args is ["access", .. var accessArgs])
    {
        return await Daoris.Driver.Host.AccessConsole.RunAsync(accessArgs);
    }

    // The standing choices (`place`, `settings`, `role`, `workflow`'s saved graphs and `quest retry`, which the tree routes as the
    // choices' own words), `tool`, `agent` and `plugin`'s list and edits (PUSH1f2a-f, D194 point 1). One Ctrl+C handler for these,
    // registered here and not where `closing` is made, so a command routed above keeps the runtime's own Ctrl+C: it keeps the
    // process and cancels `closing`, whose token each command is handed, so a wait stops and is said, never a torn write.
    if (args is ["driver" or "tool" or "agent" or "plugin", ..])
    {
        Console.CancelKeyPress += (_, press) =>
        {
            press.Cancel = true;
            closing.Cancel();
        };
        return args[0] switch
        {
            "tool" => await Daoris.Driver.Host.ToolsConsole.RunAsync(args[1..], log, closing.Token),
            "agent" => await Daoris.Driver.Host.AgentsConsole.RunAsync(args[1..], closing.Token),
            "plugin" => await Daoris.Driver.Host.PluginConsole.RunAsync(args[1..], closing.Token),
            _ => await Daoris.Driver.Host.DriverChoicesConsole.RunAsync(args[1..], closing.Token),
        };
    }

    // This machine's heavy gates for a repository whose own daoris.gates.json marks none (HEAVYRUN1b2a, D180's HEAVYRUN1b2a note,
    // D50): kept in the driver's file, never in the repository; the repository's setup page is the other door (HEAVYRUN1b2b).
    if (args is ["heavy", .. var mappingArgs])
    {
        return Daoris.Driver.Host.HeavyMappingConsole.Run(mappingArgs);
    }

    // Which checks a repository's sessions run under Current (FABRIC-PIPELINE1b, D192 point 4, D50): kept in the driver's file; the
    // repository's setup page is the other door.
    if (args is ["checks", .. var checksArgs])
    {
        return Daoris.Driver.Host.SessionChecksConsole.Run(checksArgs);
    }

    // Finished history from a terminal (HIST1d, D153, the history-clearing design §6.2, D50): what this machine keeps of it, per
    // workspace, and a workspace's clear, listed first and pressed with --yes. The workspace page's *Kept on this machine* is the
    // other door; a clear's log line says this one.
    if (args is ["history", .. var historyArgs])
    {
        return await Daoris.Driver.Host.HistoryConsole.RunAsync(historyArgs, log);
    }

    // The tree lifecycle from a terminal (D51, D50): `daoris place trees <repo> on|off` is the standing opt-in, a file edit; these are disk.
    if (args is ["trees", .. var treesArgs])
    {
        return await Daoris.Driver.Host.TreesConsole.RunAsync(treesArgs, log);
    }

    // The machine's remotes, Daoris's browser and the registry (ONEWAY1ai-i; the oneway design §6 X3): the typed operations the window
    // calls, and what the CLI's TypeScript did before them.
    if (args is ["remote", .. var remoteArgs])
    {
        return Daoris.Driver.Host.RemoteConsole.Run(remoteArgs);
    }

    if (args is ["browser", .. var browserArgs])
    {
        return Daoris.Driver.Host.BrowserConsole.Run(browserArgs);
    }

    if (args is ["repository", .. var repositoryArgs])
    {
        return await Daoris.Driver.Host.RepositoryConsole.RunAsync(repositoryArgs);
    }

    // Each unit of work's milestone (FABRIC-MILESTONE1a, D50): a look, read from the records and changing nothing.
    if (args is ["work", "state", .. var stateArgs])
    {
        return await Daoris.Driver.Host.WorkConsole.MilestonesAsync(stateArgs);
    }

    // The attempts held for the person, and their word on one (FABRIC-PERFORM1g, D50): What needs you's presses are the other door.
    if (args is ["work", "held", .. var heldArgs])
    {
        return HeldAttempts.Run(DriverConfig.HomeOf(DriverConfig.ResolvePath()), heldArgs, Console.Out, Console.Error);
    }

    // The person's delete of the recovery refs nothing holds (RECOVERYDROP1, D50): the workspace's Branches press is the other door.
    if (args is ["work", "recovery", "drop", .. var dropArgs])
    {
        return await Daoris.Driver.Host.WorkConsole.RecoveryDropAsync(dropArgs);
    }

    // How a press this terminal sent to the running Daoris ended (RPC1j, the terminal RPC design §6.1): asked again by its id and the
    // key it was sent with, kept under the home for a day. It sends nothing again.
    if (args is ["result", var resultId])
    {
        return await InstanceDoor.ResultAsync(resultId, Daoris.Driver.Host.InstanceHost.World("result"));
    }

    // A press this terminal sent that still waits for the person's Confirm, taken back (CONFIRMWAIT1b, the confirm-wait design §4.3):
    // the pipe's own WITHDRAW with the key it was sent with, which never touches a press the person already confirmed.
    if (args is ["withdraw", var withdrawId])
    {
        return await InstanceDoor.WithdrawAsync(withdrawId, Daoris.Driver.Host.InstanceHost.World("withdraw"));
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

    // How this Daoris is wired, from a terminal (WIRING1b, D50): Settings → This Daoris is the other door to the same reader.
    // Needs no service: a status door is asked only where an address is named or left in the home's address file.
    if (args is ["wiring", .. var wiringArgs])
    {
        var exit = WiringCommand.Run(wiringArgs, WiringWorld.OfThisTerminal(DaorisHome.Resolve()), out var wiringSaid);
        (exit == 2 ? Console.Error : Console.Out).Write(wiringSaid);
        return exit;
    }

    // Free up space from a terminal (KEEP1e, D195 point 4, D50): the Disk space card's *Free up space…* is the other door to the same
    // list and run. Listed first and changing nothing; with --yes, the client of a running Daoris (confirmed in its window), or alone
    // under the home's run lock, refused from a session's terminal.
    if (args is ["space", "free", .. var freeArgs])
    {
        var freeHome = DaorisHome.Resolve() ?? throw new DriverException(DaorisHome.Sentence);
        // A Ctrl+C is a stop heard between units, never inside one's delete (the cleanup design §3.1).
        Console.CancelKeyPress += (_, press) =>
        {
            press.Cancel = true;
            closing.Cancel();
        };
        return await SpaceFreeCommand.RunAsync(
            freeArgs, SpaceFreeWorld.OfThisTerminal(freeHome, log), Daoris.Driver.Host.InstanceHost.World(freeHome, "daoris"), closing.Token);
    }

    // The disk space from a terminal (KEEP1c, D195 point 5, D50): This Daoris's Disk space card is the other door to the same reader.
    // Reads only, and needs no service: each kind's size is this process's walk, `--measure`'s.
    if (args is ["space", .. var spaceArgs])
    {
        var spaceHome = DaorisHome.Resolve() ?? throw new DriverException(DaorisHome.Sentence);
        var exit = SpaceCommand.Run(spaceArgs, SpaceWorld.OfThisTerminal(spaceHome), out var spaceSaid);
        (exit == 2 ? Console.Error : Console.Out).Write(spaceSaid);
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
        var pluginsHome = DaorisHome.Resolve();
        // RPC1e: approve and withdraw go through the running Daoris where one answers, confirmed in its window.
        return await PluginsCommand.RunAsync(
            pluginsArgs, Console.Out, pluginsHome, log, instance: Daoris.Driver.Host.InstanceHost.World(pluginsHome, "plugins"));
    }

    // 🔴 The loop only by its verb (DRV8a, D104): a bare invocation, or a word nobody answers, is the
    // usage and starts nothing — it used to fall through to the watch loop.
    if (DriverCommand.Read(args, out var problem) is not { } loop)
    {
        var help = DriverCommand.AskedForHelp(args);
        if (problem is not null) Console.Error.WriteLine($"daoris: {problem}");
        (help ? Console.Out : Console.Error).WriteLine(DriverCommand.Usage);
        return help ? 0 : 2;
    }

    // FOUNDATION1f-i: this process is the headless loop, and its run of the desktop store says so; a move it claims names that run.
    StoreProcess.Kind = StoreProcess.Host;

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
    // The home a verb's lease is taken in too (`DriverConfig.FindHome`, LEASEHOME2): the folder this config is in.
    var home = DriverConfig.HomeOf(configPath);

    // One live driver per home (DRV8a, D104), taken before anything reaches the service: a second loop
    // races the first for the same quests, so it is refused naming the first — a refusal, exit 1, not a
    // tool error — unless it was asked to share. The lock is the home's lease now (HOMELEASE1): an act holding it
    // (a terminal's verb, the first-start move, the launcher's swap) is waited for up to thirty seconds, as a verb waits,
    // and only then refused; `--share` runs beside an instance, never beside an act.
    var held = loop.Share
        ? HomeLease.Share(home, LeaseKind.Headless, out var holder)
        : HomeLease.TryAcquire(home, LeaseKind.Headless, out holder);
    if (held is null && holder is { IsInstance: false })
    {
        held = HomeLease.Await(home, LeaseKind.Headless, null, HomeLease.Wait, out holder, besideInstance: loop.Share);
    }

    if (held is null)
    {
        Console.Error.WriteLine($"driver: {(holder!.IsInstance ? HomeLease.Refusal(home, holder) : HomeLease.WaitRefusal(home, holder))}");
        return 1;
    }

    using var leaseHeld = held;

    if (held.Beside is { } beside)
    {
        // FABRIC-END7's review: only the lease's holder lands done work automatically, so this loop lands none.
        Console.WriteLine(
            $"driver: running beside {beside.Named}, as --share asked — the two loops race for the same quests, and the take decides. "
            + "Done work lands automatically from that one alone, which holds the home.");
    }

    var started = DateTimeOffset.UtcNow;
    log.Info("app.started", ("mode", once ? "once" : untilIdle ? "until-idle" : "watch"));
    // Marked under the home, so the desktop sharing it can tell this host's sessions from orphans.
    var processes = new SessionProcesses(DaorisHome.Entry(home, HomeEntry.SessionPids));

    using var service = ServiceClient.FromEnvironment();

    // What this host runs and how long each part takes, into its log (LOG1b): the watcher hears the
    // client's opens and moves and the record's events, so the record is handed to every driver below.
    var events = new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions));
    using var sessionLog = new SessionLog(log, service, events);
    // FOUNDATION1 l-iv: the service's journal read from this process's come-up, so `session.ended` is written for every end, whoever wrote it.
    // FOUNDATION1 m-ii: and this host's own teller of the driven work's news, from a cursor it keeps, so a park made just before a restart is told once after it.
    var news = new News(home);
    var hostNews = new NewsTeller(
        news, "host", row => !row.Marks.Contains("chat", StringComparer.Ordinal), () => DateTimeOffset.UtcNow,
        async pieces =>
        {
            foreach (var piece in pieces)
            {
                var item = await NewsAttention.OfAsync(service, piece, closing.Token);
                // Kept in Notifications whatever the switch says (NOTIFY2f): the desktop's windows read the record again and say what this host wrote.
                Notices.Keep(home, [item], DateTimeOffset.UtcNow, why => log.Warn("notices.refused", ("why", why)));
                if (NotifyRule.Tells(item, NotifyRule.SwitchOn(configPath))) Console.WriteLine($"  !  {item.Line}");
            }
        })
    {
        Incomplete = batch => log.Warn("news.incomplete", ("teller", "host"), ("store", batch.Store), ("floor", batch.Floor)),
        Refused = why => log.Warn("news.refused", ("teller", "host"), ("why", why)),
    };
    _ = Task.Run(
        () => JournalFeed.RunAsync(
            service,
            [new JournalTeller(sessionLog.TellAsync), new JournalTeller(hostNews.TellAsync, () => news.Cursor("host"), position => news.Advance("host", position))],
            log, closing.Token),
        closing.Token);
    // SESSWATCH1a (D200): no session is ended on time; --quiet-end ends one of this run unheard that long, and says so.
    if (loop.QuietEnd is { } quietEnd)
    {
        SessionWatch.Of(events).EndAfter = TimeSpan.FromMinutes(quietEnd);
        Console.WriteLine($"driver: --quiet-end {quietEnd}: a session of this run unheard for {quietEnd} minutes is ended and carried on.");
    }
    else if (once || untilIdle)
    {
        Console.WriteLine("driver: no session is ended on time; a quiet one is said here, and this run waits for it (--quiet-end <minutes> ends one).");
    }

    // The watch, once it runs, so a terminal's words a loop keeps are taken up at once rather than at its next look (MSG1e).
    DriverWatch? watching = null;

    // Every loop on the home watches the requests a terminal's `sessions stop|finish|decline` writes (SESSUX1g, D126 §7.1),
    // and acts on one for a session this host runs as the screen's route would. A terminal's `sessions say` (MSG1e) is held
    // at the door of a session this host runs, or kept on the record of one nothing here runs, shown in this host's own
    // record of it, and the look nudged.
    await using var requests = new SessionRequestWatch(home, processes, () => service)
    {
        Say = new LoopWords(processes, () => service) { Events = events, Nudge = session => watching?.Said(session), Home = home }.HoldAsync,
        // SAIDBACK1c: a terminal's `sessions take-back` settles the word in this host's own record of the conversation.
        TakeBack = new LoopWords(processes, () => service) { Events = events, Nudge = session => watching?.Said(session) }.TakeBackAsync,
        // FABRIC-EVENT2: each look the folder woke, with its ms and why.
        Log = log,
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
        HomeSeed.Once(configPath, home);
        watching = new DriverWatch(service, configPath, home, processes, sync, hooks: hooks, events: events) { Log = log, Share = held.Beside is not null };
        await watching.RunAsync(
            async (report, ticked) =>
            {
                Print(report, quietWhenIdle: true);
                // Observed either way, so turning notifications back on does not then announce
                // everything that happened while they were off — the switch is about being TOLD.
                var read = await parks.LookAsync(
                    report, ticked.ForgivenAt, token => SessionRecords.ReadAsync(service.BaseUrl, key, ct: token), closing.Token);
                // The look's own news, told once across restarts by the said-set and not by a first look's baseline (FOUNDATION1 m-iv).
                var events = AttentionNews.Told(news, attention.Observe(report, read), DateTimeOffset.UtcNow);
                // Kept in Notifications whatever the switch says (NOTIFY2f, the notification design §2.5): the desktop's windows
                // read the record again and say what this host wrote; a write refused is logged and dropped.
                Notices.Keep(home, events, DateTimeOffset.UtcNow, why => log.Warn("notices.refused", ("why", why)));
                foreach (var item in events.Where(each => NotifyRule.Tells(each, ticked.Notify))) Console.WriteLine($"  !  {item.Line}");
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
catch (HomeLeaseException error)
{
    // The home's lease changed under every attempt, or would not be written (HOMELEASE1): a sentence, never a stack trace.
    Console.Error.WriteLine($"driver: {error.Message}");
    log.Failed("the headless driver, taking the home", error);
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
