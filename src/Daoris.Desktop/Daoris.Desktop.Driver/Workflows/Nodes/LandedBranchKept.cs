namespace Daoris.Driver;

/// <summary>
/// The branch a run's landing reached on, which the steps bound there keep. Once the landing has reached, its work has left the branch
/// (merged into the line, or onto a branch that is pushed, pulled into the line on the forge and released), so another branch coming to stand
/// as the run's (an earlier attempt's, left standing once the landed one is tidied away; another repository's, on the same ask) never moves
/// them: judging, landing and pushing that branch would undo nothing outside the machine and repeat it. The chain coming back here lapses
/// <c>claimed</c> on its new quest and takes the run back from there, and the person's word on a step takes it back from that step.
/// </summary>
internal static class LandedBranchKept
{
    /// <summary>The branch unit the run's landing reached on, or null while it has not reached.</summary>
    public static string? Of(NodeGraph graph, IReadOnlyDictionary<string, NodeStatus> statuses) =>
        graph.Nodes.FirstOrDefault(node => NodeKit.Of(node.Kind) is IMakesLanding) is { } land
        && statuses.GetValueOrDefault(land.Id) is { State: NodeStates.Reached, Acceptance.Subject: { } subject }
            ? subject
            : null;

    /// <summary>
    /// The revision a step keeps on <paramref name="landed"/> where the run's branch is another now: its own binding's, while it stands bound
    /// there; null where it is judged on the run's branch as it stands.
    /// </summary>
    public static string? Revision(NodeStatus status, string? landed, string? branch) =>
        landed is not null && !string.Equals(branch, landed, StringComparison.Ordinal) && NodeStates.Bound.Contains(status.State)
        && status.Binding is { } binding && string.Equals(binding.Observes, landed, StringComparison.Ordinal)
            ? binding.SubjectRevision
            : null;
}
