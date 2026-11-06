namespace Daoris.Driver;

/// <summary>
/// Where a failure or a refusal stops the run short of its end (FABRIC-WORKFLOW2): a decoded D157 version's <c>stop</c>, §E7's synthetic sink, reached
/// by the wire evaluation on the receipt of the path into it. It is the run stopped: never skipped, and never a graph's end.
/// </summary>
[NodeKind(NodeKinds.Stop, NodeGroup.Others, NodeKinds.All)]
internal sealed class StopNode : BuiltInNode
{
    public override NodeAuthority Row { get; } = new(NodeKinds.Stop, NodeReach.Observed, NodeSource.Path, null, false, false, MayEnd: false, Evidenced: false);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Run;

    public override string NeverEndBecause => "a failure stops the run there, short of its end.";

    public override string? Title => "Stop";

    public override NodeLook Look => NodeLook.Other;
}
