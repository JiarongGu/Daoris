namespace Daoris.Contracts.Quests;

/// <summary>
/// Who answered at a person's door when an appointed role did: the role, the agent that held it, and the decision its desktop's file
/// keeps. Posted by the driver alone, with the key, as the role's door's release; a body naming it narrows the answer to the role's and
/// never widens a call, so without the key it is refused as the driver's door refuses.
/// </summary>
public sealed record RoleAnswerWire(string Role, string Agent, string Decision);
