namespace Daoris.Driver;

/// <summary>
/// A plugin's <c>&lt;plugin&gt;/pull-request</c> step (G8; D241 point 4): the performed step that opens the pull request of the branch the run's
/// <c>push</c> pushed. The step's evidence address is the pull request's, which the runtime writes on the landing record, so the
/// <c>work/state</c> asks of a pull request have the address to ask about and the page has one to show.
/// </summary>
public static class PullRequestSteps
{
    /// <summary>The id a plugin's pull-request step ends in.</summary>
    public const string Id = "pull-request";

    /// <summary>The plugin whose pull-request step <paramref name="kind"/> is, or null where it is any other node.</summary>
    public static string? PluginOf(string kind) =>
        kind.EndsWith("/" + Id, StringComparison.Ordinal) && kind.Length > Id.Length + 1 ? kind[..^(Id.Length + 1)] : null;

    /// <summary>
    /// Why a run of <paramref name="graph"/> is refused at its bind, or null: it holds a plugin's pull-request step, and no plugin by that id is
    /// installed, switched on, sound and performing steps here, so the run would reach a step nothing performs.
    /// </summary>
    public static BindingProblem? Unserved(NodeGraph graph, IReadOnlyList<WorkflowPlugin> plugins, string workflow, int version)
    {
        foreach (var node in graph.Nodes)
        {
            if (PluginOf(node.Kind) is not { } plugin) continue;
            var serves = plugins.Any(each => string.Equals(each.Id, plugin, StringComparison.OrdinalIgnoreCase)
                && each.Points.Contains(HookPoints.Perform, StringComparer.Ordinal));
            if (!serves) return BindingProblem.ForUnserved(workflow, version, node.Id, plugin);
        }

        return null;
    }

    /// <summary>Whether <paramref name="address"/> is an address a pull request is kept by: an absolute <c>http</c> or <c>https</c> one.</summary>
    public static bool IsAddress(string? address) =>
        Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";
}
