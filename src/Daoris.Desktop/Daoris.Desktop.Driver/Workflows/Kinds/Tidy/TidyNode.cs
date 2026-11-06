namespace Daoris.Driver;

/// <summary>
/// The chain's parts ended on the line once its pull request merged (D245 point 1): each part's branch removed through the journal's delete, its
/// commits kept on a recovery ref, and what it ended written on the landing record, which the keeper and the press read.
/// </summary>
[NodeKind(NodeKinds.Tidy, NodeGroup.Landing, NodeKinds.Monitor)]
internal sealed class TidyNode : BuiltInNode
{
    /// <summary>Read from the landing record its door wrote. Never skipped, since a skip would read as tidied; the person's door is the stop.</summary>
    public override NodeAuthority Row { get; } =
        new(NodeKinds.Tidy, NodeReach.Performed, NodeSource.Landing, NodeSource.Landing, false, PersonSkips: false, true, true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Landing;

    public override string? Evidence => BuiltInNodes.EvidenceSchema(NodeKinds.Tidy);

    public override string? Title => "The chain's clean-up";

    public override NodeLook Look => NodeLook.Line;
}
