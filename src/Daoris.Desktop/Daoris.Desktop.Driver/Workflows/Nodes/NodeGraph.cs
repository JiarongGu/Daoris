using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>
/// A graph (FABRIC-GRAPH1; the fabric events design §E7): <c>{ schemaVersion, nodes, wires, entries, end, cleanup }</c>. Its nodes are
/// <see cref="NodeDeclaration"/>s (NODE1), its wires <see cref="Wire"/>s (WIRE1a); <see cref="WireEvaluation"/> evaluates it (WIRE1b).
/// Each list is a copy of what was set, so a digest taken of it never moves with a caller's collection (NODE1d's rule).
/// </summary>
/// <param name="Entries">The nodes evaluation starts from: none takes a wire from another node of the graph.</param>
/// <param name="End">The node whose reach ends the work (§C2's <i>end</i>); a failure sink may end without reaching it.</param>
public sealed record NodeGraph(IReadOnlyList<NodeDeclaration> Nodes, IReadOnlyList<Wire> Wires, IReadOnlyList<string> Entries, string End)
{
    public IReadOnlyList<NodeDeclaration> Nodes { get; init; } = Nodes.ToImmutableArray();

    public IReadOnlyList<Wire> Wires { get; init; } = Wires.ToImmutableArray();

    public IReadOnlyList<string> Entries { get; init; } = Entries.ToImmutableArray();

    /// <summary>The graph's schema: <see cref="NodeGraphs.SchemaVersion"/>. D157's versions are schema 1, decoded and never saved as graphs.</summary>
    public int SchemaVersion { get; init; } = NodeGraphs.SchemaVersion;

    public GraphCleanup Cleanup { get; init; } = GraphCleanup.Held;
}
