namespace Daoris.Driver;

/// <summary>
/// The run steps that wait on the person (<see cref="PersonWaits"/>). **Read, never stored**, as <see cref="HeldSteps"/> are: from the runs
/// bound here and each run's node record read now, so a step the person's word is kept on, or that moved on, waits on no one.
/// </summary>
public static class PersonWaitReader
{
    public static IReadOnlyList<PersonWait> Of(
        IEnumerable<GraphRun> runs, Func<string, NodeRecord?> record, string? home = null, PlaceRiskBook? risk = null, Func<string?, string?, bool>? live = null)
    {
        var waits = new List<PersonWait>();
        var casts = home is null ? null : new PlaceCastFiles(home).Book();
        foreach (var run in runs.Where(run => run.Line is null).OrderBy(run => run.Run, StringComparer.Ordinal))
        {
            if (run.Ended is { Outcome: RunOutcomes.Failed } ended)
            {
                if (LeftSteps.Waiting(run, record(run.Unit)) is { } failed)
                {
                    waits.Add(new PersonWait(run.Run, failed, PersonWaits.Left, run.Binding.Repository, ended.At) { Role = StepWaiting.RoleOf(PersonWaits.Left), Ended = true });
                }

                continue;
            }

            if (run.Ended is not null || run.Version.Graph is not { } graph || record(run.Unit) is not { } kept) continue;
            var holding = home is null ? null : StepTenureFiles.HoldingOf(home, run.Run, kept);
            var cast = casts is null ? null : CastReading.At(casts, risk, run.Binding.Workspace, run.Binding.Repository);
            var vacancies = home is null ? null : new StepVacancyFiles(home).Of(run.Unit);
            var serves = casts is null ? null : StepVacancies.ServesAt(casts, risk, run.Binding.Workspace, run.Binding.Repository, live);
            foreach (var node in graph.Nodes)
            {
                var status = WorkPerformer.StatusOf(kept, node.Id);
                if (StepWaiting.Read(kept, node, status, holding?.GetValueOrDefault(node.Id), cast) is not { } read
                    || kept.Pending.Any(word => word.Node == node.Id))
                {
                    continue;
                }

                var vacancy = StepVacancies.Kept(vacancies, read, node.Id, status.Attempt, serves);
                var wait = StepWaiting.Exited(read, vacancy);
                var vacant = vacancy is { Exited: false };
                if (wait.Holder.Kind != StepHolders.Person && !vacant) continue;
                waits.Add(new PersonWait(run.Run, node.Id, wait.Why, run.Binding.Repository, wait.Since) { Role = wait.Role, Vacant = vacant });
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
