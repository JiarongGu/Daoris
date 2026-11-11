namespace Daoris.Driver;

/// <summary>
/// The apply of a system whose adapter declares its precondition <c>none</c> (design §5.5): the system guards no write, so the lease
/// keeps one applier and this reads each resource just before writing it and again after. A resource planned alone at
/// <see cref="HookPoints.PlanRead"/> is the read: no operation says the system holds it, one names its present value and version.
/// A version other than the plan's is <c>stale</c>, nothing of it written; a resource read back still to change is
/// <c>changed</c> while applying, by another writer.
/// </summary>
public sealed class SystemReadBack(IHookChannel channel, SystemPlace place)
{
    /// <summary><paramref name="plan"/>'s operations, each read, written alone at <see cref="HookPoints.ApplyPerform"/> and read back, in order.</summary>
    public async Task<SystemApplied> ApplyAsync(SystemChangeSet plan, CancellationToken ct)
    {
        var applied = new List<string>();
        foreach (var operation in plan.Operations)
        {
            if (await ReadAsync(operation, ct).ConfigureAwait(false) is not { } before) continue;
            if (before.Precondition.Version != operation.Precondition.Version)
            {
                return new SystemApplied(SystemApplyStates.Stale, applied, operation.Resource, [PresentOf(before)], null);
            }

            var answer = await channel.SystemAsync(HookPoints.ApplyPerform, SystemWire.Apply(place.System, plan with { Operations = [operation] }), ct).ConfigureAwait(false);
            var written = SystemWire.ReadApplied(answer) ?? throw SystemAdapter.NotApplied(place, answer);
            if (written.State is SystemApplyStates.Stale or SystemApplyStates.Failed) return written with { Applied = [.. applied, .. written.Applied] };

            if (await ReadAsync(operation, ct).ConfigureAwait(false) is { } after)
            {
                return new SystemApplied(SystemApplyStates.Changed, applied, operation.Resource, [PresentOf(after)], null);
            }

            applied.Add(operation.Resource);
        }

        return applied.Count == 0
            ? new SystemApplied(SystemApplyStates.Unchanged, [], null, [], null)
            : new SystemApplied(SystemApplyStates.Applied, applied, null, [], null);
    }

    /// <summary>What <paramref name="operation"/> would still change in the system as it stands; null where the system holds it.</summary>
    private async Task<SystemOperation?> ReadAsync(SystemOperation operation, CancellationToken ct)
    {
        var intent = new SystemIntent(operation.Resource, operation.After, operation.Op == "delete");
        var answer = await channel.SystemAsync(HookPoints.PlanRead, SystemWire.Plan(place.System, [intent]), ct).ConfigureAwait(false);
        var read = SystemWire.ReadPlan(answer) ?? throw SystemAdapter.NotPlanned(place, answer);
        return read.FirstOrDefault(each => each.Resource == operation.Resource);
    }

    /// <summary>A resource as a read found it: absent where it is still to be created, else its value before and its version.</summary>
    private static SystemPresent PresentOf(SystemOperation read) => read.Op == "create"
        ? new SystemPresent(read.Resource, false, null, null)
        : new SystemPresent(read.Resource, true, read.Before, read.Precondition.Version);
}
