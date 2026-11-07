namespace Daoris.Driver;

/// <summary>
/// The agent whose toolchain and accounts Daoris manages, by the name a press or a verb gives it: a name the registry holds no door or
/// agent of is refused naming the doors that exist (D23), and a known agent Daoris manages nothing for is told its accounts are its own
/// tooling's. Every door that acts on an agent's toolchain or accounts asks here, so each refusal is said one way.
/// </summary>
public static class ManagedAgents
{
    /// <exception cref="DriverException">The name is no agent this registry holds, or one whose toolchain Daoris does not manage.</exception>
    public static AccountAgent Of(HarnessRoster harnesses, string harness)
    {
        if (harnesses.AgentOf(harness) is { } agent) return agent;
        var registry = harnesses.Adapters;
        if (!registry.Names.Contains(harness, StringComparer.Ordinal) && !registry.Providers.Any(provider => provider.Id.Value == harness))
        {
            _ = registry.Door(harness);
        }

        throw new DriverException($"Daoris manages no toolchain for `{harness}` — its accounts are its own tooling's.");
    }
}
