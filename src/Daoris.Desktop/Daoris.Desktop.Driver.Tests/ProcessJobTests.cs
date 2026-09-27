using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// ORPHAN1, found on FG5's run: a verify session started its repository's dev servers from a
/// background shell, its turn ended, the agent exited — and the servers kept running, holding two
/// ports and a tree whose session was over. Killing the agent's process tree could not reach them,
/// because by then the agent was gone and they were nobody's children. Everything a session starts
/// ends when its session does.
/// </summary>
public sealed class ProcessJobTests : IDisposable
{
    private readonly string _home = Path.Combine(
        Path.GetTempPath(), "daoris-job-" + Guid.NewGuid().ToString("N")[..8]);

    public ProcessJobTests() => Directory.CreateDirectory(_home);

    public void Dispose()
    {
        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public async Task A_child_that_outlives_its_parent_ends_when_the_session_is_untracked()
    {
        if (!OperatingSystem.IsWindows()) return; // The job object is Windows'; elsewhere this is a no-op by design.

        var heartbeat = Path.Combine(_home, "heartbeat.txt");
        var processes = new SessionProcesses();
        using var parent = Process.Start(Parent(heartbeat))!;
        var tracked = processes.Track("s1", parent);

        // The measured shape: the parent is gone, and its child is still beating.
        await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
        await Poll.Until(() => File.Exists(heartbeat), () => "the child never started", TimeSpan.FromSeconds(15));
        Assert.True(await Beating(heartbeat), "the child should outlive its parent — the orphan this test is about");

        tracked.Dispose();

        Assert.False(await Beating(heartbeat), "the session's child outlived its session");
    }

    /// <summary>A parent that starts a detached child writing a heartbeat, then exits at once.</summary>
    private ProcessStartInfo Parent(string heartbeat)
    {
        var child = Path.Combine(_home, "child.mjs");
        File.WriteAllText(child, """
            import { writeFileSync } from 'node:fs';
            const at = process.argv[2];
            setInterval(() => writeFileSync(at, String(Date.now())), 100);
            setTimeout(() => process.exit(0), 60000);
            """);
        var parent = Path.Combine(_home, "parent.mjs");
        File.WriteAllText(parent, """
            import { spawn } from 'node:child_process';
            const [child, at] = process.argv.slice(2);
            spawn(process.execPath, [child, at], { detached: true, stdio: 'ignore' }).unref();
            """);

        var info = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true };
        info.ArgumentList.Add(parent);
        info.ArgumentList.Add(child);
        info.ArgumentList.Add(heartbeat);
        return info;
    }

    /// <summary>Whether the heartbeat moves over most of a second.</summary>
    private static async Task<bool> Beating(string heartbeat)
    {
        var before = File.Exists(heartbeat) ? File.ReadAllText(heartbeat) : null;
        await Task.Delay(800);
        var after = File.Exists(heartbeat) ? File.ReadAllText(heartbeat) : null;
        return before != after;
    }
}
