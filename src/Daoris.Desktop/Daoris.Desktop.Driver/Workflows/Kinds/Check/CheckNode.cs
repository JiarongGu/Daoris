namespace Daoris.Driver;

/// <summary>
/// The declared gates Daoris runs itself (FABRIC-PIPELINE1a, D192 point 6; the pipeline design §4.1): performed with no agent session, bound by its
/// place, and never skipped by its kind, since a skip would read as a verdict a stage and the line rest on.
/// </summary>
[NodeKind(NodeKinds.Check, NodeGroup.Checks)]
internal sealed class CheckNode : BuiltInNode
{
    /// <summary>Reached and failed by the gates' exits; the person's doors on it are a new attempt and a stop.</summary>
    public override NodeAuthority Row { get; } = new(NodeKinds.Check, NodeReach.Performed, NodeSource.Gates, NodeSource.Gates, false, false, true, true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubjects.Placed(node, afterHanded);

    public override IReadOnlyList<NodeOptionField> Options { get; } = [NodeOptionFields.Names("gates")];

    public override string? Title => "The checks";

    public override NodeLook Look => NodeLook.Check;
}
