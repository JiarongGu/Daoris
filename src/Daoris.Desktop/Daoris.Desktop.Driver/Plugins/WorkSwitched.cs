namespace Daoris.Driver;

/// <summary>
/// The awaited work as the person's switches let it be told: a plugin's units at a place where its <c>work/watch</c> or
/// <c>work/perform</c> is switched off are not given to it, so its process there is idle and asked nothing.
/// </summary>
internal static class WorkSwitched
{
    public static IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>> Kept(
        IReadOnlyDictionary<string, IReadOnlyList<WatchedUnit>> awaited, PluginSwitchTable switches)
    {
        var kept = new Dictionary<string, IReadOnlyList<WatchedUnit>>(StringComparer.Ordinal);
        foreach (var (plugin, units) in awaited)
        {
            var on = units.Where(unit => Told(switches, plugin, new PluginPlace(unit.Repository, unit.Workspace))).ToList();
            if (on.Count > 0) kept[plugin] = on;
        }

        return kept;
    }

    private static bool Told(PluginSwitchTable switches, string plugin, PluginPlace place) =>
        switches.IsOn(plugin, HookPoints.Watch, place) && switches.IsOn(plugin, HookPoints.Perform, place);
}
