using System.Diagnostics;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// A stand-in agent (<c>node</c> running a script of this test's, never an agent) starts a detached grandchild that never ends, then
/// ends one of the ways a session ends: it exits on its own (completed), its tracking is let go while it still runs (the run's end), or
/// the person's stop. Once the session has ended no grandchild remains, whether the start went through the launcher laid out beside the
/// home (every agent of an install) or none was laid out (a development run's).
/// </summary>
[Trait(Category.Name, Category.Process)]
[Trait(Category.Needs, Needed.Process)]
public sealed class SessionTreeEndProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(TestHomes.Root, "daoris-session-tree-end-" + Guid.NewGuid().ToString("N")[..8]);

    public SessionTreeEndProcessTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>A process id no process here holds a job under: high, and its own per test.</summary>
    private static int StandInId() => 1_900_000_000 + Random.Shared.Next(100_000_000);

    /// <summary>
    /// The agent: starts the grandchild detached and unreferenced with its output ignored, writes the grandchild's id down, says
    /// <c>ready</c>, and then exits at once when told to, else idles.
    /// </summary>
    private ProcessStartInfo Agent(string pidFile, bool exits)
    {
        var script = Path.Combine(_root, "agent.mjs");
        File.WriteAllText(script, """
            import { spawn } from 'node:child_process';
            import { writeFileSync } from 'node:fs';
            const grandchild = spawn(process.execPath, ['-e', 'setInterval(() => {}, 1e9)'], { detached: true, stdio: 'ignore', windowsHide: true });
            grandchild.unref();
            writeFileSync(process.argv[2], String(grandchild.pid));
            console.log('ready');
            if (process.argv[3] === 'exit') process.exit(0);
            setInterval(() => {}, 1000);
            """);
        var info = new ProcessStartInfo("node")
        {
            UseShellExecute = false,
            WorkingDirectory = _root,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };
        foreach (var word in new[] { script, pidFile, exits ? "exit" : "idle" }) info.ArgumentList.Add(word);
        return info;
    }

    private static bool Running(int id)
    {
        try
        {
            using var process = Process.GetProcessById(id);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static async Task Until(Func<bool> holds, string what)
    {
        var bound = Stopwatch.StartNew();
        while (!holds())
        {
            Assert.True(bound.Elapsed < TimeSpan.FromSeconds(30), what);
            await Task.Delay(50);
        }
    }

    public enum Ending { Exits, Untracked, Stopped }

    [Theory]
    [InlineData(true, Ending.Exits)]
    [InlineData(true, Ending.Untracked)]
    [InlineData(true, Ending.Stopped)]
    [InlineData(false, Ending.Exits)]
    [InlineData(false, Ending.Untracked)]
    [InlineData(false, Ending.Stopped)]
    public async Task A_grandchild_the_agent_left_running_ends_with_the_session(bool launcher, Ending ending)
    {
        if (!OperatingSystem.IsWindows()) return;
        var home = launcher ? AgentStartsProcessTests.Installed(_root) : Path.Combine(_root, "home");
        Directory.CreateDirectory(home);
        var pidFile = Path.Combine(_root, "grandchild.pid");
        using var jobs = new AgentJobs(StandInId());

        Process process;
        using (AgentJobs.Within(jobs))
        {
            process = (await AgentStarts.StartAsync(Agent(pidFile, ending == Ending.Exits), ContainedKind.Agent, StartClass.Background, home, log: null, CancellationToken.None))!;
        }

        var grandchild = 0;
        using (process)
        {
            Assert.Equal(launcher, PieceLauncher.Carries(process.StartInfo));
            var processes = new SessionProcesses();
            var tracked = processes.Track("s1", process);
            Assert.Equal("ready", await process.StandardOutput.ReadLineAsync().WaitAsync(Poll.Patience));
            await Until(() => File.Exists(pidFile) && File.ReadAllText(pidFile).Length > 0, "the agent wrote its grandchild's id");
            grandchild = int.Parse(File.ReadAllText(pidFile));
            Assert.True(Running(grandchild));

            switch (ending)
            {
                case Ending.Exits:
                    await process.WaitForExitAsync().WaitAsync(Poll.Patience);
                    tracked.Dispose();
                    break;
                case Ending.Untracked:
                    tracked.Dispose();
                    break;
                default:
                    Assert.True(processes.Stop("s1"));
                    await process.WaitForExitAsync().WaitAsync(Poll.Patience);
                    tracked.Dispose();
                    break;
            }
        }

        try
        {
            await Until(() => !Running(grandchild), "the grandchild ended with the session");
        }
        finally
        {
            if (Running(grandchild)) Process.GetProcessById(grandchild).Kill();
        }
    }
}
