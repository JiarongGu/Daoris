namespace Daoris.Driver;

/// <summary>
/// Whether an agent holds a role at a place, answered by the root that reads the casts (<see cref="CastReading.AgentHolds"/>): a manifest
/// names a role as a word, and its act is a tool only where one does. Unset, none does.
/// </summary>
public static class PluginRoleHolders
{
    public static Func<string, PluginPlace, Func<string, bool>>? Reading { get; set; }

    internal static Func<string, bool> At(string home, PluginPlace place) => Reading?.Invoke(home, place) ?? (_ => false);
}
