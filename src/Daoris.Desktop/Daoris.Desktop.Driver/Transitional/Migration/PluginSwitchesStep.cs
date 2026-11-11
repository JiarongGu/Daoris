using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>plugins.json</c>'s <c>off</c> as an older build kept it, by plugin the interfaces switched off everywhere: each becomes the
/// machine's word in <c>switches</c>, a word the person already wrote there kept, and <c>off</c> is removed. It ends when no home on a
/// supported update path holds <c>off</c>; it is then deleted.
/// </summary>
public sealed class PluginSwitchesStep : IHomeMigrationStep
{
    private const string Off = "off";

    public string Name => "plugin-switches";

    public void Run(HomeMigrationRun run) => run.ChangeJson(PluginState.PathIn(run.Home), root =>
    {
        if (!root.ContainsKey(Off)) return false;
        if (root[Off] is JsonObject off)
        {
            var switches = root[PluginSwitches.Key] as JsonObject ?? new JsonObject();
            root[PluginSwitches.Key] = switches;
            foreach (var (plugin, interfaces) in off)
            {
                if (interfaces is not JsonArray named) continue;
                var row = switches[plugin] as JsonObject ?? new JsonObject();
                switches[plugin] = row;
                foreach (var each in named.Select(HomeMigrationRun.Text).OfType<string>())
                {
                    var places = row[each] as JsonObject ?? new JsonObject();
                    row[each] = places;
                    if (!places.ContainsKey(PluginPlace.MachineKey)) places[PluginPlace.MachineKey] = false;
                }
            }
        }

        root.Remove(Off);
        return true;
    });
}
