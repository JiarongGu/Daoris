namespace Daoris.Knowledge;

/// <param name="Quest">The quest as it stood when the delete is judged — null when there is no such quest.</param>
/// <param name="Deleted">Whether THIS call deleted it. False with a quest is the table refusing: it is not open.</param>
/// <param name="Tombstoned">Whether the delete is an operation that travels, rather than the quest simply going.</param>
public sealed record QuestDeletion(Quest? Quest, bool Deleted, bool Tombstoned = false);
