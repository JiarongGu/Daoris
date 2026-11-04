namespace Daoris.Knowledge;

/// <summary>
/// A bring-forward of a lost take (SYNCMERGE1b0, D178 points 3 and 8): the fresh take it made, when, and where that take stands as a claim
/// reads one (<see cref="QuestLog.Claim"/>): held once a remote numbered it, unconfirmed while it is only here, lost where a rebase kept
/// it as a conflict.
/// </summary>
public sealed record QuestForwardTake(QuestOperationRef Take, DateTimeOffset At, QuestClaim Standing);
