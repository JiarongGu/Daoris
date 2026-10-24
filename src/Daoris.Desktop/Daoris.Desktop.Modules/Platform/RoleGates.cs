namespace Daoris.Desktop;

/// <summary>
/// One scope a gate is held on (STEWARDROLE1a-ii, D209 §5.4): a workspace, a repository or a quest, by its name. The fixed order
/// every holder takes several in is <see cref="Order"/>: workspace, then repository, then quest, each by its name, ordinally and
/// ignoring case, so no two holders ever wait on each other in a circle.
/// </summary>
public sealed record RoleScope(string Kind, string Name)
{
    /// <summary>The scope's key in the run's memory, as a mark and a gate are kept: <c>kind:NAME</c>.</summary>
    public string Key => $"{Kind}:{Name.ToUpperInvariant()}";

    /// <summary>Workspace first, then repository, then quest; anything else after them.</summary>
    public int Rank => Kind switch
    {
        OwnerKinds.Workspace => 0,
        OwnerKinds.Repository => 1,
        OwnerKinds.Quest => 2,
        _ => 3,
    };

    /// <summary><paramref name="scopes"/> once each, in the fixed order.</summary>
    public static IReadOnlyList<RoleScope> Order(IEnumerable<RoleScope> scopes) =>
    [
        .. scopes.GroupBy(each => each.Key, StringComparer.Ordinal).Select(each => each.First())
            .OrderBy(each => each.Rank).ThenBy(each => each.Key, StringComparer.Ordinal),
    ];
}

/// <summary>
/// The run's gates (STEWARDROLE1a-ii, D209 §5.4): one per workspace, repository and quest, the run's memory's own and never the judge's
/// lock. A role's act, and each consumer of its pending operations, holds the gates of its target and its dependencies
/// <b>alone</b> across its check and its handler; an owner's press holds the gates of every scope it resolves to <b>beside the owner's
/// other presses</b> across its mark and its handler. So an owner's act and a role's never interleave on a scope, either way round,
/// while the owner's own presses never wait on each other, as they did not before the gates (a reading of §5.4, whose gate exists to
/// order the owner against a role).
/// </summary>
/// <remarks>Every scope is taken in <see cref="RoleScope.Order"/>; a waiter that is cancelled takes nothing and leaves the queue.</remarks>
public sealed class RoleGates
{
    private readonly Lock _lock = new();
    private readonly Dictionary<string, Gate> _gates = new(StringComparer.Ordinal);

    private sealed class Gate
    {
        public int Shared;
        public bool Alone;
        public readonly LinkedList<(bool Alone, TaskCompletionSource Granted)> Waiting = new();
    }

    /// <summary>
    /// Every gate of <paramref name="scopes"/> taken in the fixed order, <paramref name="alone"/> for a role's act, beside the owner's
    /// others otherwise; released, in the reverse order, as the answer is disposed. A cancel while waiting releases what was taken.
    /// </summary>
    public async Task<IAsyncDisposable> EnterAsync(IEnumerable<RoleScope> scopes, bool alone, CancellationToken ct)
    {
        var ordered = RoleScope.Order(scopes);
        var taken = new List<string>(ordered.Count);
        try
        {
            foreach (var scope in ordered)
            {
                await TakeAsync(scope.Key, alone, ct).ConfigureAwait(false);
                taken.Add(scope.Key);
            }
        }
        catch
        {
            Release(taken, alone);
            throw;
        }

        return new Held(this, taken, alone);
    }

    /// <summary>Whether a gate on <paramref name="scope"/> is held, by a role's act or an owner's press: a test's look.</summary>
    public bool IsHeld(RoleScope scope)
    {
        lock (_lock) return _gates.TryGetValue(scope.Key, out var gate) && (gate.Alone || gate.Shared > 0);
    }

    private Task TakeAsync(string key, bool alone, CancellationToken ct)
    {
        TaskCompletionSource granted;
        LinkedListNode<(bool, TaskCompletionSource)> node;
        lock (_lock)
        {
            if (!_gates.TryGetValue(key, out var gate)) _gates[key] = gate = new Gate();
            if (gate.Waiting.Count == 0 && !gate.Alone && (!alone || gate.Shared == 0))
            {
                if (alone) gate.Alone = true;
                else gate.Shared++;
                return Task.CompletedTask;
            }

            granted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            node = gate.Waiting.AddLast((alone, granted));
        }

        if (!ct.CanBeCanceled) return granted.Task;
        return WaitAsync(key, node, granted, ct);
    }

    private async Task WaitAsync(string key, LinkedListNode<(bool, TaskCompletionSource)> node, TaskCompletionSource granted, CancellationToken ct)
    {
        await using var registration = ct.Register(() =>
        {
            lock (_lock)
            {
                // Granted already: the take stands, and the caller's catch releases it.
                if (granted.Task.IsCompleted || node.List is null) return;
                node.List.Remove(node);
                if (_gates.TryGetValue(key, out var gate)) Grant(gate);
            }

            granted.TrySetCanceled(ct);
        }).ConfigureAwait(false);
        await granted.Task.ConfigureAwait(false);
    }

    private void Release(IReadOnlyList<string> keys, bool alone)
    {
        lock (_lock)
        {
            for (var at = keys.Count - 1; at >= 0; at--)
            {
                if (!_gates.TryGetValue(keys[at], out var gate)) continue;
                if (alone) gate.Alone = false;
                else gate.Shared--;
                Grant(gate);
                if (!gate.Alone && gate.Shared == 0 && gate.Waiting.Count == 0) _gates.Remove(keys[at]);
            }
        }
    }

    // Under the lock: the waiters at the head the gate now admits, in their order.
    private static void Grant(Gate gate)
    {
        while (gate.Waiting.First is { } first && !gate.Alone)
        {
            var (alone, granted) = first.Value;
            if (alone && gate.Shared > 0) return;
            gate.Waiting.RemoveFirst();
            if (alone) gate.Alone = true;
            else gate.Shared++;
            granted.TrySetResult();
            if (alone) return;
        }
    }

    private sealed class Held(RoleGates gates, IReadOnlyList<string> keys, bool alone) : IAsyncDisposable
    {
        private int _released;

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0) gates.Release(keys, alone);
            return ValueTask.CompletedTask;
        }
    }
}
