namespace Daoris.Driver;

/// <summary>Git's proof that the line holds the work (§B3): the kept end fact, <i>held</i>, observed and never skipped, since the cleanup rests on it.</summary>
[NodeKind(NodeKinds.Line, NodeGroup.Landing, NodeKinds.Tidy)]
internal sealed class LineNode : BuiltInNode
{
    public override NodeAuthority Row { get; } = new(NodeKinds.Line, NodeReach.Observed, NodeSource.Git, null, false, false, true, true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Landing;

    public override string? Evidence => BuiltInNodes.EvidenceSchema(NodeKinds.Line);

    public override string? Title => "On the line";

    public override NodeLook Look => NodeLook.Line;
}
