namespace Daoris.Driver;

/// <summary>
/// A move's hook into its new part's start, asked once the start's own walk has settled the door and the account and the record is open:
/// the carry the part runs as (<see cref="ChatCarry.RunAsync"/>), or null where it starts fresh, handed the brief.
/// </summary>
internal delegate ChatCarry? ChatCarries(AgentDoor door, HarnessSelection selection);
