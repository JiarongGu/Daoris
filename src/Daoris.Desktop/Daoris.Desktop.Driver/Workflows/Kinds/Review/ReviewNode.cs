namespace Daoris.Driver;

/// <summary>
/// The person's Accept as a step of its own (LANDNODE1c2), placed before a <c>land</c> where a workflow wants one. Never skipped, since a skip would
/// read as accepted, and never a graph's end.
/// </summary>
[NodeKind(NodeKinds.Review, NodeGroup.You)]
internal sealed class ReviewNode : BuiltInNode, IObserves, IPressed
{
    /// <summary>
    /// The person's Accept reaches it and nothing fails it. DECIDE1b: their changes, with words, are its rejection, so an <c>after</c> from it still
    /// means on Accept and a <c>rejected</c> or <c>back on: rejected</c> wire is the changes route.
    /// </summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.Review, NodeReach.Pressed, NodeSource.Person, null, true, PersonSkips: false, MayEnd: false, Evidenced: false);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Branch;

    public override NodePart Part(NodeDeclaration node) => new(NodeKinds.Review, WorkflowParticipation.You, WorkflowExecutor.Daoris, WorkflowPress.Accept);

    public override string? Evidence => BuiltInNodes.EvidenceSchema(NodeKinds.Review);

    public override string? Title => "Your Accept";

    public override NodeLook Look => NodeLook.You;

    /// <summary>Reached by the person's Accept, read from the landing it made as a <c>land</c>'s reach is (LANDNODE1c2).</summary>
    public NodeObservation Observe(NodeReading reading) => BuiltInNodes.Handed(reading, NodeSource.Person);

    /// <summary>The person's Accept, and their changes with their words (LANDNODE1c2, DECIDE1c).</summary>
    public IReadOnlyList<string> Takes { get; } = [NodeWordActs.Accept, NodeWordActs.Changes];

    public string Refusal => RunNodeWords.NotReview;

    /// <summary>DECIDE1c: a review waiting on the person's Accept takes their changes, with their words, beside it.</summary>
    public IReadOnlyList<string> Acts(NodeDeclaration node, NodeStatus status) => status.State == NodeStates.Active ? [NodeWordActs.Changes] : [];

    /// <summary>
    /// Their Accept is its reach by its authority, the person, on the binding it stands on, so one given before the review began is refused; their
    /// changes its rejection on that attempt, with their words, which the table needs.
    /// </summary>
    public NodeMove? Move(NodeDeclaration node, NodeWord word, NodeStatus status, NodeScope scope) => word.Act == NodeWordActs.Accept
        ? new NodeMove(NodeStates.Reached, NodeCause.Authority) { Source = NodeSource.Person, Attempt = status.Attempt, Acceptance = status.Binding }
        : new NodeMove(NodeStates.Rejected, NodeCause.Person) { Attempt = status.Attempt, Words = word.Words };
}
