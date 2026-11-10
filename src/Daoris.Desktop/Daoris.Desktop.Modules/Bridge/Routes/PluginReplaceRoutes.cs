using Daoris.Driver;
using Shenora.Core.Ipc;

namespace Daoris.Desktop;

/// <summary>
/// An installed plugin replaced wholesale from a folder, <c>daoris plugin add &lt;folder&gt;</c> over a plugin already installed: its files
/// are the folder's, its data folder kept, and where it came from recorded. The window's add and its install add and never replace, so
/// this is the replace's own press, the person's, confirmed in the window.
/// 🔴 Nothing starts at the press; the loop starts what it runs at its next look.
/// </summary>
public sealed class PluginReplaceRoutes(DriverLoop loop)
{
    private readonly DriverLoop _loop = loop;

    [DriverRoute("PLUGIN_REPLACE", PipeClass.Person, Drain = PipeDrain.Waits)]
    [Owner("PLUGIN_REPLACE", None = OwnerNone.MachineSetting)]
    [GroundFiles("PLUGIN_REPLACE", "plugin-folder", "folder")]
    [Publishes("PLUGIN_REPLACE", StateScopes.Driver | StateScopes.Plugins | StateScopes.Harnesses)]
    private object? PluginReplace(PluginReplacePress press)
    {
        var folder = Path.GetFullPath(press.Folder);
        var source = string.IsNullOrWhiteSpace(press.Offer) ? PluginSource.FromFolder(folder) : PluginSource.FromOffer(press.Offer.Trim());
        var replaced = PluginInstall.Replace(_loop.Home, folder, source, ProviderRegistry.Built().Names);
        _loop.Nudge();
        return new { replaced.Id, replaced.Name, replaced.Version };
    }
}
