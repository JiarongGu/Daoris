namespace Daoris.Driver;

/// <summary>A graph's plugin pull-request step (<see cref="PullRequestStep"/>) that no installed plugin performs.</summary>
public static class PullRequestUnserved
{
    /// <summary>
    /// Why a run of <paramref name="graph"/> is refused at its bind, or null: it holds a plugin's pull-request step, and no plugin by that id is
    /// installed, switched on, sound and performing steps here, so the run would reach a step nothing performs.
    /// </summary>
    public static BindingProblem? Of(NodeGraph graph, IReadOnlyList<WorkflowPlugin> plugins, string workflow, int version)
    {
        foreach (var node in graph.Nodes)
        {
            if (PullRequestStep.PluginOf(node.Kind) is not { } plugin) continue;
            var serves = plugins.Any(each => string.Equals(each.Id, plugin, StringComparison.OrdinalIgnoreCase)
                && each.Points.Contains(HookPoints.Perform, StringComparer.Ordinal));
            if (!serves) return BindingProblem.ForUnserved(workflow, version, node.Id, plugin);
        }

        return null;
    }
}
