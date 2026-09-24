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
//   --once        one tick, then exit
//   --until-idle  tick until nothing starts, then exit — the deterministic mode a gate drives
//   (default)     watch: tick forever, pollSeconds apart
//
//   chat --repository <name> [--adapter <name>] [--own-tree]
//                 hold a conversation in a repository (D49 §3): stdin is the person, stdout is the
//                 session, end of input ends it. One session per working tree (D51) — and --own-tree
//                 opens the conversation in a worktree of its own, beside your work in the checkout.
//
//   ask [--workspace <name>] [--to <repo>] [--file <path>]… [--url <address>]… "…"
//                 ask at a WORKSPACE (D65 §1a): the service answers with the tier that answered —
//                 by declarations only (proposes, publishes nothing), or the receiver --to names.
//   ask --publish <id> --to <repo> · ask --close <id> --reason "…"
//                 turn an ask into a quest, or close it with the reason. Where driver.json names an
//                 `intakeAdapter` (`daoris driver intake <adapter>`), the loop answers an ask the
//                 declarations left open with an INTAKE session (D65 §1b) in <home>/intake/<workspace>/:
//                 it publishes onto the ask, or parks asking you — and ends when you answer the ask.
//
//   trees [list | remove <path> [--force]]
//                 the session worktrees this machine has grown (D51): list them, or remove one —
//                 refusing while it holds uncommitted changes or unmerged commits, unless forced.
//
//   sync [status | dismiss <quest>] [--workspace <name>]
//                 one pass now, the tick's own, for every circle with a remote or the one named; with
//                 `status`, where each circle stands — ahead, behind, in conflict, last synced (SYNC6a);
//                 with `dismiss`, the conflicts a quest carries go, here and at the next pass (SYNC6c).
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

// The tree lifecycle from a terminal (D51, D50): the verbs live on the binary that already owns git —
// the CLI's `daoris driver trees <repo> on|off` is the standing opt-in, a file edit; these are disk.
if (args is ["trees", .. var treesArgs])
{
    return await Daoris.Driver.Host.TreesConsole.RunAsync(treesArgs);
}

// The sync on demand (SYNC6a, D50): the screen's *Sync now* is the other door to the same pass.
if (args is ["sync", .. var syncArgs])
{
    return await Daoris.Driver.Host.SyncConsole.RunAsync(syncArgs);
}

var once = args.Contains("--once");
var untilIdle = args.Contains("--until-idle");

try
{
    var configPath = DriverConfig.ResolvePath();
    var config = DriverConfig.Load(configPath);
    var home = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
    // Marked under the home, so the desktop sharing it can tell this host's sessions from orphans.
    var processes = new SessionProcesses(Path.Combine(home, "sessions"));

    using var service = ServiceClient.FromEnvironment();

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
    await using var hooks = new HookSet(home, say: (id, line) => Console.Error.WriteLine($"plugin:{id}  {line}"));

    if (once)
    {
        Print(await new Driver(service, config, AdapterSet.Built(), home, processes, sync, hooks: hooks).TickAsync());
    }
    else if (untilIdle)
    {
        foreach (var report in await new Driver(service, config, AdapterSet.Built(), home, processes, sync, hooks: hooks).RunUntilIdleAsync())
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

        // The loop itself — re-read the config, tick, wait — is the library's (DriverWatch); this host
        // keeps only its reporting half. A null onError lets a failed tick propagate to the catch
        // below, which is this door's exit-2 contract.
        await new DriverWatch(service, configPath, home, processes, sync, hooks: hooks).RunAsync(
            (report, ticked) =>
            {
                Print(report, quietWhenIdle: true);
                // Observed either way, so turning notifications back on does not then announce
                // everything that happened while they were off — the switch is about being TOLD.
                var events = attention.Observe(report);
                if (ticked.Notify) foreach (var item in events) Console.WriteLine($"  !  {item.Line}");
                return Task.CompletedTask;
            },
            onError: null,
            CancellationToken.None);
    }

    return 0;
}
catch (DriverException error)
{
    Console.Error.WriteLine($"driver: {error.Message}");
    return 2;
}
catch (HttpRequestException error)
{
    Console.Error.WriteLine($"driver: could not reach the service — {error.Message}");
    return 2;
}

static void Print(TickReport report, bool quietWhenIdle = false)
{
    if (quietWhenIdle && !report.PlannedAnything && report.Events.Count == 0) return;

    foreach (var consideration in report.Considerations)
    {
        var verdict = consideration.Verdict == StartVerdict.Start ? "start" : "sitting";
        Console.WriteLine($"  {verdict,-8} #{consideration.Quest.Id} → {consideration.Quest.To} — {consideration.Reason}");
    }

    foreach (var line in report.Events)
    {
        Console.WriteLine($"  {line}");
    }
}
