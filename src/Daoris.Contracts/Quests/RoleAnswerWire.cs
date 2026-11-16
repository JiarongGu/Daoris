namespace Daoris.Contracts.Quests;

/// <summary>
/// Who answered at a person's door when an appointed role did: the role, the agent that held it, the decision its desktop's file
/// keeps, and the record of the agent's turn where one held it (a terminal names none), whose lineage a memory end's approver is judged
/// by. Posted by the driver alone, with the key, as the role's door's release; a body naming it narrows the answer to the role's and
/// never widens a call, so without the key it is refused as the driver's door refuses. A role naming no role or no agent is refused.
/// </summary>
public sealed record RoleAnswerWire(string Role, string Agent, string Decision, string? Session = null)
{
    /// <summary>Why the form is refused as it stands: a role or an agent left blank, which would name no one's answer; null where it reads.</summary>
    public string? Malformed => string.IsNullOrWhiteSpace(Role) || string.IsNullOrWhiteSpace(Agent)
        ? "role names the appointed role and the agent that held it, neither blank: a role's answer names whose it is"
        : null;
}
