namespace Daoris.Driver;

/// <summary>
/// Daoris closing the quest at the work's end (§E5), through the close door. Never skipped: its override is the person's own done.
/// </summary>
[NodeKind(NodeKinds.QuestClose, NodeGroup.Others, NodeKinds.WaitUnit)]
internal sealed class QuestCloseNode : BuiltInNode
{
    public override NodeAuthority Row { get; } =
        new(NodeKinds.QuestClose, NodeReach.Performed, NodeSource.CloseDoor, NodeSource.CloseDoor, false, false, true, true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Quest;

    public override string? Title => "The quest's close";

    public override NodeLook Look => NodeLook.Other;
}
