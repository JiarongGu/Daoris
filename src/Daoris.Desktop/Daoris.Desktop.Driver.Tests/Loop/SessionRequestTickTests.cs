using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSUX1g (D126 §7.1) through a REAL tick: a terminal's <c>sessions stop</c> reaches a session the running loop runs. The
/// verb writes its request, the loop's watch takes it and stops the process as the person's stop, the record says so
/// within the verb's wait, and the next look holds the quest (SESSUX1b). A real process (node) and the stand-in service
/// <see cref="AskAndWaitTickTests.StandIn"/>, as the stop-hold ticks use them; no model and no account.
/// </summary>
/// <remarks>
/// The loop's registry keeps its markers under the home, as a driver's does, so the verb, which holds no process, sees
/// that something on this machine runs the session, and asks rather than ending it as an orphan.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class SessionRequestTickTests : IDisposable
{
    private readonly string _home = Path.Combine(Path.GetTempPath(), "daoris-request-tick-" + Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    private readonly SessionProcesses _processes;

    public SessionRequestTickTests()
    {
        Directory.CreateDirectory(_home);
        _processes = new SessionProcesses(Path.Combine(_home, "sessions"));
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "requests@example.com");
        Git("config", "user.name", "SESSUX1g");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_terminals_stop_is_taken_by_the_loop_that_runs_the_session_and_its_quest_is_held()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Trees = ["engine"],
            Adapter = "stub",
            TimeoutMinutes = 1,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Agent()] },
        };
        using var loopClient = new ServiceClient(service.Url, null);
        await using var watch = new SessionRequestWatch(_home, _processes, () => loopClient, every: TimeSpan.FromMilliseconds(100));

        // 1. The loop's session takes its quest and works on.
        var running = Driver(service, config).RunOnceAsync();
        await UntilAsync(() => service.SessionCount == 1 && service.Status("q1") == "Taken" && _processes.Running.Contains("s1"));

        // 2. The terminal asks; the loop takes the request; the record is the person's stop within the verb's wait.
        using var verbClient = new ServiceClient(service.Url, null);
        var output = new StringWriter();
        var ask = SessionsCommand.Read(["stop", "s1"], out _)!;
        var exit = await SessionsCommand.RunAsync(ask, new SessionsWorld(verbClient, _home, config, SessionWire.Pipe, Log: null), output);
        await running.WaitAsync(TimeSpan.FromSeconds(60));

        Assert.True(exit == 0, output.ToString());
        Assert.Contains("stopped s1 — the loop that runs it took the request", output.ToString());
        var stopped = service.Session("s1");
        Assert.Equal("stopped", stopped["state"]!.GetValue<string>());
        Assert.Equal("the person stopped it.", stopped["note"]!.GetValue<string>());
        Assert.False(File.Exists(Path.Combine(_home, "sessions", "requests", "s1.json")));

        // 3. The next look holds the quest, as a person's stop holds it, and starts nothing.
        var held = await Driver(service, config).TickAsync().WaitAsync(TimeSpan.FromSeconds(30));
        Assert.Equal(StartVerdict.Stopped, Assert.Single(held.Considerations, c => c.Quest.Id == "q1").Verdict);
        Assert.Equal(1, service.SessionCount);
    }

    private Daoris.Driver.Driver Driver(AskAndWaitTickTests.StandIn service, DriverConfig config)
    {
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: _processes,
            harnesses: new HarnessRoster(adapters, Path.Combine(_home, "harnesses.json")));
    }

    /// <summary>The stand-in harness: it takes its quest and works until it is stopped.</summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "agent.mjs");
        File.WriteAllText(script, """
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            await fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ action: 'take' }) });
            setInterval(() => {}, 1 << 30);
            """);
        return script;
    }

    private static async Task UntilAsync(Func<bool> condition)
    {
        var patience = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < patience, "the condition never held");
            await Task.Delay(50);
        }
    }

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
}
