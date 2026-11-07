namespace Daoris.Driver;

/// <summary>
/// A join of a group's members, keeping its identity and failure policy. Its reaching is the wire evaluation's, from its members; never
/// skipped, since its members are skipped instead.
/// </summary>
[NodeKind(NodeKinds.All, NodeGroup.Others, NodeKinds.QuestClose)]
internal sealed class AllNode : BuiltInNode
{
    public override NodeAuthority Row { get; } = new(NodeKinds.All, NodeReach.Observed, NodeSource.Members, NodeSource.Members, false, false, true, false);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Run;

    public override string? Title => "The join";

    public override NodeLook Look => NodeLook.Other;
}
