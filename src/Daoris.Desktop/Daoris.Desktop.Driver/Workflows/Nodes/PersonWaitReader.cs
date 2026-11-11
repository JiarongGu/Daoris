namespace Daoris.Driver;

/// <summary>
/// The run steps that wait on the person (<see cref="PersonWaits"/>). **Read, never stored**, as <see cref="HumanTasks"/> are: from the runs
/// bound here and each run's node record read now, so a step the person's word is kept on, or that moved on, waits on no one.
/// </summary>
public static class PersonWaitReader
{
    /// <summary>Every step of a run not ended that waits on the person, by run and then by the graph's order.</summary>
    /// <param name="record">A unit's node record, or null where it does not read.</param>
    public static IReadOnlyList<PersonWait> Of(IEnumerable<GraphRun> runs, Func<string, NodeRecord?> record)
    {
        var waits = new List<PersonWait>();
        foreach (var run in runs.Where(run => run.Ended is null && run.Line is null).OrderBy(run => run.Run, StringComparer.Ordinal))
        {
            if (run.Version.Graph is not { } graph || record(run.Unit) is not { } kept) continue;
            foreach (var node in graph.Nodes)
            {
                var status = WorkPerformer.StatusOf(kept, node.Id);
                if (WhyOf(node, status) is not { } why || kept.Pending.Any(word => word.Node == node.Id)) continue;
                waits.Add(new PersonWait(run.Run, node.Id, why, run.Binding.Repository, status.Since));
            }
        }

        return waits;
    }

    /// <summary>The steps waiting on the person in the runs bound in <paramref name="home"/>, read from its files now: a look no runtime keeps.</summary>
    public static IReadOnlyList<PersonWait> Read(string home)
    {
        var files = new NodeRecordFiles(home);
        return Of(new GraphRuns(files).Read(), files.Read);
    }

    private static string? WhyOf(NodeDeclaration node, NodeStatus status) => status.State switch
    {
        NodeStates.Unknown => PersonWaits.Held,
        NodeStates.Active => NodeKit.Of(node.Kind) switch
        {
            ReviewNode => PersonWaits.Accept,
            HumanNode => HumanNode.Presses(node).Contains(NodeWordActs.Approve, StringComparer.Ordinal) ? PersonWaits.Approval : PersonWaits.Task,
            GoAheadNode => PersonWaits.GoAhead,
            ReturnNode when status.Code == ReturnNode.BothChanged => PersonWaits.Choose,
            _ => null,
        },
        _ => null,
    };
}
