namespace Daoris.Driver;

/// <summary>
/// Whether a work is delivering (§22 of the process steps' design), which the together press refuses a member for: its run has a <c>rebase</c>
/// reached at the member's tip whose commit feeds a step that has not settled at that commit, or the work has an unsettled delivery.
/// </summary>
internal static class RebaseDelivering
{
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
