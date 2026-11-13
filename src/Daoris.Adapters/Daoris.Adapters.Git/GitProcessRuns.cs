using System.Text;
using Daoris.Ports.Git;
using Daoris.Ports.Processes;

namespace Daoris.Adapters.Git;

/// <summary>
/// Git run as this machine's processes: each run is a process the machine's host starts as the application's policy resolved it,
/// placed as the policy places it, bounded, and ended with its tree past its bound or its caller's cancellation. It answers the port
/// and nothing above it: which git, whether the library answers without one, the scheduler's place, the count and the memos are the
/// policy's and the caller's.
/// </summary>
/// <param name="host">Starts the processes: admitted, owned and contained as the machine does for its class.</param>
/// <param name="policy">What Daoris decides about a git that this adapter runs without deciding.</param>
public sealed class GitProcessRuns(IProcessHost host, IGitPolicy policy) : IGit
{
    // The purpose the machine's log and the processes' owner know a git's start by.
    private const string Purpose = "Git";

    private static readonly UTF8Encoding Utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public string? InUse(string? home) => policy.InUse(home);

    public async Task<GitAnswer> RunAsync(GitAsk ask, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        // A read the library answers starts no git.
        if (await policy.AnswerInProcessAsync(ask, ct).ConfigureAwait(false) is { } answered) return answered;
        return await policy.LaunchAsync(ask, GitRunKind.Whole, () => RunLaunchedAsync(ask, ct)).ConfigureAwait(false);
    }

    private async Task<GitAnswer> RunLaunchedAsync(GitAsk ask, CancellationToken ct)
    {
        try
        {
            var start = await policy.StartAsync(ask, GitRunKind.Whole, ct).ConfigureAwait(false);
            // A git the person chose that cannot run is git's answer, said in the resolution's words: never PATH's.
            if (start.Refusal is not null) return new GitAnswer(null, "", start.Refusal);
            return await policy.PlaceAsync(ask, start, GitRunKind.Whole, token => RunPlacedAsync(ask, start, token), ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new GitAnswer(null, "", error.Message);
        }
    }

    private async Task<GitAnswer> RunPlacedAsync(GitAsk ask, GitRunSpec start, CancellationToken ct)
    {
        await using var run = await host.StartAsync(SpecOf(ask, start, raw: false), ct).ConfigureAwait(false);
        using var bound = CancellationTokenSource.CreateLinkedTokenSource(ct);
        bound.CancelAfter(start.Bound);
        var stdout = new StringBuilder();
        var stderr = new StringBuilder();
        var reading = ReadAsync(run, piece => (piece.Stream == ProcessStream.Output ? stdout : stderr).Append(piece.Text));
        try
        {
            await WriteInputAsync(run, ask.Input, bound.Token).ConfigureAwait(false);
            var end = await run.EndAsync(bound.Token).ConfigureAwait(false);
            await reading.WaitAsync(bound.Token).ConfigureAwait(false);
            return new GitAnswer(end.Exit, stdout.ToString(), stderr.ToString());
        }
        catch (OperationCanceledException error) when (error is not ProcessUnendedException)
        {
            // The tree ended and its exit awaited, so the run never returns while its process runs on: one that outlived its kill is said.
            var reason = await StopAsync(run, ct).ConfigureAwait(false);
            _ = reading.ContinueWith(static ended => _ = ended.Exception, TaskScheduler.Default);
            ct.ThrowIfCancellationRequested();
            var seconds = start.Bound.TotalSeconds;
            return new GitAnswer(null, "", reason is null
                ? $"git did not answer within {seconds:0} seconds, and was stopped."
                : $"git did not answer within {seconds:0} seconds, and did not end when stopped: {reason}.");
        }
    }

    public Task<GitBytes> ReadBytesAsync(GitAsk ask, int limit, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return policy.LaunchAsync(ask, GitRunKind.Bytes, () => BytesLaunchedAsync(ask, limit, ct));
    }

    private async Task<GitBytes> BytesLaunchedAsync(GitAsk ask, int limit, CancellationToken ct)
    {
        try
        {
            var start = await policy.StartAsync(ask, GitRunKind.Bytes, ct).ConfigureAwait(false);
            if (start.Refusal is not null) return new GitBytes(null, [], start.Refusal);
            return await policy.PlaceAsync(ask, start, GitRunKind.Bytes, token => BytesPlacedAsync(ask, start, limit, token), ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return new GitBytes(null, [], error.Message);
        }
    }

    private async Task<GitBytes> BytesPlacedAsync(GitAsk ask, GitRunSpec start, int limit, CancellationToken ct)
    {
        await using var run = await host.StartAsync(SpecOf(ask, start, raw: true), ct).ConfigureAwait(false);
        using var within = CancellationTokenSource.CreateLinkedTokenSource(ct);
        within.CancelAfter(start.Bound);
        var stderr = new StringBuilder();
        var reading = ReadAsync(run, piece => stderr.Append(piece.Text));
        try
        {
            await WriteInputAsync(run, ask.Input, within.Token).ConfigureAwait(false);
            // Grown as git writes, so a whole file's bound is not its room.
            var most = Math.Max(0, limit);
            var buffer = new byte[Math.Min(most, 64 * 1024)];
            var count = 0;
            var cut = false;
            await foreach (var piece in run.Bytes(within.Token).ConfigureAwait(false))
            {
                var room = most - count;
                if (piece.Length > room) cut = true;
                var take = Math.Min(piece.Length, room);
                if (count + take > buffer.Length) Array.Resize(ref buffer, (int)Math.Min(most, Math.Max(2L * buffer.Length, count + take)));
                piece.Span[..take].CopyTo(buffer.AsSpan(count));
                count += take;
                if (cut) break;
            }

            // More than the bound is waiting: git would block on a full pipe, so it is stopped. What it wrote is whole.
            if (cut) await StopAsync(run, ct).ConfigureAwait(false);
            var end = await run.EndAsync(within.Token).ConfigureAwait(false);
            await reading.WaitAsync(within.Token).ConfigureAwait(false);
            // A read the kill below ended answers as git's end would: past the bound, it is none.
            within.Token.ThrowIfCancellationRequested();
            return new GitBytes(cut ? null : end.Exit, buffer[..count], stderr.ToString(), cut);
        }
        catch (OperationCanceledException error) when (error is not ProcessUnendedException)
        {
            await StopAsync(run, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return new GitBytes(null, [], $"git did not answer within {start.Bound.TotalSeconds:0} seconds, and was stopped.");
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // A read that failed under a running git ends it before the answer, as a cancelled one does.
            await StopAsync(run, CancellationToken.None).ConfigureAwait(false);
            return new GitBytes(null, [], error.Message);
        }
    }

    public Task<int?> ReadAsync(GitAsk ask, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return policy.LaunchAsync(ask, GitRunKind.Streamed, () => StreamLaunchedAsync(ask, take, ct));
    }

    private async Task<int?> StreamLaunchedAsync(GitAsk ask, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct)
    {
        try
        {
            var start = await policy.StartAsync(ask, GitRunKind.Streamed, ct).ConfigureAwait(false);
            if (start.Refusal is not null) return null;
            return await policy.PlaceAsync(ask, start, GitRunKind.Streamed, token => StreamPlacedAsync(ask, start, take, token), ct).ConfigureAwait(false);
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            return null;
        }
    }

    private async Task<int?> StreamPlacedAsync(GitAsk ask, GitRunSpec start, Func<ReadOnlyMemory<char>, bool> take, CancellationToken ct)
    {
        await using var run = await host.StartAsync(SpecOf(ask, start, raw: false), ct).ConfigureAwait(false);
        using var within = CancellationTokenSource.CreateLinkedTokenSource(ct);
        within.CancelAfter(start.Bound);
        try
        {
            var stopped = false;
            await foreach (var piece in run.Output(within.Token).ConfigureAwait(false))
            {
                if (piece.Stream != ProcessStream.Output || take(piece.Text.AsMemory())) continue;
                stopped = true;
                break;
            }

            if (stopped) await StopAsync(run, ct).ConfigureAwait(false);
            var end = await run.EndAsync(within.Token).ConfigureAwait(false);
            // A read the kill ended answers as git's end would: past the bound, it is a read that failed.
            within.Token.ThrowIfCancellationRequested();
            return stopped ? 0 : end.Exit;
        }
        catch (OperationCanceledException error) when (error is not ProcessUnendedException)
        {
            await StopAsync(run, ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return null;
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            // A read that failed under a running git ends it before the answer, as a cancelled one does.
            await StopAsync(run, CancellationToken.None).ConfigureAwait(false);
            return null;
        }
    }

    private static ProcessSpec SpecOf(GitAsk ask, GitRunSpec start, bool raw) =>
        new(start.Program ?? "git", start.Arguments, Purpose, AdmissionClasses.Background)
        {
            WorkingFolder = ask.Folder,
            Environment = start.Environment,
            Input = ask.Input is not null,
            RawOutput = raw,
        };

    /// <summary>The run's text read to its end, each piece handed to <paramref name="keep"/>; the task ends with the run's streams.</summary>
    private static Task ReadAsync(IChildRun run, Action<ProcessOutput> keep) => Task.Run(async () =>
    {
        await foreach (var piece in run.Output(CancellationToken.None).ConfigureAwait(false)) keep(piece);
    });

    /// <summary>The input written whole as BOM-less UTF-8 and closed; a child that ended before reading it all says why in its own exit and words.</summary>
    private static async Task WriteInputAsync(IChildRun run, string? input, CancellationToken ct)
    {
        if (input is null) return;
        try
        {
            if (input.Length > 0) await run.WriteAsync(Utf8.GetBytes(input), ct).ConfigureAwait(false);
            await run.CloseInputAsync().ConfigureAwait(false);
        }
        catch (IOException)
        {
            // The pipe closed under the write: the child is ending, and its answer is the one that counts.
        }
    }

    /// <summary>
    /// A git past its bound or its caller's cancellation: its tree ended and its exit awaited, so the runner never returns while git
    /// runs on. A git that outlived its kill is told to the policy and answered as its reason; where the caller cancelled, it is
    /// thrown as that cancellation instead, since the caller leaves.
    /// </summary>
    private async Task<string?> StopAsync(IChildRun run, CancellationToken ct)
    {
        var reason = await run.StopAsync(CancellationToken.None).ConfigureAwait(false);
        if (reason is null) return null;
        policy.Unended(run.Id, reason);
        if (ct.IsCancellationRequested) throw new ProcessUnendedException(run.Id, reason, ct);
        return reason;
    }
}
