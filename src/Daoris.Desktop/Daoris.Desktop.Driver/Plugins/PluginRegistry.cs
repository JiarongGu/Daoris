namespace Daoris.Driver;

/// <summary>Where the plugins' doors join the registry: Plugins offers them, so the registry's Agents keeps names no plugin.</summary>
public static class PluginRegistry
{
    /// <summary>
    /// <paramref name="set"/> plus every harness the catalogue's contributing plugins declare. The catalogue has already refused any
    /// name the registry carries, so a plugin adds and the built-in registry stays exactly as it is.
    /// </summary>
    public static ProviderRegistry WithPlugins(this ProviderRegistry set, IAgentPlugins catalog) => set.Joined(catalog.Doors.Select(DeclaredDoors.Door), catalog.DeclaredAgents);
}
