using System.Runtime.CompilerServices;

namespace Daoris.Driver;

internal sealed class InstalledAgentPluginReads : IAgentPluginReads
{
    [ModuleInitializer]
    internal static void Install() => AgentPluginReads.Current = new InstalledAgentPluginReads();

    public IAgentPlugins Load(string home, IEnumerable<string> reservedHarnesses) => PluginCatalog.Load(home, reservedHarnesses);
}
