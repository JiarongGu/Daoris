using System.Collections.Concurrent;

namespace Daoris.Driver;

/// <summary>
/// Performs a run's <c>push</c> node: the branch the run's <c>land</c> made is pushed to origin by the one push act on the
/// act journal (<see cref="GitPushActs"/>), under its ref-bound grant, exactly as the person's press pushes it. A push that ends is written on the
/// landing record, which the node reads against git's remote-tracking ref; one the act refuses fails the node with the act's sentence kept in the
/// machine log, so a refused push (Write denied, a read-only identity, origin held) is said and never retried on its own.
/// </summary>
/// <param name="home">The Daoris home whose journal, landing record and node records the push is kept in.</param>
/// <param name="log">Told what a push came to; the machine log's line.</param>
/// <param name="runs">Whether a push may begin now: not while an update drains; null always may.</param>
/// <param name="holdsLease">Whether this process holds the home's lease, as the node records ask it; null asks the lease itself. A test's stand-in sets it.</param>
public sealed class PushDoor(string home, Action<string>? log = null, Func<bool>? runs = null, Func<bool>? holdsLease = null) : IPushDoor
{
    /// <summary>The code a push the act refused fails its node with.</summary>
    public const string Refused = "push-refused";

    private readonly ConcurrentDictionary<string, Task> _running = new(StringComparer.Ordinal);

    /// <inheritdoc/>
    public bool Start(PushStart start)
    {
        if (runs is not null && !runs()) return false;
        var key = $"{start.Unit}\n{start.Node.Id}\n{start.Tip}";
        var gate = new TaskCompletionSource();
        if (!_running.TryAdd(key, gate.Task)) return false;
        using (ExecutionContext.SuppressFlow())
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await PerformAsync(start, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception error) when (error is DriverException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException or Microsoft.Data.Sqlite.SqliteException)
                {
                    log?.Invoke($"push  {start.Unit}: its `{start.Node.Id}` could not be tried: {error.Message.TrimEnd().TrimEnd('.')}. It is tried again at a later pass.");
                }
                finally
                {
                    _running.TryRemove(key, out _);
                    gate.TrySetResult();
                }
            });
        }

        return true;
    }

    /// <summary>Completes once every push started has ended; a test's wait, and a closing loop's.</summary>
    public Task IdleAsync() => Task.WhenAll(_running.Values);

    /// <summary>The push act for the landed branch at its tip, and what it came to written: the landing record on success, the node's failure otherwise.</summary>
    internal async Task PerformAsync(PushStart start, CancellationToken ct)
    {
        var ask = new PushAsk(start.Repository, start.Root, "branch", start.Branch, start.Tip, start.Workspace);
        var run = await GitPushActs.PushAsync(new ActRuns(new ActJournal(home)), ask, "workflow", "yes", ct).ConfigureAwait(false);
        Settle(start, run);
    }

    /// <summary>What the act's end comes to: done is written on the landing; a refusal fails the node; any other end is said and leaves the node active.</summary>
    internal void Settle(PushStart start, ActRun run)
    {
        if (!run.Refused && run.Outcome == ActOutcomes.Done)
        {
            new LandedBranches(home).Pushed(start.Repository, start.Branch, start.Tip);
            log?.Invoke($"push  {start.Unit}: `{start.Branch}` was pushed at {TextBounds.ShortCommit(start.Tip, 8)}.");
            return;
        }

        // An end that is neither done nor refused leaves the node active for a later pass or the person's word, and only a refusal fails it.
        if (!run.Refused)
        {
            log?.Invoke($"push  {start.Unit}: `{start.Node.Id}` ended {run.Outcome ?? "with no outcome"}, not done: {run.Said}");
            return;
        }

        log?.Invoke($"push  {start.Unit}: `{start.Node.Id}` was refused: {run.Said}");
        Fail(start);
    }

    private void Fail(PushStart start)
    {
        var records = new NodeRecordFiles(home, holdsLease);
        records.Edit<bool>(start.Unit, record =>
        {
            var status = WorkPerformer.StatusOf(record, start.Node.Id);
            // A refusal of an earlier tip's push is not the verdict of the attempt a newer tip has since bound.
            if (status.State != NodeStates.Active || status.Binding?.SubjectRevision is { } bound && !string.Equals(bound, start.Tip, StringComparison.Ordinal)) return (null, false);
            var moved = NodeLifecycle.Apply(start.Node, status, new NodeMove(NodeStates.Failed, NodeCause.Authority) { Source = NodeSource.Git, Attempt = status.Attempt, Code = Refused });
            return (moved.Moved ? WorkPerformer.Kept(record, moved, "push-refused", DateTimeOffset.UtcNow) : null, moved.Moved);
        });
    }
}
