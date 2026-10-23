using System.Diagnostics;
using Daoris.Driver;

namespace Daoris.Desktop.Driver.Tests;

/// <summary>
/// Each of the three git runners returns only once its process has exited (GITAPP1a-iii, the git-app design P1.6 and P1.7's
/// "kill and wait, observed", Codex R4-5): <c>RunGitAsync</c> beneath <see cref="WorkingTree.GitAsync(string, IReadOnlyList{string}, CancellationToken)"/>,
/// <see cref="WorkingTree.ReadGitAsync"/> and <see cref="WorkingTree.GitBytesAsync"/>, cancelled during the process's creation and
/// while it runs; and one whose kill is refused says <c>git.unended</c> rather than returning as if it had ended.
/// </summary>
/// <remarks>
/// Today's tests saw that git's tree was ended (<see cref="GitStreamEndsTests"/>, by a marker a grandchild would have written); never
/// that the runner returned after the exit. The stand-in is a node that waits and ignores its input, started in git's place through the
/// gate's test seam (<see cref="ProcessStarts.Within"/>); its pid is watched through a handle of the test's own, which the runner's
/// dispose of its own cannot close. Fixtures live under the repository's gitignored <c>_fixtures/</c>. 🔴 Nothing here reaches a network.
/// </remarks>
[Trait(Category.Name, Category.Process)]
public sealed class GitRunnersEndTests : IDisposable
{
    private static readonly string[] Waits = ["status"];

    private readonly string _root;
    private readonly List<Process> _watched = [];

    public GitRunnersEndTests()
    {
        _root = Path.Combine(GitSeed.RepoRoot(), "_fixtures", "git-runners-end", Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        foreach (var watched in _watched)
        {
            try
            {
                if (!watched.HasExited) watched.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) { /* gone */ }

            watched.Dispose();
        }

        try
        {
            GitSeed.Remove(_root);
        }
        catch (IOException) { /* a straggling handle; the next run's folder is its own */ }
        catch (UnauthorizedAccessException) { /* likewise */ }
    }

    public enum Runner
    {
        Whole,
        Streamed,
        Bytes,
    }

    private Task Run(Runner runner, CancellationToken ct) => runner switch
    {
        Runner.Whole => WorkingTree.GitAsync(_root, Waits, ct),
        Runner.Streamed => WorkingTree.ReadGitAsync(_root, Waits, _ => true, ct),
        _ => WorkingTree.GitBytesAsync(_root, Waits, 1024, ct),
    };

    [Theory]
    [InlineData(Runner.Whole)]
    [InlineData(Runner.Streamed)]
    [InlineData(Runner.Bytes)]
    public async Task A_runner_cancelled_while_git_runs_returns_once_it_has_exited(Runner runner)
    {
        using var cancel = new CancellationTokenSource();
        var started = new TaskCompletionSource<Process>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (ProcessStarts.Within(info => Stand(info, started)))
        {
            var run = Run(runner, cancel.Token);
            var watched = await started.Task.WaitAsync(Poll.Patience);
            await cancel.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(Poll.Patience));
            Assert.True(watched.HasExited, $"{runner} returned while its git ran on");
        }
    }

    [Theory]
    [InlineData(Runner.Whole)]
    [InlineData(Runner.Streamed)]
    [InlineData(Runner.Bytes)]
    public async Task A_runner_cancelled_while_git_is_created_returns_once_it_has_exited(Runner runner)
    {
        using var cancel = new CancellationTokenSource();
        var creating = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource<Process>(TaskCreationOptions.RunContinuationsAsynchronously);
        // The start under way when the token is cancelled, and seen through (ProcessStarts): the process it makes is the runner's.
        using (ProcessStarts.Within(info =>
        {
            creating.TrySetResult();
            Assert.True(SpinWait.SpinUntil(() => cancel.IsCancellationRequested, Poll.Patience), "the test never cancelled");
            return Stand(info, started);
        }))
        {
            var run = Run(runner, cancel.Token);
            await creating.Task.WaitAsync(Poll.Patience);
            await cancel.CancelAsync();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(Poll.Patience));
            var watched = await started.Task.WaitAsync(Poll.Patience);
            Assert.True(watched.HasExited, $"{runner} returned while the git it was creating ran on");
        }
    }

    [Theory]
    [InlineData(Runner.Whole)]
    [InlineData(Runner.Streamed)]
    [InlineData(Runner.Bytes)]
    public async Task A_git_whose_kill_is_refused_is_said_unended_never_returned_as_ended(Runner runner)
    {
        var home = Path.Combine(_root, "home");
        var log = new MachineLog(home, "driver");
        using var cancel = new CancellationTokenSource();
        var started = new TaskCompletionSource<Process>(TaskCreationOptions.RunContinuationsAsynchronously);
        using (ProcessStarts.Within(info => Stand(info, started)))
        using (ChildProcess.KillingWith(_ => throw new System.ComponentModel.Win32Exception(5, "refused by the test")))
        using (GitUnended.SaidTo(log))
        {
            var run = Run(runner, cancel.Token);
            var watched = await started.Task.WaitAsync(Poll.Patience);
            await cancel.CancelAsync();

            // The runner's own bound on the exit wait is the claim: it is waited out, and the cancellation carries the fact.
            var unended = await Assert.ThrowsAsync<ProcessUnendedException>(() => run.WaitAsync(Poll.Patience));
            Assert.Equal(watched.Id, unended.Pid);
            Assert.Contains("refused by the test", unended.Reason);
            Assert.False(watched.HasExited);
        }

        // Closed first: the log holds its file while open.
        log.Dispose();
        var said = string.Join('\n', Directory.GetFiles(DaorisHome.Entry(home, HomeEntry.Logs)).Select(File.ReadAllText));
        Assert.Contains("git.unended", said);
        Assert.Contains("\"node\":\"status\"", said);
        Assert.DoesNotContain(_root.Replace("\\", "\\\\"), said);
    }

    /// <summary>The stand-in for git: a node that waits and ignores its input, its streams redirected as git's would be.</summary>
    private Process Stand(ProcessStartInfo info, TaskCompletionSource<Process> started)
    {
        var stand = new ProcessStartInfo("node")
        {
            RedirectStandardOutput = info.RedirectStandardOutput,
            RedirectStandardError = info.RedirectStandardError,
            RedirectStandardInput = info.RedirectStandardInput,
            StandardOutputEncoding = info.StandardOutputEncoding,
            StandardErrorEncoding = info.StandardErrorEncoding,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _root,
        };
        stand.ArgumentList.Add("-e");
        stand.ArgumentList.Add("setInterval(()=>{},1000)");
        var process = Process.Start(stand)!;
        // A handle of the test's own: the runner disposes its process, and this one still says whether the pid has exited.
        var watched = Process.GetProcessById(process.Id);
        lock (_watched) _watched.Add(watched);
        started.TrySetResult(watched);
        return process;
    }
}
