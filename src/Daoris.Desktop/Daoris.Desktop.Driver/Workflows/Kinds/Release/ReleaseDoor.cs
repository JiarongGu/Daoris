namespace Daoris.Driver;

/// <summary>
/// Performs a run's <c>release</c>: each tree its run owns ended through <see cref="TreeReleases"/>, so a tree whose work the line holds goes
/// with its branch and one holding uncommitted work or commits no branch of the person's holds stays, its reason kept on its record. The node is
/// reached either way, its words saying what went and what stayed and why; a tree that stays is the sweep's to end once its work lands.
/// </summary>
/// <param name="home">The Daoris home whose session branches and run records are read and written.</param>
/// <param name="log">Told what a release came to; the machine log's line.</param>
/// <param name="runs">Whether a release may begin now: not while an update drains; null always may.</param>
public sealed class ReleaseDoor(string home, Action<string>? log = null, Func<bool>? runs = null, Func<bool>? holdsLease = null) : IReleaseDoor, IDoorRuns
{
    private readonly NodeDoorRuns _runs = new(runs);

    NodeDoorRuns IDoorRuns.Runs => _runs;

    /// <inheritdoc/>
    public bool Start(ReleaseStart start) => _runs.Start(start, ct => PerformAsync(start, ct), error =>
    {
        if (error is not (DriverException or IOException or UnauthorizedAccessException)) return false;
        log?.Invoke($"release  {start.Unit.Id}: its `{start.Node.Id}` could not be tried: {error.Message.TrimEnd().TrimEnd('.')}. It is tried again at a later pass.");
        return true;
    });

    /// <summary>Completes once every release started has ended; a test's wait, and a closing loop's.</summary>
    public Task IdleAsync() => _runs.IdleAsync();

    /// <summary>The run's trees ended and the node reached with what came of each.</summary>
    internal async Task PerformAsync(ReleaseStart start, CancellationToken ct)
    {
        var releases = new TreeReleases(home);
        var done = new List<TreeReleased>();
        foreach (var branch in start.Branches) done.Add(await releases.ReleaseAsync(start.Root, start.Repository, branch, ct).ConfigureAwait(false));
        var said = Said(done);
        log?.Invoke($"release  {start.Unit.Id}: {said}");
        Settle(start, said);
    }

    /// <summary>What a release came to, each tree that stayed with its reason.</summary>
    internal static string Said(IReadOnlyList<TreeReleased> done)
    {
        if (done.Count == 0) return "no tree of its run stands, so nothing was ended";
        var stays = done.Where(each => !each.Gone).ToList();
        var gone = done.Count - stays.Count;
        return $"{gone} tree{(gone == 1 ? "" : "s")} of its run ended"
               + (stays.Count == 0 ? "" : $", {stays.Count} kept: " + string.Join(" ", stays.Select(each => $"`{each.Branch}` — {each.Said}")));
    }

    private void Settle(ReleaseStart start, string said) => new NodeRecordFiles(home, holdsLease).Edit<bool>(start.Unit.Id, record =>
    {
        var status = WorkPerformer.StatusOf(record, start.Node.Id);
        if (status.State != NodeStates.Active || status.Attempt != start.Attempt || status.Binding is not { } binding) return (null, false);
        var move = NodeObserving.Reached(new NodeReading(start.Unit, start.Node, binding), NodeSource.Release, binding.SubjectRevision,
            "release", start.Node.Id, said).Move! with { Words = said };
        var moved = NodeLifecycle.Apply(start.Node, status with { Code = null, Holds = null }, move);
        return moved.Moved ? (WorkPerformer.Kept(record, moved, "release-reached", DateTimeOffset.UtcNow), true) : (null, false);
    });
}
