namespace Daoris.Knowledge;

/// <summary>
/// What a scout answered when the lead asked for a part to be oriented: the pack in the scout's words (the part's files, contract and tests to read),
/// kept for the lead to read through the plan door and write into the next revision. Never a revision itself: a plan is versioned by its lead alone.
/// </summary>
/// <param name="Ask">The ask the team builds.</param>
/// <param name="Part">The part scouted.</param>
/// <param name="Session">The scout's session.</param>
/// <param name="Pack">The scout's answer.</param>
/// <param name="At">When it answered.</param>
public sealed record TeamScouted(string Ask, string Part, string Session, string Pack, DateTimeOffset At);
