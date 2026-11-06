namespace Daoris.Driver;

/// <summary>
/// The watch on the pull request the run's graph opened (D245 point 2): reached once the answer its plugin gave through <c>work/state</c> says the
/// request was merged or closed, a squash merge read as merged.
/// </summary>
[NodeKind(NodeKinds.Monitor, NodeGroup.Landing, "*/pull-request")]
internal sealed class MonitorNode : BuiltInNode, IObserves
{
    /// <summary>Read from the landing record, and nothing fails it. Never skipped, since a skip would read as merged; the person's door is the stop.</summary>
    public override NodeAuthority Row { get; } = new(NodeKinds.Monitor, NodeReach.Observed, NodeSource.Landing, null, false, PersonSkips: false, true, true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Landing;

    public override string? Evidence => BuiltInNodes.EvidenceSchema(NodeKinds.Monitor);

    public override string? Title => "The pull request's watch";

    public override NodeLook Look => NodeLook.Line;

    public override bool ReadsTheLanding => true;

    public NodeObservation Observe(NodeReading reading) => BuiltInNodes.Completion(reading, CompletionNodes.Monitor);
}
