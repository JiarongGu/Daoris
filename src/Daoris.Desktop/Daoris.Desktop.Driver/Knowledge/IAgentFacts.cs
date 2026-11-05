namespace Daoris.Driver;

/// <summary>
/// The agents as their registrations declare them (the agent-provider design §2.7, §6; COAGENT1 §3): the port the room, the roster's
/// answer and the terminal's list read for an agent's facts, each agent once with its doors and each door's sheet. It sits here, below
/// Agents, since the room reads it; Agents answers it from each provider's registration.
/// </summary>
public interface IAgentFacts
{
    /// <summary>Every agent this machine knows, by its id in ordinal order.</summary>
    IReadOnlyList<AgentFacts> Agents { get; }

    /// <summary>The agent a door's or an agent's id names, or null for a name no agent answers for.</summary>
    AgentFacts? Of(string name);
}

/// <summary>
/// One agent as its registration declares it: who it is, what a person can do with its accounts, and its doors' sheets.
/// </summary>
/// <param name="Id">The agent's id: its provider's, under which its accounts are listed.</param>
/// <param name="Product">What a person calls it, where it says.</param>
/// <param name="Maker">Whose it is, where it says.</param>
/// <param name="Mark">The mark it wears on the page's strip, where it declares one.</param>
/// <param name="StandIn">A test double standing in for an agent.</param>
/// <param name="Acts">What a person can do with its accounts.</param>
/// <param name="Doors">Its doors' sheets: the door whose id is the agent's first, then the rest in ordinal order.</param>
public sealed record AgentFacts(
    string Id, string? Product, string? Maker, string? Mark, bool StandIn, AgentActs Acts, IReadOnlyList<AgentCapabilities> Doors)
{
    /// <summary>The door its account acts run through: the first of <see cref="Doors"/>, or null for a name kept with no door.</summary>
    public string? Lead => Doors.Count > 0 ? Doors[0].Door : null;

    /// <summary>The sheet of one of its doors, or null for a door not its.</summary>
    public AgentCapabilities? Door(string door) =>
        Doors.FirstOrDefault(sheet => string.Equals(sheet.Door, door, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// What a person can do with an agent's accounts, by its own flows (AGT7, CODEXACCT1, AGENTMARK1): each control is absent where its
/// only outcome would be a refusal.
/// </summary>
/// <param name="SignsIn">It declares a sign-in flow.</param>
/// <param name="TakesKey">It takes an API key, by the variable it declares.</param>
/// <param name="DeviceCode">It signs in by a device code beside its own sign-in.</param>
/// <param name="Installs">Daoris has an installer for it.</param>
public sealed record AgentActs(bool SignsIn, bool TakesKey, bool DeviceCode, bool Installs)
{
    public static AgentActs None { get; } = new(false, false, false, false);
}
