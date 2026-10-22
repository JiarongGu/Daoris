using System.Diagnostics;
using System.Text.Json.Nodes;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// RETRYWORK1 through REAL ticks: a session commits its work and then fails because its quest's door is closed, so it never takes
/// the quest; the next look carries the open quest on in that same tree, its instruction naming what the session before committed
/// and said, and its record's note saying it carried that work on. A real process (node) and the stand-in service
/// <see cref="AskAndWaitTickTests.StandIn"/>, as the stop-hold ticks use them; no model and no account.
/// </summary>
/// <remarks>
/// Found watching the install: two sessions built their rows, committed them, and failed only because their connector could not
/// start (QUESTOPENLOCK1). Each quest was started again in a new tree, which built the same row from nothing.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class RetryWorkTickTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-retry-work-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    private readonly SessionProcesses _processes = new();

    public RetryWorkTickTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "retry@example.com");
        Git("config", "user.name", "RETRYWORK1");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_session_that_committed_and_failed_on_a_closed_quest_door_is_carried_on_in_its_tree()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.KeepsBaseCommit = true;
        var log = Path.Combine(_home, "agent.log");
        var config = Config(log);

        // 1. The session commits its work, then cannot reach its quest's door to take it, and fails. The quest is still open.
        await Driver(service, config).RunOnceAsync().WaitAsync(Poll.Patience);
        var failed = service.Session("s1");
        Assert.Equal("failed", failed["state"]!.GetValue<string>());
        Assert.Equal("Open", service.Status("q1"));
        var tree = failed["tree"]!.GetValue<string>();

        // 2. The next look carries it on in that tree, rather than growing a new one, and the session takes it and closes it.
        var carried = await Driver(service, config).RunOnceAsync().WaitAsync(Poll.Patience);

        var plan = Assert.Single(carried.Considerations, c => c.Quest.Id == "q1");
        Assert.Equal(failed["id"]!.GetValue<string>(), plan.Resumes?.Session);
        Assert.Equal(1, plan.CarriesWork);
        Assert.Contains("session `s1` failed before it took the quest, and its tree holds 1 commit beyond its start", plan.Reason);
        var second = service.Session("s2");
        Assert.Equal("completed", second["state"]!.GetValue<string>());
        Assert.Equal(Path.GetFullPath(tree), Path.GetFullPath(second["tree"]!.GetValue<string>()), ignoreCase: true);
        Assert.Equal("Done", service.Status("q1"));

        // Its instruction: take the quest, then finish what the session before committed, which the brief names with its words.
        var run = Runs(log)[1];
        var handed = run["target"]!.GetValue<string>();
        Assert.Contains("An earlier session on this quest committed work in this tree and failed before it took the quest", handed);
        Assert.Contains("First take the quest", handed);
        Assert.Contains("finish its work from there rather\nthan starting again", handed);
        Assert.Contains("Build the note field's first half", handed);
        Assert.Contains("the quest's door is closed", handed);
        // The work is there to finish: the predecessor's file in the tree it runs in.
        Assert.True(run["kept"]!.GetValue<bool>());

        // Its record's note, as it started, says it carried that work on.
        var started = run["record"]!["note"]!.GetValue<string>();
        Assert.Contains("carries `#q1` on with the work session `s1` committed before it failed", started);
    }

    private DriverConfig Config(string log) => DriverConfig.Empty with
    {
        Drivable = ["engine"],
        Trees = ["engine"],
        Adapter = "stub",
        TestBound = 1,
        PollSeconds = 1,
        Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Agent(), log] },
    };

    // A driver per look, sharing the processes, as the watch builds one per look (DEV3).
    private Daoris.Driver.Driver Driver(AskAndWaitTickTests.StandIn service, DriverConfig config)
    {
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: _processes,
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>
    /// The stand-in harness: it writes down what it was handed and its own record as it started. On its first run it commits a file
    /// in its tree, says its quest's door is closed and fails, never taking the quest; on a later run it takes the quest and closes it
    /// done, noting whether the first run's file is in the tree it runs in.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "agent.mjs");
        File.WriteAllText(script, """
            import { appendFileSync, existsSync, readFileSync, writeFileSync } from 'node:fs';
            import { execFileSync } from 'node:child_process';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const [log] = process.argv.slice(2);
            const runs = existsSync(log) ? readFileSync(log, 'utf8').split('\n').filter(Boolean).length : 0;
            const target = process.env.DAORIS_TARGET ?? '';
            const url = process.env.DAORIS_SERVICE_URL;
            const records = await (await fetch(`${url}/api/sessions?includeClosed=true`)).json();
            const record = records[records.length - 1];
            appendFileSync(log, JSON.stringify({ cwd: process.cwd(), target, record, kept: existsSync('note.txt') }) + '\n');
            const respond = (body) => fetch(`${url}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            if (runs === 0) {
              writeFileSync('note.txt', 'the first half\n');
              execFileSync('git', ['add', '-A']);
              execFileSync('git', ['commit', '-qm', "Build the note field's first half"]);
              console.error("the quest's door is closed: the connector could not start, so the quest was never taken.");
              process.exit(1);
            }
            await respond({ action: 'take' });
            await respond({ action: 'done', reason: 'landed' });
            """);
        return script;
    }

    private static List<JsonNode> Runs(string log) => [.. File.ReadAllLines(log).Select(line => JsonNode.Parse(line)!)];

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
}
