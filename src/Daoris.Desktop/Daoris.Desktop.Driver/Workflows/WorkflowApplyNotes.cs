namespace Daoris.Driver;

/// <summary>
/// What <c>workflow apply</c> says of a new version of a workflow beside what this install holds, so a switch to it carries what its
/// steps need or says what it leaves out. Words only: it writes nothing, and the carry of a declared value is a line the person runs.
/// </summary>
public static class WorkflowApplyNotes
{
    /// <summary>A plugin as the notes read it: its id, its version and each step kind it declares (<c>&lt;plugin&gt;/&lt;step&gt;</c>).</summary>
    public sealed record Plugin(string Id, string Version, IReadOnlyCollection<string> Kinds);

    /// <summary>
    /// A line for each plugin step kind in <paramref name="next"/> that no <paramref name="installed"/> plugin declares: its nodes, its kind,
    /// and the plugin to update where this build's <paramref name="offered"/> copy of it declares the kind and the installed one is older.
    /// </summary>
    public static IReadOnlyList<string> Undeclared(NodeGraph next, IReadOnlyList<Plugin> installed, IReadOnlyList<Plugin> offered)
    {
        var declared = installed.SelectMany(plugin => plugin.Kinds).ToHashSet(StringComparer.Ordinal);
        var lines = new List<string>();
        foreach (var group in next.Nodes.Where(node => NodeKit.IsPlugin(node.Kind) && !declared.Contains(node.Kind)).GroupBy(node => node.Kind, StringComparer.Ordinal))
        {
            var nodes = string.Join(", ", group.Select(node => $"`{node.Id}`"));
            var plugin = group.Key[..group.Key.IndexOf('/')];
            var have = installed.FirstOrDefault(each => string.Equals(each.Id, plugin, StringComparison.OrdinalIgnoreCase));
            var ships = offered.FirstOrDefault(each => string.Equals(each.Id, plugin, StringComparison.OrdinalIgnoreCase) && each.Kinds.Contains(group.Key, StringComparer.Ordinal));
            var what = $"{(group.Count() == 1 ? "node" : "nodes")} {nodes} read the step `{group.Key}`, which no installed plugin declares, so";
            lines.Add((have, ships) switch
            {
                ({ } held, { } shipped) when PluginShipped.Compare(held.Version, shipped.Version) < 0 =>
                    $"{what} they would sit Ready and never run: `{plugin}` is installed at {held.Version} and this build ships {shipped.Version}, which declares it; "
                    + $"`daoris plugin update {plugin} --yes` takes it.",
                (null, { } shipped) =>
                    $"{what} they would sit Ready and never run: `{plugin}` is not installed, and this build offers {shipped.Version}, which declares it; "
                    + "Plugins in the window installs it from this build's offers.",
                _ => $"{what} they would sit Ready and never run: install the plugin that declares it.",
            });
        }

        return lines;
    }

    /// <summary>
    /// A line for each repository's declared value of <paramref name="workflow"/> keyed by a node id <paramref name="next"/> does not hold
    /// (<c>workflowValues</c> is read by node id), and for each of them the <c>workflow values</c> line that would carry it to each new node of the
    /// same plugin that takes none, the plugin read from <paramref name="previous"/>'s node of that id. Nothing is written.
    /// </summary>
    public static IReadOnlyList<string> Unread(DriverConfig config, string workflow, int version, NodeGraph? previous, NodeGraph next)
    {
        var held = next.Nodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var lines = new List<string>();
        foreach (var (repository, values) in config.WorkflowValues.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!values.ByWorkflow.TryGetValue(workflow, out var nodes)) continue;
            foreach (var (node, options) in nodes.Where(pair => !held.Contains(pair.Key)).OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                lines.Add($"`{repository}`'s values for `{node}` ({string.Join(", ", options.Keys)}) are left unread by v{version}: it holds no node `{node}`, so its steps run without them.");
                if (previous?.Nodes.FirstOrDefault(old => old.Id == node) is not { } was || !NodeKit.IsPlugin(was.Kind)) continue;
                var plugin = was.Kind[..was.Kind.IndexOf('/')];
                var bare = next.Nodes.Where(each => NodeKit.IsPlugin(each.Kind) && each.Kind.StartsWith(plugin + "/", StringComparison.Ordinal)
                    && !nodes.ContainsKey(each.Id)).ToList();
                if (bare.Count == 0) continue;
                lines.Add($"`{plugin}`'s new nodes {string.Join(", ", bare.Select(each => $"`{each.Id}`"))} take none; to carry them over, run:");
                foreach (var each in bare)
                {
                    var carried = string.Join(' ', options.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => $"{pair.Key}='{pair.Value}'"));
                    lines.Add($"  daoris workflow values {repository} {workflow} {each.Id} {carried} --why \"carried from {node} into v{version}\"");
                }
            }
        }

        return lines;
    }
}
