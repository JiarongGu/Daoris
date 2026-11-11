namespace Daoris.Driver;

/// <summary>
/// The command a refused step's plugin named as its fix, one the plugin declares: its plugin, its name and its title as the window shows
/// it. The window opens the command's round card from it.
/// </summary>
public sealed record WorkflowRunNeeds(string Plugin, string Command, AuthoredText Title);

/// <summary>What a step's <c>needs</c> came to where the run is read: the command it names, or the words that say it was ignored.</summary>
public sealed record RunNeed(WorkflowRunNeeds? Needs, string? Said);

/// <summary>
/// A failed step's <c>needs</c>, judged against its plugin's declared commands where a run is read: a command it declares is drawn as the
/// step's door, any other is ignored and said on the step. A step that did not fail, or named nothing, has none.
/// </summary>
public static class RunNeeds
{
    /// <summary>The needs of <paramref name="record"/>'s failed steps by node, the plugin's declaration read from the home's plugins.</summary>
    public static IReadOnlyDictionary<string, RunNeed> Of(string home, NodeRecord? record)
    {
        if (record is null || !record.Statuses.Values.Any(status => status.Needs is not null)) return new Dictionary<string, RunNeed>();
        var catalog = PluginCatalog.Load(home, ProviderRegistry.Built().Names);
        return Of(record, plugin => catalog.Plugins.FirstOrDefault(entry => entry.Contributes && entry.Manifest.Id == plugin)?.Manifest.Interfaces);
    }

    /// <summary>The needs of <paramref name="record"/>'s failed steps by node, each plugin's declaration given by <paramref name="declared"/>.</summary>
    public static IReadOnlyDictionary<string, RunNeed> Of(NodeRecord? record, Func<string, PluginInterfacesDeclared?> declared)
    {
        var read = new Dictionary<string, RunNeed>(StringComparer.Ordinal);
        if (record is null) return read;
        foreach (var (node, status) in record.Statuses)
        {
            if (status.State != NodeStates.Failed || status.Needs is not { } name) continue;
            var plugin = record.Attempts.LastOrDefault(attempt => attempt.Node == node)?.Plugin;
            var command = plugin is null ? null : declared(plugin)?.Commands.FirstOrDefault(each => each.Name == name);
            read[node] = command is null
                ? new RunNeed(null, $"Its plugin named `{name}` as the fix, which {plugin ?? "the plugin"} does not declare; ignored.")
                : new RunNeed(new WorkflowRunNeeds(plugin!, command.Name, command.Title), null);
        }

        return read;
    }
}
