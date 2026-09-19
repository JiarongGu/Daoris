using System.Text;
using Daoris.Driver;

// The headless door to the driver (D46 §7): what the family rehearsal drives, and what the desktop
// shell embeds. It watches a service, starts sessions where the person opted in, and prints every
// verdict — a quest that is sitting must always say why.
//
//   DAORIS_SERVICE_URL     where the service is                (required — the driver is its client)
//   DAORIS_SERVICE_KEY     sent as a bearer token when set     (absent: local trust, D21)
//   DAORIS_DRIVER_CONFIG   the person's standing choices       (default: ~/.daoris/driver.json)
//
//   --once        one tick, then exit
//   --until-idle  tick until nothing starts, then exit — the deterministic mode a gate drives
//   (default)     watch: tick forever, pollSeconds apart
//
// Exit codes keep the family contract: 0 clean · 2 tool error.
if (OperatingSystem.IsWindows())
{
    Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
}

var once = args.Contains("--once");
var untilIdle = args.Contains("--until-idle");

try
{
    var configPath = Environment.GetEnvironmentVariable(DriverConfig.PathVariable) ?? DriverConfig.DefaultPath;
    var config = DriverConfig.Load(configPath);
    var home = Path.GetDirectoryName(Path.GetFullPath(configPath))!;

    using var service = ServiceClient.FromEnvironment();

    if (config.Drivable.Count == 0)
    {
        Console.WriteLine($"driver: nothing is opted in — name repositories under \"drivable\" in {configPath}");
    }

    if (once)
    {
        Print(await new Driver(service, config, AdapterSet.Built(), home).TickAsync());
    }
    else if (untilIdle)
    {
        foreach (var report in await new Driver(service, config, AdapterSet.Built(), home).RunUntilIdleAsync())
        {
            Print(report);
        }
    }
    else
    {
        Console.WriteLine($"driver: watching {service.BaseUrl} every {config.PollSeconds}s — Ctrl+C stops it");
        while (true)
        {
            // The person's standing choices are re-read every tick, so a hold, an opt-in, or a new
            // adapter command takes effect without a restart — the shell's controls are edits to this
            // file, and a control that needs a bounce is a control nobody trusts.
            config = DriverConfig.Load(configPath);
            Print(await new Driver(service, config, AdapterSet.Built(), home).TickAsync(), quietWhenIdle: true);
            await Task.Delay(TimeSpan.FromSeconds(config.PollSeconds));
        }
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
