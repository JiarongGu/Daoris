namespace Daoris.Driver;

/// <summary>
/// What a quest not yet taken says of its run: it has not started, and the workflow its start will bind is the one its repository's choice
/// resolves now. Only where nothing is chosen for it does the run say so (<see cref="WorkflowProcess.NoWorkflow"/>).
/// </summary>
public static class WorkflowNotStarted
{
    /// <summary>A quest the work waits on: open, so no session has taken it and no run is bound.</summary>
    public static bool Waits(QuestView quest) => quest.Status == "Open";

    /// <summary>
    /// The words of an open quest's run before its start: the workflow the choice names and the level that chose it; null where the choice
    /// is Current, which binds none.
    /// </summary>
    public static string? Said(WorkflowSelected selected)
    {
        if (selected.Workflow == WorkflowSelection.Current) return null;
        var by = selected.Level switch
        {
            WorkflowLevels.Task => "by its ask's choice",
            WorkflowLevels.RepositoryKind or WorkflowLevels.Repository => "by the repository's choice",
            WorkflowLevels.WorkspaceKind or WorkflowLevels.Workspace => "by the workspace's choice",
            _ => "by this machine's default",
        };
        return $"not started yet: waits to be taken (`{selected.Workflow}`, {by})";
    }
}
