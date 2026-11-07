using System.Collections.Immutable;

namespace Daoris.Driver;

/// <summary>
/// A graph: <c>{ schemaVersion, nodes, wires, entries, end, cleanup }</c>. Its nodes are
/// <see cref="NodeDeclaration"/>s, its wires <see cref="Wire"/>s; <see cref="WireEvaluation"/> evaluates it.
/// Each list is a copy of what is set, so a digest taken of it never moves with a caller's collection.
/// </summary>
/// <param name="Entries">The nodes evaluation starts from: none takes a wire from another node of the graph.</param>
/// <param name="End">The node whose reach ends the work ; a failure sink may end without reaching it.</param>
public sealed record NodeGraph(IReadOnlyList<NodeDeclaration> Nodes, IReadOnlyList<Wire> Wires, IReadOnlyList<string> Entries, string End)
{
    public IReadOnlyList<NodeDeclaration> Nodes { get; init; } = Nodes.ToImmutableArray();

    public IReadOnlyList<Wire> Wires { get; init; } = Wires.ToImmutableArray();

    public IReadOnlyList<string> Entries { get; init; } = Entries.ToImmutableArray();

    /// <summary>The graph's schema: <see cref="NodeGraphs.SchemaVersion"/>. Named versions are schema 1, decoded and never saved as graphs.</summary>
    public int SchemaVersion { get; init; } = NodeGraphs.SchemaVersion;

    public GraphCleanup Cleanup { get; init; } = GraphCleanup.Held;
}
