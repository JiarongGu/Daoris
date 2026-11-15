namespace Daoris.Driver;

/// <summary>
/// Why a run whose work was done in place ends with steps still standing: the work was made in the checkout, on the line, with no session
/// branch, so the steps that read a run's branch (<c>content.holds</c>, the <c>land</c> and what follows it) have nothing to read and nothing to
/// land, and no step the person or an agent can take is left. A run's end is otherwise judged on a closed quest, which only the run's own end
/// closes, so without this such a run would wait for good. A run with a branch is never ended here: its branch steps read it.
/// </summary>
internal static class InPlaceRunEnds
{
    /// <summary>The end's reason, as a run's end says it.</summary>
    public const string Why = "in-place";

    /// <summary>
    /// Whether the run ends: every work step reached, no branch of it ever recorded (<paramref name="attributed"/> says the records were all
    /// read), nothing waiting, and each standing step one that reads the run's branch.
    /// </summary>
    public static bool Ends(NodeGraph graph, NodeRecord record, bool hasBranch, bool attributed, bool waits)
    {
        if (hasBranch || !attributed || waits) return false;
        var work = graph.Nodes.Where(node => node.Kind == NodeKinds.Claimed).ToList();
        if (work.Count == 0 || work.Any(node => WorkPerformer.StatusOf(record, node.Id).State != NodeStates.Reached)) return false;
        var standing = ClosedRunEnds.Standing(graph, record);
        var after = RunSubjects.AfterHanded(graph);
        return standing.Count > 0 && standing.All(node => RunSubjects.Of(node, after) is RunSubject.Branch or RunSubject.Landing);
    }
}
