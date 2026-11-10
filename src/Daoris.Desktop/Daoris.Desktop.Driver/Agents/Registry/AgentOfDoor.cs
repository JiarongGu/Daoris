namespace Daoris.Driver;

/// <summary>
/// Which agent a door is: its provider, so a move between one agent's two
/// doors, <c>claude-code</c> and <c>claude-code-acp</c>, is one agent. Never its account key (<see cref="ProviderRegistry.OwnerOf"/>), which
/// two providers sharing a pool hold alike. A door the registry does not know, a removed plugin's, is its own agent.
/// </summary>
public sealed class AgentOfDoor(ProviderRegistry registry)
{
    /// <summary>The agent <paramref name="door"/> is: its provider's id, else the door itself.</summary>
    public string Of(string door) => registry.ProviderNamed(door)?.Id.Value ?? door.Trim();

    /// <summary>Whether two doors are one agent's.</summary>
    public bool Same(string door, string other) => Ids.Same(Of(door), Of(other));
}
