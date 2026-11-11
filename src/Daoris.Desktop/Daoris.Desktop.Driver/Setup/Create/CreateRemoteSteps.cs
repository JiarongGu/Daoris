namespace Daoris.Driver;

/// <summary>
/// The step of a workflow that needs a remote: a <c>push</c>, or a plugin's <c>pull-request</c>. A new department has none, so where the
/// workflow it inherits holds one its drive stays off and the person's next press is a delivery chosen for it.
/// </summary>
public static class CreateRemoteSteps
{
    /// <summary>The first step of <paramref name="graph"/> that needs a remote, by its kind; null where it has none or there is no graph.</summary>
    public static string? In(NodeGraph? graph) =>
        graph?.Nodes.FirstOrDefault(node => node.Kind == NodeKinds.Push || PullRequestKind.PluginOf(node.Kind) is not null)?.Kind;

    /// <summary>The remote step of the workflow <paramref name="repository"/> follows under <paramref name="config"/> and the home's saved workflows.</summary>
    public static string? Of(string home, DriverConfig config, string repository, string? workspace)
    {
        var workflow = WorkflowSelection.Resolve(config, repository, workspace, task: null).Workflow;
        if (workflow == WorkflowSelection.Current || !WorkflowNamed.IsId(workflow)) return null;
        return In(WorkflowPorts.Store(home).Load(workflow)?.Read.Versions.LastOrDefault()?.Graph);
    }
}
