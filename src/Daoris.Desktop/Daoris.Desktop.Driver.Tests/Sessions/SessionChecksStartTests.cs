using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// FABRIC-PIPELINE1b (D192 point 5) through a REAL tick: a driven start in a repository whose sessions run light. Its session is told its
/// light gates and its heavy gates as the verification's, and, on an agent that takes no rules from Daoris, that they are said rather than
/// refused; while it runs, its tree's git lock names it and its checks are kept by its id where <c>run-heavy</c> reads them (the pair
/// <see cref="HeavyRunCommandTests"/> refuses); once its run ends, the kept checks are gone. FABRIC-PIPELINE1b3: a run bound to a named
/// workflow's version takes that version's checks at its first start, a steps version's <c>work</c> and a graph version's <c>claimed</c>
/// alike, where its repository sets no rule. A real process (node), a real git repository and a stand-in service on a loopback port, the
/// shape <see cref="StartInSyncTickTests"/> uses; no model and no account.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class SessionChecksStartTests : IDisposable
{
    private readonly string _folder = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "session-checks-start", Guid.NewGuid().ToString("N")[..8]);

    public SessionChecksStartTests()
    {
        // The install first, so what a test saves under its home is where the driver's start reads it.
        Directory.CreateDirectory(Home);
        File.WriteAllText(Path.Combine(Install, DaorisHome.InstallMarker), "# Daoris — installed desktop\n");
    }

    public void Dispose() => GitSeed.Discard(_folder);

    private string Log => Path.Combine(_folder, "agent.log");

    private string Install => Path.Combine(_folder, "install");

    private string Home => Path.Combine(Install, DaorisHome.InstallData);

    [Fact]
    public async Task A_light_start_is_told_its_checks_and_holds_its_tree_and_its_checks_while_it_runs()
    {
        var run = await StartAsync(DriverConfig.Empty.WithSessionChecks("engine", SessionChecks.Light));

        var session = run["session"]!.GetValue<string>();
        Assert.Contains(HeavyTold.LightWords(["universal"], ["driver"], takesRules: false), run["target"]!.GetValue<string>());
        Assert.Equal(SessionLocks.Reason(session), run["locked"]?.GetValue<string>().Trim());
        Assert.Equal("""{"checks":"light"}""", JsonNode.Parse(run["checks"]!.GetValue<string>())!.ToJsonString());
        Assert.Null(SessionChecks.Held(Home, session));
    }

    /// <summary>
    /// FABRIC-PIPELINE1b3 (D192's FABRIC-PIPELINE1b2 note, point 5): a run's first start binds it before its take (FABRIC-CUTOVER1g), so a
    /// run bound to a named workflow whose <c>work</c> sets <c>light</c> starts its first session light in a repository that sets no rule.
    /// </summary>
    [Fact]
    public async Task A_run_bound_to_a_light_named_workflow_starts_its_first_session_light_where_its_repository_sets_none()
    {
        WorkflowStore.Add(Home, new WorkflowVersionAdded(
            "light-work", "Light work", "terminal", "2026-10-22T09:00:00Z",
            JsonNode.Parse("""[{"id":"work","kind":"work","checks":"light"},{"id":"landing","kind":"landing","form":"merge","accept":"you"}]""")), kept: []);

        var run = await StartAsync(DriverConfig.Parse("""{"workflows":{"engine":{"default":"light-work"}}}"""));

        var bound = WorkflowRunBindings.Read(Home, "q1")!;
        Assert.Equal(("light-work", (int?)1, (string?)null), (bound.Workflow, bound.Version, bound.Problem));
        Assert.Contains(HeavyTold.LightWords(["universal"], ["driver"], takesRules: false), run["target"]!.GetValue<string>());
        Assert.Equal("""{"checks":"light"}""", JsonNode.Parse(run["checks"]!.GetValue<string>())!.ToJsonString());
    }

    /// <summary>
    /// FABRIC-PIPELINE1b3: a run bound to a graph version takes its <c>claimed</c> node's <c>checks</c> at its first start, and keeps its
    /// <c>callMinutes</c> beside them for the long-call watch, where its repository sets no rule.
    /// </summary>
    [Fact]
    public async Task A_run_bound_to_a_graph_version_whose_claimed_is_light_starts_its_first_session_light()
    {
        RunHome.SaveGraph(Home, """
            {"schemaVersion":2,"entries":["claimed"],"end":"handed",
             "nodes":[{"id":"claimed","kind":"claimed","version":1,"reach":"observed","options":{"checks":"light","callMinutes":30}},
                      {"id":"handed","kind":"handed","version":1,"reach":"observed"}],
             "wires":[{"id":"c>h","kind":"after","from":"claimed","to":"handed"}]}
            """, workflow: "light-graph");

        var run = await StartAsync(DriverConfig.Parse("""{"workflows":{"engine":{"default":"light-graph"}}}"""));

        var bound = WorkflowRunBindings.Read(Home, "q1")!;
        Assert.Equal(("light-graph", (int?)1, (string?)null), (bound.Workflow, bound.Version, bound.Problem));
        Assert.StartsWith("gph-", bound.Digest, StringComparison.Ordinal);
        Assert.Contains(HeavyTold.LightWords(["universal"], ["driver"], takesRules: false), run["target"]!.GetValue<string>());
        Assert.Equal("""{"checks":"light","callMinutes":30}""", JsonNode.Parse(run["checks"]!.GetValue<string>())!.ToJsonString());
    }

    /// <summary>
    /// One look's start of q1 in a repository that marks one gate heavy, under <paramref name="choices"/>: what the stand-in harness wrote
    /// down of its run.
    /// </summary>
    private async Task<JsonObject> StartAsync(DriverConfig choices)
    {
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
        var config = choices with
        {
            Drivable = ["engine"], Trees = ["engine"], Adapter = "stub", TimeoutMinutes = 1, PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Agent(), Log, DaorisHome.Entry(Home, HomeEntry.Spawn)] },
        };
        var adapters = AdapterSet.Built();
        var driver = new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, Home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(Home, HomeEntry.Harnesses)));

        await driver.RunOnceAsync().WaitAsync(Poll.Patience);

        return JsonNode.Parse(File.ReadAllLines(Log).First())!.AsObject();
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
        using var git = Process.Start(TestGit.Past(info))!;
        git.WaitForExit();
        Assert.Equal(0, git.ExitCode);
    }
}
