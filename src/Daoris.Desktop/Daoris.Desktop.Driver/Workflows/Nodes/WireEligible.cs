using static Daoris.Driver.WireEligible;
using static Daoris.Driver.WireEvaluation;
using static Daoris.Driver.WireLapsing;

namespace Daoris.Driver;

/// <summary>One node's eligibility on the receipts: its inputs, its join members and its groups.</summary>
internal static class WireEligible
{
    /// <summary>
    /// One node's eligibility on <paramref name="receipts"/>: its data inputs, its join members, and its groups (§E3, §E7); and the binding a
    /// start takes on its subject, none while it is withheld there (FABRIC-CUTOVER1a). An input from another subject is resolved on its producer's
    /// reach as any is: the producer's acceptance was judged on its own subject in step 1.
    /// </summary>
    internal static WireEligibility Eligible(
        NodeDeclaration node, NodeStatus status, IReadOnlyList<Wire> wires, Dictionary<string, WireReceipt> receipts, WireRun run, Subjected subjected,
        Dictionary<string, IReadOnlyList<string>> untaken)
    {
        var join = node.Kind == NodeKinds.All;
        var into = wires.Where(wire => wire.To is { Unit: null } to && to.Node == node.Id).OrderBy(wire => wire.Id, StringComparer.Ordinal).ToList();
        var byId = into.GroupBy(wire => wire.Id, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var waiting = new List<string>();
        var consumed = new List<string>();
        var firings = new List<WireFiring>();
        var witnesses = new Dictionary<string, string>(StringComparer.Ordinal);
        var inputs = new Dictionary<string, NodePort>(StringComparer.Ordinal);
        WireReceipt? From(Wire wire) => wire.From is { Unit: null } from && receipts.TryGetValue(from.Node, out var receipt) ? receipt : null;

        // A data wire is an input, mandatory whatever the groups say: a current reach exposing the port at the input's schema.
        foreach (var wire in into.Where(wire => wire.Kind == WireKinds.Data))
        {
            var port = From(wire) is { Kind: WireReceiptKinds.Reached, Acceptance: { } accepted } && wire.From!.Port is { } name
                ? name == WireEnd.Evidence ? accepted.Evidence : accepted.Outputs.GetValueOrDefault(name)
                : null;
            if (port is not null && wire.To.Port is { } input && node.Inputs.TryGetValue(input, out var schema) && port.Schema == schema) inputs[input] = port;
            else waiting.Add(wire.Id);
        }

        // An all join's members are its after wires' sources: every one reached or skipped, or one failed (§E7, the group's policy). DECIDE1a: a
        // member no route takes is settled as a skip, on the outcome receipts that closed it, so another outcome lapses the join.
        if (join)
        {
            var members = into.Where(wire => wire.Kind == WireKinds.After).ToList();
            var failed = members.Select(From).FirstOrDefault(receipt => receipt?.Kind == WireReceiptKinds.Failed);
            if (failed is not null) consumed.Add(failed.Id);
            else
            {
                foreach (var member in members)
                {
                    if (From(member) is { Kind: WireReceiptKinds.Reached or WireReceiptKinds.Skipped } settled) consumed.Add(settled.Id);
                    else if (member.From is { Unit: null } from && untaken.TryGetValue(from.Node, out var closing)) consumed.AddRange(closing);
                    else waiting.Add(member.Id);
                }
            }
        }

        (string? Receipt, WireFiring? Firing) Satisfied(Wire wire)
        {
            var receipt = From(wire);
            switch (wire.Kind)
            {
                case WireKinds.After when receipt?.Kind is WireReceiptKinds.Reached or WireReceiptKinds.Skipped:
                case WireKinds.Failed when receipt?.Kind == WireReceiptKinds.Failed:
                case WireKinds.Rejected when receipt?.Kind == WireReceiptKinds.Rejected:
                    return (receipt!.Id, null);

                // DECIDE1a: a reached decision's chosen outcome alone, on its own receipt; a lost outcome never routes.
                case WireKinds.When when receipt is { Kind: WireReceiptKinds.Reached, Outcome: { } outcome } && string.Equals(outcome, wire.Outcome, StringComparison.Ordinal):
                    return (OutcomeReceipt(receipt.Id, outcome), null);

                case WireKinds.Trigger when receipt?.Kind == WireReceiptKinds.Reached:
                    // FABRIC-RUN1f: a firing kept before was keyed by the graph and the run's old name, so it is looked for under both rules.
                    var key = FiringKey(run, node.Id, wire.Id, receipt.Id);
                    var rests = status.Binding?.PrerequisiteReceipts.Contains(receipt.Id, StringComparer.Ordinal) == true;
                    var spent = rests || run.Fired.Contains(key) || (run.Places.ContainsKey(node.Id) && run.Fired.Contains(LegacyKey(run, wire.Id, receipt.Id)));
                    var firing = spent ? null : new WireFiring(key, wire.Id, receipt.Id, node.Id);
                    return status.State == NodeStates.Ready || (rests && NodeStates.Bound.Contains(status.State)) || !spent ? (receipt.Id, firing) : (null, null);

                default:
                    return (null, null);
            }
        }

        foreach (var group in NodeWires.Groups(node.Id, wires))
        {
            var members = group.Wires.Where(id => byId.ContainsKey(id) && !(join && byId[id].Kind == WireKinds.After)).ToList();
            if (members.Count == 0) continue;
            var judged = members.Select(id => (Wire: id, Said: Satisfied(byId[id]))).ToList();
            if (group.Mode == WireGroupModes.AnyOf)
            {
                // The witness a started attempt rests on is kept while it holds; otherwise the first by wire id.
                var kept = NodeStates.Bound.Contains(status.State) && status.Binding is { } binding
                    ? judged.FirstOrDefault(each => each.Said.Receipt is { } receipt && binding.PrerequisiteReceipts.Contains(receipt, StringComparer.Ordinal))
                    : default;
                var witness = kept.Wire is not null ? kept : judged.FirstOrDefault(each => each.Said.Receipt is not null);
                if (witness.Wire is null)
                {
                    waiting.AddRange(members);
                    continue;
                }

                witnesses[group.Id ?? witness.Wire] = witness.Wire;
                consumed.Add(witness.Said.Receipt!);
                if (witness.Said.Firing is { } firing) firings.Add(firing);
                continue;
            }

            foreach (var (wire, said) in judged)
            {
                if (said.Receipt is null) waiting.Add(wire);
                else
                {
                    consumed.Add(said.Receipt);
                    if (said.Firing is { } firing) firings.Add(firing);
                }
            }
        }

        var eligible = waiting.Count == 0;
        var bound = eligible && subjected.Withheld is null;
        return new WireEligibility(node.Id, eligible)
        {
            Witnesses = witnesses,
            Receipts = [.. consumed.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            Waiting = [.. waiting.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
            Binding = bound
                ? new NodeAcceptance(status.Unit, subjected.Revision, run.GraphOf(node.Id), node.Id, node.Version, status.Attempt + 1)
                {
                    // Named only where it is not the record's unit, so every binding made as before keeps its id.
                    Subject = subjected.Subject == status.Unit ? null : subjected.Subject,
                    Inputs = inputs,
                    PrerequisiteReceipts = [.. consumed.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)],
                }
                : null,
            Withheld = subjected.Withheld,
            Waits = into.FirstOrDefault(wire => wire.Kind == WireKinds.Wait)?.Waits,
            Firings = bound ? firings : [],
        };
    }
}
