namespace Daoris.Driver;

/// <summary>
/// The ask's parts landed, as a step of the ask's delivery run (<see cref="PartsReading"/>): reached once every required part of the standing plan is on the
/// ask's branch. It has no failure of its own: a part that goes back to its worker waits here, and the person's pass over it is the run's skip.
/// </summary>
[NodeKind(NodeKinds.Parts, NodeGroup.Others, NodeKinds.Request)]
public sealed class PartsNode : BuiltInNode, IObserves
{
    public NodeObservation Observe(NodeReading reading) => PartsReading.Observe(reading);

    public override NodeAuthority Row { get; } =
        new(NodeKinds.Parts, NodeReach.Observed, NodeSource.Integration, null, false, PersonSkips: true, true, true);

    /// <summary>It reads the ask's plan and the integration's record, so its subject is the run itself.</summary>
    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Run;

    public override NodePart Part(NodeDeclaration node) => NodePart.Automatic(node.Kind) with { Lowered = NodeKinds.Parts };

    public override string? Title => "The ask's parts";

    public override NodeLook Look => NodeLook.Other;
}
