using System.Text.Json.Nodes;

namespace Daoris.Driver;

/// <summary>
/// <c>daoris plugin try-again &lt;id&gt;</c>: a plugin the running Daoris holds unsound (three unasked exits of its work process within an
/// hour) started again at the loop's next look, its count of exits afresh, through <c>PLUGIN_ACTION</c>'s <c>try-again</c>
/// (<see cref="HookSet.TryAgain"/>). The mark is the running loop's own, so with no Daoris running nothing holds it, and that is said.
/// </summary>
internal static class PluginTryAgain
{
    public const string Action = "try-again";

    public static PluginPress Press(string home, string id, IReadOnlyList<string> reserved, Action<string> say)
    {
        var entry = PluginCatalog.Load(home, reserved).Plugins
            .FirstOrDefault(each => string.Equals(each.Manifest.Id, id, StringComparison.OrdinalIgnoreCase))
            ?? throw new PluginWords($"no plugin `{id}` on this machine — `daoris plugin list` shows what there is.");
        var named = entry.Manifest.Id;
        var alone = false;
        int Said()
        {
            say(alone
                ? $"daoris: Daoris is not running, so nothing holds plugin `{named}` back: it starts with Daoris."
                : $"daoris: plugin `{named}` starts again at the driver's next look, its count of exits afresh.");
            return 0;
        }

        return new PluginPress(Action, PluginRoutes.Action, new JsonObject { ["action"] = Action, ["id"] = named }, () => alone = true, Said);
    }
}
