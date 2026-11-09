namespace Daoris.Driver;

/// <summary>
/// A skipped producer's readers: a skipped node carries no value, so a node whose mandatory input it feeds is skipped with it, its words
/// naming the producer, and judged again once the producer is no longer skipped.
/// </summary>
internal static class WireSkips
{
    /// <summary>The producer whose skip skips <paramref name="node"/>: the first source, by wire id, of a <c>data</c> wire into it whose receipt is a skip.</summary>
    public static string? Producer(NodeDeclaration node, IReadOnlyList<Wire> wires, IReadOnlyDictionary<string, WireReceipt> receipts) =>
        wires.Where(wire => wire.Kind == WireKinds.Data && wire.To is { Unit: null } to && to.Node == node.Id && wire.From is { Unit: null })
            .OrderBy(wire => wire.Id, StringComparer.Ordinal)
            .Select(wire => wire.From!.Node)
            .FirstOrDefault(from => receipts.GetValueOrDefault(from)?.Kind == WireReceiptKinds.Skipped);

    /// <summary>The skip <paramref name="node"/> takes with <paramref name="producer"/>, for this graph and run.</summary>
    public static NodeMove With(string producer, NodeScope scope) =>
        new(NodeStates.Skipped, NodeCause.Daoris)
        {
            Source = NodeSource.Path,
            Words = $"skipped with `{producer}`, which feeds its input: a skipped step carries no value.",
            Scope = scope,
        };

    /// <summary>Whether <paramref name="status"/>, a skip with its producer, lapses: no producer of its inputs is skipped any more.</summary>
    public static bool Lapses(NodeDeclaration node, NodeStatus status, IReadOnlyList<Wire> wires, IReadOnlyDictionary<string, WireReceipt> receipts) =>
        status is { State: NodeStates.Skipped, By: NodeSource.Path } && Producer(node, wires, receipts) is null;
}
