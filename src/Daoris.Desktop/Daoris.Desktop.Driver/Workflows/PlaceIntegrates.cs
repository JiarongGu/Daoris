namespace Daoris.Driver;

/// <summary>Reads a place's <see cref="PlaceIntegrate"/> from the saved workflows.</summary>
internal static class PlaceIntegrates
{
    /// <summary>
    /// The <c>integrate</c> step of the workflow <paramref name="repository"/>'s work follows by default, its newest version as a run's bind
    /// would take it: a newest version that does not read is not passed over for an older, so it names no requester.
    /// </summary>
    public static PlaceIntegrate Of(IWorkflowStore workflows, DriverConfig config, string repository, string? workspace)
    {
        var workflow = WorkflowSelection.Resolve(config, repository, workspace, task: null).Workflow;
        if (workflow == WorkflowSelection.Current || !WorkflowNamed.IsId(workflow)) return PlaceIntegrate.None;
        var step = workflows.Load(workflow)?.Read.Versions.LastOrDefault()?.Graph?.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Integrate);
        return step is null ? PlaceIntegrate.None : new(IntegrateNodes.RequestOf(step), IntegrateNodes.ConflictsOf(step), workflow);
    }
}
