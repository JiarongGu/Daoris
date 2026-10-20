namespace Daoris.Driver;

/// <summary>What the roster says of an agent a quest's session may go on with: its accounts and the wire its door rides (MIXAGENT1a-ii).</summary>
/// <param name="Accounts">The accounts the roster holds for the agent; none is the tool's own sign-in.</param>
/// <param name="Wire">The door it drives by: the protocol door hands the quest's connector itself, the pipe door only in an adopted repository (D70).</param>
public sealed record HandOverAgent(IReadOnlyList<string> Accounts, SessionWire Wire);

/// <summary>
/// A quest handed to another agent (MIXAGENT1a-ii, D163 point 1), as this machine holds it: the agent and account its next start
/// runs on, the session it went on from, why, by whom, and when.
/// </summary>
/// <param name="Agent">The adapter every start of the quest runs on from now, until the quest leaves this machine.</param>
/// <param name="Account">The account its next start asks for first, or null for the roster's walk.</param>
/// <param name="From">The session that was stopped to hand the quest over: the one stop the planner reads as released.</param>
/// <param name="Why">One of <see cref="HandOverWhy"/>.</param>
/// <param name="AskedBy">One of <see cref="HandOverBy"/>.</param>
public sealed record HandedQuest(string Agent, string? Account, string From, string Why, string AskedBy, DateTimeOffset At)
{
    /// <summary>
    /// The note the agent it went on from left where it asked to hand over (MIXAGENT1c): told to the carry-on as that agent's claim.
    /// Null for every other hand-over, and in a file from before, which keeps its shape.
    /// </summary>
    public string? Note { get; init; }
}
