namespace Daoris.Driver;

/// <summary>
/// Daoris closing the quest at the work's end, through the close door. Never skipped: its override is the person's own done.
/// </summary>
[NodeKind(NodeKinds.QuestClose, NodeGroup.Others, NodeKinds.WaitUnit)]
internal sealed class QuestCloseNode : BuiltInNode, IStarts
{
    public override NodeAuthority Row { get; } =
        new(NodeKinds.QuestClose, NodeReach.Performed, NodeSource.CloseDoor, NodeSource.CloseDoor, false, false, true, true);

    public override RunSubject Subject(NodeDeclaration node, IReadOnlySet<string> afterHanded) => RunSubject.Quest;

    /// <summary>
    /// In a graph that holds it, it is the end, nothing comes after it but its failure's path, and nothing before it waits on the quest's own done,
    /// which a closure graph never writes before its end.
    /// </summary>
    public override IEnumerable<WireProblem> Problems(NodeGraph graph, NodeDeclaration node)
    {
        var standing = GraphStanding.Of(graph);
        var (byId, local) = (standing.ById, standing.Local);
        if (!string.IsNullOrWhiteSpace(graph.End) && byId.TryGetValue(graph.End, out var end) && node.Id != end.Id)
        {
            yield return new WireProblem(GraphRefusal.CloseEnd, null, node.Id,
                $"`{node.Id}` closes the quest, so it is the graph's end, never `{end.Id}`: the quest would read done while its work still moves.");
        }

        foreach (var wire in local.Where(wire => wire.From!.Node == node.Id && wire.Kind != WireKinds.Failed))
        {
            yield return new WireProblem(GraphRefusal.AfterClose, wire.Id, node.Id,
                $"wire `{wire.Id}`: nothing comes after `{node.Id}`, which closes the quest; only its failure's path leaves it.");
        }

        // A claim two closes both wait behind is said once, against the first of them.
        var said = new HashSet<string>(StringComparer.Ordinal);
        foreach (var earlier in byId.Values.TakeWhile(each => !ReferenceEquals(each, node)).Where(each => each.Kind == NodeKinds.QuestClose))
        {
            said.UnionWith(NodeGraphStarts.Reach([earlier.Id], local, forward: false));
        }

        foreach (var claim in NodeGraphStarts.Reach([node.Id], local, forward: false).Select(id => byId[id]).Where(each => each.Kind == NodeKinds.Claimed && !said.Contains(each.Id)))
        {
            yield return new WireProblem(GraphRefusal.ClosureDeadlock, null, claim.Id,
                $"`{claim.Id}` waits on the quest's own done before `{node.Id}` closes it, and a closure graph writes no done before its end: the agent submits as `ready` instead.");
        }
    }

    public override string? Title => "The quest's close";

    public override NodeLook Look => NodeLook.Other;

    /// <summary>None from the wiring: its door is the close door the runtime holds.</summary>
    public INodeDoor? Door(NodeDoorWiring wiring) => null;

    /// <summary>
    /// Closed through the close door in the pass. A fence that found the unit moved drops what the pass governs, which ends its judgement
    /// here: the next pass reads it again.
    /// </summary>
    public async Task<WorkPerformed?> StartAsync(NodeStarting starting, CancellationToken ct) =>
        await starting.Closing.CloseAsync(starting.Governed, starting.Node, starting.Start, starting.Status, starting.Sends, starting.Said, starting.Units, ct)
            .ConfigureAwait(false);
}
