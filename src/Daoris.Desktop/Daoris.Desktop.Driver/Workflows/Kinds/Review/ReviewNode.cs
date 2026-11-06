namespace Daoris.Driver;

/// <summary>
/// The person's Accept as a step of its own (LANDNODE1c2), placed before a <c>land</c> where a workflow wants one. Never skipped, since a skip would
/// read as accepted, and never a graph's end.
/// </summary>
[NodeKind(NodeKinds.Review, NodeGroup.You)]
internal sealed class ReviewNode : BuiltInNode
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
}
