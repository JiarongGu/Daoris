namespace Daoris.Driver;

/// <summary>
/// The runs an older build ended as a closed quest's (<see cref="ClosedRunEnds"/>) under a step still to run after a reached landing, such
/// as a pull request's monitor: their end lapses and each is counted as reopened. Any other end stays, and the step run again changes nothing.
/// </summary>
public sealed class ReopenedRunsStep : IHomeMigrationStep
{
    public string Name => "reopened-runs";

    public void Run(HomeMigrationRun run)
    {
        if (!Directory.Exists(WorkflowRunBindings.FolderOf(run.Home))) return;

        // The migration holds the home's lease while its steps run.
        var records = new NodeRecordFiles(run.Home, () => true);
        foreach (var bound in new GraphRuns(records).Read().Where(each => each.Ended is { Why: ClosedRunEnds.BranchGone or ClosedRunEnds.OnLine } && each.Line is null))
        {
            if (bound.Version.Graph is not { } graph || records.Read(bound.Unit) is not { } record || !ClosedRunEnds.StandsPastLanding(graph, record)) continue;

            var path = NodeRecordFiles.PathOf(run.Home, bound.Unit);
            run.Keep(path);
            var (refused, kept) = records.Edit<bool>(bound.Unit, current =>
                current.Ended is { Why: ClosedRunEnds.BranchGone or ClosedRunEnds.OnLine } && ClosedRunEnds.StandsPastLanding(graph, current)
                    ? (current with { Ended = null }, true) : (null, false));
            if (refused is not null || kept != true) continue;
            run.Said(path);
            run.Count(Name, "reopened");
        }
    }
}
