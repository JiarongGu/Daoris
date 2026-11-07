namespace Daoris.Knowledge;

/// <summary>
/// One conflict as its quest's log tells it: the conflict; whether the take lost to the person's reopen of the work its
/// step grew from (<see cref="QuestLog.LostToTheReopen"/>'s reading, for this conflict); how it ended, null while it stands; and its
/// bring-forward: the one that settled it, or else this machine's latest that lost again.
/// </summary>
public sealed record QuestConflictReading(
    QuestConflict Conflict, bool LostToTheReopen = false, QuestConflictEnd? Ended = null, QuestForwardTake? Forward = null);
