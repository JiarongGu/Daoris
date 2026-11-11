namespace Daoris.Driver;

/// <summary>Git's proof that the line holds the work: the kept end fact, <i>held</i>, observed and never skipped, since the cleanup rests on it.</summary>
[NodeKind(NodeKinds.Line, NodeGroup.Git, NodeKinds.Tidy)]
internal sealed class LineNode : BuiltInNode, IObserves
{
    public override NodeAuthority Row { get; } = new(NodeKinds.Line, NodeReach.Observed, NodeSource.Git, null, false, false, true, true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Landing;

    public override string? Evidence => NodeObserving.EvidenceSchema(NodeKinds.Line);

    public override string? Title => "On the line";

    public override NodeLook Look => NodeLook.Line;

    /// <summary>It observes git's line, so a folder's graph ends at its last node instead.</summary>
    public override IReadOnlyList<string> Places => PlaceKind.GitAlone;

    public override bool ReadsTheLanding => true;

    public override bool EndsByTheLine => true;

    public override NodeArrivals Arrives { get; } = NodeArrivals.Taking(("commit", WireValues.Commit));

    public NodeObservation Observe(NodeReading reading) => LineEndReading.Observe(reading);
}
