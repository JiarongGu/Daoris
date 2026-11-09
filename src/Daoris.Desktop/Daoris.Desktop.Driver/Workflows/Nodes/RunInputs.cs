namespace Daoris.Driver;

/// <summary>
/// What a node fed a value binds to (<see cref="RunSubject.Input"/>): the run's work on its branch at the tip read in the pass, or the value
/// its producer's reach carries on the wire, at the revision the value names. So a moved input rebinds only what it feeds.
/// </summary>
public static class RunInputs
{
    /// <summary>The input <paramref name="node"/> binds to: its first input of a value schema, by name; null where it takes none.</summary>
    public static string? Port(NodeDeclaration node) =>
        NodePorts.Inputs(node).Where(input => WireValues.Is(input.Value)).Select(input => input.Key).Order(StringComparer.Ordinal).FirstOrDefault();

    /// <summary>The value <paramref name="node"/> binds to, by its schema; null where it takes none.</summary>
    public static string? Value(NodeDeclaration node) => Port(node) is { } input ? NodePorts.Inputs(node)[input] : null;

    /// <summary>
    /// The subject and revision <paramref name="node"/> binds to this pass: the work's branch unit <paramref name="work"/> at
    /// <paramref name="tip"/> for a <see cref="WireValues.Work"/>; else the ref and revision of the value its producer's current reach carries on
    /// the wire into it. No subject where its producer has not reached with one, so the node is withheld until its input binds.
    /// </summary>
    public static (string? Subject, string? Revision) Of(
        NodeDeclaration node, IReadOnlyList<Wire> wires, IReadOnlyDictionary<string, NodeStatus> statuses, string? work, string? tip)
    {
        if (Port(node) is not { } input) return (null, null);
        if (NodePorts.Inputs(node)[input] == WireValues.Work) return (work, tip);
        var wire = wires.FirstOrDefault(each => each.Kind == WireKinds.Data && each.To is { Unit: null } to && to.Node == node.Id && to.Port == input);
        if (wire?.From is not { Unit: null, Port: { } output } from
            || statuses.GetValueOrDefault(from.Node) is not { State: NodeStates.Reached, Current: { } reached })
        {
            return (null, null);
        }

        var value = output == WireEnd.Evidence ? reached.Evidence : reached.Outputs.GetValueOrDefault(output);
        return value is null ? (null, null) : (value.Ref, value.Revision);
    }
}
