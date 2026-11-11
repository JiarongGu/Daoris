using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris plugin switch &lt;id&gt; &lt;what&gt; on|off [--place &lt;place&gt;]</c>: one interface or point of an installed plugin switched at
/// one place (<see cref="PluginSwitches"/>), through <c>PLUGIN_SWITCH</c> while Daoris runs, else alone under the home's lease.
/// </summary>
internal static class PluginSwitchVerb
{
    public const string Verb = "switch";

    public const string Words = "daoris plugin switch <id> <what> on|off [--place machine|workspace:<name>|repository:<id>]";

    public static PluginPress Press(string home, IReadOnlyList<string> args, IReadOnlyList<string> reserved, Action<string> say)
    {
        var placeAt = args.ToList().IndexOf("--place");
        var place = placeAt == -1 ? PluginPlace.MachineKey : placeAt + 1 < args.Count ? args[placeAt + 1] : "";
        var words = args.Where((_, index) => placeAt == -1 || (index != placeAt && index != placeAt + 1)).ToList();
        if (words.Count != 4 || words[3] is not ("on" or "off")) throw new PluginWords($"usage: {Words}");
        var entry = PluginCatalog.Load(home, reserved).Plugins
            .FirstOrDefault(each => string.Equals(each.Manifest.Id, words[1], StringComparison.OrdinalIgnoreCase))
            ?? throw new PluginWords($"no plugin `{words[1]}` on this machine — `daoris plugin list` shows what there is.");
        var (id, what, on) = (entry.Manifest.Id, words[2], words[3] == "on");
        if (PluginSwitches.Refusal(what, place) is { } refusal) throw new PluginWords(refusal);
        int Said()
        {
            say($"daoris: plugin `{id}`'s {what} is {(on ? "on" : "off")} at {place}; the driver reads it at its next look.");
            return 0;
        }

        return new PluginPress(Verb, PluginRoutes.Switch, new JsonObject { ["id"] = id, ["what"] = what, ["place"] = place, ["on"] = on },
            () => PluginSwitches.Set(home, id, what, place, on), Said);
    }
}
