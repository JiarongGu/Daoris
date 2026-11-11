namespace Daoris.Driver;

/// <summary>
/// Why a run whose quest closed (done or declined) ends while steps of it still stand: every such step observes the run's branch, nothing
/// waits on the person's word, and none can move again, because the line holds each one's bound work (<see cref="LineHolding"/>, the
/// landing's own judgement by content and by patch) or no branch of the run stands. Its steps stay as they stood; the end says why. The
/// runtime's end (<see cref="NodeRunKeeping.EndAsync"/>) and the migration that ends the runs left before it (<see cref="ClosedRunsStep"/>)
/// both judge here.
/// </summary>
public static class ClosedRunEnds
{
    /// <summary>No branch of the run stands, so nothing it waits on can be read again.</summary>
    public const string BranchGone = "branch-gone";

    /// <summary>The line holds the work each standing step is bound to, so nothing is left to land.</summary>
    public const string OnLine = "on-line";

    /// <summary>Whether a quest's status closes it: done or declined.</summary>
    public static bool Closed(string status) =>
        string.Equals(status, "Done", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "Declined", StringComparison.OrdinalIgnoreCase);

    /// <summary>The steps that still stand: ready, active, unknown or invalidated, as a settled graph has none.</summary>
    public static IReadOnlyList<NodeDeclaration> Standing(NodeGraph graph, NodeRecord record) =>
        [.. graph.Nodes.Where(node => WorkPerformer.StatusOf(record, node.Id).State is NodeStates.Ready or NodeStates.Active or NodeStates.Unknown or NodeStates.Invalidated)];

    /// <summary>The commits the standing steps are bound to: what the line is asked about.</summary>
    public static IReadOnlyList<string> Bound(NodeGraph graph, NodeRecord record) =>
        [.. Standing(graph, record).Select(node => WorkPerformer.StatusOf(record, node.Id).Binding?.SubjectRevision)
            .OfType<string>().Where(WorkingTree.IsCommitId).Distinct(StringComparer.OrdinalIgnoreCase)];

    /// <summary>
    /// Why the run ends though steps stand, or null where one can still move: a step observing anything but the run's branch (the quest,
    /// the run itself, a review's record, an input), one with no bound commit while a branch stands, or one whose commit the line lacks while
    /// a branch stands. <paramref name="waits"/> says whether an attempt or a word still waits, as a settled run's end asks.
    /// </summary>
    /// <param name="onLine">Each bound commit the line holds whole; a commit not read is not held.</param>
    public static string? Why(NodeGraph graph, NodeRecord record, bool waits, bool branchStands, IReadOnlySet<string> onLine)
    {
        if (waits) return null;
        var standing = Standing(graph, record);
        if (standing.Count == 0) return null;
        var after = RunSubjects.AfterHanded(graph);
        if (standing.Any(node => RunSubjects.Of(node, after) is not (RunSubject.Branch or RunSubject.Landing))) return null;
        var bound = standing.Select(node => WorkPerformer.StatusOf(record, node.Id).Binding?.SubjectRevision).ToList();
        if (bound.All(tip => tip is not null && onLine.Contains(tip))) return OnLine;
        return branchStands ? null : BranchGone;
    }

    /// <summary>
    /// Each of <paramref name="tips"/> the line holds whole, asked through <paramref name="onLine"/> (<see cref="NodeRuntimeLook.OnLine"/>);
    /// one it cannot say is left out.
    /// </summary>
    public static async Task<IReadOnlySet<string>> OnLineAsync(IReadOnlyList<string> tips, Func<string, CancellationToken, Task<bool?>> onLine, CancellationToken ct)
    {
        var held = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tip in tips)
        {
            if (await onLine(tip, ct).ConfigureAwait(false) == true) held.Add(tip);
        }

        return held;
    }

    /// <summary>
    /// Whether a governed run's branch stands by the look's records: any branch its quests produced not recorded gone. One that names none
    /// stands only where the work's attribution is unread, since a dropped link names none either.
    /// </summary>
    public static bool BranchStands(RunScope scope) =>
        !scope.Attributed || scope.Branches.Concat(scope.Branch is { } branch ? [branch] : []).Any(each => each.Records.Branch is { GoneAt: null });
}
