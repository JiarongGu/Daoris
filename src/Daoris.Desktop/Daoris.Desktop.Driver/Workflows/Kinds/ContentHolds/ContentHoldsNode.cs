namespace Daoris.Driver;

/// <summary>
/// What the line already holds, as a step before the landing (<see cref="HoldReading"/>): the proof that the work holds a commit no
/// branch of the person's holds, and that neither the line nor such a branch holds it by content.
/// </summary>
[NodeKind(NodeKinds.ContentHolds, NodeGroup.Checks, NodeKinds.QuestHolds)]
internal sealed class ContentHoldsNode : BuiltInNode, IObserves, IAsksReads
{
    public PassRead Asks => PassRead.Content;

    /// <summary>As <c>quest.holds</c>'s: a hold that does not let go keeps it active with its reason, and the person's pass is the way past it.</summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.ContentHolds, NodeReach.Observed, NodeSource.Holds, null, false, PersonSkips: true, MayEnd: false, Evidenced: true);

    /// <summary>The run's branch, as the landing's.</summary>
    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Branch;

    /// <summary>Its switches, on where not written.</summary>
    public override IReadOnlyList<NodeOptionField> Options { get; } = [.. ContentHoldsReading.Options.Select(NodeOptionFields.Switch)];

    public override string? Problem(NodeDeclaration node) => HoldReading.Problem(node, ContentHoldsReading.Options);

    public override IEnumerable<WireProblem> Problems(NodeGraph graph, NodeDeclaration node) => HoldReading.Problems(graph, node);

    public override string? Evidence => NodeObserving.EvidenceSchema(NodeKinds.ContentHolds);

    public override string? Title => "What the line holds";

    public override NodeLook Look => NodeLook.Holds;

    /// <summary>It reads what the branch holds against the line.</summary>
    public override IReadOnlyList<string> Places => PlaceKind.GitAlone;

    public NodeObservation Observe(NodeReading reading) => ContentHoldsReading.Observe(reading);
}
