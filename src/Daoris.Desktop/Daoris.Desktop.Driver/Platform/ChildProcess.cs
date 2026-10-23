using System.ComponentModel;
using System.Diagnostics;

namespace Daoris.Driver;

/// <summary>
/// What a child run to its end, or past its patience, left (DEDUP4a): the exit code and both streams, or why there is none.
/// </summary>
/// <param name="Started">Whether the program started at all; when not, <see cref="Error"/> says why, if the runtime did.</param>
/// <param name="ExitCode">The exit code; null when the program did not start or was ended for taking too long.</param>
/// <param name="TimedOut">Whether the program was still running when its patience ran out, and its tree was ended.</param>
public sealed record ChildRun(bool Started, int? ExitCode, string Stdout, string Stderr, bool TimedOut)
{
    /// <summary>The runtime's failure to start the program, for a caller to put in its own words.</summary>
    public Exception? Error { get; init; }
}

/// <summary>
/// One way to run a child to its end with a patience (DEDUP4a, survey §4). Four callers once carried a copy each, and two
/// of them ended a process that had just exited with an unguarded kill, which threw.
/// </summary>
/// <remarks>
/// The runner sets nothing the caller's <see cref="ProcessStartInfo"/> did not: encodings, the environment and the window
/// are the caller's. Its words are the caller's too.
/// </remarks>
public static class ChildProcess
{
    /// <summary>
    /// Start <paramref name="info"/>, read both redirected streams whole, and wait <paramref name="patience"/> at most.
    /// A program still running then has its tree ended and is answered <see cref="ChildRun.TimedOut"/>; cancelling
    /// <paramref name="ct"/> ends the tree and throws <see cref="OperationCanceledException"/>. Either way the run returns only
    /// once the process has exited (GITAPP1a-iii), or says it has not (<see cref="ProcessUnendedException"/>, thrown on a
    /// cancellation and carried as <see cref="ChildRun.Error"/> past the patience).
    /// </summary>
    /// <param name="closeInput">Close the child's input at once, so a program that waits for input reads its end; the
    /// info must redirect it.</param>
    /// <remarks>
    /// The start passes the process-wide gate (<see cref="ProcessStarts"/>, UIFREEZE1): a few at a time, on the thread pool,
    /// so no caller's thread waits on the runtime's process-start lock. Cancelling while it waits for its place throws. Every
    /// run here is a question read to its end (git, a version, an agent's status, a plugin's tool check), so its start is the
    /// background's (<see cref="StartClass.Background"/>, PARALLEL1d): a person's start never queues behind one.
    /// </remarks>
    public static Task<ChildRun> RunAsync(ProcessStartInfo info, TimeSpan patience, CancellationToken ct, bool closeInput) =>
        RunAsync(info, patience, ct, closeInput, ProcessStarts.Shared);

    /// <summary>The same, through a gate a test hands in.</summary>
    internal static Task<ChildRun> RunAsync(
        ProcessStartInfo info, TimeSpan patience, CancellationToken ct, bool closeInput, ProcessStarts starts) =>
        RunAsync(info, patience, ct, closeInput ? "" : null, starts);

    /// <summary>
    /// The same, writing <paramref name="input"/> to the child's input and closing it (FABRIC-END4a, second review: git's
    /// <c>update-ref --stdin</c> takes a transaction there); null leaves the input as the info says. The info must redirect it.
    /// </summary>
    /// <remarks>Both outputs are read before the input is written, so a child that answers as it reads never fills a pipe and waits.</remarks>
    internal static async Task<ChildRun> RunAsync(
        ProcessStartInfo info, TimeSpan patience, CancellationToken ct, string? input, ProcessStarts starts)
    {
        Process? process;
        try
        {
            process = await starts.StartAsync(info, StartClass.Background, ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is Win32Exception or IOException or InvalidOperationException)
        {
            return new ChildRun(false, null, "", "", false) { Error = error };
        }

        if (process is null) return new ChildRun(false, null, "", "", false);
        using (process)
        {
            if (input == "") process.StandardInput.Close();
            using var bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
            bound.CancelAfter(patience);
            var stdout = process.StandardOutput.ReadToEndAsync(bound.Token);
            var stderr = process.StandardError.ReadToEndAsync(bound.Token);
            try
            {
                if (input is { Length: > 0 }) await WriteInputAsync(process, input, bound.Token).ConfigureAwait(false);
                await process.WaitForExitAsync(bound.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // GITAPP1a-iii: the tree ended and its exit awaited, so the runner never returns while its process runs on; one
                // that outlived its kill is said, never swallowed.
                ProcessUnendedException? unended = null;
                try
                {
                    await EndAsync(process, ct).ConfigureAwait(false);
                }
                catch (ProcessUnendedException said)
                {
                    unended = said;
                }

                // The reads are still outstanding, and abandoning them leaves faulted tasks nobody observes: awaited to
                // completion and dropped, since the answer is already "no answer in time" or the caller's cancellation.
                // One that runs on holds its pipes open, so then they are only observed, never awaited.
                var drained = Task.WhenAll(stdout, stderr).ContinueWith(_ => { }, TaskScheduler.Default);
                if (unended is null) await drained.ConfigureAwait(false);
                if (unended is not null && ct.IsCancellationRequested) throw unended;
                ct.ThrowIfCancellationRequested();
                return new ChildRun(true, null, "", "", true) { Error = unended };
            }

            return new ChildRun(true, process.ExitCode, await stdout.ConfigureAwait(false), await stderr.ConfigureAwait(false), false);
        }
    }

    /// <summary>The input written whole and closed; a child that ended before reading it all says why in its own exit and words.</summary>
    private static async Task WriteInputAsync(Process process, string input, CancellationToken ct)
    {
        try
        {
            await process.StandardInput.WriteAsync(input.AsMemory(), ct).ConfigureAwait(false);
            process.StandardInput.Close();
        }
        catch (IOException)
        {
            // The pipe closed under the write: the child is ending, and its answer is the one that counts.
        }
    }

    /// <summary>
    /// End a process and everything it started. A process that ended as it was stopped is already what was asked for, so
    /// the refusals of a process that is gone are swallowed.
    /// </summary>
    /// <remarks>
    /// Whether it ended is not this method's to say: a runner that must know awaits the exit after it
    /// (<see cref="EndAsync"/>, GITAPP1a-iii), which tells a kill that failed from one that worked.
    /// </remarks>
    public static void KillTree(Process process)
    {
        try
        {
            Kill(process);
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // It ended as it was stopped, or the exit wait after it says it did not.
        }
    }

    /// <summary>
    /// How long a runner waits for a process's exit after its kill (GITAPP1a-iii, the git-app design P1.6; provisional): past
    /// it, the process is said unended.
    /// </summary>
    public static readonly TimeSpan ExitBound = TimeSpan.FromSeconds(5);

    // The kill a test's flow makes instead of the runtime's (KillingWith); null in production.
    private static readonly AsyncLocal<Action<Process>?> s_kill = new();

    // Who is told of a process that outlived its kill in this flow (Telling): the git seam, which says it as `git.unended`.
    private static readonly AsyncLocal<Action<int, string>?> s_unended = new();

    private static void Kill(Process process)
    {
        if (s_kill.Value is { } stand) stand(process);
        else process.Kill(entireProcessTree: true);
    }

    /// <summary>
    /// End <paramref name="process"/>'s tree and await its exit within <see cref="ExitBound"/> (GITAPP1a-iii, the git-app design P1.6,
    /// Codex R4-5): the runners call it on their caller's cancellation or past their bound, and dispose the process only after.
    /// </summary>
    /// <exception cref="ProcessUnendedException">
    /// Its exit was not seen within the bound, its kill refused or not: told to this flow's listener (<see cref="Telling"/>) and
    /// thrown as the cancellation of <paramref name="ct"/> with the pid and the reason attached.
    /// </exception>
    internal static async Task EndAsync(Process process, CancellationToken ct)
    {
        var pid = PidOf(process);
        string? refused = null;
        try
        {
            Kill(process);
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Read by the exit wait: a process that ended as it was stopped refuses its kill and has exited.
            refused = error.Message;
        }

        using var bound = new CancellationTokenSource(ExitBound);
        try
        {
            await process.WaitForExitAsync(bound.Token).ConfigureAwait(false);
            return;
        }
        catch (OperationCanceledException) when (bound.IsCancellationRequested)
        {
        }
        catch (InvalidOperationException)
        {
            // No process is associated with it: nothing runs.
            return;
        }

        var reason = refused is null
            ? $"no exit within {ExitBound.TotalSeconds:0} s of its kill"
            : $"its kill was refused ({refused}) and it did not exit within {ExitBound.TotalSeconds:0} s";
        s_unended.Value?.Invoke(pid, reason);
        throw new ProcessUnendedException(pid, reason, ct);
    }

    private static int PidOf(Process process)
    {
        try
        {
            return process.Id;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    /// <summary>
    /// The test seam (GITAPP1a-iii): until disposed, every kill this flow's runners make is <paramref name="kill"/> instead of the
    /// runtime's, so a test can stand in a kill that is refused. Production opens none.
    /// </summary>
    internal static IDisposable KillingWith(Action<Process> kill)
    {
        var before = s_kill.Value;
        s_kill.Value = kill;
        return new Restore(() => s_kill.Value = before);
    }

    /// <summary>
    /// Tell <paramref name="unended"/> the pid and the reason of each process this flow's runners could not end, until disposed
    /// (GITAPP1a-iii): the git seam opens it per start, so the fact is said with the node it belongs to.
    /// </summary>
    internal static IDisposable Telling(Action<int, string> unended)
    {
        var before = s_unended.Value;
        s_unended.Value = unended;
        return new Restore(() => s_unended.Value = before);
    }

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}

/// <summary>
/// A runner's cancellation whose process outlived its kill (GITAPP1a-iii, the git-app design P1.6): the pid and why, so the
/// owner hears the process was not ended rather than a cancellation that reads as one that worked.
/// </summary>
public sealed class ProcessUnendedException(int pid, string reason, CancellationToken token)
    : OperationCanceledException($"process {pid} did not end: {reason}", token)
{
    /// <summary>The process that runs on.</summary>
    public int Pid { get; } = pid;

    /// <summary>Why it is not known to have ended: its kill refused, or no exit seen within the bound.</summary>
    public string Reason { get; } = reason;
}
