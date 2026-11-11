namespace Daoris.Driver;

/// <summary>
/// The work set apart from the place's other sessions (<see cref="IsolateStep"/>): the graph's entry, which the start performs before the
/// record opens, so it is reached once the quest's session has taken it. A graph without one runs one session at a time in the place.
/// </summary>
[NodeKind(NodeKinds.Isolate, NodeGroup.Work)]
internal sealed class IsolateNode : BuiltInNode, IObserves
{
    public override NodeAuthority Row { get; } =
        new(NodeKinds.Isolate, NodeReach.Observed, NodeSource.Ledger, null, false, PersonSkips: false, MayEnd: false, Evidenced: true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Quest;

    public override IReadOnlyList<NodeOptionField> Options { get; } =
    [
        NodeOptionFields.Choice(IsolateStep.KindOption, IsolateStep.Kinds),
        NodeOptionFields.Choice(IsolateStep.AtOnceOption, IsolateStep.Counts),
        NodeOptionFields.Choice(IsolateStep.OverlapOption, IsolateStep.Overlaps),
    ];

    public override string? Problem(NodeDeclaration node) => IsolateSteps.Problem(node);

    public override string NeverEndBecause => "it sets the work apart, and the work comes after it.";

    /// <summary>One, the graph's only entry, wired by <c>after</c> straight into a <c>claimed</c>.</summary>
    public override IEnumerable<WireProblem> Problems(NodeGraph graph, NodeDeclaration node)
    {
        var into = graph.Wires.Where(wire => wire.Kind == WireKinds.After && wire.From?.Node == node.Id)
            .Select(wire => graph.Nodes.FirstOrDefault(each => each.Id == wire.To.Node)?.Kind).ToList();
        var said = graph.Nodes.Count(each => each.Kind == NodeKinds.Isolate) > 1 ? "a graph sets its work apart once"
            : graph.Entries.Count != 1 || graph.Entries[0] != node.Id ? "it is the graph's one entry"
            : into is not [NodeKinds.Claimed] ? $"it is wired by `after` straight into the work, a `{NodeKinds.Claimed}`"
            : null;
        if (said is not null)
        {
            yield return new WireProblem(GraphRefusal.IsolateEntry, null, node.Id, $"node `{node.Id}`: an `{NodeKinds.Isolate}` sets the work apart, and {said}.");
        }
    }

    public override IReadOnlyList<string> Waits { get; } = [IsolateStep.AtOnceOption, IsolateStep.OverlapOption];

    public override string? Evidence => NodeObserving.EvidenceSchema(NodeKinds.Isolate);

    public override string? Title => "Work apart";

    public override NodeLook Look => NodeLook.Work;

    public override IReadOnlyList<string> Places => PlaceKind.GitAlone;

    public override bool EndsByTheLine => true;

    /// <summary>Reached once the quest's session has taken it, since the start set the work apart before that session's record opened.</summary>
    public NodeObservation Observe(NodeReading reading)
    {
        if (NodeObserving.Unbound(reading, WorkUnitKind.Quest) is { } refused) return refused;
        var unit = reading.Unit;
        return unit.Records.Quest?.Status switch
        {
            "Taken" or "Done" or "Declined" => NodeObserving.Reached(reading, NodeSource.Ledger, unit.Subject.Revision, WorkRecord.Quests,
                unit.Records.Quest.Id, "its session was started apart from the place's others"),
            "Open" => NodeObserving.Seen(reading, null, unit.End.Said),
            _ => NodeObserving.Seen(reading, NodeStates.Unknown, unit.End.Said),
        };
    }
}
