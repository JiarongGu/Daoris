namespace Daoris.Driver;

/// <summary>
/// The person's look at the work before it lands (FABRIC-WIRE1h, FABRIC-NODE1b; the fabric events design §E4, §E2), observed from its gate's records
/// (<see cref="LookReading"/>).
/// </summary>
[NodeKind(NodeKinds.Look, NodeGroup.You, NodeKinds.Review)]
public sealed class LookNode : BuiltInNode, IObserves
{
    public NodeObservation Observe(NodeReading reading) => LookReading.Observe(reading);

    /// <summary>
    /// §E4: the look is its gate, observed, and the person's <i>reviewed</i> is what reaches it. FABRIC-WIRE1h: the review gate's record passes it
    /// over where the chain's or the ask's choice turns it off for this work.
    /// </summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.Look, NodeReach.Observed, NodeSource.Person, null, true, true, true, false) { SkipBy = NodeSource.Review };

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Branch;

    public override NodePart Part(NodeDeclaration node) =>
        new(WorkflowKinds.Look, WorkflowParticipation.AgentAndYou, WorkflowExecutor.Agent, WorkflowPress.Reviewed, WorkflowKinds.Look);

    public override string? Title => "Your look";

    public override NodeLook Look => NodeLook.You;

    /// <summary>
    /// A gate before every landing, and no landing starts before a look has passed, whichever member of an <c>anyOf</c> a run takes
    /// (FABRIC-GRAPH1's review, finding 4).
    /// </summary>
    public override LandingGuard Guard => LandingGuard.Passed;
}
