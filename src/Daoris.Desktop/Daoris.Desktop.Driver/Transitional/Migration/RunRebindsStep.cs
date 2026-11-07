namespace Daoris.Driver;

/// <summary>
/// A run bound to a workflow version this build cannot read, Current's or one its file no longer holds as a graph, rebound to the place's choice
/// today, as a first start would bind it: such a run is held for good, so its chain's work would never land. Only a run with no node record is
/// rebound, since a record's steps are bound to its old version's digest; a run whose place's choice is itself held, or names no graph, is left as
/// it is, said by its own problem.
/// </summary>
public sealed class RunRebindsStep : IHomeMigrationStep
{
    public string Name => "run-rebinds";

    public void Run(HomeMigrationRun run)
    {
        var folder = WorkflowRunBindings.FolderOf(run.Home);
        if (!Directory.Exists(folder)) return;
        var files = new WorkflowRunFiles(run.Home);
        var store = WorkflowPorts.Store(run.Home);
        DriverConfig? config = null;
        IReadOnlyList<WorkflowPlugin>? plugins = null;
        foreach (var path in Directory.EnumerateFiles(folder, "*.json", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
        {
            var id = Path.GetFileNameWithoutExtension(path);
            if (files.Read(id) is not { } bound || Reads(store, bound)) continue;
            if (File.Exists(NodeRecordFiles.PathOf(run.Home, RunUnits.IdOf(bound.Run)))) continue;

            config ??= DriverConfig.Load(DaorisHome.Entry(run.Home, HomeEntry.DriverConfig));
            plugins ??= WorkflowSteps.PluginsOf(PluginCatalog.Load(run.Home));
            WorkflowRunBinding planned;
            try
            {
                planned = WorkflowRunBindings.Plan(config, run.Home, bound.Run, bound.Repository, bound.Workspace, bound.Ask, null, plugins, bound.At);
            }
            catch (DriverException)
            {
                // The role catalogue does not read: the run stays as it was, and its start says why.
                continue;
            }

            if (planned.Problem is not null || !GraphRun.Names(planned)) continue;
            run.Write(path, WorkflowRunFiles.ToJson(planned));
        }
    }

    /// <summary>Whether a run's binding reads as a graph version here: bound to a version its workflow's file holds, at its digest.</summary>
    private static bool Reads(IWorkflowStore store, WorkflowRunBinding binding) =>
        GraphRun.Names(binding) && store.Load(binding.Workflow) is { } found && WorkflowGraphs.ForRun(found.Read, binding) is { Readable: true };
}
