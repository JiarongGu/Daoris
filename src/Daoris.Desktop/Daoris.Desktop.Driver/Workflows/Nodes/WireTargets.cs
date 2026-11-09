namespace Daoris.Driver;

/// <summary>
/// The bound on a target's move: a step with a target (<see cref="ITargets"/>) is bound to the target's tip it read, and a move of the
/// target rebinds it only while nothing it feeds, directly or through another step, is active or reached. Once what it feeds has started, the
/// target moving pulls nothing from under it.
/// </summary>
internal static class WireTargets
{
    /// <summary>
    /// Why <paramref name="node"/> lapses on its target, or null: bound to a tip the target no longer stands at, with nothing it feeds active
    /// or reached in <paramref name="statuses"/>.
    /// </summary>
    public static (string Why, string? Detail)? Lapse(
        NodeDeclaration node, NodeStatus status, WireRun run, IReadOnlyDictionary<string, NodeStatus> statuses, IReadOnlyList<Wire> wires)
    {
        if (!NodeStates.Bound.Contains(status.State) || status.Binding?.Target is not { } bound || !run.Targets.TryGetValue(node.Id, out var target)) return null;
        if (bound.Ref == target.Ref && string.Equals(bound.Revision, target.Revision, StringComparison.OrdinalIgnoreCase)) return null;
        return Fed(node.Id, wires).Any(fed => statuses.GetValueOrDefault(fed)?.State is NodeStates.Active or NodeStates.Reached)
            ? null
            : (WireWhy.Lapsed, NodeLapse.Target);
    }

    /// <summary>
    /// The targets the pass hands its steps: each node whose kind names a target, at the tip <paramref name="tip"/> reads; none for a node
    /// whose target's tip is not read, which binds with none.
    /// </summary>
    public static Dictionary<string, NodePort> Read(IEnumerable<NodeDeclaration> nodes, Func<string, string?>? tip)
    {
        var targets = new Dictionary<string, NodePort>(StringComparer.Ordinal);
        if (tip is null) return targets;
        foreach (var node in nodes)
        {
            if (NodeKit.Of(node.Kind) is ITargets targeting && targeting.Target(node) is { } named && tip(named) is { } at)
            {
                targets[node.Id] = new NodePort(WireValues.Ref, named, at, null);
            }
        }

        return targets;
    }

    /// <summary>Every node <paramref name="node"/>'s values reach along <c>data</c> wires, however far.</summary>
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
