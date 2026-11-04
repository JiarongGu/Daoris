using static Daoris.Driver.WireEligible;
using static Daoris.Driver.WireEvaluation;
using static Daoris.Driver.WireLapsing;
using static Daoris.Driver.WireReceipts;

namespace Daoris.Driver;

/// <summary>The receipts an evaluation judges on: each node's current one, a decision's outcome receipt, and the nodes no route can start any more.</summary>
internal static class WireReceipts
{
    /// <summary>
    /// Each node's one current receipt, by node id: its reach, this run's skip, a spent failure or a rejection of an attempt started in
    /// this graph and run (FABRIC-WIRE1e: the attempt's scope, never the caller's run hashed into another run's outcome). The nodes in
    /// <paramref name="lapsing"/> have none, since they are about to be invalidated.
    /// </summary>
    internal static Dictionary<string, WireReceipt> Receipts(
        List<NodeDeclaration> nodes, Dictionary<string, NodeStatus> statuses, WireRun run, IEnumerable<string> lapsing)
    {
        var gone = lapsing.ToHashSet(StringComparer.Ordinal);
        var receipts = new Dictionary<string, WireReceipt>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            var status = statuses[node.Id];
            if (gone.Contains(node.Id)) continue;

            // FABRIC-RUN1f (the cutover design §9): an id is worked out from its status's own scope, a place's from the place and one written
            // before from the whole graph's digest and the run's old name, so a receipt kept before keeps its id and nothing resting on it lapses.
            var skipped = run.Own(node.Id, status.Scope?.GraphDigest);
            var (ours, bound) = Ours(node, status, run);
            WireReceipt? receipt = status.State switch
            {
                NodeStates.Reached when status.Current is { } accepted =>
                    new(accepted.Id, WireReceiptKinds.Reached, node.Id) { Acceptance = accepted, Outcome = accepted.Outcome },
                NodeStates.Skipped when NodeLifecycle.Skips(status, skipped) =>
                    new(Digest("skp-", skipped.GraphDigest, skipped.Run, status.Unit, node.Id), WireReceiptKinds.Skipped, node.Id),
                NodeStates.Failed when ours is not null && !Retries(node, status) =>
                    new(Digest("fld-", bound.GraphDigest, bound.Run, ours.Id), WireReceiptKinds.Failed, node.Id),
                NodeStates.Rejected when ours is not null => new(Digest("rej-", bound.GraphDigest, bound.Run, ours.Id), WireReceiptKinds.Rejected, node.Id),
                _ => null,
            };

            if (receipt is not null) receipts[node.Id] = receipt;
        }

        return receipts;
    }

    /// <summary>
    /// The receipt a <c>when</c> wire consumes (DECIDE1a): a digest of the decision's acceptance and its outcome (<c>out-</c>), so what rests on
    /// one route lapses once that outcome is lost or another attempt chooses another, while an <c>after</c> rests on the acceptance alone.
    /// </summary>
    public static string OutcomeReceipt(string acceptance, string outcome) => Digest("out-", acceptance, outcome);

    /// <summary>The ids a receipt is current under: its own, and a reached decision's outcome receipt beside it.</summary>
    internal static IEnumerable<string> Named(WireReceipt receipt) =>
        receipt.Outcome is { } outcome ? [receipt.Id, OutcomeReceipt(receipt.Id, outcome)] : [receipt.Id];

    /// <summary>
    /// The nodes no route can start any more (DECIDE1a), each with the outcome receipts that closed it: not started, behind a <c>when</c> whose
    /// decision reached another outcome, or behind a wire from such a node, by an <c>allOf</c> member, every member of an <c>anyOf</c>, or a data
    /// input; an <c>all</c> join's members never close it. Grown to a fixpoint, so it ends within the nodes.
    /// </summary>
    internal static Dictionary<string, IReadOnlyList<string>> NotTaken(
        List<NodeDeclaration> nodes, Dictionary<string, NodeStatus> statuses, IReadOnlyList<Wire> wires, Dictionary<string, WireReceipt> receipts)
    {
        var closed = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        IReadOnlyList<string>? Closing(Wire wire)
        {
            if (wire.From is not { Unit: null } from) return null;
            if (closed.TryGetValue(from.Node, out var by)) return by;
            if (wire.Kind != WireKinds.When || receipts.GetValueOrDefault(from.Node) is not { Kind: WireReceiptKinds.Reached, Outcome: { } outcome } receipt) return null;
            return string.Equals(outcome, wire.Outcome, StringComparison.Ordinal) ? null : [OutcomeReceipt(receipt.Id, outcome)];
        }

        IReadOnlyList<string>? ClosedBy(NodeDeclaration node)
        {
            var into = wires.Where(wire => wire.To is { Unit: null } to && to.Node == node.Id).OrderBy(wire => wire.Id, StringComparer.Ordinal).ToList();
            if (into.Where(wire => wire.Kind == WireKinds.Data).Select(Closing).FirstOrDefault(each => each is not null) is { } input) return input;
            foreach (var group in NodeWires.Groups(node.Id, wires))
            {
                var members = into.Where(wire => group.Wires.Contains(wire.Id, StringComparer.Ordinal) && !(node.Kind == NodeKinds.All && wire.Kind == WireKinds.After))
                    .Select(Closing).ToList();
                if (members.Count == 0) continue;
                if (group.Mode != WireGroupModes.AnyOf && members.FirstOrDefault(each => each is not null) is { } member) return member;
                if (group.Mode == WireGroupModes.AnyOf && members.All(each => each is not null))
                {
                    return [.. members.SelectMany(each => each!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
                }
            }

            return null;
        }

        for (var found = true; found;)
        {
            found = false;
            foreach (var node in nodes)
            {
                if (closed.ContainsKey(node.Id) || statuses[node.Id].State is not (NodeStates.Blocked or NodeStates.Ready or NodeStates.Invalidated)) continue;
                if (ClosedBy(node) is not { } by) continue;
                closed[node.Id] = by;
                found = true;
            }
        }

        return closed;
    }

    /// <summary>
    /// The nodes no route can start any more given the outcomes reached on <paramref name="statuses"/> (DECIDE1c, the run's view): what an
    /// evaluation reports as <see cref="WireEvaluated.NotTaken"/>, read from a record alone, since a route closes on a reach's outcome, which needs
    /// no run's scope. A node with no status is not reached yet.
    /// </summary>
    public static IReadOnlyList<string> NotTakenOn(
        IReadOnlyList<NodeDeclaration> nodes, IReadOnlyList<Wire> wires, IReadOnlyDictionary<string, NodeStatus> statuses, string unit)
    {
        var declared = nodes.DistinctBy(node => node.Id, StringComparer.Ordinal).ToList();
        var now = declared.ToDictionary(node => node.Id, node => statuses.GetValueOrDefault(node.Id) ?? NodeLifecycle.Start(unit, node.Id), StringComparer.Ordinal);
        var reached = new Dictionary<string, WireReceipt>(StringComparer.Ordinal);
        foreach (var node in declared)
        {
            if (now[node.Id] is { State: NodeStates.Reached, Current: { } accepted }) reached[node.Id] = new(accepted.Id, WireReceiptKinds.Reached, node.Id) { Outcome = accepted.Outcome };
        }

        return [.. NotTaken(declared, now, wires, reached).Keys.Order(StringComparer.Ordinal)];
    }
}
