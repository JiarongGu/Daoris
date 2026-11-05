namespace Daoris.Driver;

/// <summary>Where the plugins' doors join the adapter set: Plugins offers them, so the set Agents keeps names no plugin (AGENTPROVIDER1 row q).</summary>
public static class PluginAdapterSets
{
    /// <summary>
    /// <paramref name="set"/> plus every harness the catalogue's contributing plugins declare (D64 §3). The catalogue has already refused any
    /// name the set carries, so a plugin adds and the built-in set is exactly what it was.
    /// </summary>
    public static AdapterSet WithPlugins(this AdapterSet set, IAgentPlugins catalog) => set.Joined(catalog.Adapters, catalog.DeclaredAgents);
}
