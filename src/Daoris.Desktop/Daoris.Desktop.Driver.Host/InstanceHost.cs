namespace Daoris.Driver.Host;

/// <summary>
/// Where this host's verbs look for the running Daoris: the process's own environment and
/// output, on the home the verb acts on. One place, so no console builds its own and every verb says its sentences under its own name.
/// </summary>
internal static class InstanceHost
{
    /// <summary>
    /// How long a press waits on its Confirm, as the entry took it from the words (<c>--wait</c>, <c>--no-wait</c>),
    /// set once there for every world made below; null where none is given, the default.
    /// </summary>
    public static TimeSpan? Wait { get; set; }

    /// <summary>The world a verb named <paramref name="verb"/> looks for the instance in, on <paramref name="home"/>.</summary>
    public static InstanceWorld World(string? home, string verb) =>
        new() { Home = home, Verb = verb, Wait = Wait ?? InstanceDoor.LongestWait };

    /// <summary>The world for the home the host's configuration resolves, as every console reads it.</summary>
    public static InstanceWorld World(string verb) => World(DriverConfig.HomeOf(DriverConfig.ResolvePath()), verb);
}
