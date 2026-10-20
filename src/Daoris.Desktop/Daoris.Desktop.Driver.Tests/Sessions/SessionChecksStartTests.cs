using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PIPELINE1b (D192 point 5) through a REAL tick: a driven start in a repository whose sessions run light. Its session is told its
/// light gates and its heavy gates as the verification's, and, on an agent that takes no rules from Daoris, that they are said rather than
/// refused; while it runs, its tree's git lock names it and its checks are kept by its id where <c>run-heavy</c> reads them (the pair
/// <see cref="HeavyRunCommandTests"/> refuses); once its run ends, the kept checks are gone. A real process (node), a real git repository
/// and a stand-in service on a loopback port, the shape <see cref="StartInSyncTickTests"/> uses; no model and no account.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class SessionChecksStartTests : IDisposable
{
    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "session-checks-start", Guid.NewGuid().ToString("N")[..8]);

    public SessionChecksStartTests() => Directory.CreateDirectory(_folder);

    public void Dispose() => GitSeed.Discard(_folder);

    private string Log => Path.Combine(_folder, "agent.log");

    [Fact]
    public async Task A_light_start_is_told_its_checks_and_holds_its_tree_and_its_checks_while_it_runs()
    {
        var install = Path.Combine(_folder, "install");
        var home = Path.Combine(install, DaorisHome.InstallData);
        Directory.CreateDirectory(home);
        File.WriteAllText(Path.Combine(install, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
        var checkout = Path.Combine(_folder, "engine");
        Directory.CreateDirectory(checkout);
        Git(checkout, "init", "-q", "-b", "main");
        Git(checkout, "config", "user.email", "checks@example.com");
        Git(checkout, "config", "user.name", "FABRIC-PIPELINE1b");
        File.WriteAllText(
            Path.Combine(checkout, HeavyGates.FileName),
            """{"gates":[{"name":"universal","run":"npm run verify"},{"name":"driver","run":"dotnet test src/Driver","heavy":true}]}""");
        Git(checkout, "add", "-A");
        Git(checkout, "commit", "-qm", "the starting point");

        await using var service = AskAndWaitTickTests.StandIn.Start(checkout);
        var config = DriverConfig.Empty.WithSessionChecks("engine", SessionChecks.Light) with
        {
            Drivable = ["engine"], Trees = ["engine"], Adapter = "stub", TimeoutMinutes = 1, PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Agent(), Log, DaorisHome.Entry(home, HomeEntry.Spawn)] },
        };
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(home, HomeEntry.Harnesses)));

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        var run = JsonNode.Parse(File.ReadAllLines(Log).First())!.AsObject();
        var session = run["session"]!.GetValue<string>();
        Assert.Contains(HeavyTold.LightWords(["universal"], ["driver"], takesRules: false), run["target"]!.GetValue<string>());
        Assert.Equal(SessionLocks.Reason(session), run["locked"]?.GetValue<string>().Trim());
        Assert.Equal("""{"checks":"light"}""", JsonNode.Parse(run["checks"]!.GetValue<string>())!.ToJsonString());
        Assert.Null(SessionChecks.Held(home, session));
    }

    /// <summary>
    /// The pipe door's stand-in harness: it writes down what it was handed, its tree's git lock and the checks kept by its id, then takes
    /// the quest and closes it.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_folder, "agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync, existsSync, readFileSync } from 'node:fs';
            import { join, resolve } from 'node:path';
            import { execFileSync } from 'node:child_process';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const [log, spawn] = process.argv.slice(2);
            const session = process.env.DAORIS_SESSION_ID ?? '';
            const git = resolve(execFileSync('git', ['rev-parse', '--git-dir'], { encoding: 'utf8' }).trim());
            const read = (path) => (existsSync(path) ? readFileSync(path, 'utf8') : null);
            appendFileSync(log, JSON.stringify({
              session, target: process.env.DAORIS_TARGET ?? '', locked: read(join(git, 'locked')), checks: read(join(spawn, `${session}.checks.json`)),
            }) + '\n');
            const respond = (body) => fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            await respond({ action: 'take' });
            await respond({ action: 'done', reason: 'nothing to land' });
            """);
        return script;
    }

    private static void Git(string folder, params string[] args)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = folder, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var git = Process.Start(info)!;
        git.WaitForExit();
        Assert.Equal(0, git.ExitCode);
    }
}
