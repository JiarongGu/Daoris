namespace Daoris.Driver;

/// <summary>
/// The save's refusal of what arrives: a kind declared before it is offered (<see cref="NodeKindAttribute.Arriving"/>), and a port or an outcome
/// a kind already offered declares before the change that wires it (<see cref="NodeArrivals"/>), so no saved graph holds one until the change
/// that offers it removes the mark. An import is a save; a graph read back from a file is never refused here.
/// </summary>
internal static class ArrivingKinds
{
    /// <summary>
    /// One refusal for each node of <paramref name="graph"/> whose kind <paramref name="kit"/> marks arriving, then one for each wire end that names an
    /// arriving port and each way taken on an arriving outcome, against the node that declares it.
    /// </summary>
    public static IEnumerable<WireProblem> Problems(NodeGraph graph, NodeKit kit)
    {
        foreach (var node in graph.Nodes.Where(node => kit.IsArriving(node.Kind)))
        {
            yield return new WireProblem(GraphRefusal.Arriving, null, node.Id,
                $"node `{node.Id}`: `{node.Kind}` is arriving: this build declares it, and no saved graph holds one until it is offered.");
        }

        var byId = graph.Nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        NodeArrivals? Of(WireEnd? end) =>
            end is { Unit: null } && byId.TryGetValue(end.Node, out var node) && !kit.IsArriving(node.Kind) && kit.Find(node.Kind) is BuiltInNode unit ? unit.Arrives : null;
        WireProblem Refused(Wire wire, string node, string what) => new(GraphRefusal.Arriving, wire.Id, node,
            $"wire `{wire.Id}`: `{node}`'s {what} is arriving: this build declares it, and no saved graph names it until it is offered.");

        foreach (var wire in graph.Wires)
        {
            if (wire.Kind == WireKinds.Data && wire.From is { Port: { } output } from && Of(from)?.Outputs.ContainsKey(output) == true)
            {
                yield return Refused(wire, from.Node, $"`{output}` output");
            }

            if (wire.Kind == WireKinds.Data && wire.To is { Port: { } input } to && Of(to)?.Inputs.ContainsKey(input) == true)
            {
                yield return Refused(wire, to.Node, $"`{input}` input");
            }

            if ((wire.Outcome ?? wire.On) is { } outcome && Of(wire.From)?.Outcomes.Contains(outcome, StringComparer.Ordinal) == true)
            {
                yield return Refused(wire, wire.From!.Node, $"`{outcome}` outcome");
            }
        }
    }
}
