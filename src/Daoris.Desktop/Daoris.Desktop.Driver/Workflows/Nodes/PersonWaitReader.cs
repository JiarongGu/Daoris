namespace Daoris.Driver;

/// <summary>
/// The run steps that wait on the person (<see cref="PersonWaits"/>). **Read, never stored**, as <see cref="HeldSteps"/> are: from the runs
/// bound here and each run's node record read now, so a step the person's word is kept on, or that moved on, waits on no one.
/// </summary>
public static class PersonWaitReader
{
    /// <summary>
    /// Every step of a run not ended whose wait (<see cref="StepWaiting.Read"/>, the one reading of every step's wait) the person holds, by
    /// run and then by the graph's order, and the step a run ended failed at.
    /// </summary>
    /// <param name="record">A unit's node record, or null where it does not read.</param>
    public static IReadOnlyList<PersonWait> Of(IEnumerable<GraphRun> runs, Func<string, NodeRecord?> record)
    {
        var waits = new List<PersonWait>();
        foreach (var run in runs.Where(run => run.Line is null).OrderBy(run => run.Run, StringComparer.Ordinal))
        {
            if (run.Ended is { Outcome: RunOutcomes.Failed, Node: { Length: > 0 } failed } ended)
            {
                waits.Add(new PersonWait(run.Run, failed, PersonWaits.Left, run.Binding.Repository, ended.At) { Role = StepWaiting.RoleOf(PersonWaits.Left) });
                continue;
            }

            if (run.Ended is not null || run.Version.Graph is not { } graph || record(run.Unit) is not { } kept) continue;
            foreach (var node in graph.Nodes)
            {
                if (StepWaiting.Read(kept, node, WorkPerformer.StatusOf(kept, node.Id)) is not { } wait
                    || wait.Holder.Kind != StepHolders.Person
                    || kept.Pending.Any(word => word.Node == node.Id))
                {
                    continue;
                }

                waits.Add(new PersonWait(run.Run, node.Id, wait.Why, run.Binding.Repository, wait.Since) { Role = wait.Role });
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
}
