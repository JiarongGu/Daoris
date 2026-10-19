namespace Daoris.Driver.Host;

/// <summary>
/// Where this host's verbs look for the running Daoris (RPCOPEN1, the terminal RPC design §7.4): the process's own environment and
/// output, on the home the verb acts on. One place, so no console builds its own and every verb says its sentences under its own name.
/// </summary>
internal static class InstanceHost
{
    /// <summary>The world a verb named <paramref name="verb"/> looks for the instance in, on <paramref name="home"/>.</summary>
    public static InstanceWorld World(string? home, string verb) => new() { Home = home, Verb = verb };

    /// <summary>The world for the home the host's configuration resolves, as every console reads it.</summary>
    public static InstanceWorld World(string verb) => World(DriverConfig.HomeOf(DriverConfig.ResolvePath()), verb);
}
