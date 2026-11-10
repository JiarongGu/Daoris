namespace Daoris.Driver;

/// <summary>
/// Reads a place's <see cref="IsolateStep"/> from the saved workflows: the step of the workflow its work follows by default, newest version,
/// as a run's bind would take it. None where that workflow has no step, or no workflow is chosen: the place runs one session at a time in place.
/// </summary>
public static class PlaceIsolates
{
    public static IsolateStep? Of(IWorkflowStore workflows, DriverConfig config, string repository, string? workspace)
    {
        var workflow = WorkflowSelection.Resolve(config, repository, workspace, task: null).Workflow;
        if (workflow == WorkflowSelection.Current || !WorkflowNamed.IsId(workflow)) return null;
        return IsolateSteps.In(workflows.Load(workflow)?.Read.Versions.LastOrDefault()?.Graph);
    }

    /// <summary>The step of each of <paramref name="repositories"/> whose workflow sets its work apart, by repository.</summary>
    public static IReadOnlyDictionary<string, IsolateStep> Of(IWorkflowStore workflows, DriverConfig config, IReadOnlyList<RepoView> repositories)
    {
        var steps = new Dictionary<string, IsolateStep>(StringComparer.OrdinalIgnoreCase);
        foreach (var repository in repositories)
        {
            if (Of(workflows, config, repository.Repository, repository.Workspace) is { } step) steps[repository.Repository] = step;
        }

        return steps;
    }

    /// <summary>The step of <paramref name="repository"/>'s workflow under the home's saved workflows.</summary>
    public static IsolateStep? Of(string home, DriverConfig config, string repository, string? workspace) =>
        Of(WorkflowPorts.Store(home), config, repository, workspace);
}
