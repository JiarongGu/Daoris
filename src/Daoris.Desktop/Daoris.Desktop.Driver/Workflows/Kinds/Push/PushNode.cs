namespace Daoris.Driver;

/// <summary>
/// Daoris's own push of the branch the <c>land</c> made (G8, D241 point 2): performed through the push act, reached when git's remote-tracking ref
/// holds the pushed commit.
/// </summary>
[NodeKind(NodeKinds.Push, NodeGroup.Landing, NodeKinds.Land)]
internal sealed class PushNode : BuiltInNode
{
    /// <summary>Failed by the push act's refusal. Never skipped, since a skip would read as pushed where nothing was; the person's doors are stop and Try again.</summary>
    public override NodeAuthority Row { get; } = new(NodeKinds.Push, NodeReach.Performed, NodeSource.Git, NodeSource.Git, false, PersonSkips: false, true, true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Landing;

    public override string? Evidence => BuiltInNodes.EvidenceSchema(NodeKinds.Push);

    public override string? Title => "The push";

    public override NodeLook Look => NodeLook.Line;
}
