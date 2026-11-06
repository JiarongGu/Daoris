namespace Daoris.Driver;

/// <summary>
/// The quest's own holds as a step before the landing (LANDNODE1k, <see cref="HoldReading"/>): its done or submission standing, not taken back, and
/// not held for the person's yes (D133) or its evidence's verdict (D144).
/// </summary>
[NodeKind(NodeKinds.QuestHolds, NodeGroup.Checks, NodeKinds.WorkRequests)]
internal sealed class QuestHoldsNode : BuiltInNode, IObserves
{
    /// <summary>
    /// Reached by what Daoris reads at the bound tip; a hold that does not let go keeps it active with its reason, as <c>work.requests</c> is kept,
    /// since each can let go later and a failure would be a verdict routed on. The person's pass with words is the way past it.
    /// </summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.QuestHolds, NodeReach.Observed, NodeSource.Holds, null, false, PersonSkips: true, MayEnd: false, Evidenced: true);

    /// <summary>The run's newest work quest's.</summary>
    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Quest;

    /// <summary>Its switches, on where not written.</summary>
    public override IReadOnlyList<NodeOptionField> Options { get; } = [.. QuestHoldsReading.Options.Select(NodeOptionFields.Switch)];

    public override string? Problem(NodeDeclaration node) => HoldReading.Problem(node, QuestHoldsReading.Options);

    public override IEnumerable<WireProblem> Problems(NodeGraph graph, NodeDeclaration node) => HoldReading.Problems(graph, node);

    /// <summary>Kept by its node's id for the comparison, which reads its <c>yes</c>.</summary>
    public override NodePart Part(NodeDeclaration node) => NodePart.Automatic(node.Kind) with { Lowered = NodeKinds.QuestHolds };

    public override string? Evidence => NodeObserving.EvidenceSchema(NodeKinds.QuestHolds);

    public override string? Title => "The quest's holds";

    public override NodeLook Look => NodeLook.Holds;

    public NodeObservation Observe(NodeReading reading) => QuestHoldsReading.Observe(reading);
}
