using System.Text.Json;
using Daoris.Driver;

namespace Daoris.Desktop;

/// <summary>
/// <c>daoris plugin enable|disable|remove &lt;id&gt;</c> and <c>daoris plugins approve|withdraw &lt;id&gt; &lt;server&gt;</c> as the card shows them
/// (<c>PLUGIN_ACTION</c>): an installed plugin switched, removed, or what one of its servers offers Ask Daoris approved or withdrawn. A
/// switch is judged as the Plugins row judges it, and an approval as <see cref="HelpGrants"/> takes it (<see cref="PluginGrantJudge"/>).
/// </summary>
public sealed class PluginActionView : IPressView
{
    public string Module => DriverModule.Name;

    public string Route => "PLUGIN_ACTION";

    public async Task<PressViewed> ViewAsync(IPipeGroundWorld world, JsonElement? payload, CancellationToken cancellationToken)
    {
        var (action, id) = (PressPayload.Text(payload, "action"), PressPayload.Text(payload, "id"));
        var machine = await world.MachineAsync(cancellationToken).ConfigureAwait(false);
        if (machine.Facts.Home is not { } home) return PressViewed.Refused("this machine's plugins were not read, so nothing is asked.");
        var catalog = machine.Facts.Plugins;
        if (PluginNamed.Installed(catalog, id) is not { } found) return PressViewed.Refused(PluginNamed.NotInstalled(catalog, id));
        var named = found.Manifest.Id;

        switch (action)
        {
            case "enable" or "disable":
            {
                var on = action == "enable";
                if (found.Enabled == on) return PressViewed.Refused($"`{named}` is already {(on ? "on" : "off")}, so there is nothing to switch.");
                return PressViewed.Shown(new PressView($"plugin-{action}", PluginNamed.Facts(("id", named)), $"daoris plugin {action} {named}")
                {
                    Plugin = PressPlugin.Read(named, found.Folder),
                    Also = on && found.Problem is { } problem ? [new PressSaid("plugin-unsound", ("problem", problem))] : null,
                });
            }
            case "remove":
                return PressViewed.Shown(new PressView("plugin-remove", PluginNamed.Facts(("id", named)), $"daoris plugin remove {named}"));
            case "help-approve" or "help-reads" or "help-withdraw":
                return PluginGrantJudge.View(home, catalog, found, action, PressPayload.Text(payload, "server"), Writes(payload, action));
            default:
                return PressViewed.Refused($"unknown plugin action '{action}' — one of: enable, disable, remove, help-approve, help-reads, help-withdraw");
        }
    }

    /// <summary>The writes an approval accepts: none for its reads only, every one it declares where none is named.</summary>
    private static IReadOnlyList<string>? Writes(JsonElement? payload, string action) =>
        action == "help-reads" ? []
        : payload is { ValueKind: JsonValueKind.Object } body && body.TryGetProperty("writes", out var writes) && writes.ValueKind == JsonValueKind.Array
            ? [.. writes.EnumerateArray().Where(tool => tool.ValueKind == JsonValueKind.String).Select(tool => tool.GetString()!.Trim())]
            : null;
}
