namespace Daoris.Driver;

/// <summary>
/// A decision's closed route: a step a reached decision's <c>when</c> wire leads to on another outcome is passed on the record, its words
/// the decision's line ("Not needed: proposed by …, confirmed by …"), so what follows it goes on as after any skip; it takes the line's newer
/// words as the owner confirms, and is judged again once a changed answer opens its route. A step closed only behind another passed one is
/// not passed: it reads that step's skip. Pure.
/// </summary>
internal static class WireDecisions
{
    /// <summary>
    /// The decision whose answer closed <paramref name="node"/>'s route: one wired into it by <c>when</c> whose reached outcome's receipt is
    /// among those that closed it (<paramref name="untaken"/>); null where no decision's did.
    /// </summary>
    public static NodeDeclaration? Closer(
        NodeDeclaration node, IReadOnlyList<NodeDeclaration> nodes, IReadOnlyList<Wire> wires, IReadOnlyDictionary<string, WireReceipt> receipts,
        IReadOnlyDictionary<string, IReadOnlyList<string>> untaken) =>
        untaken.TryGetValue(node.Id, out var closing)
            ? Deciding(node, nodes, wires, receipts).FirstOrDefault(each => closing.Contains(WireReceipts.OutcomeReceipt(each.Receipt.Id, each.Receipt.Outcome!), StringComparer.Ordinal)).Decision
            : null;

    /// <summary>
    /// What a decision does to <paramref name="node"/> this evaluation: passes it where its answer closed its route, or rewords a step it
    /// passed whose decision's line changed; null where neither.
    /// </summary>
    public static (NodeMove Move, string Decision)? Move(
        NodeDeclaration node, NodeStatus status, IReadOnlyList<NodeDeclaration> nodes, IReadOnlyList<Wire> wires, IReadOnlyDictionary<string, NodeStatus> statuses,
        IReadOnlyDictionary<string, WireReceipt> receipts, IReadOnlyDictionary<string, IReadOnlyList<string>> untaken, NodeScope scope)
    {
        var decision = status.State switch
        {
            NodeStates.Blocked or NodeStates.Ready or NodeStates.Invalidated => Closer(node, nodes, wires, receipts, untaken),
            NodeStates.Skipped when status.By == NodeSource.Decision => Deciding(node, nodes, wires, receipts)
                .FirstOrDefault(each => wires.Any(wire => Into(wire, node, each.Decision) && wire.Outcome != each.Receipt.Outcome)).Decision,
            _ => null,
        };
        if (decision is null || statuses.GetValueOrDefault(decision.Id)?.Words is not { Length: > 0 } line || (status.State == NodeStates.Skipped && status.Words == line)) return null;
        return (new NodeMove(NodeStates.Skipped, NodeCause.Daoris) { Source = NodeSource.Decision, Words = line, Scope = scope }, decision.Id);
    }

    /// <summary>
    /// Whether <paramref name="status"/>, a step a decision passed, lapses: seen as not started, no decision's answer closes its route any
    /// more, so it is judged again.
    /// </summary>
    public static bool Lapses(
        NodeDeclaration node, NodeStatus status, List<NodeDeclaration> nodes, Dictionary<string, NodeStatus> statuses, IReadOnlyList<Wire> wires,
        Dictionary<string, WireReceipt> receipts)
    {
        if (status is not { State: NodeStates.Skipped, By: NodeSource.Decision }) return false;
        var seen = new Dictionary<string, NodeStatus>(statuses, StringComparer.Ordinal) { [node.Id] = status with { State = NodeStates.Blocked } };
        return Closer(node, nodes, wires, receipts, WireReceipts.NotTaken(nodes, seen, wires, receipts)) is null;
    }

    /// <summary>The reached decisions wired into <paramref name="node"/> by <c>when</c>, each with its receipt, in wire-id order.</summary>
    private static IEnumerable<(NodeDeclaration Decision, WireReceipt Receipt)> Deciding(
        NodeDeclaration node, IReadOnlyList<NodeDeclaration> nodes, IReadOnlyList<Wire> wires, IReadOnlyDictionary<string, WireReceipt> receipts) =>
        wires.Where(wire => wire.Kind == WireKinds.When && wire.To is { Unit: null } to && to.Node == node.Id && wire.From is { Unit: null })
            .OrderBy(wire => wire.Id, StringComparer.Ordinal)
            .Select(wire => nodes.FirstOrDefault(each => each.Id == wire.From!.Node))
            .Where(from => from is not null && TaskDecisions.Is(from))
            .Select(from => (Decision: from!, Receipt: receipts.GetValueOrDefault(from!.Id)))
            .Where(each => each.Receipt is { Kind: WireReceiptKinds.Reached, Outcome: not null })
            .Select(each => (each.Decision, each.Receipt!));

    private static bool Into(Wire wire, NodeDeclaration node, NodeDeclaration from) =>
        wire.Kind == WireKinds.When && wire.To is { Unit: null } to && to.Node == node.Id && wire.From is { Unit: null } source && source.Node == from.Id;
}
