namespace Daoris.Driver;

/// <summary>The step of a run's bound workflow that its quest's session performs: the version's graph's work node.</summary>
public static class WorkflowWorkSteps
{
    /// <summary>
    /// The work step the graph version <paramref name="binding"/> names holds, its first attempt; null where the run is bound to no named
    /// graph version, or its graph observes no chain's work.
    /// </summary>
    public static WorkStep? Of(IWorkflowStore store, WorkflowRunBinding? binding)
    {
        if (binding is not { IsCurrent: false, Version: { } number } || !WorkflowNamed.IsId(binding.Workflow)) return null;
        var graph = store.Load(binding.Workflow)?.Read?.Versions.FirstOrDefault(each => each.Version == number)?.Graph;
        return graph?.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Claimed) is { } work ? new WorkStep(work.Id) : null;
    }
}
