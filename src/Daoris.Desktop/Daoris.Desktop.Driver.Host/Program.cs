using System.Text;
using Daoris.Driver;

// The headless door to the driver (D46 §7): what the family rehearsal drives, and what the desktop
// shell embeds. It watches a service, starts sessions where the person opted in, and prints every
// verdict — a quest that is sitting must always say why.
//
//   DAORIS_SERVICE_URL     where the service is                (required — the driver is its client)
//   DAORIS_SERVICE_KEY     sent as a bearer token when set     (absent: local trust, D21)
//   DAORIS_DRIVER_CONFIG   the person's standing choices       (default: ~/.daoris/driver.json)
//   DAORIS_REMOTE_URL      one workspace's remote, with its key (or ~/.daoris/remotes.json — D48 §5;
//   DAORIS_REMOTE_KEY        either env var present means the environment is the answer, whole,
//   DAORIS_REMOTE_WORKSPACE  for the workspace named here — absent: `default`)
//   DAORIS_REMOTE_CONFIG   where the map is                    (default: ~/.daoris/remotes.json)
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
//   trees [list | remove <path> [--force]]
//                 the session worktrees this machine has grown (D51): list them, or remove one —
//                 refusing while it holds uncommitted changes or unmerged commits, unless forced.
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

// The tree lifecycle from a terminal (D51, D50): the verbs live on the binary that already owns git —
// the CLI's `daoris driver trees <repo> on|off` is the standing opt-in, a file edit; these are disk.
if (args is ["trees", .. var treesArgs])
{
    return await Daoris.Driver.Host.TreesConsole.RunAsync(treesArgs);
}

var once = args.Contains("--once");
var untilIdle = args.Contains("--until-idle");

try
{
    var configPath = DriverConfig.ResolvePath();
    var config = DriverConfig.Load(configPath);
    var home = Path.GetDirectoryName(Path.GetFullPath(configPath))!;
    var processes = new SessionProcesses();

    using var service = ServiceClient.FromEnvironment();

    // The machine's remotes, one per workspace that has one (~/.daoris/remotes.json, environment
    // overriding — D47 §9, D48 §5): the syncs ride the tick, so a headless driver on a server machine
    // feeds and mirrors exactly as the desktop does. Absence is silent and local.
    using var sync = RemoteSyncSet.FromEnvironment(
        service.BaseUrl, Environment.GetEnvironmentVariable(ServiceClient.KeyVariable));
    if (sync is not null)
    {
        Console.WriteLine(
            $"driver: syncing each tick with the remotes for {string.Join(", ", sync.Workspaces)}");
    }

    if (config.Drivable.Count == 0)
    {
        Console.WriteLine($"driver: nothing is opted in — name repositories under \"drivable\" in {configPath}");
    }

    if (once)
    {
        Print(await new Driver(service, config, AdapterSet.Built(), home, processes, sync).TickAsync());
    }
    else if (untilIdle)
    {
        foreach (var report in await new Driver(service, config, AdapterSet.Built(), home, processes, sync).RunUntilIdleAsync())
        {
            Print(report);
        }
    }
    else
    {
        Console.WriteLine($"driver: watching {service.BaseUrl} every {config.PollSeconds}s — Ctrl+C stops it");
        // The loop itself — re-read the config, tick, wait — is the library's (DriverWatch); this host
        // keeps only its reporting half. A null onError lets a failed tick propagate to the catch
        // below, which is this door's exit-2 contract.
        await new DriverWatch(service, configPath, home, processes, sync).RunAsync(
            (report, _) => { Print(report, quietWhenIdle: true); return Task.CompletedTask; },
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
