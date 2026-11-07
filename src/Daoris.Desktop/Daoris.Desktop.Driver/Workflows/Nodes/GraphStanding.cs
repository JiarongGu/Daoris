namespace Daoris.Driver;

/// <summary>
/// A graph read the way the save's order rules read it: its nodes by id, the entries that name a node, the wires between its own nodes, the ones
/// that order a run, and its landings. The graph's save and each kind's own rules (<see cref="INodeKind.Problems"/>) judge on the same reading.
/// </summary>
internal sealed record GraphStanding(
    Dictionary<string, NodeDeclaration> ById, IReadOnlyList<string> Starts, IReadOnlyList<Wire> Local, IReadOnlyList<Wire> Ordered, IReadOnlyList<NodeDeclaration> Landings)
{
    public static GraphStanding Of(NodeGraph graph)
    {
        var byId = new Dictionary<string, NodeDeclaration>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes) byId.TryAdd(node.Id, node);
        // A `back` is a way back, which NodeWires judges; no rule reads it, as an entry's wire, a reach or an order.
        var local = graph.Wires
            .Where(wire => wire.Kind is not (WireKinds.Wait or WireKinds.Back) && WireKinds.All.Contains(wire.Kind) && wire.From is { Unit: null } from && byId.ContainsKey(from.Node)
                && wire.To.Unit is null && byId.ContainsKey(wire.To.Node))
            .ToList();
        var starts = graph.Entries.Where(byId.ContainsKey).Distinct(StringComparer.Ordinal).ToList();
        return new(byId, starts, local, [.. local.Where(wire => WireKinds.Ordered.Contains(wire.Kind))],
            [.. byId.Values.Where(node => node.Kind == NodeKinds.Land)]);
    }
}
