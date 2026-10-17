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
    /// <paramref name="ct"/> ends the tree and throws <see cref="OperationCanceledException"/>.
    /// </summary>
    /// <param name="closeInput">Close the child's input at once, so a program that waits for input reads its end; the
    /// info must redirect it.</param>
    /// <remarks>
    /// The start passes the process-wide gate (<see cref="ProcessStarts"/>, UIFREEZE1): a few at a time, on the thread pool,
    /// so no caller's thread waits on the runtime's process-start lock. Cancelling while it waits for its place throws.
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
            process = await starts.StartAsync(info, ct).ConfigureAwait(false);
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
                KillTree(process);
                // The reads are still outstanding, and abandoning them leaves faulted tasks nobody observes: awaited to
                // completion and dropped, since the answer is already "no answer in time" or the caller's cancellation.
                await Task.WhenAll(stdout, stderr).ContinueWith(_ => { }, TaskScheduler.Default).ConfigureAwait(false);
                ct.ThrowIfCancellationRequested();
                return new ChildRun(true, null, "", "", true);
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
    public static void KillTree(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception error) when (error is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // It ended as it was stopped.
        }
    }
}
