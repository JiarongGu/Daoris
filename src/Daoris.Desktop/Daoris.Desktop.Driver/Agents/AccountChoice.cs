namespace Daoris.Driver;

/// <summary>
/// The step of the goal's walk that chose a start's account, and what its record says first.
/// Machine-local, as <see cref="RotatedStart"/> is: the clause may name accounts.
/// </summary>
/// <param name="Step">The step: the one that moved it off where its scope begins, or that ranked it ahead of the next.</param>
/// <param name="Clause">The step said for a person, and, where the first account is passed for itself, the step among the rest.</param>
public sealed record AccountChoice(WalkStep Step, string Clause);
