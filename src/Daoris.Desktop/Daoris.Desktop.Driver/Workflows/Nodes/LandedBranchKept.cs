namespace Daoris.Driver;

/// <summary>
/// The branch a run's landing reached on, which the steps bound there keep at the revision they were bound at. Once the landing has reached,
/// its work has left the branch (merged into the line, or onto a branch that is pushed, pulled into the line on the forge and released), so
/// neither another branch coming to stand as the run's (an earlier attempt's, left standing once the landed one is tidied away; another
/// repository's, on the same ask) nor the landed branch moving where no landing took it (its session's turn on the person's words, its tree
/// brought to the line the work merged into, a commit of its own) moves them: judging, landing and pushing it again would undo nothing
/// outside the machine and repeat it. A commit after the landing is new work for the chain's next quest here, which lapses <c>claimed</c> on
/// its new quest and takes the run back from there; the person's word on a step takes it back from that step. A landing that itself moved
/// on (an advance its record keeps at the tip) is followed, as before.
/// </summary>
internal static class LandedBranchKept
{
    /// <summary>
    /// The branch unit the run's landing reached on, whose steps keep their revisions: null while it has not reached, and where the run's
    /// <paramref name="branch"/> is the landed one and its <paramref name="tip"/> read is a landing of its own, which the steps follow.
    /// </summary>
    public static string? Of(NodeGraph graph, IReadOnlyDictionary<string, NodeStatus> statuses, WorkUnit? branch, string? tip)
    {
        if (graph.Nodes.FirstOrDefault(node => NodeKit.Of(node.Kind) is IMakesLanding) is not { } land
            || statuses.GetValueOrDefault(land.Id) is not { State: NodeStates.Reached, Acceptance.Subject: { } subject })
        {
            return null;
        }

        return branch is not null && tip is not null && branch.Id == subject && LandedTip(branch, tip) ? null : subject;
    }

    /// <summary>The revision a step keeps on <paramref name="landed"/>: its own binding's, while it stands bound there; null where it is judged on the run's branch as it stands.</summary>
    public static string? Revision(NodeStatus status, string? landed) =>
        landed is not null && NodeStates.Bound.Contains(status.State)
        && status.Binding is { } binding && string.Equals(binding.Observes, landed, StringComparison.Ordinal)
            ? binding.SubjectRevision
            : null;

    /// <summary>Whether a landing of <paramref name="branch"/> still kept stands at <paramref name="tip"/>, made or squashed there.</summary>
    private static bool LandedTip(WorkUnit branch, string tip) =>
        branch.Records.Landings.Any(each => each.GoneAt is null
            && (string.Equals(each.Tip, tip, StringComparison.OrdinalIgnoreCase) || string.Equals(each.Squashed, tip, StringComparison.OrdinalIgnoreCase)));
}
