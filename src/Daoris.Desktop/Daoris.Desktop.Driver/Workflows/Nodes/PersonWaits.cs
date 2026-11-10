namespace Daoris.Driver;

/// <summary>A step of a run that waits on the person: only their press, or their word on a held attempt, moves it.</summary>
/// <param name="Run">The run, as its binding names it: its chain's first quest in its repository.</param>
/// <param name="Node">The step's id in the run's graph.</param>
/// <param name="Why">What it waits for, one of <see cref="PersonWaits"/>' words.</param>
/// <param name="Repository">The run's repository.</param>
/// <param name="Since">When the step came to stand where it does; null on a record written before it carried a clock.</param>
public sealed record PersonWait(string Run, string Node, string Why, string Repository, DateTimeOffset? Since);

/// <summary>
/// The run steps that wait on the person: a review's Accept, a person's task or approval, a go-ahead, and an attempt held unknown for
/// them. **Read, never stored**, as <see cref="HumanTasks"/> are: from the runs bound here and each run's node record read now, so a step the
/// person's word is kept on, or that moved on, waits on no one. Sessions' groups and *What needs you* place the session each run stands for
/// by them.
/// </summary>
public static class PersonWaits
{
    /// <summary>A <c>review</c> waiting for the person's Accept.</summary>
    public const string Accept = "accept";

    /// <summary>A <c>human</c> approval waiting for their Approve or Reject.</summary>
    public const string Approval = "approval";

    /// <summary>A <c>human</c> task waiting for their Done.</summary>
    public const string Task = "task";

    /// <summary>A <c>go-ahead</c> waiting for their yes.</summary>
    public const string GoAhead = "go-ahead";

    /// <summary>A step whose attempt is held unknown for the person's word.</summary>
    public const string Held = "held";

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
                if (WhyOf(node, status.State) is not { } why || kept.Pending.Any(word => word.Node == node.Id)) continue;
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

    private static string? WhyOf(NodeDeclaration node, string state) => state switch
    {
        NodeStates.Unknown => Held,
        NodeStates.Active => NodeKit.Of(node.Kind) switch
        {
            ReviewNode => Accept,
            HumanNode => HumanNode.Presses(node).Contains(NodeWordActs.Approve, StringComparer.Ordinal) ? Approval : Task,
            GoAheadNode => GoAhead,
            _ => null,
        },
        _ => null,
    };
}
