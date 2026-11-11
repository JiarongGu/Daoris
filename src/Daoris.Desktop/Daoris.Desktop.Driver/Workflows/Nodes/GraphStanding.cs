namespace Daoris.Driver;

/// <summary>
/// A graph read as the save's order rules read it: its nodes, entries, own and ordering wires, landings, and what a hold judges the work before
/// (<see cref="Judged"/>: its landings; in a graph landing nothing, its reviews and the steps writing the work), on which each kind's
/// <see cref="INodeKind.Problems"/> judges too.
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
        IReadOnlyList<NodeDeclaration> landings = [.. byId.Values.Where(node => node.Kind == NodeKinds.Land)];
        IReadOnlyList<NodeDeclaration> carried = [.. byId.Values.Where(node => node.Kind == NodeKinds.Review || NodeKit.Of(node.Kind) is IWritesWork)];
        IReadOnlyList<NodeDeclaration> judged = landings.Count > 0 ? landings : carried;
        return new(byId, starts, local, [.. local.Where(wire => WireKinds.Ordered.Contains(wire.Kind))], landings) { Judged = judged };
    }

    public IReadOnlyList<NodeDeclaration> Judged { get; init; } = [];
}
