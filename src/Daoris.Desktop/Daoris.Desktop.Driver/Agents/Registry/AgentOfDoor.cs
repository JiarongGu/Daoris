namespace Daoris.Driver;

/// <summary>
/// Which agent a door is (AGENTPROVIDER1 s-vii; the agent-provider design §7 row 10): its provider, so a move between one agent's two
/// doors, <c>claude-code</c> and <c>claude-code-acp</c>, is one agent. Never its account key (<see cref="ProviderRegistry.OwnerOf"/>), which
/// two providers sharing a pool hold alike. A door the registry does not know, a removed plugin's, is its own agent.
/// </summary>
public sealed class AgentOfDoor
{
    private readonly Dictionary<string, string> _agents = new(StringComparer.OrdinalIgnoreCase);

    public AgentOfDoor(ProviderRegistry registry)
    {
        foreach (var provider in registry.Providers)
        {
            foreach (var door in provider.Doors) _agents.TryAdd(door.Id.Value, provider.Id.Value);
        }
    }

    /// <summary>The agent <paramref name="door"/> is: its provider's id, else the door itself.</summary>
    public string Of(string door) => _agents.TryGetValue(door.Trim(), out var agent) ? agent : door.Trim();

    /// <summary>Whether two doors are one agent's.</summary>
    public bool Same(string door, string other) => Ids.Same(Of(door), Of(other));
}
