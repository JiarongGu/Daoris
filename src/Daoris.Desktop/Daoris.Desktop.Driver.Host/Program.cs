using System.Text;
using Daoris.Adapters.DesktopStore.Sqlite;
using Daoris.Adapters.Git;
using Daoris.Adapters.Machine;
using Daoris.Driver;
using Daoris.Ports.Ambient;

// The desktop's program: every group of `daoris …`, which the install's launcher in app/bin/ runs, what the family
// rehearsal drives, and what the desktop shell embeds. A person's words are routed once, at the entry, by the one command tree
// (`CommandTree`, over the groups the library's commands declare), into the words each command below reads: a word no group owns is
// handed to the CLI the install carries, and `DriverCommand.Usage` says each group. The loop watches a service, starts sessions where
// the person opted in, and prints every verdict — a quest that is sitting must always say why.
//
//   DAORIS_HOME            where every machine-local file lives — required unless each file's
//                          own variable names it; the installed desktop sets it for the account, and a
//                          host the install carries takes the install's `data/` when none is named
//   DAORIS_SERVICE_URL     where the service is                (the driver is its client: a host the install
//                          carries finds the running desktop's from its home when none is named)
//   DAORIS_SERVICE_KEY     sent as a bearer token when set     (absent: local trust)
//   DAORIS_REMOTE_URL      one workspace's remote, with its key (or $DAORIS_HOME/remotes.json;
//   DAORIS_REMOTE_KEY        either env var present means the environment is the answer, whole,
//   DAORIS_REMOTE_WORKSPACE  for the workspace named here — absent: `default`)
//   DAORIS_REMOTE_CONFIG   where the map is                    (default: $DAORIS_HOME/remotes.json)
//
// Every command that changes the home while acting alone holds the home's lease for its run (VerbLeases), and waits up to thirty
// seconds where another such act holds it, then is refused naming it (exit 1); beside a running Daoris it sends the person's press
// to it, confirmed in its window. With no words, the usage, exit 2 — never a loop: only `drive` starts one.
//
// While watching, a line marked `!` is what would have been a toast on a machine with a screen:
// a session parked, or one ended without the person asking. `daoris settings notify off`
// turns it off here exactly as the desktop's checkbox does — one file, two doors.
//
// Exit codes keep the family contract: 0 clean · 1 a refusal · 2 tool error.
// The holder key leaves this process first, so no start it builds can copy it. The verb is its one reader:
// `session supervise` reads what is taken, and nothing else does.
HolderKey.Take();

// This process is a root: it chooses the ports the library reads, once, before anything opens the store or starts a process.
var processes = new MachineProcessHost(StartPlaces.Shared, GitStartAdmission.Admit, new ProcessOwner(OwnedProcesses.Own, OwnedProcesses.End))
{
    Timed = ProcessStarts.Told,
};
AmbientPorts.Set(new PortSet
{
    Clock = TimeProvider.System,
    Store = SqliteDesktopStores.Engines,
    Admission = StartPlaces.Shared,
    Processes = processes,
    Liveness = MachineLiveness.Shared,
    StartPolicy = GitStartAdmission.Admit,
    Seal = KeySeal.Port,
    Git = new GitProcessRuns(processes, GitRunPolicy.Shared),
    Repository = GitRepository.Shared,
});

if (OperatingSystem.IsWindows())
{
    Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
}

// A word handed once is never handed back: who handed these words is read, and taken out of what this process passes on.
var handedBy = Environment.GetEnvironmentVariable(CommandWords.HandedVariable);
Environment.SetEnvironmentVariable(CommandWords.HandedVariable, null);

// How long a press waits on its Confirm, taken from the words before any verb, lease or
// family reads them, and set once on every world a verb is given (`InstanceHost.World`).
if (!InstanceDoor.TakeWait(args, out var verbArgs, out var pressWait, out var waitProblem))
{
    Console.Error.WriteLine($"daoris: {waitProblem}");
    return 2;
}

Daoris.Driver.Host.InstanceHost.Wait = pressWait;

// The one tree routes the words once, into the words the commands below read.
var typed = verbArgs;
var route = CommandTree.Route(typed, handedBy);
if (route.Kind == TreeRouteKind.Usage)
{
    if (route.Said is not null) Console.Error.WriteLine($"daoris: {route.Said}");
    (route.Asked ? Console.Out : Console.Error).WriteLine(
        route.Group is { } group ? DriverCommand.UsageOf(group) ?? DriverCommand.Usage : DriverCommand.Usage);
    if (route.Asked && route.Group is null) await Daoris.Driver.Host.DoctrineConsole.UsageAsync();
    return route.Asked ? 0 : 2;
}

args = [.. route.Words];

// A role's terminal, by the entry's marker, runs its roles' verbs and the open reads its holder needs, and
// nothing else: no person's press and no verb acting alone. Those it runs refuse their own alone paths (`InstanceDoor`).
if (RoleTerminal.AtEntry(args, Console.Error) is { } roleRefused) return roleRefused;

if (route.Kind == TreeRouteKind.Doctrine && args.Length > 0 && DaorisHome.Resolve() is { } pluginHome && PluginCommandConsole.Names(pluginHome, args[0]))
    return await PluginCommandConsole.RunAsync(pluginHome, args, PluginCommandConsole.Console(), CancellationToken.None);

// A word no group owns is the CLI's: handed to the one this install carries, which refuses it where it is no doctrine command.
if (route.Kind == TreeRouteKind.Doctrine) return await Daoris.Driver.Host.DoctrineConsole.RunAsync(args);

// The holder's brief: Daoris's own text, an open verb that reads nothing and names no key.
if (args is ["roles", "brief"])
{
    Console.Out.Write(RolesBrief.Text);
    return 0;
}

// The events relay: routed before the home's adoption, the generic lease and the machine
// log, whose own lines print what no fixed sentence holds; its console says every failure of both its forms by a fixed sentence.
if (args is ["events", .. var eventsArgs])
{
    return await RelayCommand.RunAsync(eventsArgs, Console.Out, Console.Error, AppContext.BaseDirectory);
}

// A terminal finds the Daoris that runs: the install's launcher sets no variable, so a host the
// install carries takes the install's own `data/` as its home where none is named, as the desktop does, before any door
// reads one. The service is found where a door asks for it (`ServiceHttp.FromEnvironment`): the variable, else the address the
// running desktop left in this home. A workspace build is no install, and keeps needing both named.
DaorisHome.Adopt(AppContext.BaseDirectory);

// The home this program acts for, named once: what carries no home of its own (Daoris's own git, a probe's tools) reads it, never the
// variable a process inherited.
ProgramHome.Named = DaorisHome.Resolve();

// The home's driver.json reads its staffing from the store, wired before any door or the
// loop loads it.
RoleStaffing.WireHome(Environment.GetEnvironmentVariable);

// One writer per home: a verb acting alone holds the home's lease for its mutation,
// never for a read (`VerbLeases` says which). Beside a running instance it holds nothing, and the pipe
// makes it that instance's client; where another verb, the first-start move or the launcher's swap holds the home, it
// waits up to thirty seconds and is then refused, exit 1, naming which. A home named nowhere takes none: the verb refuses.
// The lease is let go as the verb ends. It is taken in the home the loop drives and holds, `DAORIS_HOME`.
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

// One read from a commit, a session or a quest back to its ask. Routed before the machine log opens,
// since that open prunes old files and the trace writes nothing anywhere; it keeps its own catch for the same reason.
if (args is ["trace", .. var traceArgs])
{
    return await Daoris.Driver.Host.TraceConsole.RunAsync(traceArgs);
}

// The branch list: a read, routed before the machine log opens for the trace's reason, with its own catch.
if (args is ["git", .. var gitArgs])
{
    return await Daoris.Driver.Host.GitConsole.RunAsync(gitArgs);
}

// Where a piece of work stands in its workflow: the session's Workflow view's twin, a read, routed before
// the machine log opens for the trace's reason, with its own catch; and the person's Keep, which writes only its run.
if (args is ["workflow", .. var workflowArgs])
{
    return await Daoris.Driver.Host.WorkflowConsole.RunAsync(workflowArgs);
}

// A checkout's heavy gates under its repository's one slot. Routed before the machine log opens,
// with its own catch: it runs for minutes beside everything else, holds no home lease (VerbLeases), and writes only its slot's files.
if (args is ["run-heavy", .. var heavyArgs])
{
    return await Daoris.Driver.Host.HeavyConsole.RunAsync(heavyArgs);
}

// A person's verification of a branch or a commit: a run of its own bound under the home's lines, which the running
// driver verifies. Routed before the machine log opens, with its own catch: it writes only its run's binding, and holds no lease.
if (args is ["verify", .. var verifyArgs])
{
    return await Daoris.Driver.Host.VerifyConsole.RunAsync(verifyArgs);
}

// A repository's integration shown: read from the record, no lease; its acts are the owner's cards in the running window's, which it waits on.
if (args is ["integrate", .. var integrateArgs])
{
    return await Daoris.Driver.Host.IntegrateConsole.RunAsync(integrateArgs, CancellationToken.None);
}

// The machine log: this host's watch and every exception nothing caught, in a file of its
// own beside the desktop's. With no home it writes nothing, and the sentence below still says why.
using var log = MachineLog.Open("driver");
log.WatchUnhandled();

// The exception that ends this entry point is written as `error`. The entry point is async, so the `using` above
// has closed `log` by the time the runtime raises that exception as unhandled, and a driver that died at start left nothing
// of why. `start` is the loop's start, until its first look; a verb runs once with no start of its own to tell apart, so
// what ends one is `unhandled` from the moment it is chosen.
var entryPoint = log.WatchEntryPoint();
if (DriverCommand.Read(args, out _) is null) entryPoint.Running();

// The loop's close, cancelled only by the person's Ctrl+C, which is registered below once a loop is asked for. Made
// here, outside the one catch, so that catch can tell that close from any other cancellation.
using var closing = new CancellationTokenSource();

// Every door inside the one catch, so a missing home or service is exit 2 and a sentence, never a
// stack trace (`trees` with no home would otherwise be an unhandled exception).
try
{
    // A conversation from a terminal: the same ledger, the same lock, the same record —
    // the desktop is where a person usually chats, and a machine with no screen is still a machine.
    if (args is ["chat", .. var chatArgs])
    {
        return await Daoris.Driver.Host.ChatConsole.RunAsync(chatArgs);
    }

    // An ask's channel from a terminal: read it, or post the person's words to it; the ask page's *Channel* is
    // the other door. Asked for before the ask's other words, which would take `channel` as the first word of a new ask.
    if (args is ["ask", .. var channelArgs] && ChannelCommand.Asks(channelArgs))
    {
        return await Daoris.Driver.Host.ChannelConsole.RunAsync(channelArgs[1..]);
    }

    // Who is at work on an ask: the ask page's *At work* section, beside its channel and for the same reason before the
    // ask's other words.
    if (args is ["ask", .. var atWorkArgs] && AskAtWorkVerb.Asks(atWorkArgs))
    {
        return await Daoris.Driver.Host.AskAtWorkConsole.RunAsync(atWorkArgs[1..]);
    }

    // Pausing, resuming and abandoning an ask's work from a terminal: the ask's page is the
    // other door. Asked for before the ask's other words, which would take `--pause` as the words of a new ask.
    if (args is ["ask", .. var workArgs] && WorkCommand.Asks(WorkScope.Ask, workArgs))
    {
        return await Daoris.Driver.Host.WorkConsole.RunAsync(WorkScope.Ask, workArgs, log);
    }

    // Clearing an ask's work from this machine: the ask's page is the
    // other door. Asked for before the ask's other words, which would take `--clear` as the words of a new ask.
    if (args is ["ask", .. var clearArgs] && HistoryCommand.Asks(WorkScope.Ask, clearArgs))
    {
        return await Daoris.Driver.Host.HistoryConsole.RunAsync(WorkScope.Ask, clearArgs, log);
    }

    // An ask from a terminal: the page's composer at workspace scope is the other door.
    if (args is ["ask", .. var askArgs])
    {
        return await Daoris.Driver.Host.AskConsole.RunAsync(askArgs);
    }

    // A place's quick set-up from a terminal: PLACE_PLAN's and PLACE_APPLY's twin, and the one door that adopts a repository.
    // With `--workspace`, a whole workspace's paced adoption, whose lines go to this host's log.
    if (args is ["place", .. var placeArgs])
    {
        return await Daoris.Driver.Host.PlaceConsole.RunAsync(placeArgs, log);
    }

    // Registration followed from each line when the person asks: the repository row's
    // *Refresh* is the other door. A start and a line Daoris moves follow on their own.
    if (args is ["register", .. var registerArgs])
    {
        return await Daoris.Driver.Host.RegisterConsole.RunAsync(registerArgs, log);
    }

    // The second opinion's gate: ask, show, stop, go on anyway, or look yourself; the review's and
    // What needs you's presses are doors to the same. Ask Daoris is exempt: each is the person's own.
    if (args is ["opinion", .. var opinionArgs])
    {
        return await Daoris.Driver.Host.OpinionConsole.RunAsync(opinionArgs);
    }

    // A pass's room measured: without --run the account the walk would take, with it one probe pass on it,
    // a spend the person presses by running it. Before the brainstorm's own verb, which only writes files.
    if (args is ["brainstorm", "measure", .. var measureArgs])
    {
        return await Daoris.Driver.Host.BrainstormMeasureConsole.RunAsync(measureArgs);
    }

    // A brainstorm: the verb writes a request the driver's look takes, and show, send and clear are
    // the person's own. Files under this home only: it spawns nothing and asks no service.
    if (args is ["brainstorm", .. var brainstormArgs])
    {
        return BrainstormCommand.Run(brainstormArgs, DriverConfig.HomeOf(DriverConfig.ResolvePath()), Console.Out, DateTimeOffset.UtcNow);
    }

    // A meeting: the verb writes a request the driver's look takes, and show, send, stop and clear are
    // the person's own. Files under this home only, as the brainstorm's: it spawns nothing and asks no service.
    if (args is ["meeting", .. var meetingArgs])
    {
        return MeetingCommand.Run(meetingArgs, DriverConfig.HomeOf(DriverConfig.ResolvePath()), Console.Out, DateTimeOffset.UtcNow);
    }

    // Deleting a quest made by mistake: the quest drawer's Delete is the other door. Accepting a done's
    // departure from what the person required: the quest page's yes and Ask Daoris's are owed. Pausing,
    // resuming and abandoning one quest's work: the quest's page is the other door. Clearing a
    // closed quest's work, or its failed sessions, from this machine: the quest's
    // page is the other door. Reading a done's evidence again: the quest page's Check again is its door.
    if (args is ["quest", .. var questArgs])
    {
        // A read of the quests this machine holds; it changes nothing.
        if (QuestListCommand.Asks(questArgs))
        {
            if (QuestListCommand.Read(questArgs, out var listProblem) is not { } listed)
            {
                Console.Error.WriteLine($"daoris: {listProblem}");
                Console.Error.WriteLine(QuestListCommand.Usage);
                return 2;
            }

            using var listClient = ServiceClient.FromEnvironment();
            return await QuestListCommand.RunAsync(listed, listClient, Console.Out);
        }

        if (WorkCommand.Asks(WorkScope.Quest, questArgs))
        {
            return await Daoris.Driver.Host.WorkConsole.RunAsync(WorkScope.Quest, questArgs, log);
        }

        if (HistoryCommand.Asks(WorkScope.Quest, questArgs))
        {
            return await Daoris.Driver.Host.HistoryConsole.RunAsync(WorkScope.Quest, questArgs, log);
        }

        // A done's evidence read again: the quest page's Check again is the other door to the same read.
        if (QuestCheckCommand.Asks(questArgs))
        {
            return await Daoris.Driver.Host.QuestCheckConsole.RunAsync(questArgs, log);
        }

        // The person's verdict on a review: the quest's page and the strip's chip are
        // doors to the same verdict. Ask Daoris is exempt: the verdict is a look only the person took.
        if (QuestReviewCommand.Asks(questArgs))
        {
            return await Daoris.Driver.Host.QuestReviewConsole.RunAsync(questArgs, log);
        }

        // A quest's own kind and workflow, where no ask published it: the person's door, as an ask's
        // `--set-workflow` is; the quest page's choice is the other.
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
            return await QuestWorkflowCommand.PressAsync(
                workflowAsk, new AskWorkflowWorld(workflowClient, () => DriverConfig.Load(workflowConfig), DriverConfig.HomeOf(workflowConfig)),
                Console.Out, Daoris.Driver.Host.InstanceHost.World("quest"));
        }

        // The person marks a quest done: the quest page's *Mark done…* is the other door, and a
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
            return await QuestDoneCommand.PressAsync(doneAsk, doneClient, Console.Out, Daoris.Driver.Host.InstanceHost.World("quest"));
        }

        // The person's reopen of a sealed submission and their go-on with the step it held: the quest
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
            return await QuestReopenCommand.PressAsync(reopenAsk, reopenClient, Console.Out, Daoris.Driver.Host.InstanceHost.World("quest"));
        }

        if (questArgs is not [("delete" or "accept") and var verb, var questId])
        {
            Console.Error.WriteLine(
                "usage: daoris quest list [--repository <name>] [--workspace <name>] [--open] [--json]  ·  quest delete <id>  ·  quest accept <id>  ·  quest done <id> [--note \"…\"]  ·  "
                + "quest reopen <id> [--note \"…\"]  ·  quest go-on <id>  ·  "
                + "quest pause <id>  ·  quest resume <id>  ·  "
                + "quest abandon <id> [--reason \"…\" --yes]  ·  quest clear <id> [--failed] [--yes]  ·  "
                + "quest check <id> [--commit <sha>]  ·  quest review <id> reviewed|not-yet|skip [\"…\"]  ·  "
                + "quest review <id> off [\"…\"]  ·  quest review <id> on|<environment>  ·  "
                + "quest workflow <id> [--kind <kind>] [--workflow <id>|current]|--clear [\"…\"]");
            return 2;
        }

        using var client = ServiceClient.FromEnvironment();
        var questWorld = Daoris.Driver.Host.InstanceHost.World("quest");
        return verb == "delete"
            ? await QuestDeleteCommand.PressAsync(questId, client, Console.Out, questWorld)
            : await QuestAcceptCommand.PressAsync(questId, client, Console.Out, questWorld);
    }

    // Sessions from a terminal: Sessions' list and its acts are the other door to the same
    // reader and the same owners; a stop of a session another process runs reaches it through the request its loop takes.
    if (args is ["sessions", .. var sessionsArgs])
    {
        return await Daoris.Driver.Host.SessionsConsole.RunAsync(sessionsArgs, log);
    }

    // The steward's holder's verbs: each act sent only to the running Daoris as ROLE_ACT, proved by the key the
    // entry took; no alone path.
    if (args is ["steward", .. var stewardArgs])
    {
        return await StewardCommand.RunAsync(
            stewardArgs, Daoris.Driver.Host.InstanceHost.World("steward"), () => HolderKey.Taken, Console.Out);
    }

    // What came to the person, from a terminal: the running Daoris's NOTIFICATIONS where it answers, the notices'
    // record alone otherwise; it changes nothing. The window's Notifications is the other door.
    if (args is ["notices", .. var noticesArgs])
    {
        var noticesHome = DriverConfig.HomeOf(DriverConfig.ResolvePath());
        return await NoticesRead.RunAsync(noticesArgs, noticesHome, Daoris.Driver.Host.InstanceHost.World(noticesHome, "notices"), Console.Out, Console.Error);
    }

    // Ask Daoris's history from a terminal: the panel's list and search, words to one that go on in it, its name,
    // its pin and its delete. `help` alone, or a word it does not take, is still the usage below.
    if (args is ["help", var helpVerb, ..] && HelpCommand.Verbs.Contains(helpVerb))
    {
        return await Daoris.Driver.Host.HelpConsole.RunAsync(args[1..], log);
    }

    // A repository's session access setup, its workspace's or this machine's: the repository's setup
    // page's `SET_ACCESS` is the other door, over the same file.
    if (args is ["access", .. var accessArgs])
    {
        return await Daoris.Driver.Host.AccessConsole.RunAsync(accessArgs);
    }

    // The standing choices (`place`, `settings`, `role`, `workflow`'s saved graphs and `quest retry`, which the tree routes as the
    // choices' own words), `tool`, `agent` and `plugin`'s list and edits. One Ctrl+C handler for these,
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

    // This machine's heavy gates for a repository whose own daoris.gates.json marks none:
    // kept in the driver's file, never in the repository; the repository's setup page is the other door.
    if (args is ["heavy", .. var mappingArgs])
    {
        return Daoris.Driver.Host.HeavyMappingConsole.Run(mappingArgs);
    }

    // Which checks a repository's sessions run under Current: kept in the driver's file; the
    // repository's setup page is the other door.
    if (args is ["checks", .. var checksArgs])
    {
        return Daoris.Driver.Host.SessionChecksConsole.Run(checksArgs);
    }

    // Finished history from a terminal: what this machine keeps of it, per
    // workspace, and a workspace's clear, listed first and pressed with --yes. The workspace page's *Kept on this machine* is the
    // other door; a clear's log line says this one.
    if (args is ["history", .. var historyArgs])
    {
        return await Daoris.Driver.Host.HistoryConsole.RunAsync(historyArgs, log);
    }

    // The tree lifecycle from a terminal: a workflow's isolate step opts a repository in; these are disk.
    if (args is ["trees", .. var treesArgs])
    {
        return await Daoris.Driver.Host.TreesConsole.RunAsync(treesArgs, log);
    }

    // The machine's remotes, Daoris's browser and the registry: the typed operations the window calls.
    if (args is ["remote", .. var remoteArgs])
    {
        return Daoris.Driver.Host.RemoteConsole.Run(remoteArgs);
    }

    if (args is ["browser", .. var browserArgs])
    {
        return await Daoris.Driver.Host.BrowserConsole.RunAsync(browserArgs);
    }

    if (args is ["files", .. var filesArgs])
    {
        return await Daoris.Driver.Host.FilesConsole.RunAsync(filesArgs);
    }

    if (args is ["repository", .. var repositoryArgs])
    {
        return await Daoris.Driver.Host.RepositoryConsole.RunAsync(repositoryArgs);
    }

    // Each unit of work's milestone: a look, read from the records and changing nothing.
    if (args is ["work", "state", .. var stateArgs])
    {
        return await Daoris.Driver.Host.WorkConsole.MilestonesAsync(stateArgs);
    }

    // The attempts held for the person, and their word on one: What needs you's presses are the other door.
    if (args is ["work", "held", .. var heldArgs])
    {
        return HeldAttempts.Run(DriverConfig.HomeOf(DriverConfig.ResolvePath()), heldArgs, Console.Out, Console.Error);
    }

    // The person's delete of the recovery refs nothing holds: the workspace's Branches press is the other door.
    if (args is ["work", "recovery", "drop", .. var dropArgs])
    {
        return await Daoris.Driver.Host.WorkConsole.RecoveryDropAsync(dropArgs);
    }

    // How a press this terminal sent to the running Daoris ended: asked again by its id and the
    // key it carries, kept under the home for a day. It sends nothing again.
    if (args is ["result", var resultId])
    {
        return await InstanceDoor.ResultAsync(resultId, Daoris.Driver.Host.InstanceHost.World("result"));
    }

    // A press this terminal sent that still waits for the person's Confirm, taken back:
    // the pipe's own WITHDRAW with the key it carries, which never touches a press the person already confirmed.
    if (args is ["withdraw", var withdrawId])
    {
        return await InstanceDoor.WithdrawAsync(withdrawId, Daoris.Driver.Host.InstanceHost.World("withdraw"));
    }

    // The sync on demand: the screen's *Sync now* is the other door to the same pass.
    if (args is ["sync", .. var syncArgs])
    {
        return await Daoris.Driver.Host.SyncConsole.RunAsync(syncArgs);
    }

    // A plugin package from a file: read, checked and installed with no network. Asked
    // for before the kit's verbs below, which answer every other `plugins` word.
    if (args is ["plugins", "install", .. var installArgs])
    {
        return PluginPackageCommand.Install(installArgs, Console.Out, DaorisHome.Resolve());
    }

    // An install's update from a terminal: the window's banner is the other door to the same request.
    // Needs no service: the request is a file under the home, and the staged build a folder beside its install.
    if (args is ["update", .. var updateArgs])
    {
        var exit = UpdateCommand.Run(
            updateArgs, DaorisHome.Resolve() ?? throw new DriverException(DaorisHome.Sentence), () => DateTimeOffset.UtcNow, log,
            Environment.GetEnvironmentVariable(DriverChoicesCommand.SessionVariable), out var updateSaid);
        (exit == 2 ? Console.Error : Console.Out).Write(updateSaid);
        return exit;
    }

    // How this Daoris is wired, from a terminal: Settings → This Daoris is the other door to the same reader.
    // Needs no service: a status door is asked only where an address is named or left in the home's address file.
    if (args is ["wiring", .. var wiringArgs])
    {
        var exit = WiringCommand.Run(wiringArgs, WiringWorld.OfThisTerminal(DaorisHome.Resolve()), out var wiringSaid);
        (exit == 2 ? Console.Error : Console.Out).Write(wiringSaid);
        return exit;
    }

    // Free up space from a terminal: the Disk space card's *Free up space…* is the other door to the same
    // list and run. Listed first and changing nothing; with --yes, the client of a running Daoris (confirmed in its window), or alone
    // under the home's run lock, refused from a session's terminal.
    if (args is ["space", "free", .. var freeArgs])
    {
        var freeHome = DaorisHome.Resolve() ?? throw new DriverException(DaorisHome.Sentence);
        // A Ctrl+C is a stop heard between units, never inside one's delete.
        Console.CancelKeyPress += (_, press) =>
        {
            press.Cancel = true;
            closing.Cancel();
        };
        return await SpaceFreeCommand.RunAsync(
            freeArgs, SpaceFreeWorld.OfThisTerminal(freeHome, log), Daoris.Driver.Host.InstanceHost.World(freeHome, "daoris"), closing.Token);
    }

    // The disk space from a terminal: Settings → Disk space is the other door to the same reader.
    // Reads only, and needs no service: each kind's size is this process's walk, `--measure`'s.
    if (args is ["space", .. var spaceArgs])
    {
        var spaceHome = DaorisHome.Resolve() ?? throw new DriverException(DaorisHome.Sentence);
        var exit = SpaceCommand.Run(spaceArgs, SpaceWorld.OfThisTerminal(spaceHome), out var spaceSaid);
        (exit == 2 ? Console.Error : Console.Out).Write(spaceSaid);
        return exit;
    }

    // The machine log from a terminal: Settings → Machine log is the other door to the same reading.
    if (args is ["logs", .. var logsArgs])
    {
        return Daoris.Driver.Host.LogsConsole.Run(logsArgs);
    }

    // Plugins from a terminal: the kit's new and try, and a plugin's page and activity,
    // the Plugins view's twins. None needs a service; try a home only to try an installed plugin by its id.
    if (args is ["plugins", .. var pluginsArgs])
    {
        var pluginsHome = DaorisHome.Resolve();
        // A session's plugin tools, served on stdio to the harness that started it: frames alone on stdout.
        if (pluginsArgs is ["tools", .. var toolWords])
        {
            using var stdin = new StreamReader(Console.OpenStandardInput(), new UTF8Encoding(false));
            await using var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
            return await PluginToolsServer.ServeAsync(pluginsHome, toolWords, log, stdin, stdout, Console.Error, CancellationToken.None);
        }

        // Approve and withdraw go through the running Daoris where one answers, confirmed in its window.
        return await PluginsCommand.RunAsync(
            pluginsArgs, Console.Out, pluginsHome, log, instance: Daoris.Driver.Host.InstanceHost.World(pluginsHome, "plugins"));
    }

    // The loop only by its verb: a bare invocation, or a word nobody answers, is the usage and starts nothing.
    return await Daoris.Driver.Host.DriveConsole.RunAsync(args, log, closing, entryPoint);
}
catch (OperationCanceledException cancelled)
{
    // Ctrl+C during --once or --until-idle is the close: the tick ended its sessions before it let go. Any other cancellation,
    // a look's own request meeting the client's timeout among them, is a failure that says so, exit 2, not the close's line and exit 0.
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
    // The home's lease changed under every attempt, or would not be written: a sentence, never a stack trace.
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
