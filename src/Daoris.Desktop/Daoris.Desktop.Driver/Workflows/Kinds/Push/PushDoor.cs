namespace Daoris.Driver;

/// <summary>Performs a run's <c>push</c>: the branch its <c>land</c> made pushed by the one push act (<see cref="GitPushActs"/>) under its ref-bound grant, as the
/// person's press pushes it. A push that ends is written on the landing record; one the act refuses fails the node with the act's sentence in the machine log,
/// so a refused push (Write denied, a read-only identity, origin held) is said and never retried on its own.</summary>
/// <param name="home">The Daoris home whose journal, landing record and node records the push is kept in.</param>
public sealed class PushDoor(string home, Action<string>? log = null, Func<bool>? runs = null, Func<bool>? holdsLease = null) : IPushDoor, IDoorRuns
{
    /// <summary>The code a push the act refused fails its node with.</summary>
    public const string Refused = "push-refused";

    private readonly NodeDoorRuns _runs = new(runs);

    NodeDoorRuns IDoorRuns.Runs => _runs;

    /// <inheritdoc/>
    public bool Start(PushStart start) => _runs.Start(start, ct => PerformAsync(start, ct), error =>
    {
        if (error is not (DriverException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException or Microsoft.Data.Sqlite.SqliteException)) return false;
        log?.Invoke($"push  {start.Unit}: its `{start.Node.Id}` could not be tried: {error.Message.TrimEnd().TrimEnd('.')}. It is tried again at a later pass.");
        return true;
    });

    /// <summary>Completes once every push started has ended; a test's wait, and a closing loop's.</summary>
    public Task IdleAsync() => _runs.IdleAsync();

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
            if (status.State != NodeStates.Active || status.Binding?.SubjectRevision is { } bound && !string.Equals(bound, start.Tip, StringComparison.Ordinal)) return (null, false);
            var moved = NodeLifecycle.Apply(start.Node, status, new NodeMove(NodeStates.Failed, NodeCause.Authority) { Source = NodeSource.Git, Attempt = status.Attempt, Code = Refused });
            return (moved.Moved ? WorkPerformer.Kept(record, moved, "push-refused", DateTimeOffset.UtcNow) : null, moved.Moved);
        });
    }
}
