namespace Daoris.Driver;

/// <summary>
/// A start of this machine's loop that has not yet opened its record (PARALLEL1b, D181 point 3): reserved by the look that began it,
/// which no longer waits for it, until the record opens or the start comes to nothing. No snapshot the service answers holds it, so
/// the look hands it to the planner (<see cref="Snapshot.Starting"/>), which holds its quest, its place under the cap, the root of a
/// repository whose sessions run there, and the tree it goes back into, as it holds a live session's.
/// </summary>
/// <param name="Quest">The quest it serves, or null for an ask's intake.</param>
/// <param name="Ask">The ask an intake answers, or null for a quest's start.</param>
/// <param name="Repository">Where it runs: the quest's receiver, or an intake's <c>ask #id</c>, which no quest names.</param>
/// <param name="Tree">The tree it goes back into (a resume, a carry-on, a further part), or null for one that grows its own.</param>
public sealed record StartReservation(string? Quest, string? Ask, string Repository, string? Tree);
