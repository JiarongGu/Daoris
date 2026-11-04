namespace Daoris.Driver;

/// <summary>What a graph's or a fragment's JSON declares, each part as written; an absent list is empty, an absent end or cleanup null.</summary>
internal sealed record GraphParts(int SchemaVersion, List<NodeDeclaration> Nodes, List<Wire> Wires, List<string> Entries, string? End, GraphCleanup? Cleanup);
