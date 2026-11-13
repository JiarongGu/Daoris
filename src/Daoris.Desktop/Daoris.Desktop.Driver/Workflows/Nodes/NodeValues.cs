namespace Daoris.Driver;

/// <summary>
/// A node as a run of a repository bound to a workflow reads it: the repository's values for that node (<see cref="WorkflowValuesKey"/>)
/// laid over its own options, each value replacing the option of its name, so a step's attempts carry them.
/// </summary>
internal static class NodeValues
{
    /// <summary><paramref name="node"/> with <paramref name="repository"/>'s values for it in <paramref name="workflow"/>; the node itself where none are kept.</summary>
    public static NodeDeclaration Over(DriverConfig config, string repository, string workflow, NodeDeclaration node)
    {
        if (WorkflowValuesKey.Of(config.WorkflowValues, repository, workflow, node.Id) is not { } options) return node;
        var laid = new Dictionary<string, string>(node.Options, StringComparer.Ordinal);
        foreach (var (name, json) in options) laid[name] = json;
        return node with { Options = laid };
    }
}
