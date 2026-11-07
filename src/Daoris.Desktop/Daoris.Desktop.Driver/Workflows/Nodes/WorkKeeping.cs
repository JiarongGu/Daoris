using static Daoris.Driver.WorkPerformer;

namespace Daoris.Driver;

/// <summary>The performer's keeping of an evaluation's moves, and its start of a node that dispatches nothing.</summary>
internal sealed class WorkKeeping(NodeRecordFiles records, Func<DateTimeOffset> clock)
{
    /// <summary>
    /// Keeps an evaluation's moves on their units' records, where each node still stands as it
    /// stood when evaluated (<paramref name="before"/>): a status another writer moved is not written over. Compared and written in one
    /// hold of the record's lock.
    /// </summary>
    public IReadOnlyList<WorkPerformed> Keep(IReadOnlyDictionary<string, NodeStatus> before, WireEvaluated evaluated)
    {
        if (evaluated.Refusal is not null) return [];
        var done = new List<WorkPerformed>();
        var now = clock();
        foreach (var unit in evaluated.Statuses.Values.Select(status => status.Unit).Distinct(StringComparer.Ordinal))
        {
            var (refused, kept) = records.Edit<List<WorkPerformed>>(unit, record =>
            {
                var said = new List<WorkPerformed>();
                var statuses = new Dictionary<string, NodeStatus>(record.Statuses, StringComparer.Ordinal);
                var history = record.History.ToList();
                var fired = record.Fired.ToList();
                var changed = false;
                foreach (var (node, status) in evaluated.Statuses.Where(each => each.Value.Unit == unit))
                {
                    var held = statuses.GetValueOrDefault(node) ?? NodeLifecycle.Start(unit, node);
                    if (!before.TryGetValue(node, out var was) || !Same(held, was))
                    {
                        said.Add(new(unit, node, PerformedWhat.Refused, Moved));
                        continue;
                    }

                    if (Same(held, status) && statuses.ContainsKey(node)) continue;
                    statuses[node] = status;
                    changed = true;
                    foreach (var move in evaluated.Moves.Where(move => move.Node == node && move.Moved.Cleared is not null))
                    {
                        history.Add(new NodeHistory(now, move.Why, move.Moved.Cleared!));
                    }

                    // A back wire taken into this node is kept with its move, so it is taken once on its receipt.
                    fired.AddRange(evaluated.Backs.Where(back => back.Node == node && !fired.Contains(back.Key, StringComparer.Ordinal)).Select(back => back.Key));
                }

                return (changed ? record with { Statuses = statuses, History = history, Fired = fired } : null, said);
            });
            if (refused is not null) done.Add(new(unit, "", PerformedWhat.Refused, refused));
            else done.AddRange(kept!);
        }

        return done;
    }

    /// <summary>
    /// Starts a node that dispatches nothing (an observed built-in, read from its records at the next evaluation) on the start
    /// the evaluation proposed, its firings kept with it, where the record still holds the node as evaluated
    /// (<paramref name="evaluated"/>): one locked edit, so a start another writer made is not made again.
    /// </summary>
    public WorkPerformed Begin(NodeDeclaration node, WireStart start, NodeStatus evaluated)
    {
        if (start.Move.Binding is not { } binding) return new("", start.Node, PerformedWhat.Refused, NodeRefusal.NoBinding);
        var unit = binding.Unit;
        var now = clock();
        return records.Edited(unit, node.Id, record =>
        {
            var held = StatusOf(record, node.Id);
            if (!Same(held, evaluated)) return (null, new WorkPerformed(unit, node.Id, PerformedWhat.Refused, Moved));
            var moved = NodeLifecycle.Apply(node, held, start.Move);
            if (!moved.Moved) return (null, new WorkPerformed(unit, node.Id, PerformedWhat.Refused, moved.Refused));
            var fired = record.Fired.Concat(start.Firings.Select(firing => firing.Key)).Distinct(StringComparer.Ordinal).ToList();
            return (Kept(record, moved, "start", now) with { Fired = fired }, new WorkPerformed(unit, node.Id, PerformedWhat.Begun));
        });
    }
}
