using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSWATCH1a's headless end (D200, the session-watch design §2, case 12), through a REAL look on the pipe door: a stand-in takes its
/// quest and then hangs, saying nothing and running nothing. No time limit ends it any more; under <c>--quiet-end</c> the watcher ends
/// it through the run's own wait once it has been unheard that long, the record concludes <c>failed</c> with <c>ended.quiet</c> (a
/// strike, as a timeout was), and <c>--once</c> returns. Without it the run would wait for its operator.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class QuietEndProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "quiet-end", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public QuietEndProcessTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "quiet-end@example.com");
        Git("config", "user.name", "SESSWATCH1a");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task Once_with_quiet_end_ends_a_session_that_hangs_silently_and_returns()
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.Took = true;
        var driver = Driver(service);
        driver.Watch.EndAfter = TimeSpan.FromSeconds(4);
        var clock = Stopwatch.StartNew();

        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(90));
        clock.Stop();

        var record = service.Session("s1");
        Assert.Equal("failed", record["state"]!.GetValue<string>());
        var note = record["note"]!.GetValue<string>();
        Assert.Contains("nothing came from its agent", note, StringComparison.Ordinal);
        Assert.DoesNotContain("timed out", note, StringComparison.Ordinal);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(60), $"the run took {clock.Elapsed}");
    }

    private Daoris.Driver.Driver Driver(AskAndWaitTickTests.StandIn service)
    {
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Adapter = "stub",
            TestBound = 3,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Agent()] },
        };
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>The pipe door's stand-in: it takes its quest, says one line, then hangs on a timer, saying nothing more.</summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "agent.mjs");
        File.WriteAllText(script, """
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            await fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify({ action: 'take' }) });
            console.log('taken; now waiting on something that never answers');
            setInterval(() => {}, 1000);
            """);
        return script;
    }

    private void Git(params string[] arguments)
    {
        var info = new ProcessStartInfo("git") { WorkingDirectory = _repository, UseShellExecute = false, RedirectStandardOutput = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(TestGit.Past(info))!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
    }
}
