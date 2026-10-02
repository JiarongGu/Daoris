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
//                 the other door; there is no other quest verb here, since a quest is answered by the
//                 session that takes it.
//
//   answer <session> ["…"]
//                 answer a driven session that parked to ask you (STANDDOWN2): its record stays parked with
//                 your words, and at the next tick the same session goes on with them (D131), its own
//                 conversation resumed; where it cannot be, a new session carries the quest on in the same
//                 tree, handed them, and says why. Nothing after the id is "carry on". The page's box on the
//                 parked session is the other door.
//
//   trees [list | remove <path> [--force] | clean [--yes] | land <session> [--plan]
//         | hand <session|branch> [...] | sync [--repository <name>] [--all] [--yes]]
//                 the session worktrees this machine has grown (D51): list them, or remove one —
//                 refusing while it holds uncommitted changes or unmerged commits, unless forced —
//                 or list every session branch and, with --yes, remove the empty and landed (D88),
//                 or accept a session's work as the review's Accept does, by the workspace's rule (D87),
//                 or bring each repository up to date after its pull request merged (WSR6, D109).
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

// The machine log (LOG1, D94): this host's watch and every exception nothing caught, in a file of its
// own beside the desktop's. With no home it writes nothing, and the sentence below still says why.
using var log = MachineLog.Open("driver");
log.WatchUnhandled();

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

    // Deleting a quest made by mistake (D95, D50): the quest drawer's Delete is the other door.
    if (args is ["quest", .. var questArgs])
    {
        if (questArgs is not ["delete", var questId])
        {
            Console.Error.WriteLine("usage: daoris-driver quest delete <id>");
            return 2;
        }

        using var client = ServiceClient.FromEnvironment();
        var (ok, message) = await client.DeleteQuestAsync(questId);
        Console.WriteLine($"daoris-driver: {message}");
        // A refusal — something stands on the quest — is an answer, not a tool error.
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
    using var closing = new CancellationTokenSource();
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

    if (once)
    {
        // One look, then what it started to its end (DEV3): a look no longer waits for its sessions, and a
        // single run that let go of them would leave them working with nothing watching.
        Print(await new Driver(service, config, AdapterSet.Built(), home, processes, sync, hooks: hooks, events: events).RunOnceAsync(closing.Token));
    }
    else if (untilIdle)
    {
        foreach (var report in await new Driver(service, config, AdapterSet.Built(), home, processes, sync, hooks: hooks, events: events).RunUntilIdleAsync(closing.Token))
        {
            Print(report);
        }
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
        // below, which is this door's exit-2 contract.
        await new DriverWatch(service, configPath, home, processes, sync, hooks: hooks, events: events).RunAsync(
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
            closing.Token);
        Console.WriteLine("driver: stopped — every session it ran was ended and recorded.");
    }

    log.Info("app.stopped", ("uptimeSeconds", (long)(DateTimeOffset.UtcNow - started).TotalSeconds));
    return 0;
}
catch (OperationCanceledException)
{
    // Ctrl+C during --once or --until-idle: the tick ended its sessions before it let go.
    Console.WriteLine("driver: stopped.");
    log.Info("app.stopped");
    return 0;
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
