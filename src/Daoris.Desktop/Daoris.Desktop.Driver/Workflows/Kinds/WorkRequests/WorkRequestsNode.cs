namespace Daoris.Driver;

/// <summary>
/// The work's requests held as a step, read from the quests' records (<see cref="WorkRequestsReading"/>).
/// </summary>
[NodeKind(NodeKinds.WorkRequests, NodeGroup.Checks, NodeKinds.Opinion)]
public sealed class WorkRequestsNode : BuiltInNode, IObserves
{
    public NodeObservation Observe(NodeReading reading) => WorkRequestsReading.Observe(reading);

    public override bool EndsByTheLine => true;

    /// <summary>
    /// Reached by the quests' records, each the run's sessions published here done. A decline holds it active and waiting
    /// on the person, so nothing fails it; the person's pass with words is the run's skip, and no record passes it over.
    /// </summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.WorkRequests, NodeReach.Observed, NodeSource.Requests, null, false, PersonSkips: true, true, true);

    /// <summary>It reads the quests the run's sessions published here, so its subject is the run itself.</summary>
    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Run;

    /// <summary>A graph-only kind, kept by its node's id for the comparison's graph part (<see cref="Involvement.Lowers"/>).</summary>
    public override NodePart Part(NodeDeclaration node) => NodePart.Automatic(node.Kind) with { Lowered = NodeKinds.WorkRequests };

    /// <summary>
    /// At most one, after a <c>claimed</c> and before every landing. After: a <c>claimed</c> of this graph reaches it through wires an order holds.
    /// Before: no landing can start with it unreached, by the look's own reading (<see cref="NodeGraphStarts.StartsWithout"/>), so a way to the
    /// landing past it is refused as an <c>anyOf</c> member or a bare wire alike. Each is said once, against the node it is about.
    /// </summary>
    public override IEnumerable<WireProblem> Problems(NodeGraph graph, NodeDeclaration node)
    {
        var standing = GraphStanding.Of(graph);
        var first = standing.ById.Values.First(each => each.Kind == NodeKinds.WorkRequests);
        if (first.Id != node.Id)
        {
            yield return new WireProblem(GraphRefusal.WorkRequestsTwice, null, node.Id,
                $"node `{node.Id}`: `{first.Id}` and `{node.Id}` are both a `{NodeKinds.WorkRequests}`, and the graph holds the work on its requests once.");
        }

        var claimed = NodeGraphStarts.Reach(standing.ById.Values.Where(each => each.Kind == NodeKinds.Claimed).Select(each => each.Id), standing.Ordered, forward: true);
        if (!claimed.Contains(node.Id))
        {
            yield return new WireProblem(GraphRefusal.WorkRequestsUnclaimed, null, node.Id,
                $"node `{node.Id}`: a `{NodeKinds.WorkRequests}` holds the work a `{NodeKinds.Claimed}` observes, and no `{NodeKinds.Claimed}` of this graph comes before it.");
        }

        var early = NodeGraphStarts.StartsWithout(standing.ById, standing.Starts, standing.Local, each => each.Id == node.Id);
        foreach (var landing in standing.Judged.Where(landing => early.Contains(landing.Id)))
        {
            yield return new WireProblem(GraphRefusal.WorkRequestsLate, null, node.Id,
                $"node `{node.Id}`: the landing `{landing.Id}` can start before `{node.Id}` is reached, and a `{NodeKinds.WorkRequests}` holds the work before every landing: "
                + $"every way to it passes `{node.Id}`.");
        }
    }

    public override string? Title => "The work's requests";

    public override NodeLook Look => NodeLook.Holds;
}
