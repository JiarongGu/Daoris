namespace Daoris.Driver;

/// <summary>
/// Daoris closing the quest at the work's end (§E5), through the close door. Never skipped: its override is the person's own done.
/// </summary>
[NodeKind(NodeKinds.QuestClose, NodeGroup.Others, NodeKinds.WaitUnit)]
internal sealed class QuestCloseNode : BuiltInNode, IStarts
{
    public override NodeAuthority Row { get; } =
        new(NodeKinds.QuestClose, NodeReach.Performed, NodeSource.CloseDoor, NodeSource.CloseDoor, false, false, true, true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Quest;

    public override string? Title => "The quest's close";

    public override NodeLook Look => NodeLook.Other;

    /// <summary>None from the wiring: its door is the close door the runtime was made with.</summary>
    public INodeDoor? Door(NodeDoorWiring wiring) => null;

    /// <summary>
    /// Closed through the close door in the pass. A fence that found the unit moved drops what the pass governs, which ends its judgement
    /// here: the next pass reads it again.
    /// </summary>
    public async Task<WorkPerformed?> StartAsync(NodeStarting starting, CancellationToken ct) =>
        await starting.Closing.CloseAsync(starting.Governed, starting.Node, starting.Start, starting.Status, starting.Sends, starting.Said, starting.Units, ct)
            .ConfigureAwait(false);
}
