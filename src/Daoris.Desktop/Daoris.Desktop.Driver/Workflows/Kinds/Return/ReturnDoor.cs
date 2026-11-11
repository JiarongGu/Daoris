namespace Daoris.Driver;

/// <summary>
/// Performs a run's <c>return</c>: the copy one of its work's records worked in brought back into its folder under the folder's start
/// lock, with no turn running there, and what it came to written on the node. Every file back reaches it and removes the copy; a file
/// changed on both sides keeps it active on <c>both-changed</c> with the paths, the copy kept, until each has the person's choice; a turn
/// in the folder keeps it on <c>place-busy</c>. A run whose session worked in the folder itself reaches it with nothing to bring back.
/// </summary>
/// <param name="turning">The folders a session runs a turn or waits in, which a return waits out.</param>
public sealed class ReturnDoor(
    string home, Action<string>? log = null, Func<CancellationToken, Task<IReadOnlySet<string>>>? turning = null, Func<bool>? runs = null,
    Func<bool>? holdsLease = null, Func<DateTimeOffset>? clock = null) : IReturnDoor, IDoorRuns
{
    private readonly NodeDoorRuns _runs = new(runs);
    private readonly SemaphoreSlim _one = new(1, 1);
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);

    NodeDoorRuns IDoorRuns.Runs => _runs;

    /// <inheritdoc/>
    public bool Start(ReturnStart start) => _runs.Start(start, ct => PerformAsync(start, ct), error =>
    {
        if (error is not (DriverException or IOException or UnauthorizedAccessException)) return false;
        log?.Invoke($"return  {start.Unit.Id}: its `{start.Node.Id}` could not be tried: {error.Message.TrimEnd().TrimEnd('.')}. It is tried again at a later pass.");
        return true;
    });

    /// <summary>The folder of a run's return of a copy, beside the run's record: its journal, and what it replaced under <c>kept/</c>.</summary>
    public static string JournalOf(string home, string unit, string copy) =>
        Path.Combine(NodeRecordFiles.FolderOf(home), NodeRecordFiles.KeyOf(unit) + ".return", copy);

    /// <summary>Completes once every return started has ended; a test's wait, and a closing loop's.</summary>
    public Task IdleAsync() => _runs.IdleAsync();

    /// <summary>The run's copy brought back, and what it came to written on the node.</summary>
    internal async Task PerformAsync(ReturnStart start, CancellationToken ct)
    {
        if (FolderCopies.Of(home, start.Unit.Executors) is not { } copy)
        {
            log?.Invoke($"return  {start.Unit.Id}: its session worked in the folder itself, so there is nothing to bring back.");
            Settle(start, null, "its session worked in the folder itself: nothing to bring back");
            return;
        }

        await _one.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var held = TreeLock.TryStarting(home, copy.Root, StartIn.In(copy.Root));
            if (held is null || (turning is not null
                    && (await turning(ct).ConfigureAwait(false)).Any(folder => TreeGit.SamePath(folder, copy.Root) || TreeGit.SamePath(folder, copy.Folder))))
            {
                Settle(start, ReturnNode.PlaceBusy, Path.GetFileName(copy.Root));
                return;
            }

            var returned = FolderReturn.Run(home, copy, start.Choices, ManifestBounds.Of(home), JournalOf(home, start.Unit.Id, copy.Id));
            if (returned.Waiting.Count > 0 || returned.Refused.Count > 0)
            {
                if (returned.Waiting.Count > 0)
                    log?.Invoke($"return  {start.Unit.Id}: {returned.Waiting.Count} file(s) changed in both the folder and copy `{copy.Id}` wait for your choice: {string.Join(", ", returned.Waiting)}.");
                if (returned.Refused.Count > 0)
                    log?.Invoke($"return  {start.Unit.Id}: {returned.Refused.Count} file(s) of copy `{copy.Id}` are not brought back, as found: "
                        + string.Join(", ", returned.Refused.Select(each => $"{each.Key} ({each.Value})")) + "; each waits for your Keep yours.");
                Settle(start, ReturnNode.BothChanged, ReturnNode.HoldsOf(returned));
                return;
            }

            log?.Invoke(returned.Nothing
                ? $"return  {start.Unit.Id}: copy `{copy.Id}` changed nothing, so nothing came back."
                : $"return  {start.Unit.Id}: copy `{copy.Id}` brought back: {returned.Written.Count} file(s) written into `{copy.Root}`.");
            if (Settle(start, null, returned.Nothing ? "its copy changed nothing" : $"{returned.Written.Count} file(s) brought back")) FolderCopies.Remove(copy);
        }
        finally
        {
            _one.Release();
        }
    }

    /// <summary>The node reached where <paramref name="waits"/> is null, else kept active on that wait with <paramref name="said"/>.</summary>
    /// <returns>Whether this call reached the node.</returns>
    private bool Settle(ReturnStart start, string? waits, string said) => new NodeRecordFiles(home, holdsLease).Edit<bool>(start.Unit.Id, record =>
    {
        var status = WorkPerformer.StatusOf(record, start.Node.Id);
        if (status.State != NodeStates.Active || status.Attempt != start.Attempt || status.Binding is not { } binding) return (null, false);
        if (waits is not null)
        {
            return status.Code == waits && status.Holds == said
                ? (null, false)
                : (record with { Statuses = WorkPerformer.With(record.Statuses, status with { Code = waits, Holds = said }) }, false);
        }

        var move = NodeObserving.Reached(new NodeReading(start.Unit, start.Node, binding), NodeSource.Return, binding.SubjectRevision,
            "return", start.Node.Id, said).Move!;
        var moved = NodeLifecycle.Apply(start.Node, status with { Code = null, Holds = null }, move);
        return moved.Moved ? (WorkPerformer.Kept(record, moved, "return-reached", _clock()), true) : (null, false);
    }).Result;
}
