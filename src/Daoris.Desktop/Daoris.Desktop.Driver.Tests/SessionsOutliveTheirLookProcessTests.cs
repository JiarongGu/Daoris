using System.Diagnostics;
using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// DEV3 (D115 §3.1) over a REAL stub harness: node, holding its turn, spawned by the executor into real git
/// checkouts, against the in-process ledger. <see cref="SessionsOutliveTheirLookTests"/> holds the scheduling
/// in the fast half; this holds it where the spawn, the process registry and the shutdown's kill are real.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class SessionsOutliveTheirLookProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-outlive-process-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly StandInLedger _ledger = new();
    private readonly string _engine;
    private readonly string _tools;

    public SessionsOutliveTheirLookProcessTests()
    {
        Directory.CreateDirectory(_home);
        _engine = Repository("engine");
        _tools = Repository("tools");
        _ledger.Register("engine", _engine).Register("tools", _tools);
    }

    public void Dispose()
    {
        _ledger.Dispose();
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// 🔴 The row's own case, over a real harness: a quest published while a long session works in one
    /// repository starts at the next look, and both sessions' processes run at once.
    /// </summary>
    [Fact]
    public async Task A_quest_published_during_a_long_stub_session_starts_at_the_next_look()
    {
        var processes = new SessionProcesses();
        var driver = Driver(processes);
        _ledger.Publish("q1", "engine");

        await driver.TickAsync().WaitAsync(TimeSpan.FromSeconds(60));
        await Poll.Until(() => processes.Running.Contains("s1"), () => "the first session never started", TimeSpan.FromSeconds(60));

        _ledger.Publish("q2", "tools");
        var next = await driver.TickAsync().WaitAsync(TimeSpan.FromSeconds(60));
        await Poll.Until(() => processes.Running.Contains("s2"), () => "the second session never started", TimeSpan.FromSeconds(60));

        Assert.Equal(StartVerdict.Start, next.Considerations.Single(c => c.Quest.Id == "q2").Verdict);
        Assert.Contains("s1", processes.Running);
        Assert.Equal("working", _ledger.Session("s1")["state"]!.GetValue<string>());

        Assert.True(processes.Stop("s1"));
        Assert.True(processes.Stop("s2"));
        await driver.Running.SettledAsync().WaitAsync(TimeSpan.FromSeconds(90));
        Assert.All(_ledger.Sessions, record => Assert.Equal("stopped", record["state"]!.GetValue<string>()));
    }

    /// <summary>
    /// 🔴 D104 with DEV3: closing the watch under two sessions started at two looks ends both, and it lets go
    /// only once each record says it was stopped by the driver's shutdown, interrupted, to be carried on.
    /// </summary>
    [Fact]
    public async Task A_shutdown_still_marks_every_running_session_interrupted()
    {
        var processes = new SessionProcesses();
        var config = Path.Combine(_home, "driver.json");
        File.WriteAllText(config, JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["drivable"] = new[] { "engine", "tools" },
            ["adapter"] = "stub",
            ["cap"] = 3,
            ["pollSeconds"] = 1,
            ["timeoutMinutes"] = 2,
            ["commands"] = new Dictionary<string, string[]> { ["stub"] = ["node", Holding()] },
        }));
        using var service = _ledger.Client();
        var watch = new DriverWatch(
            service, config, _home, processes, sync: null,
            harnesses: new HarnessRoster(AdapterSet.Built(), Path.Combine(_home, "harnesses.json")));
        using var closing = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        _ledger.Publish("q1", "engine");
        var published = 0;

        var watching = watch.RunAsync(
            (_, _) =>
            {
                if (Interlocked.Exchange(ref published, 1) == 0) _ledger.Publish("q2", "tools");
                return Task.CompletedTask;
            },
            onError: null, closing.Token);
        await Poll.Until(
            () => processes.Running.Contains("s1") && processes.Running.Contains("s2"),
            () => $"running: {string.Join(", ", processes.Running)}", TimeSpan.FromSeconds(60));

        await closing.CancelAsync();
        await watching.WaitAsync(TimeSpan.FromSeconds(90));

        Assert.Empty(processes.Running);
        Assert.Equal(2, _ledger.Sessions.Count);
        Assert.All(_ledger.Sessions, record =>
        {
            Assert.Equal("stopped", record["state"]!.GetValue<string>());
            Assert.True(record["interrupted"]?.GetValue<bool>(), record.ToJsonString());
            Assert.Contains("the driver was stopped", record["note"]!.GetValue<string>());
        });
    }

    private Daoris.Driver.Driver Driver(SessionProcesses processes)
    {
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine", "tools"],
            Adapter = "stub",
            Cap = 3,
            TimeoutMinutes = 2,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Holding()] },
        };
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            _ledger.Client(), config, adapters, _home, processes: processes,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
    }

    /// <summary>The pipe door's stand-in: it answers the toolchain's two questions, then holds its turn.</summary>
    private string Holding()
    {
        var script = Path.Combine(_home, "holding-agent.mjs");
        File.WriteAllText(script, """
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            console.log('stub: driven for quest ' + process.env.DAORIS_QUEST_ID);
            await new Promise((resolve) => setTimeout(resolve, 60000));
            """);
        return script;
    }

    private string Repository(string name)
    {
        var root = Path.Combine(_home, name);
        Directory.CreateDirectory(root);
        Git(root, "init", "-q", "-b", "main");
        Git(root, "config", "user.email", "dev3@example.com");
        Git(root, "config", "user.name", "DEV3");
        File.WriteAllText(Path.Combine(root, "README.md"), $"# {name}\n");
        Git(root, "add", "-A");
        Git(root, "commit", "-qm", "the starting point");
        return root;
    }

    private static void Git(string cwd, params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = cwd, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
}
