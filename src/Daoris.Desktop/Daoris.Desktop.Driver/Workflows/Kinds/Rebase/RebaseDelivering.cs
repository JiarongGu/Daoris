namespace Daoris.Driver;

/// <summary>
/// Whether a work is delivering (§22 of the process steps' design), which the together press refuses a member for: its run has a <c>rebase</c>
/// reached at the member's tip whose commit feeds a step that has not settled at that commit, or the work has an unsettled delivery.
/// </summary>
public static class RebaseDelivering
{
    /// <summary>
    /// Why <paramref name="member"/> at <paramref name="tip"/> is delivering, read from <paramref name="home"/>: an unsettled delivery of its unit,
    /// or a run not ended whose <c>rebase</c> is reached on its work at that tip, read over the graph its run is bound to. Null where it may be bound.
    /// </summary>
    public static string? Of(string home, TogetherCandidate member, string tip)
    {
        if (member.Unit is not { } unit) return null;
        if (new DeliveryStore(home).OfUnit(unit).Any(each => each.Settled is null)) return Of(member.Session, null, tip, deliveryUnsettled: true);
        var (records, _) = new NodeRecordFiles(home).All();
        var store = new StampedWorkflows(new WorkflowFiles(home), home);
        var runs = new WorkflowRunFiles(home);
        foreach (var record in records.Where(each => each.Ended is null && RunUnits.Is(each.Unit)))
        {
            if (!record.Statuses.Values.Any(status => status.Current is { } on && on.Observes == unit && string.Equals(on.SubjectRevision, tip, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (WorkflowRunNodes.VersionOf(store, runs, record.Unit[RunUnits.Prefix.Length..])?.Graph is not { } graph) continue;
            if (Of(member.Session, (graph.Nodes, graph.Wires, record.Statuses), tip, deliveryUnsettled: false) is { } delivering) return delivering;
        }

        return null;
    }

    /// <summary>Why the member may not be bound now, said; null where it may be.</summary>
    /// <param name="steps">The run's graph and its node statuses, or null where the work has no run.</param>
    public static string? Of(
        string session, (IReadOnlyList<NodeDeclaration> Nodes, IReadOnlyList<Wire> Wires, IReadOnlyDictionary<string, NodeStatus> Statuses)? steps, string tip,
        bool deliveryUnsettled)
    {
        if (deliveryUnsettled) return $"`{session}` is delivering: bring it in once its delivery settles";
        if (steps is not { } run) return null;
        foreach (var rebase in run.Nodes.Where(node => node.Kind == NodeKinds.Rebase))
        {
            if (run.Statuses.GetValueOrDefault(rebase.Id) is not { State: NodeStates.Reached, Current: { } reached }
                || !string.Equals(reached.SubjectRevision, tip, StringComparison.OrdinalIgnoreCase)
                || reached.Outputs.Values.FirstOrDefault(port => port.Schema == WireValues.Commit) is not { } commit)
            {
                continue;
            }

            if (Fed(rebase.Id, run.Wires).Any(fed => !Settled(run.Statuses.GetValueOrDefault(fed), commit.Revision)))
            {
                return $"`{session}` is delivering: bring it in once its merge settles";
            }
        }

        return null;
    }

    /// <summary>Whether a step the commit feeds has settled at it: reached at the commit, or failed, skipped or stopped.</summary>
    private static bool Settled(NodeStatus? status, string? commit) => status switch
    {
        { State: NodeStates.Reached, Current: { } reached } => string.Equals(reached.SubjectRevision, commit, StringComparison.OrdinalIgnoreCase),
        { State: NodeStates.Failed or NodeStates.Skipped or NodeStates.Stopped } => true,
        _ => false,
    };

    private static HashSet<string> Fed(string node, IReadOnlyList<Wire> wires)
    {
        var fed = new HashSet<string>(StringComparer.Ordinal);
        var next = new Queue<string>([node]);
        while (next.TryDequeue(out var from))
        {
            foreach (var wire in wires.Where(wire => wire.Kind == WireKinds.Data && wire.From is { Unit: null } source && source.Node == from && wire.To.Unit is null))
            {
                if (fed.Add(wire.To.Node)) next.Enqueue(wire.To.Node);
            }
        }

        return fed;
    }
}
