namespace Daoris.Driver;

/// <summary>
/// The person's yes to an act (D135's answers; FABRIC-NODE1b; the fabric events design §E4: a pressed node on D135's answer), read by
/// <see cref="GoAheadReading"/>.
/// </summary>
[NodeKind(NodeKinds.GoAhead, NodeGroup.You, NodeKinds.Look)]
public sealed class GoAheadNode : BuiltInNode, IObserves
{
    public NodeObservation Observe(NodeReading reading) => GoAheadReading.Observe(reading);

    /// <summary>On a run alone, though it is pressed, since its answer is D135's record; on a unit it waits ready for the person's press.</summary>
    public bool Begins(GovernedUnit governed) => governed.Scope is not null;

    public override NodeAuthority Row { get; } = new(NodeKinds.GoAhead, NodeReach.Pressed, NodeSource.Person, null, true, true, true, false);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Run;

    public override IReadOnlyList<NodeOptionField> Options { get; } = [NodeOptionFields.Text("act")];

    public override NodePart Part(NodeDeclaration node) =>
        new(WorkflowKinds.GoAhead, WorkflowParticipation.You, WorkflowExecutor.Daoris, Lowered: WorkflowKinds.GoAhead);

    /// <summary>None: a go-ahead is named by its step's id.</summary>
    public override string? Title => null;

    public override NodeLook Look => NodeLook.You;
}
