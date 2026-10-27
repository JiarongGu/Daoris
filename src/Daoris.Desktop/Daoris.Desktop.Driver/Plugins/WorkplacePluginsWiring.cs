using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class WorkplacePluginOperations : IWorkplacePluginOperations
{
    [ModuleInitializer]
    internal static void Install() => WorkplacePlugins.Current = new WorkplacePluginOperations();

    public IWorkplacePluginWire Wire(string home) => new LandingPlugins(home);

    public IWorkplacePluginCatalog Load(string home) => PluginCatalog.Load(home, AdapterSet.Built().Names);
}
