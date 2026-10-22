using System.Diagnostics;
using System.Text;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// CONTAINCPU1 (D200 §1.2, D201 point 4): a session started through the launcher, as every agent is since TERMTRUST1a part (i), still has a
/// job the driver reads and ends. The stand-in harness (the system's <c>cmd</c> running a script of this test's, never an agent) leaves a
/// child computing whose parent has already exited, so no walk of the tree from the launcher finds it, then waits on its standard input,
/// saying nothing. The registry opens the launcher's own job by name (<see cref="SessionProcesses.JobOf"/>), the watcher reads its CPU
/// through the driver's own read of it and brings the session as <i>busy, nothing heard</i>, never marking it, and the person's stop ends
/// the launcher, the harness and the orphaned child.
/// </summary>
/// <remarks>
/// Alone (PROCPAR1): its claim is a computation's CPU over a one-second window of the machine's clock, as
/// <see cref="SessionWatchProcessTests"/>' busy case is, which the load of classes beside it could break.
/// </remarks>
[Collection(Alone.Name)]
[Trait(Category.Name, Category.Process)]
public sealed class LaunchedSessionJobProcessTests : IDisposable
{
    private readonly string _root = Path.Combine(WorkspaceRoot.Folder, "_fixtures", "launched-session-job", Guid.NewGuid().ToString("N")[..8]);

    public LaunchedSessionJobProcessTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
    }

    private static string Cmd => Path.Combine(Environment.SystemDirectory, "cmd.exe");

    /// <summary>A process id no process here holds a job under: high, and its own per test.</summary>
    private static int StandInId() => 1_900_000_000 + Random.Shared.Next(100_000_000);

    /// <summary>
    /// A stand-in harness's start, built as a session's is: it starts a computing child through a <c>cmd</c> that exits at once, so the child
    /// is nobody's child, says <c>ready</c>, and waits on its standard input for a line.
    /// </summary>
    private ProcessStartInfo Harness()
    {
        var spin = Path.Combine(_root, "spin.cmd");
        var harness = Path.Combine(_root, "harness.cmd");
        File.WriteAllText(spin, "@echo off\r\n:spin\r\nset /a n+=1 >nul\r\ngoto spin\r\n");
        File.WriteAllText(harness, $"@echo off\r\ncmd /d /c start \"\" /b cmd /d /c \"{spin}\"\r\necho ready\r\nset /p line=\r\n");
        var info = new ProcessStartInfo(Cmd)
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
        foreach (var word in new[] { "/d", "/c", harness }) info.ArgumentList.Add(word);
        return info;
    }

    /// <summary>Until <paramref name="holds"/> holds, bounded: a condition that never holds fails.</summary>
    private static async Task Until(Func<bool> holds, string what)
    {
        var bound = Stopwatch.StartNew();
        while (!holds())
        {
            Assert.True(bound.Elapsed < TimeSpan.FromSeconds(30), what);
            await Task.Delay(50);
        }
    }

    [Fact]
    public async Task A_launched_sessions_job_is_read_busy_by_the_watcher_and_its_stop_ends_its_whole_tree()
    {
        if (!OperatingSystem.IsWindows()) return;
        var home = AgentStartsProcessTests.Installed(_root);
        using var jobs = new AgentJobs(StandInId());

        Process process;
        using (AgentJobs.Within(jobs))
        {
            process = (await AgentStarts.StartAsync(Harness(), ContainedKind.Agent, StartClass.Background, home, log: null, CancellationToken.None))!;
        }

        using (process)
        {
            Assert.True(PieceLauncher.Carries(process.StartInfo));
            var processes = new SessionProcesses();
            using var tracked = processes.Track("s1", process);
            Assert.Equal("ready", await process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)));

            // The launcher's own job, opened by its name: the launcher, the harness and the orphaned child, the start's go-between gone.
            var job = processes.JobOf("s1");
            Assert.NotNull(job);
            Assert.True(job.Holds);
            Assert.Same(job, processes.JobOf("s1"));
            await Until(() => job.Active == 3, "the launcher's own job held the launcher, the harness and its child");

            // The watcher, through the driver's own read (Driver.LookIn): busy by the job's CPU over the window, never marked.
            var events = new SessionEvents(DaorisHome.Entry(home, HomeEntry.Sessions));
            var watch = SessionWatch.Of(events);
            watch.Quiet = TimeSpan.FromSeconds(3);
            watch.Window = TimeSpan.FromSeconds(1);
            var steps = new List<QuietStep>();
            using (watch.Watch("s1", new WatchedRun(SessionWatch.Quest, "q1", "claude-code")))
            {
                var bound = Stopwatch.StartNew();
                while (!steps.Any(step => step.Step == SessionWatch.Brought))
                {
                    Assert.True(bound.Elapsed < TimeSpan.FromSeconds(30), "the busy, nothing heard row never came");
                    steps.AddRange(watch.Look(
                        id => processes.JobOf(id) is { } read ? new JobRead(read.Holds, read.Cpu) : null, TimeSpan.FromMilliseconds(200)));
                    await Task.Delay(200);
                }

                Assert.True(watch.RowOf("s1")!.Busy);
                Assert.DoesNotContain(steps, step => step.Step == SessionWatch.Marked);
            }

            // The person's stop: the launcher's tree, and through its job the child no walk of that tree reaches.
            Assert.True(processes.Stop("s1"));
            await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(30));
            await job.WaitEmptyAsync().WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(0, job.Active);
            await Until(() => ProcessJob.ActiveOf(jobs.Name) == 0, "the agents' job emptied as the session was stopped");
        }
    }
}
