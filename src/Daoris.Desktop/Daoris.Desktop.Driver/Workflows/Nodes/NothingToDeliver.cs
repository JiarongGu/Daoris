namespace Daoris.Driver;

/// <summary>
/// What a run's landing leaves to deliver. A session that committed nothing lands nothing: no branch is made and its session branch is
/// held <c>empty</c>, so every step after the <c>land</c> is passed over as nothing to deliver. A landed branch the line already holds (its
/// pull request completed on the platform, or the work merged by hand) leaves no push and no pull request to make. Each is Daoris's skip on
/// the path, with the sentence the run says, and holds while what it rests on does.
/// </summary>
internal static class NothingToDeliver
{
    /// <summary>
    /// Daoris's skip of <paramref name="node"/>, a step a way leads to from a reached <c>land</c> whose landing leaves it nothing to deliver,
    /// the landing read on the land's own subject (<paramref name="unitOf"/>); else null.
    /// </summary>
    public static NodeMove? Skip(
        NodeDeclaration node, IReadOnlyList<NodeDeclaration> nodes, IReadOnlyList<Wire> wires, IReadOnlyDictionary<string, NodeStatus> statuses,
        Func<string, WorkUnit?> unitOf, NodeScope scope)
    {
        foreach (var land in nodes.Where(each => each.Kind == NodeKinds.Land && statuses.GetValueOrDefault(each.Id)?.State == NodeStates.Reached))
        {
            if (!Follows(land.Id, node.Id, wires) || unitOf(land.Id) is not { } unit || Why(node, unit) is not { } words) continue;
            return new NodeMove(NodeStates.Skipped, NodeCause.Daoris) { Source = NodeSource.Path, Words = words, Scope = scope };
        }

        return null;
    }

    /// <summary>Why <paramref name="node"/>, past the landing on <paramref name="unit"/>, has nothing to deliver; else null.</summary>
    internal static string? Why(NodeDeclaration node, WorkUnit unit)
    {
        if (Empty(unit) is { } empty) return empty;
        if (node.Kind != NodeKinds.Push && PullRequestKind.PluginOf(node.Kind) is null) return null;
        return NodeObserving.LandingOf(unit) is { } landing && PullRequestReading.OnTheLine(landing) is { } held
            ? $"nothing left to deliver: `{landing.Branch}` is on `{held.On}` already ({WorkInventory.ProofSaid(held.By, held.On)}), "
                + "its pull request completed on the platform or the work merged by hand."
            : null;
    }

    /// <summary>Why a landing on <paramref name="unit"/> delivers nothing: no branch made, and its session branch held <c>empty</c>; else null.</summary>
    internal static string? Empty(WorkUnit unit) =>
        NodeObserving.LandingOf(unit) is null && unit.Records.Branch is { Held.By: EndBy.Empty } branch
            ? $"nothing to deliver: `{branch.Branch}` committed nothing past where it grew, so nothing is pushed and no pull request is opened."
            : null;

    /// <summary>Whether a way forward of any kind leads from <paramref name="from"/> to <paramref name="to"/> within the graph: a <c>back</c> wire leads to what came before.</summary>
    private static bool Follows(string from, string to, IReadOnlyList<Wire> wires)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { from };
        var next = new Queue<string>([from]);
        while (next.TryDequeue(out var at))
        {
            foreach (var wire in wires.Where(wire => wire.Kind != WireKinds.Back && wire.From is { Unit: null } source && source.Node == at && wire.To.Unit is null))
            {
                if (wire.To.Node == to) return true;
                if (seen.Add(wire.To.Node)) next.Enqueue(wire.To.Node);
            }
        }

        return false;
    }
}
