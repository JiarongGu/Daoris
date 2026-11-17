namespace Daoris.Driver;

/// <summary>
/// The run steps that wait on the person (<see cref="PersonWaits"/>). **Read, never stored**, as <see cref="HeldSteps"/> are: from the runs
/// bound here and each run's node record read now, so a step the person's word is kept on, or that moved on, waits on no one. Its holder is
/// read as theirs is: the step's own role by its attempt's tenure, any other role by the place's cast (<see cref="StepWaiting.HolderOf"/>).
/// </summary>
public static class PersonWaitReader
{
    /// <param name="home">The home whose tenures and cast name each wait's holder; null reads none, each step's role then the person's.</param>
    public static IReadOnlyList<PersonWait> Of(IEnumerable<GraphRun> runs, Func<string, NodeRecord?> record, string? home = null, PlaceRiskBook? risk = null)
    {
        var waits = new List<PersonWait>();
        var casts = home is null ? null : new PlaceCastFiles(home).Book();
        foreach (var run in runs.Where(run => run.Line is null).OrderBy(run => run.Run, StringComparer.Ordinal))
        {
            if (run.Ended is { Outcome: RunOutcomes.Failed, Node: { Length: > 0 } failed } ended)
            {
                waits.Add(new PersonWait(run.Run, failed, PersonWaits.Left, run.Binding.Repository, ended.At) { Role = StepWaiting.RoleOf(PersonWaits.Left) });
                continue;
            }

            if (run.Ended is not null || run.Version.Graph is not { } graph || record(run.Unit) is not { } kept) continue;
            var holding = home is null ? null : StepTenureFiles.HoldingOf(home, run.Run, kept);
            var cast = casts is null ? null : CastReading.At(casts, risk, run.Binding.Workspace, run.Binding.Repository);
            foreach (var node in graph.Nodes)
            {
                if (StepWaiting.Read(kept, node, WorkPerformer.StatusOf(kept, node.Id), holding?.GetValueOrDefault(node.Id), cast) is not { } wait
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
        return Of(new GraphRuns(files).Read(), files.Read, home, DriverConfig.Load(DriverConfig.ResolvePath(home)).PlaceRisk);
    }
}
