namespace Daoris.Driver;

/// <summary>
/// One part's receipt as the run's view names it: <i>&lt;part&gt;'s &lt;fact&gt;</i>, the quest the plan gave that part (its run, which the
/// view links), and the state and words of the newest receipt of the fact its run journaled; no state where it journaled none.
/// </summary>
public sealed record WorkflowRunPartReceipt(string Part, string Fact)
{
    public string? Quest { get; init; }

    /// <summary>One of <c>reached</c>, <c>waiting</c>, <c>failed</c>; null where the part's run journaled no receipt of the fact.</summary>
    public string? State { get; init; }

    public string? Message { get; init; }
}

/// <summary>A joint check as the run's view draws it: each part's receipt it holds over, reached or waited for, in its option's order.</summary>
public sealed record WorkflowRunJoint(IReadOnlyList<WorkflowRunPartReceipt> Parts)
{
    /// <summary>
    /// The part its newest test failed on, once the failure is journaled on that part's step (<see cref="JointReturns"/>): its fact the test's,
    /// its words the test's; null while no failure went back.
    /// </summary>
    public WorkflowRunPartReceipt? Returned { get; init; }
}

/// <summary>What the run's view reads of its ask's parts: what its own part waits on before it starts, and each joint check's parts, by node.</summary>
public sealed record RunPartsView(IReadOnlyList<WorkflowRunPartReceipt> Waits, IReadOnlyDictionary<string, WorkflowRunJoint> Joints)
{
    public static readonly RunPartsView None = new([], new Dictionary<string, WorkflowRunJoint>(StringComparer.Ordinal));
}

/// <summary>
/// The run's view of its ask's parts, read as the pass reads them (<see cref="PartReceipts"/>, <see cref="JointChecks"/>): the plan's waits
/// of the run's part not reached yet, and each joint check's parts' newest receipts and where its failure went back. Reads the parts' journals only.
/// </summary>
public static class PartViews
{
    /// <param name="report">The ask's plan as read for the run (<see cref="PartsReports.Of"/>); null where the run is no ask's or it is not read.</param>
    /// <param name="run">The run, and <paramref name="quests"/> its chain's quests here, which find its part.</param>
    /// <param name="record">The run's node record, whose attempt a joint check's own tests are read on; null where it does not read.</param>
    public static RunPartsView Read(string home, PartsReport? report, string run, IEnumerable<string> quests, NodeGraph graph, NodeRecord? record)
    {
        if (report is null) return RunPartsView.None;
        var waits = PartReceipts.Waiting(home, report, run, quests)
            .Select(wait => Receipt(report, wait.On, wait.Fact, PartReceipts.Newest(home, report, wait.On, wait.Fact)))
            .ToList();
        var journal = StepActFiles.Journal(home, RunUnits.IdOf(run));
        var joints = new Dictionary<string, WorkflowRunJoint>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes.Where(each => JointChecks.Over(each).Count > 0))
        {
            var attempt = record is null ? 0 : WorkPerformer.StatusOf(record, node.Id).Attempt;
            var parts = JointChecks.Receipts(home, report, node);
            var own = journal.Where(row => row.Node == node.Id && row.Attempt == attempt);
            joints[node.Id] = new WorkflowRunJoint([.. JointChecks.Over(node).Select(each => Receipt(report, each.Key, each.Value, parts[each.Key]))])
            {
                Returned = Returned(home, report, node, own),
            };
        }

        return new RunPartsView(waits, joints);
    }

    private static WorkflowRunPartReceipt Receipt(PartsReport report, string part, string fact, StepActRow? newest) =>
        new(part, fact) { Quest = PartReceipts.RunOf(report, part), State = newest?.State, Message = newest?.Message };

    /// <summary>The part the check's newest failed test went back to, where its run journaled the return.</summary>
    private static WorkflowRunPartReceipt? Returned(string home, PartsReport report, NodeDeclaration node, IEnumerable<StepActRow> own) =>
        JointChecks.Failed(node, own) is { Part: { } part } failed && PartReceipts.RunOf(report, part) is { } run
        && StepActFiles.Journal(home, RunUnits.IdOf(run)).Any(row => row.Id == JointChecks.Returned + failed.Id)
            ? Receipt(report, part, failed.Fact, failed)
            : null;
}
