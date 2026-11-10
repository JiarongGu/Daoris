namespace Daoris.Driver;

/// <summary>
/// The commit a run's person reviewed on its branch: where a pass does not read the branch's tip, a step after the review not bound yet binds
/// the commit the review reached on that same branch, so the landing the person accepted begins there rather than waiting, ready with no
/// subject, for a read that never comes; a tip read later that moved lapses it as any moved subject does.
/// </summary>
internal static class ReviewedTip
{
    /// <summary>The commit a reached <c>review</c> of <paramref name="graph"/> accepted on <paramref name="branch"/>; null where none did.</summary>
    public static string? Of(NodeGraph graph, IReadOnlyDictionary<string, NodeStatus> statuses, string branch) =>
        graph.Nodes.Where(node => node.Kind == NodeKinds.Review)
            .Select(node => statuses.GetValueOrDefault(node.Id))
            .FirstOrDefault(status => status is { State: NodeStates.Reached, Acceptance: { } accepted } && accepted.Observes == branch)
            ?.Acceptance!.SubjectRevision;
}
