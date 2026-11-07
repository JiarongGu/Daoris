namespace Daoris.Driver;

/// <summary>What the roster says of an agent a quest's session may go on with: its accounts and whether it drives an unadopted repository.</summary>
/// <param name="Accounts">The accounts the roster holds for the agent; none is the tool's own sign-in.</param>
/// <param name="Unadopted">It may drive a repository that has not adopted Daoris (<see cref="Adoption.Unadopted(DoorAbilities?)"/>): a door
/// whose servers ride its wire hands the quest's connector itself, any other only in an adopted repository.</param>
public sealed record HandOverAgent(IReadOnlyList<string> Accounts, bool Unadopted);

/// <summary>
/// A quest handed to another agent, as this machine holds it: the agent and account its next start
/// runs on, the session it goes on from, why, by whom, and when.
/// </summary>
/// <param name="Agent">The adapter every start of the quest runs on from now, until the quest leaves this machine.</param>
/// <param name="Account">The account its next start asks for first, or null for the roster's walk.</param>
/// <param name="From">The session that is stopped to hand the quest over: the one stop the planner reads as released.</param>
/// <param name="Why">One of <see cref="HandOverWhy"/>.</param>
/// <param name="AskedBy">One of <see cref="HandOverBy"/>.</param>
public sealed record HandedQuest(string Agent, string? Account, string From, string Why, string AskedBy, DateTimeOffset At)
{
    /// <summary>
    /// The note the agent it went on from left where it asked to hand over: told to the carry-on as that agent's claim.
    /// Null for every other hand-over.
    /// </summary>
    public string? Note { get; init; }

    /// <summary>The model of the choice the hand-over is offered and approved on, which the next start takes while its role still lists it; null for the agent's first entry.</summary>
    public string? Model { get; init; }

    /// <summary>The effort of that choice, likewise.</summary>
    public string? Effort { get; init; }
}
