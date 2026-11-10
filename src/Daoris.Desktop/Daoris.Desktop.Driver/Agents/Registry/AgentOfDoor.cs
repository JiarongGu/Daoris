namespace Daoris.Driver;

/// <summary>Which agent a door is: its provider, so one agent's two doors are one; a door the registry does not know is its own.</summary>
public sealed class AgentOfDoor(ProviderRegistry registry)
{
    /// <summary>The agent <paramref name="door"/> is: its provider's id, else the door itself.</summary>
    public string Of(string door) => registry.ProviderNamed(door)?.Id.Value ?? door.Trim();

    public bool Same(string door, string other) => Ids.Same(Of(door), Of(other));
}
