namespace Daoris.Driver;

/// <summary>
/// The save's refusal of an arriving kind (<see cref="NodeKindAttribute.Arriving"/>): a kind declared before it is offered, so no saved graph
/// holds one until the change that offers it removes the mark. An import is a save; a graph read back from a file is never refused here.
/// </summary>
internal static class ArrivingKinds
{
    /// <summary>One refusal for each node of <paramref name="graph"/> whose kind <paramref name="kit"/> marks arriving.</summary>
    public static IEnumerable<WireProblem> Problems(NodeGraph graph, NodeKit kit) =>
        graph.Nodes.Where(node => kit.IsArriving(node.Kind)).Select(node => new WireProblem(GraphRefusal.Arriving, null, node.Id,
            $"node `{node.Id}`: `{node.Kind}` is arriving: this build declares it, and no saved graph holds one until it is offered."));
}
