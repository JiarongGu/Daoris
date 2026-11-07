using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// COAGENT1g, for a quest's part (the co-agent design §2.6): a quest's session handed over waits for what it left running before its move's
/// old end is written, as a conversation's hand-over does. The session's process, joined to its job as a run tracks it, starts a command in
/// the background and stays; the stop ends the process, and what the job still holds is ended and waited for before the stop returns.
/// </summary>
/// <remarks>Real processes in a real job, so the class is in the real-process half, and a job is Windows': elsewhere the case returns at once.</remarks>
[Trait(Category.Name, Category.Process)]
public sealed class QuestPartStopProcessTests : IDisposable
{
    private readonly string _home = Path.Combine(TestHomes.Root, "daoris-quest-stop-" + Guid.NewGuid().ToString("N")[..8]);

    public QuestPartStopProcessTests() => Directory.CreateDirectory(_home);

    private string PidFile => Path.Combine(_home, "left.pid");

    private int? LeftPid() => File.Exists(PidFile) && int.TryParse(File.ReadAllText(PidFile).Trim(), out var pid) ? pid : null;

    public void Dispose()
    {
        // A case that failed may leave the command running: it is ended here, never left on the machine.
        if (LeftPid() is { } pid)
        {
            try
            {
                using var left = Process.GetProcessById(pid);
                left.Kill();
            }
            catch (Exception error) when (error is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { }
        }

        try { Directory.Delete(_home, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    /// <summary>The session's process: once its job holds it (a go file), it starts a command detached, which writes its id, and stays.</summary>
    private string Part()
    {
        var left = Path.Combine(_home, "left.mjs");
        File.WriteAllText(left, """
            import { writeFileSync } from 'node:fs';
            writeFileSync(process.argv[2], String(process.pid));
            setTimeout(() => {}, 120000);
            """);
        // The step that starts the command and exits at once, so the command is nobody's child, out of reach of a walk of the part's tree.
        var middle = Path.Combine(_home, "middle.mjs");
        File.WriteAllText(middle, """
            import { spawn } from 'node:child_process';
            const [left, pidFile] = process.argv.slice(2);
            spawn(process.execPath, [left, pidFile], { detached: true, stdio: 'ignore', windowsHide: true }).unref();
            """);
        var part = Path.Combine(_home, "part.mjs");
        File.WriteAllText(part, """
            import { existsSync } from 'node:fs';
            import { spawn } from 'node:child_process';
            const [middle, left, pidFile, go] = process.argv.slice(2);
            const wait = setInterval(() => {
              if (!existsSync(go)) return;
              clearInterval(wait);
              spawn(process.execPath, [middle, left, pidFile], { stdio: 'ignore', windowsHide: true });
            }, 20);
            setTimeout(() => {}, 120000);
            """);
        return part;
    }

    private static bool Alive(int pid)
    {
        try
        {
            using var process = Process.GetProcessById(pid);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>🔴 The stop returns only once the command the part left in the background is gone, and says what it ended.</summary>
    [Fact]
    public async Task A_quest_parts_stop_ends_and_waits_for_what_it_left_running()
    {
        if (!OperatingSystem.IsWindows()) return;
        var processes = new SessionProcesses(DaorisHome.Entry(_home, HomeEntry.Sessions));
        var go = Path.Combine(_home, "go");
        var info = new ProcessStartInfo("node") { UseShellExecute = false, CreateNoWindow = true };
        var part = Part();
        foreach (var argument in new[] { part, Path.Combine(_home, "middle.mjs"), Path.Combine(_home, "left.mjs"), PidFile, go }) info.ArgumentList.Add(argument);
        using var process = Process.Start(info)!;
        var tracking = processes.Track("s1", process);
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => tracking.Dispose();
        File.WriteAllText(go, "");
        for (var until = DateTime.UtcNow.AddSeconds(30); LeftPid() is null; await Task.Delay(50))
        {
            Assert.True(DateTime.UtcNow < until, "the part never started its command");
        }

        var left = LeftPid()!.Value;
        Assert.True(Alive(left));

        var (stop, ended) = await QuestPartStop.StopAsync(processes, null, "s1", HandOverNotes.QuestHandedOver("codex-acp"), CancellationToken.None);

        Assert.True(stop.Stopped);
        Assert.False(Alive(left), "the command the part left running outlived its stop");
        Assert.Equal(new PartLeft(1, Emptied: true), ended);
        Assert.Contains(ended!.Note(PartLeftovers.Bound)!.Parts, part => part.Code == ChatNoteCodes.ChatLeftEnded.Code);
    }
}
