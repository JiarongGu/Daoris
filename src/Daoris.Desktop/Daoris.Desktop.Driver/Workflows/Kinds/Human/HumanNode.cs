namespace Daoris.Driver;

/// <summary>A person's task (§D5): pressed by the person, who may reject it or pass it over.</summary>
[NodeKind(NodeKinds.Human, NodeGroup.You, NodeKinds.GoAhead)]
internal sealed class HumanNode : BuiltInNode
{
    public override NodeAuthority Row { get; } = new(NodeKinds.Human, NodeReach.Pressed, NodeSource.Person, null, true, true, true, false);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Run;

    public override NodePart Part(NodeDeclaration node) => new(NodeKinds.Human, WorkflowParticipation.You, WorkflowExecutor.Daoris);

    public override string? Title => "Your task";

    public override NodeLook Look => NodeLook.You;
}
