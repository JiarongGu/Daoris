namespace Daoris.Driver;

/// <summary>Waiting on other work through a commitment (§E6): its receipt reaches it and its owner's decline fails it; a <c>wait</c> wire comes into it.</summary>
[NodeKind(NodeKinds.WaitUnit, NodeGroup.Others, NodeKinds.Request)]
internal sealed class WaitUnitNode : BuiltInNode
{
    public override NodeAuthority Row { get; } =
        new(NodeKinds.WaitUnit, NodeReach.Waited, NodeSource.Commitment, NodeSource.Commitment, false, true, true, true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Run;

    public override string? Title => "Other work";

    public override NodeLook Look => NodeLook.Other;
}
