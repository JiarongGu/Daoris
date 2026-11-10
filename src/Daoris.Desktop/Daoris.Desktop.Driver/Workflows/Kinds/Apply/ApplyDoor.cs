namespace Daoris.Driver;

/// <summary>
/// Performs a run's <c>apply</c>: the newest change set its work planned handed to the system's adapter, applied under each operation's
/// precondition, and what it came to kept beside the plan and written on the node. Applied reaches it; stale fails it with words naming
/// each stale resource's present version, which its way back tells the work; any other refusal fails it in the adapter's words. One apply
/// runs at a time in this door, so one applier writes to a system.
/// </summary>
public sealed class ApplyDoor(
    string home, Func<SystemPlace, CancellationToken, Task<IHookChannel>> adapters, Action<string>? log = null, Func<bool>? runs = null,
    Func<bool>? holdsLease = null, Func<DateTimeOffset>? clock = null) : IApplyDoor, IDoorRuns
{
    /// <summary>The code a stale plan fails its node with.</summary>
    public const string Stale = "stale";

    /// <summary>The code an apply the adapter refused otherwise fails its node with.</summary>
    public const string Refused = "apply-refused";

    private readonly NodeDoorRuns _runs = new(runs);
    private readonly SemaphoreSlim _one = new(1, 1);
    private readonly Func<DateTimeOffset> _clock = clock ?? (() => DateTimeOffset.UtcNow);

    NodeDoorRuns IDoorRuns.Runs => _runs;

    /// <inheritdoc/>
    public bool Start(ApplyStart start) => _runs.Start(start, ct => PerformAsync(start, ct), error =>
    {
        if (error is not (DriverException or IOException or UnauthorizedAccessException or System.Text.Json.JsonException)) return false;
        log?.Invoke($"apply  {start.Unit.Id}: its `{start.Node.Id}` could not be tried: {error.Message.TrimEnd().TrimEnd('.')}. It is tried again at a later pass.");
        return true;
    });

    /// <summary>Completes once every apply started has ended; a test's wait, and a closing loop's.</summary>
    public Task IdleAsync() => _runs.IdleAsync();

    /// <summary>The run's newest plan applied by its adapter, once per plan, and what it came to written on the node.</summary>
    internal async Task PerformAsync(ApplyStart start, CancellationToken ct)
    {
        var plans = new SystemPlans(home);
        var (record, plan) = start.Unit.Executors.Reverse().Select(each => (Record: each, Plan: plans.Latest(each))).FirstOrDefault(each => each.Plan is not null);
        if (plan is null || SystemPlace.Of(plan.Place) is not { } place)
        {
            log?.Invoke($"apply  {start.Unit.Id}: `{start.Node.Id}` has no change set to apply: its work planned none.");
            return;
        }

        await _one.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var applied = plans.AppliedOf(record!, plan.Plan);
            if (applied is null)
            {
                await using var channel = await adapters(place, ct).ConfigureAwait(false);
                applied = await new SystemAdapter(channel, place, plans, _clock).ApplyAsync(record!, plan, ct).ConfigureAwait(false);
            }

            Settle(start, plan, applied);
        }
        finally
        {
            _one.Release();
        }
    }

    /// <summary>What the apply came to, written on the node where it is still the active attempt.</summary>
    internal void Settle(ApplyStart start, SystemChangeSet plan, SystemApplied applied)
    {
        log?.Invoke(applied.State switch
        {
            SystemApplyStates.Applied => $"apply  {start.Unit.Id}: plan {plan.Plan} applied to `{plan.Place}`: {applied.Applied.Count} operation(s).",
            SystemApplyStates.None => $"apply  {start.Unit.Id}: plan {plan.Plan} had nothing to change in `{plan.Place}`: every resource already held what it asks, so nothing was written.",
            SystemApplyStates.Stale => $"apply  {start.Unit.Id}: plan {plan.Plan} was stale at `{applied.Refused}`, so the work plans again.",
            _ => $"apply  {start.Unit.Id}: plan {plan.Plan} was refused: {applied.Message}",
        });
        var records = new NodeRecordFiles(home, holdsLease);
        records.Edit<bool>(start.Unit.Id, record =>
        {
            var status = WorkPerformer.StatusOf(record, start.Node.Id);
            if (status.State != NodeStates.Active || status.Attempt != start.Attempt || status.Binding is not { } binding) return (null, false);
            var move = applied.State is SystemApplyStates.Applied or SystemApplyStates.None
                ? NodeObserving.Reached(new NodeReading(start.Unit, start.Node, binding), NodeSource.Plugin, binding.SubjectRevision,
                    $"plan-{plan.Plan}.json", plan.Place, applied.State == SystemApplyStates.None ? $"plan {plan.Plan} had nothing to change" : $"plan {plan.Plan} applied").Move!
                : new NodeMove(NodeStates.Failed, NodeCause.Authority)
                {
                    Source = NodeSource.Plugin, Attempt = status.Attempt, Code = applied.State == SystemApplyStates.Stale ? Stale : Refused,
                };
            var moved = NodeLifecycle.Apply(start.Node, status, move);
            if (!moved.Moved) return (null, false);

            // The failure's own words go on its status, so the way back tells the work what is in the system now.
            if (move.To == NodeStates.Failed)
            {
                var words = applied.State == SystemApplyStates.Stale ? SystemStaleWords.Of(start.Node.Id, plan.Plan, applied) : applied.Message;
                moved = moved with { Status = moved.Status with { Words = words } };
            }

            return (WorkPerformer.Kept(record, moved, $"apply-{applied.State}", _clock()), true);
        });
    }
}
