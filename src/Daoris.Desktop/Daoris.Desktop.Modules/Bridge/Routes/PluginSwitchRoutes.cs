using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>One interface or point of a plugin switched at one place: <c>plugins.json</c>'s <c>switches</c>, the person's word on the machine's record.</summary>
public sealed record PluginSwitchPress(string Id, string What, string? Place, bool On);

/// <summary>
/// The window's and the terminal's <c>daoris plugin switch</c>: the person's word on one interface or point of an installed plugin at one
/// place, read by every door at its next reach (<see cref="PluginSwitches"/>). The loop is asked to look now, so a point switched off
/// is not told at its next change.
/// </summary>
public sealed class PluginSwitchRoutes(DriverLoop loop)
{
    private readonly DriverLoop _loop = loop;

    [DriverRoute("PLUGIN_SWITCH", PipeClass.Person, Drain = PipeDrain.Passes)]
    [Owner("PLUGIN_SWITCH", None = OwnerNone.MachineSetting)]
    [GroundRead("PLUGIN_SWITCH", nameof(PipeReads.PluginState))]
    [Publishes("PLUGIN_SWITCH", StateScopes.Plugins)]
    private object? PluginSwitch(PluginSwitchPress press)
    {
        var entry = PluginsRoutes.InstalledPlugin(_loop, press.Id);
        var place = string.IsNullOrWhiteSpace(press.Place) ? PluginPlace.MachineKey : press.Place.Trim();
        PluginSwitches.Set(_loop.Home, entry.Manifest.Id, press.What, place, press.On);
        _loop.Nudge();
        return new { entry.Manifest.Id, press.What, Place = place, press.On, Switches = PluginSwitches.Read(_loop.Home).Words(entry.Manifest.Id) };
    }
}
