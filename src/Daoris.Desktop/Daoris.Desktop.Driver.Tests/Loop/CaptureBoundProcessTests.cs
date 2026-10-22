using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// SESSWATCH1a's state E (D200, the session-watch design §2, case 9), through a REAL look on the pipe door: the agent's process exits
/// and leaves a grandchild holding its output open, ORPHAN1's own case. With no time limit left on a run, nothing else would let the
/// capture go. Within the capture's bound the job is ended, the capture let go, the strays' end said as a note line, and the run
/// concluded by its exit: a clean exit with its quest done is <c>completed</c>, a crash <c>failed</c>; never a cut-off.
/// </summary>
[Trait(Category.Name, Category.Process)]
public sealed class CaptureBoundProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "capture-bound", Guid.NewGuid().ToString("N")[..8]);

    private readonly string _repository;

    public CaptureBoundProcessTests()
    {
        Directory.CreateDirectory(_home);
        _repository = Path.Combine(_home, "engine");
        Directory.CreateDirectory(_repository);
        Git("init", "-q", "-b", "main");
        Git("config", "user.email", "capture-bound@example.com");
        Git("config", "user.name", "SESSWATCH1a");
        File.WriteAllText(Path.Combine(_repository, "README.md"), "# engine\n");
        Git("add", "-A");
        Git("commit", "-qm", "the starting point");
    }

    public void Dispose()
    {
        GitSeed.Discard(_home);
    }

    [Theory]
    [InlineData("done", "completed")]
    [InlineData("crash", "failed")]
    public async Task A_run_whose_root_exited_while_a_stray_holds_its_output_concludes_by_its_exit(string mode, string state)
    {
        await using var service = AskAndWaitTickTests.StandIn.Start(_repository);
        service.Took = true;
        var pidFile = Path.Combine(_home, "stray.pid");
        var driver = Driver(service, pidFile, mode);
        driver.CaptureBound = TimeSpan.FromSeconds(3);
        var clock = Stopwatch.StartNew();

        await driver.RunOnceAsync().WaitAsync(TimeSpan.FromSeconds(90));
        clock.Stop();

        var record = service.Session("s1");
        var note = record["note"]!.GetValue<string>();
        Assert.Equal(state, record["state"]!.GetValue<string>());
        Assert.DoesNotContain("timed out", note, StringComparison.Ordinal);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(60), $"the run took {clock.Elapsed}");

        // The strays' end is a note line of the conversation, never its conclusion.
        var events = File.ReadAllText(DaorisHome.Entry(_home, HomeEntry.Sessions, "s1.events.jsonl"));
        Assert.Contains("\"ended.gone\"", events, StringComparison.Ordinal);
        Assert.DoesNotContain("still held its output", note, StringComparison.Ordinal);

        // What held its output was ended with the run's job.
        var stray = int.Parse(File.ReadAllText(pidFile).Trim(), System.Globalization.CultureInfo.InvariantCulture);
        await Poll.Until(() => !Alive(stray), () => $"the stray {stray} still runs", TimeSpan.FromSeconds(20));
    }

    private static bool Alive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    private Daoris.Driver.Driver Driver(AskAndWaitTickTests.StandIn service, string pidFile, string mode)
    {
        var config = DriverConfig.Empty with
        {
            Drivable = ["engine"],
            Adapter = "stub",
            TestBound = 3,
            PollSeconds = 1,
            Commands = new Dictionary<string, IReadOnlyList<string>> { ["stub"] = ["node", Agent(), pidFile, mode] },
        };
        var adapters = AdapterSet.Built();
        return new Daoris.Driver.Driver(
            new ServiceClient(service.Url, null), config, adapters, _home, processes: new SessionProcesses(),
            harnesses: new HarnessRoster(adapters, DaorisHome.Entry(_home, HomeEntry.Harnesses)));
    }

    /// <summary>
    /// The pipe door's stand-in: it takes its quest, finishes it (or not, for a crash), starts a grandchild that inherits its output and
    /// never ends, writes the grandchild's pid down, and exits.
    /// </summary>
    private string Agent()
    {
        var script = Path.Combine(_home, "agent.mjs");
        File.WriteAllText(script, """
            import { spawn } from 'node:child_process';
            import { writeFileSync } from 'node:fs';
            if (process.argv.includes('--version')) { console.log('stub-harness 1.0.0'); process.exit(0); }
            if (process.argv.includes('--login-state')) { console.log('logged-in'); process.exit(0); }
            const [pidFile, mode] = process.argv.slice(2);
            const respond = (body) => fetch(`${process.env.DAORIS_SERVICE_URL}/api/quests/q1/respond`, {
              method: 'POST', headers: { 'content-type': 'application/json' }, body: JSON.stringify(body) });
            await respond({ action: 'take' });
            if (mode === 'done') await respond({ action: 'done', reason: 'landed' });
            // Detached, as a dev server started from a shell is: otherwise node's own job ends it with this process.
            const stray = spawn(process.execPath, ['-e', 'setInterval(() => {}, 1000)'], { stdio: 'inherit', detached: true, windowsHide: true });
            stray.unref();
            writeFileSync(pidFile, String(stray.pid));
            console.log('finished; a dev server keeps running');
            process.exit(mode === 'done' ? 0 : 3);
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
