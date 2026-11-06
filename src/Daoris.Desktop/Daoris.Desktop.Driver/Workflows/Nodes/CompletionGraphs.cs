namespace Daoris.Driver;

/// <summary>
/// Where the steps past a pull request stand in a graph (D245), judged by the save beside the <c>push</c>'s place: a <c>monitor</c> watches the
/// pull request a plugin's <c>&lt;plugin&gt;/pull-request</c> step opened, so such a step reaches it; a <c>tidy</c> ends what a <c>monitor</c> saw
/// merged, so a monitor reaches it.
/// </summary>
internal static class CompletionGraphs
{
    /// <summary>Each monitor and tidy no step it rests on reaches, with the save's sentence.</summary>
    /// <param name="after">The nodes a way from the given nodes reaches, as the save reads its ordered wires.</param>
    internal static IEnumerable<(string Node, string Why)> Unreached(IEnumerable<NodeDeclaration> nodes, Func<IEnumerable<string>, HashSet<string>> after)
    {
        var all = nodes.ToList();
        var requested = after(all.Where(node => PullRequestSteps.PluginOf(node.Kind) is not null).Select(node => node.Id));
        foreach (var monitor in all.Where(node => node.Kind == NodeKinds.Monitor && !requested.Contains(node.Id)))
        {
            yield return (monitor.Id, $"node `{monitor.Id}`: a `{NodeKinds.Monitor}` watches the pull request a plugin's `{PullRequestSteps.Id}` step opened, "
                + "and no such step reaches it.");
        }

        var watched = after(all.Where(node => node.Kind == NodeKinds.Monitor).Select(node => node.Id));
        foreach (var tidy in all.Where(node => node.Kind == NodeKinds.Tidy && !watched.Contains(node.Id)))
        {
            yield return (tidy.Id, $"node `{tidy.Id}`: a `{NodeKinds.Tidy}` ends the chain's parts once a `{NodeKinds.Monitor}` saw its pull request merged, "
                + "and no monitor reaches it.");
        }
    }
}
