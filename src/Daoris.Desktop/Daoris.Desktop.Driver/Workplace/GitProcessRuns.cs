using System.Diagnostics;
using System.Text;
using Daoris.Ports.Git;

namespace Daoris.Driver;

/// <summary>
/// Git run as this machine's processes, the <see cref="IGit"/> a root hands while the machine's own start, ownership and containment
/// (<see cref="ProcessStarts"/>, <see cref="ChildProcess"/>, <see cref="OwnedProcesses"/>) are the driver's: each run starts as
/// <see cref="GitRuns.GitStart"/> composes it, placed by the git scheduler, bounded, and ended with its tree past its bound or its
/// caller's cancellation. It answers the port and nothing above it: the memos, the launch's count and the bounds are the caller's.
/// </summary>
public sealed class GitProcessRuns : IGit
{
    /// <summary>The one instance a root hands.</summary>
    public static GitProcessRuns Shared { get; } = new();

    private GitProcessRuns()
    {
    }

    public string? InUse(string? home) => GitRuns.GitInUse(home);

    public async Task<GitAnswer> RunAsync(Ports.Git.GitAsk ask, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // A read the library answers starts no git. One with variables, an input or a bound of its own is always git's; an empty set of
        // variables is none (a read asked as written, past the flows' memos, is still read fresh from the files).
        if (ask.Environment is null or { Count: 0 } && ask.Input is null && ask.Bound == GitBounds.Of(ask.Arguments)
            && await GitLibraryReads.AnswerAsync(ask.Folder, ask.Arguments, ct).ConfigureAwait(false) is { } answered)
        {
            return new GitAnswer(answered.Code, answered.Stdout, answered.Stderr);
        }

        // Counted, classified and timed at the one seam; the arguments go on as they came.
        return await GitLaunch.LaunchAsync(
            GitLaunch.KindOf(ask.Arguments), ask.Arguments, GitLaunch.Budget, GitRunner.Whole, () => RunLaunchedAsync(ask, ct)).ConfigureAwait(false);
    }

    private static async Task<GitAnswer> RunLaunchedAsync(Ports.Git.GitAsk ask, CancellationToken ct)
    {
        var bound = ask.Bound ?? GitBounds.Of(ask.Arguments);
        var (info, refusal) = GitRuns.GitStart(ask.Folder, ask.Arguments, ask.Home);
        // A git the person chose that cannot run is git's answer, said in the resolution's words: never PATH's.
        if (info is null) return new GitAnswer(null, "", refusal!);
        foreach (var (name, value) in ask.Environment ?? new Dictionary<string, string>()) info.Environment[name] = value;
        if (ask.Input is not null)
        {
            info.RedirectStandardInput = true;
            info.StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        // Placed by the scheduler, whose token ends a lock-free read nobody waits for; the run, the wait and the guarded tree kill
        // are ChildProcess's. The caller's own cancellation leaves the ask and throws.
        try
        {
            await GitLazyFetch.PrepareAsync(info, ask.Arguments, ct).ConfigureAwait(false);
            var placed = GitStartAsks.For(info, ask.Arguments, GitRunner.Whole, bound, shares: ask.Input is null, ask.Input);
            var run = await GitScheduler.Shared.RunAsync(placed, token => placed.Input is not { } given
                ? ChildProcess.RunAsync(info, bound, token, closeInput: false)
                : ChildProcess.RunAsync(info, bound, token, given, ProcessStarts.Shared), ct).ConfigureAwait(false);
            return Answer(run, bound);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new GitAnswer(null, "", error.Message);
        }
    }

    /// <summary>A run's end as git's answer: its code and streams, or a failure saying it did not start or did not answer in time.</summary>
    internal static GitAnswer Answer(ChildRun run, TimeSpan bound)
    {
        if (!run.Started) return new GitAnswer(null, "", run.Error?.Message ?? "git did not start");
        // One that outlived its kill is not said to be stopped.
        if (run.TimedOut && run.Error is ProcessUnendedException unended)
        {
            return new GitAnswer(null, "", $"git did not answer within {bound.TotalSeconds:0} seconds, and did not end when stopped: {unended.Reason}.");
        }

        if (run.TimedOut) return new GitAnswer(null, "", $"git did not answer within {bound.TotalSeconds:0} seconds, and was stopped.");
        return new GitAnswer(run.ExitCode!.Value, run.Stdout, run.Stderr);
    }

    public Task<GitBytes> ReadBytesAsync(Ports.Git.GitAsk ask, int limit, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return GitLaunch.LaunchAsync(
            GitLaunch.KindOf(ask.Arguments), ask.Arguments, GitLaunch.Budget, GitRunner.Bytes, () => BytesLaunchedAsync(ask, limit, ct));
    }

    private static async Task<GitBytes> BytesLaunchedAsync(Ports.Git.GitAsk ask, int limit, CancellationToken ct)
    {
        // The blob is read as bytes off the stream beneath; git's own words on the error stream are UTF-8.
        var (info, refusal) = GitRuns.GitStart(ask.Folder, ask.Arguments, ask.Home);
        if (info is null) return new GitBytes(null, [], refusal!);
        foreach (var (name, value) in ask.Environment ?? new Dictionary<string, string>()) info.Environment[name] = value;
        if (ask.Input is not null)
        {
            info.RedirectStandardInput = true;
            info.StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
            GitStartInputs.Hand(info, ask.Input);
        }

        var bound = ask.Bound ?? GitBounds.Of(ask.Arguments);
        try
        {
            await GitLazyFetch.PrepareAsync(info, ask.Arguments, ct).ConfigureAwait(false);
            var placed = GitStartAsks.For(info, ask.Arguments, GitRunner.Bytes, bound, shares: false, ask.Input);
            return await GitScheduler.Shared.RunAsync(placed, token => BytesPlacedAsync(info, bound, limit, placed.Input, token), ct).ConfigureAwait(false);
        }
        catch (GitUnplacedException unplaced)
        {
            return new GitBytes(null, [], unplaced.Message);
        }
    }

    private static async Task<GitBytes> BytesPlacedAsync(ProcessStartInfo info, TimeSpan bound, int limit, string? input, CancellationToken ct)
    {
        Process? process = null;
        Task<string>? stderr = null;
        using var within = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            // Through the process-wide gate, off the caller's thread, in its place's class, which the gate reads over the one named here.
            process = await ProcessStarts.Shared.StartAsync(info, ProcessPurpose.Git, StartClass.Background, ct).ConfigureAwait(false)
                      ?? throw new DriverException("git did not start");
            within.CancelAfter(bound);
            using var ending = Ending(within.Token, process);
            if (input is not null)
            {
                await process.StandardInput.WriteAsync(input.AsMemory(), within.Token).ConfigureAwait(false);
                process.StandardInput.Close();
            }

            stderr = process.StandardError.ReadToEndAsync(within.Token);
            // Grown as git writes, so a whole file's bound is not its room.
            var most = Math.Max(0, limit);
            var buffer = new byte[Math.Min(most, 64 * 1024)];
            var count = 0;
            var stream = process.StandardOutput.BaseStream;
            while (count < most)
            {
                if (count == buffer.Length) Array.Resize(ref buffer, (int)Math.Min(most, 2L * buffer.Length));
                var got = await Heard(stream.ReadAsync(buffer.AsMemory(count), within.Token), within.Token).ConfigureAwait(false);
                if (got == 0) break;
                count += got;
            }

            // More than the bound is waiting: git would block on a full pipe, so it is stopped. What it wrote is whole.
            var cut = count == most && await Heard(stream.ReadAsync(new byte[1], within.Token), within.Token).ConfigureAwait(false) > 0;
            if (cut)
            {
                OwnedProcesses.End(process);
            }

            await process.WaitForExitAsync(within.Token).ConfigureAwait(false);
            var error = await PipeGrace.ReadAsync(stderr).ConfigureAwait(false);
            // A read the kill below ended answers as git's end would: past the bound, it is none.
            within.Token.ThrowIfCancellationRequested();
            return new GitBytes(cut ? null : process.ExitCode, buffer[..count], error ?? "", cut);
        }
        catch (OperationCanceledException) when (process is not null)
        {
            await EndAsync(process, stderr, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return new GitBytes(null, [], $"git did not answer within {bound.TotalSeconds:0} seconds, and was stopped.");
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // A read that failed under a running git ends it before the answer, as a cancelled one does.
            if (process is not null) await EndAsync(process, stderr, CancellationToken.None).ConfigureAwait(false);
            return new GitBytes(null, [], error.Message);
        }
        finally
        {
            process?.Dispose();
        }
    }

    public Task<int?> ReadAsync(Ports.Git.GitAsk ask, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return GitLaunch.LaunchAsync(
            GitLaunch.KindOf(ask.Arguments), ask.Arguments, GitLaunch.Budget, GitRunner.Streamed, () => ReadLaunchedAsync(ask, take, ct));
    }

    private static async Task<int?> ReadLaunchedAsync(Ports.Git.GitAsk ask, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct)
    {
        var (info, _) = GitRuns.GitStart(ask.Folder, ask.Arguments, ask.Home);
        if (info is null) return null;
        foreach (var (name, value) in ask.Environment ?? new Dictionary<string, string>()) info.Environment[name] = value;
        var bound = ask.Bound ?? GitBounds.Of(ask.Arguments);
        try
        {
            await GitLazyFetch.PrepareAsync(info, ask.Arguments, ct).ConfigureAwait(false);
            var placed = GitStartAsks.For(info, ask.Arguments, GitRunner.Streamed, bound, shares: false);
            return await GitScheduler.Shared.RunAsync(placed, token => ReadPlacedAsync(info, bound, take, token), ct).ConfigureAwait(false);
        }
        catch (GitUnplacedException)
        {
            return null;
        }
    }

    private static async Task<int?> ReadPlacedAsync(ProcessStartInfo info, TimeSpan bound, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct)
    {
        Process? process = null;
        Task<string>? stderr = null;
        using var within = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            // Through the process-wide gate, off the caller's thread, in its place's class, which the gate reads over the one named here.
            process = await ProcessStarts.Shared.StartAsync(info, ProcessPurpose.Git, StartClass.Background, ct).ConfigureAwait(false)
                      ?? throw new DriverException("git did not start");
            within.CancelAfter(bound);
            using var ending = Ending(within.Token, process);
            stderr = process.StandardError.ReadToEndAsync(within.Token);
            var buffer = new char[16 * 1024];
            var stopped = false;
            int got;
            while (!stopped && (got = await Heard(process.StandardOutput.ReadAsync(buffer.AsMemory(), within.Token), within.Token).ConfigureAwait(false)) > 0)
            {
                stopped = !take(buffer.AsMemory(0, got));
            }

            if (stopped)
            {
                OwnedProcesses.End(process);
            }

            await process.WaitForExitAsync(within.Token).ConfigureAwait(false);
            await PipeGrace.ReadAsync(stderr).ConfigureAwait(false);
            // A read the kill ended answers as git's end would: past the bound, it is a read that failed.
            within.Token.ThrowIfCancellationRequested();
            return stopped ? 0 : process.ExitCode;
        }
        catch (OperationCanceledException) when (process is not null)
        {
            await EndAsync(process, stderr, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return null;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // A read that failed under a running git ends it before the answer, as a cancelled one does.
            if (process is not null) await EndAsync(process, stderr, CancellationToken.None).ConfigureAwait(false);
            return null;
        }
        finally
        {
            process?.Dispose();
        }
    }

    /// <summary>
    /// A pipe read that ends when <paramref name="ct"/> is cancelled: a read of git's redirected pipe does not hear its
    /// token, and only git's end closes the pipe under it, so a git that outlived its kill would hold its runner in the read and
    /// never be said. The read left behind writes into a buffer nobody reads again.
    /// </summary>
    private static Task<int> Heard(ValueTask<int> read, CancellationToken ct) => read.IsCompleted ? read.AsTask() : read.AsTask().WaitAsync(ct);

    /// <summary>
    /// A streamed git ended as its bound passes or its caller cancels: a read of a pipe does not always hear its
    /// token, and the kill closes the pipe under it, so the read ends with git.
    /// </summary>
    private static CancellationTokenRegistration Ending(CancellationToken within, Process process) =>
        within.Register(() => OwnedProcesses.End(process));

    /// <summary>
    /// A streamed git past its bound or its caller's cancellation: its tree ended, never left running unowned, and its error stream's
    /// read let finish unobserved, the answer already "none". Its exit awaited after the kill (<see cref="ChildProcess.EndAsync"/>), so
    /// the runner never returns while git runs on; one that outlived its kill is thrown as <paramref name="ct"/>'s cancellation where
    /// the caller cancelled, and is otherwise already said by the launch, the answer "none" either way.
    /// </summary>
    private static async Task EndAsync(Process process, Task<string>? stderr, CancellationToken ct)
    {
        ProcessUnendedException? unended = null;
        try
        {
            await ChildProcess.EndAsync(process, ct).ConfigureAwait(false);
        }
        catch (ProcessUnendedException said)
        {
            unended = said;
        }

        // A git that runs on holds its pipe open, so its error stream's read is only observed, never awaited.
        var drained = stderr?.ContinueWith(_ => { }, TaskScheduler.Default);
        if (unended is null && drained is not null) await drained.ConfigureAwait(false);
        if (unended is not null && ct.IsCancellationRequested) throw unended;
    }
}
